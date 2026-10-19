using System.Net.Http.Json;
using BrownEvents.Api.Data;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrownEvents.Tests;

public class AttendeePiiExposureTests
{
    private const string ConferenceMarkerFirstName = "PiiConferenceFirst";
    private const string ConferenceMarkerLastName = "PiiConferenceLast";
    private const string ConferenceMarkerEmail = "pii-conference-marker@example.com";
    private const string SessionMarkerFirstName = "PiiSessionFirst";
    private const string SessionMarkerLastName = "PiiSessionLast";
    private const string SessionMarkerEmail = "pii-session-marker@example.com";

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"AttendeePiiExposureTestsDb-{Guid.NewGuid()}";

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<AppDbContext>(_ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(databaseName, databaseRoot)
                        .Options;
                    return new AppDbContext(options);
                });
            });
        });
    }

    private static async Task<(int ConferenceId, int SessionId)> SeedConferenceSessionAndRegistrationsAsync(HttpClient client)
    {
        var conference = new Conference
        {
            Title = "Pii Exposure Conference",
            Description = "A conference used to verify attendee PII does not leak",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
        };
        var conferenceResponse = await client.PostAsJsonAsync("/api/conferences", conference);
        var createdConference = await conferenceResponse.Content.ReadFromJsonAsync<ConferenceDto>();

        var session = new Session
        {
            Title = "Pii Exposure Session",
            Description = "A session used to verify attendee PII does not leak",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 10,
        };
        var sessionResponse = await client.PostAsJsonAsync($"/api/conferences/{createdConference!.Id}/sessions", session);
        var createdSession = await sessionResponse.Content.ReadFromJsonAsync<SessionDto>();

        var conferenceAttendee = new Attendee
        {
            FirstName = ConferenceMarkerFirstName,
            LastName = ConferenceMarkerLastName,
            Email = ConferenceMarkerEmail,
        };
        await client.PostAsJsonAsync($"/api/conferences/{createdConference.Id}/register", conferenceAttendee);

        var sessionAttendee = new AttendeeRegistrationRequestDto
        {
            FirstName = SessionMarkerFirstName,
            LastName = SessionMarkerLastName,
            Email = SessionMarkerEmail,
        };
        await client.PostAsJsonAsync($"/api/sessions/{createdSession!.Id}/register", sessionAttendee);

        return (createdConference.Id, createdSession.Id);
    }

    [Fact]
    public async Task PublicConferenceAndSessionEndpoints_DoNotContainAttendeeMarkers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (conferenceId, sessionId) = await SeedConferenceSessionAndRegistrationsAsync(client);

        var publicResponses = new[]
        {
            await client.GetAsync("/api/conferences"),
            await client.GetAsync($"/api/conferences/{conferenceId}"),
            await client.GetAsync($"/api/conferences/{conferenceId}/sessions"),
            await client.GetAsync($"/api/sessions/{sessionId}"),
        };

        var markers = new[]
        {
            ConferenceMarkerFirstName,
            ConferenceMarkerLastName,
            ConferenceMarkerEmail,
            SessionMarkerFirstName,
            SessionMarkerLastName,
            SessionMarkerEmail,
        };

        foreach (var response in publicResponses)
        {
            var body = await response.Content.ReadAsStringAsync();
            foreach (var marker in markers)
            {
                Assert.DoesNotContain(marker, body);
            }
        }
    }

    [Fact]
    public async Task AttendeeScopedEndpoints_StillExposeAttendeeDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (conferenceId, sessionId) = await SeedConferenceSessionAndRegistrationsAsync(client);

        var registrationsResponse = await client.GetAsync($"/api/conferences/{conferenceId}/registrations");
        var registrationsBody = await registrationsResponse.Content.ReadAsStringAsync();
        Assert.Contains(ConferenceMarkerFirstName, registrationsBody);
        Assert.Contains(ConferenceMarkerLastName, registrationsBody);
        Assert.Contains(ConferenceMarkerEmail, registrationsBody);

        var conferenceRegisterResponse = await client.PostAsJsonAsync(
            $"/api/conferences/{conferenceId}/register",
            new Attendee { FirstName = ConferenceMarkerFirstName, LastName = ConferenceMarkerLastName, Email = ConferenceMarkerEmail });
        var conferenceRegisterBody = await conferenceRegisterResponse.Content.ReadAsStringAsync();
        Assert.Contains(ConferenceMarkerFirstName, conferenceRegisterBody);
        Assert.Contains(ConferenceMarkerLastName, conferenceRegisterBody);
        Assert.Contains(ConferenceMarkerEmail, conferenceRegisterBody);

        var sessionRegisterResponse = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/register",
            new AttendeeRegistrationRequestDto { FirstName = "AnotherSessionFirst", LastName = "AnotherSessionLast", Email = "another-session-marker@example.com" });
        var sessionRegisterBody = await sessionRegisterResponse.Content.ReadAsStringAsync();
        Assert.Contains("AnotherSessionFirst", sessionRegisterBody);
        Assert.Contains("AnotherSessionLast", sessionRegisterBody);
        Assert.Contains("another-session-marker@example.com", sessionRegisterBody);
    }
}

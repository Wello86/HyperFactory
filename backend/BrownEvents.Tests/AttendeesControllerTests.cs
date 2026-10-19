using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BrownEvents.Api.Data;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrownEvents.Tests;

public class AttendeesControllerTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"AttendeesControllerTestsDb-{Guid.NewGuid()}";

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

    [Fact]
    public async Task GetRegistrations_Returns404_ForUnknownEmail()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/attendees/{Guid.NewGuid()}@example.com/registrations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetRegistrations_Returns200WithEmptyArray_ForKnownAttendeeWithNoRegistrations()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Attendees.Add(new Attendee { FirstName = "No", LastName = "Registrations", Email = email });
            await context.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/attendees/{email}/registrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registrations = await response.Content.ReadFromJsonAsync<List<RegistrationDto>>();
        Assert.NotNull(registrations);
        Assert.Empty(registrations!);
    }

    [Fact]
    public async Task GetRegistrations_Returns200WithConferenceContext_ForKnownAttendeeWithRegistrations()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var caseInsensitive = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var email = $"{Guid.NewGuid()}@example.com";

        var conference = new Conference
        {
            Title = "Dashboard Conference",
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
        };
        var conferenceResponse = await client.PostAsJsonAsync("/api/conferences", conference);
        var createdConference = await conferenceResponse.Content.ReadFromJsonAsync<ConferenceDto>(caseInsensitive);

        await client.PostAsJsonAsync(
            $"/api/conferences/{createdConference!.Id}/register",
            new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = email });

        var response = await client.GetAsync($"/api/attendees/{email}/registrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registrations = await response.Content.ReadFromJsonAsync<List<RegistrationDto>>(caseInsensitive);
        Assert.NotNull(registrations);
        Assert.Single(registrations!);
        Assert.Equal(createdConference.Id, registrations![0].ConferenceId);
        Assert.Equal("Dashboard Conference", registrations[0].ConferenceTitle);
        Assert.Equal(conference.StartDate, registrations[0].ConferenceStartDate);
        Assert.Equal(conference.EndDate, registrations[0].ConferenceEndDate);
        Assert.Equal("CONFIRMED", registrations[0].Status);
    }
}

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

public class ConferencesControllerTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"ConferencesControllerTestsDb-{Guid.NewGuid()}";

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

    private static async Task<(int ConferenceId, int RegistrationId)> SeedConferenceAndRegistrationAsync(HttpClient client)
    {
        var caseInsensitive = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var conference = new Conference
        {
            Title = "Cancel Registration Conference",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
        };
        var conferenceResponse = await client.PostAsJsonAsync("/api/conferences", conference);
        var createdConference = await conferenceResponse.Content.ReadFromJsonAsync<ConferenceDto>(caseInsensitive);

        var attendee = new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };
        var registerResponse = await client.PostAsJsonAsync($"/api/conferences/{createdConference!.Id}/register", attendee);
        var createdRegistration = await registerResponse.Content.ReadFromJsonAsync<RegistrationDto>(caseInsensitive);

        return (createdConference.Id, createdRegistration!.Id);
    }

    [Fact]
    public async Task CancelRegistration_Returns204_OnSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (conferenceId, registrationId) = await SeedConferenceAndRegistrationAsync(client);

        var response = await client.DeleteAsync($"/api/conferences/{conferenceId}/registrations/{registrationId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var registrationsResponse = await client.GetAsync($"/api/conferences/{conferenceId}/registrations");
        var registrations = await registrationsResponse.Content.ReadFromJsonAsync<List<RegistrationDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("CANCELLED", registrations![0].Status);
    }

    [Fact]
    public async Task CancelRegistration_Returns404_WhenRegistrationNotFound()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (conferenceId, _) = await SeedConferenceAndRegistrationAsync(client);

        var response = await client.DeleteAsync($"/api/conferences/{conferenceId}/registrations/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

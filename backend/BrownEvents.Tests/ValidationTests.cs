using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using BrownEvents.Api.Data;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrownEvents.Tests;

public class ValidationTests
{
    private static WebApplicationFactory<Program> CreateFactory(string dbName)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddSingleton<AppDbContext>(_ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(dbName)
                        .Options;
                    return new AppDbContext(options);
                });
            });
        });
    }

    private static async Task<int> GetSeededConferenceIdAsync(HttpClient client)
    {
        var result = await client.GetFromJsonAsync<PagedResult<ConferenceDto>>("/api/conferences");
        Assert.NotNull(result);
        return result!.Data[0].Id;
    }

    [Fact]
    public async Task CreateConference_WithEmptyTitle_Returns400WithFieldError()
    {
        using var factory = CreateFactory(nameof(CreateConference_WithEmptyTitle_Returns400WithFieldError));
        using var client = factory.CreateClient();

        var conference = new Conference
        {
            Title = string.Empty,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
        };

        var response = await client.PostAsJsonAsync("/api/conferences", conference);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Title", out _));
    }

    [Fact]
    public async Task CreateConference_WithEmptyBody_Returns400()
    {
        using var factory = CreateFactory(nameof(CreateConference_WithEmptyBody_Returns400));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/conferences", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Title", out _));
    }

    [Fact]
    public async Task AddSession_WithValidTitle_Returns201NotBlockedByConferenceNavigation()
    {
        using var factory = CreateFactory(nameof(AddSession_WithValidTitle_Returns201NotBlockedByConferenceNavigation));
        using var client = factory.CreateClient();

        var conferenceId = await GetSeededConferenceIdAsync(client);

        var session = new Session
        {
            Title = "Valid Session",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 10,
        };

        var response = await client.PostAsJsonAsync($"/api/conferences/{conferenceId}/sessions", session);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSession_WithValidTitle_Returns200NotBlockedByConferenceNavigation()
    {
        using var factory = CreateFactory(nameof(UpdateSession_WithValidTitle_Returns200NotBlockedByConferenceNavigation));
        using var client = factory.CreateClient();

        var conferenceId = await GetSeededConferenceIdAsync(client);

        var created = await client.PostAsJsonAsync($"/api/conferences/{conferenceId}/sessions", new Session
        {
            Title = "Original Title",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 10,
        });
        created.EnsureSuccessStatusCode();
        var createdBody = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var sessionId = createdBody!.RootElement.GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync($"/api/sessions/{sessionId}", new Session
        {
            Title = "Updated Title",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 10,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAttendee_WithEmptyEmail_Returns400()
    {
        using var factory = CreateFactory(nameof(RegisterAttendee_WithEmptyEmail_Returns400));
        using var client = factory.CreateClient();

        var conferenceId = await GetSeededConferenceIdAsync(client);

        var attendee = new Attendee { FirstName = "Jane", LastName = "Doe", Email = string.Empty };

        var response = await client.PostAsJsonAsync($"/api/conferences/{conferenceId}/register", attendee);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task RegisterAttendee_WithInvalidEmailFormat_Returns400()
    {
        using var factory = CreateFactory(nameof(RegisterAttendee_WithInvalidEmailFormat_Returns400));
        using var client = factory.CreateClient();

        var conferenceId = await GetSeededConferenceIdAsync(client);

        var attendee = new Attendee { FirstName = "Jane", LastName = "Doe", Email = "not-an-email" };

        var response = await client.PostAsJsonAsync($"/api/conferences/{conferenceId}/register", attendee);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSpeaker_WithMissingFirstName_Returns400()
    {
        using var factory = CreateFactory(nameof(CreateSpeaker_WithMissingFirstName_Returns400));
        using var client = factory.CreateClient();

        var speaker = new Speaker { FirstName = string.Empty, LastName = "Doe" };

        var response = await client.PostAsJsonAsync("/api/speakers", speaker);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("FirstName", out _));
    }
}

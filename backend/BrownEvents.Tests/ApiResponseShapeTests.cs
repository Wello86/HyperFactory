using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public class ApiResponseShapeTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"ApiResponseShapeTestsDb-{Guid.NewGuid()}";

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
    public async Task Create_Conference_Returns201WithLocationAndBody()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var conference = new Conference
        {
            Title = "New Conference",
            Description = "A test conference",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
        };

        var response = await client.PostAsJsonAsync("/api/conferences", conference);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var rawBody = await response.Content.ReadAsStringAsync();
        using var rawJson = JsonDocument.Parse(rawBody);
        Assert.False(rawJson.RootElement.TryGetProperty("sessions", out _));
        Assert.False(rawJson.RootElement.TryGetProperty("registrations", out _));

        var body = JsonSerializer.Deserialize<ConferenceDto>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal("New Conference", body!.Title);
        Assert.True(body.Id > 0);
    }

    [Fact]
    public async Task Register_ThenGetRegistrations_ReturnsAttendeeDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var conference = new Conference
        {
            Title = "Registrations Conference",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
        };
        var caseInsensitive = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var conferenceResponse = await client.PostAsJsonAsync("/api/conferences", conference);
        var conferenceRawBody = await conferenceResponse.Content.ReadAsStringAsync();
        Assert.True(conferenceResponse.StatusCode == HttpStatusCode.Created, $"create status={conferenceResponse.StatusCode} body={conferenceRawBody}");
        var createdConference = JsonSerializer.Deserialize<ConferenceDto>(conferenceRawBody, caseInsensitive);

        var getBackResponse = await client.GetAsync($"/api/conferences/{createdConference!.Id}");
        var getBackBody = await getBackResponse.Content.ReadAsStringAsync();
        Assert.True(getBackResponse.StatusCode == HttpStatusCode.OK, $"getback status={getBackResponse.StatusCode} id={createdConference.Id} body={getBackBody}");

        var attendee = new Attendee { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" };
        var registerResponse = await client.PostAsJsonAsync($"/api/conferences/{createdConference!.Id}/register", attendee);
        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.StatusCode == HttpStatusCode.Created, $"register status={registerResponse.StatusCode} id={createdConference.Id} body={registerBody}");

        var response = await client.GetAsync($"/api/conferences/{createdConference.Id}/registrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registrations = await response.Content.ReadFromJsonAsync<List<RegistrationDto>>(caseInsensitive);
        Assert.NotNull(registrations);
        Assert.Single(registrations!);
        Assert.Equal("Jane", registrations![0].Attendee.FirstName);
    }

    [Fact]
    public async Task GetById_UnknownConference_Returns404ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/conferences/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task GetById_UnknownSpeaker_Returns404ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/speakers/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task GetById_SpeakerWithSessions_ReturnsProfileDtoWithOrderedSessions()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var caseInsensitive = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var conference = new Conference
        {
            Title = "Profile Conference",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
        };
        var conferenceResponse = await client.PostAsJsonAsync("/api/conferences", conference);
        var createdConference = await conferenceResponse.Content.ReadFromJsonAsync<ConferenceDto>(caseInsensitive);

        var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace", Bio = "Pioneer", Email = "ada@example.com" };
        var speakerResponse = await client.PostAsJsonAsync("/api/speakers", speaker);
        var createdSpeaker = await speakerResponse.Content.ReadFromJsonAsync<SpeakerDto>(caseInsensitive);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Sessions.Add(new Session
        {
            Title = "Closing Keynote",
            ConferenceId = createdConference!.Id,
            SpeakerId = createdSpeaker!.Id,
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(3),
            Capacity = 50,
        });
        context.Sessions.Add(new Session
        {
            Title = "Opening Keynote",
            ConferenceId = createdConference.Id,
            SpeakerId = createdSpeaker.Id,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(1.5),
            Capacity = 50,
        });
        await context.SaveChangesAsync();

        var response = await client.GetAsync($"/api/speakers/{createdSpeaker.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SpeakerProfileDto>(caseInsensitive);
        Assert.NotNull(body);
        Assert.Equal(2, body!.Sessions.Count);
        Assert.Equal("Opening Keynote", body.Sessions[0].Title);
        Assert.Equal("Closing Keynote", body.Sessions[1].Title);
        Assert.Equal("Profile Conference", body.Sessions[0].ConferenceTitle);
    }

    [Fact]
    public async Task Create_Conference_MalformedBody_Returns400ValidationProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new StringContent("{ not valid json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/conferences", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
    }
}

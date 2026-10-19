using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrownEvents.Tests;

public class SpeakerServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByIdWithSessionsAsync_SpeakerWithSessionsAcrossConferences_ReturnsSessionsOrderedByStartTimeWithConference()
    {
        using var context = CreateContext();

        var alpha = new Conference
        {
            Title = "Alpha Conf",
            StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            Status = "UPCOMING"
        };
        var beta = new Conference
        {
            Title = "Beta Conf",
            StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            Status = "UPCOMING"
        };
        context.Conferences.AddRange(alpha, beta);

        var speaker = new Speaker
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Bio = "Pioneer",
            Email = "ada@example.com"
        };
        context.Speakers.Add(speaker);
        await context.SaveChangesAsync();

        // Seeded out of chronological order: the later session is added first.
        var laterSession = new Session
        {
            Title = "Closing Keynote",
            ConferenceId = beta.Id,
            SpeakerId = speaker.Id,
            StartTime = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            Capacity = 50
        };
        var earlierSession = new Session
        {
            Title = "Opening Keynote",
            ConferenceId = alpha.Id,
            SpeakerId = speaker.Id,
            StartTime = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            Capacity = 100
        };
        context.Sessions.Add(laterSession);
        context.Sessions.Add(earlierSession);
        await context.SaveChangesAsync();

        var service = new SpeakerService(context);
        var result = await service.GetByIdWithSessionsAsync(speaker.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Sessions.Count);

        var sessions = result.Sessions.ToList();
        Assert.True(sessions[0].StartTime < sessions[1].StartTime);
        Assert.Equal("Opening Keynote", sessions[0].Title);
        Assert.Equal("Alpha Conf", sessions[0].Conference.Title);
        Assert.Equal("Beta Conf", sessions[1].Conference.Title);
    }

    [Fact]
    public async Task GetByIdWithSessionsAsync_SpeakerWithNoSessions_ReturnsSpeakerWithEmptySessions()
    {
        using var context = CreateContext();

        var speaker = new Speaker
        {
            FirstName = "Grace",
            LastName = "Hopper"
        };
        context.Speakers.Add(speaker);
        await context.SaveChangesAsync();

        var service = new SpeakerService(context);
        var result = await service.GetByIdWithSessionsAsync(speaker.Id);

        Assert.NotNull(result);
        Assert.NotNull(result!.Sessions);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public async Task UpdateAsync_ExistingSpeaker_UpdatesFieldsAndPersists()
    {
        using var context = CreateContext();
        var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace", Bio = "Old", Email = "old@example.com" };
        context.Speakers.Add(speaker);
        await context.SaveChangesAsync();

        var service = new SpeakerService(context);
        var result = await service.UpdateAsync(speaker.Id, new Speaker
        {
            FirstName = "Grace",
            LastName = "Hopper",
            Bio = "New",
            Email = "new@example.com"
        });

        Assert.NotNull(result);
        Assert.Equal("Grace", result!.FirstName);
        Assert.Equal("Hopper", result.LastName);
        Assert.Equal("New", result.Bio);
        Assert.Equal("new@example.com", result.Email);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new SpeakerService(context);
        var result = await service.UpdateAsync(999, new Speaker { FirstName = "X", LastName = "Y" });
        Assert.Null(result);
    }
}

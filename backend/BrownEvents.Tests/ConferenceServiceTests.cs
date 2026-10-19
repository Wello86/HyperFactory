using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrownEvents.Tests;

public class ConferenceServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConferences()
    {
        using var context = CreateContext();
        context.Conferences.AddRange(
            new Conference { Title = "Conf A", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" },
            new Conference { Title = "Conf B", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(2), Status = "UPCOMING" }
        );
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetAllAsync(1, 20);

        Assert.Equal(2, result.Data.Count);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCorrectSlice_ForSecondPage()
    {
        using var context = CreateContext();
        for (var i = 1; i <= 5; i++)
        {
            context.Conferences.Add(new Conference { Title = $"Conf {i}", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" });
        }
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetAllAsync(2, 2);

        Assert.Equal(2, result.Data.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal("Conf 3", result.Data[0].Title);
        Assert.Equal("Conf 4", result.Data[1].Title);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyData_WhenPageIsPastLastPage()
    {
        using var context = CreateContext();
        context.Conferences.Add(new Conference { Title = "Conf A", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" });
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetAllAsync(999, 20);

        Assert.Empty(result.Data);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByKeyword_MatchingTitleOrDescription()
    {
        using var context = CreateContext();
        context.Conferences.AddRange(
            new Conference { Title = "AI Summit", Description = "Talks on machine learning", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" },
            new Conference { Title = "Cooking Expo", Description = "Recipes and techniques", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" },
            new Conference { Title = "Garden Fair", Description = null, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" }
        );
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetAllAsync(1, 20, search: "machine");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("AI Summit", result.Data[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatus_MatchingStatusCaseInsensitively()
    {
        using var context = CreateContext();
        context.Conferences.AddRange(
            new Conference { Title = "Upcoming Conf", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" },
            new Conference { Title = "Active Conf", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "ACTIVE" },
            new Conference { Title = "Cancelled Conf", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "CANCELLED" }
        );
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetAllAsync(1, 20, status: "active");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Active Conf", result.Data[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDateRange_UsingOverlapSemantics()
    {
        using var context = CreateContext();
        context.Conferences.AddRange(
            new Conference { Title = "Before Window", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 5), Status = "UPCOMING" },
            new Conference { Title = "Overlaps Start", StartDate = new DateTime(2026, 1, 8), EndDate = new DateTime(2026, 1, 20), Status = "UPCOMING" },
            new Conference { Title = "After Window", StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2026, 2, 5), Status = "UPCOMING" }
        );
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetAllAsync(1, 20, from: new DateTime(2026, 1, 10), to: new DateTime(2026, 1, 31));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Overlaps Start", result.Data[0].Title);
    }

    [Fact]
    public async Task CreateAsync_PersistsConference()
    {
        using var context = CreateContext();
        var service = new ConferenceService(context);

        var conf = new Conference
        {
            Title = "New Conf",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = "UPCOMING"
        };

        var result = await service.CreateAsync(conf);

        Assert.True(result.Id > 0);
        Assert.Equal("New Conf", result.Title);
    }

    [Fact]
    public async Task UpdateAsync_ChangesTitle()
    {
        using var context = CreateContext();
        var existing = new Conference
        {
            Title = "Old Title",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = "UPCOMING"
        };
        context.Conferences.Add(existing);
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var updated = new Conference
        {
            Title = "New Title",
            StartDate = existing.StartDate,
            EndDate = existing.EndDate,
            Status = "active"
        };

        var result = await service.UpdateAsync(existing.Id, updated);

        Assert.NotNull(result);
        Assert.Equal("New Title", result!.Title);
        Assert.Equal("ACTIVE", result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        using var context = CreateContext();
        var service = new ConferenceService(context);

        var result = await service.UpdateAsync(999, new Conference { Title = "X", Status = "UPCOMING" });

        Assert.Null(result);
    }

    [Fact]
    public async Task AddSessionAsync_PersistsSession_AndSetsConferenceIdFromRoute()
    {
        using var context = CreateContext();
        var conf = new Conference
        {
            Title = "Conf A",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = "UPCOMING"
        };
        context.Conferences.Add(conf);
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var session = new Session
        {
            Title = "New Session",
            ConferenceId = 999,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 50
        };

        var result = await service.AddSessionAsync(conf.Id, session);

        Assert.True(result.Id > 0);
        Assert.Equal("New Session", result.Title);
        Assert.Equal(conf.Id, result.ConferenceId);
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsOnlyConferenceSessions()
    {
        using var context = CreateContext();
        var conf1 = new Conference { Title = "C1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
        var conf2 = new Conference { Title = "C2", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
        context.Conferences.AddRange(conf1, conf2);
        await context.SaveChangesAsync();

        context.Sessions.AddRange(
            new Session { Title = "S1", ConferenceId = conf1.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 },
            new Session { Title = "S2", ConferenceId = conf1.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 },
            new Session { Title = "S3", ConferenceId = conf2.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 }
        );
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetSessionsAsync(conf1.Id, 1, 20);

        Assert.Equal(2, result.Data.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Data, s => Assert.Equal(conf1.Id, s.ConferenceId));
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsEmptyData_WhenPageIsPastLastPage()
    {
        using var context = CreateContext();
        var conf = new Conference { Title = "C1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
        context.Conferences.Add(conf);
        await context.SaveChangesAsync();

        context.Sessions.Add(new Session { Title = "S1", ConferenceId = conf.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 });
        await context.SaveChangesAsync();

        var service = new ConferenceService(context);
        var result = await service.GetSessionsAsync(conf.Id, 999, 20);

        Assert.Empty(result.Data);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetSessionsAsync_EagerLoadsSpeakerAndRoom()
    {
        // Seed with its own context, then dispose it before querying: if seed and query share a
        // context, EF's in-memory identity resolution auto-wires navigations to already-tracked
        // entities and IsLoaded becomes true regardless of whether the service used Include,
        // masking the very bug this test exists to catch.
        var dbName = Guid.NewGuid().ToString();
        int confId, speakerId, roomId;
        using (var seedContext = CreateContext(dbName))
        {
            var conf = new Conference { Title = "C1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
            seedContext.Conferences.Add(conf);
            var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace" };
            var room = new Room { Name = "Hall A", Capacity = 100 };
            seedContext.Speakers.Add(speaker);
            seedContext.Rooms.Add(room);
            await seedContext.SaveChangesAsync();

            seedContext.Sessions.AddRange(
                new Session { Title = "S1", ConferenceId = conf.Id, SpeakerId = speaker.Id, RoomId = room.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 },
                new Session { Title = "S2", ConferenceId = conf.Id, SpeakerId = speaker.Id, RoomId = room.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 }
            );
            await seedContext.SaveChangesAsync();
            confId = conf.Id;
            speakerId = speaker.Id;
            roomId = room.Id;
        }

        using var context = CreateContext(dbName);
        var service = new ConferenceService(context);
        var result = await service.GetSessionsAsync(confId, 1, 20);

        Assert.All(result.Data, s =>
        {
            Assert.NotNull(s.Speaker);
            Assert.NotNull(s.Room);
        });
    }

    [Fact]
    public async Task GetByIdAsync_EagerLoadsSessionsWithSpeakerAndRoom()
    {
        var dbName = Guid.NewGuid().ToString();
        int confId;
        using (var seedContext = CreateContext(dbName))
        {
            var conf = new Conference { Title = "C1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
            seedContext.Conferences.Add(conf);
            var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace" };
            var room = new Room { Name = "Hall A", Capacity = 100 };
            seedContext.Speakers.Add(speaker);
            seedContext.Rooms.Add(room);
            await seedContext.SaveChangesAsync();

            seedContext.Sessions.Add(new Session { Title = "S1", ConferenceId = conf.Id, SpeakerId = speaker.Id, RoomId = room.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Capacity = 50 });
            await seedContext.SaveChangesAsync();
            confId = conf.Id;
        }

        using var context = CreateContext(dbName);
        var service = new ConferenceService(context);
        var result = await service.GetByIdAsync(confId);

        Assert.NotNull(result);
        var loadedSession = result!.Sessions.Single();
        Assert.NotNull(loadedSession.Speaker);
        Assert.NotNull(loadedSession.Room);
    }

    [Fact]
    public async Task GetAllAsync_ThrowsWhenCancelled()
    {
        using var context = CreateContext();
        var service = new ConferenceService(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAllAsync(1, 20, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenCancelled()
    {
        // Seed and query use separate context instances: FindAsync resolves from the local
        // change tracker (skipping the store and the CancellationToken check entirely) if the
        // entity is already tracked in the same context that calls UpdateAsync.
        var dbName = Guid.NewGuid().ToString();
        int confId;
        using (var seedContext = CreateContext(dbName))
        {
            var existing = new Conference { Title = "Old Title", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
            seedContext.Conferences.Add(existing);
            await seedContext.SaveChangesAsync();
            confId = existing.Id;
        }

        using var context = CreateContext(dbName);
        var service = new ConferenceService(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.UpdateAsync(confId, new Conference { Title = "New Title", Status = "ACTIVE" }, cts.Token));
    }
}

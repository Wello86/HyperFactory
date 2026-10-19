using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrownEvents.Tests;

public class SessionServiceTests
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

    private static Session CreateSession(AppDbContext context)
    {
        var conf = new Conference
        {
            Title = "Test Conf",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = "UPCOMING"
        };
        context.Conferences.Add(conf);
        context.SaveChanges();

        var session = new Session
        {
            Title = "Test Session",
            ConferenceId = conf.Id,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 100
        };
        context.Sessions.Add(session);
        context.SaveChanges();
        return session;
    }

    [Fact]
    public async Task UpdateAsync_ChangesSessionTitle()
    {
        using var context = CreateContext();
        var session = CreateSession(context);

        var service = new SessionService(context);
        var updated = new Session
        {
            Title = "Updated Title",
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Capacity = 200,
            SpeakerId = null,
            RoomId = null
        };

        var result = await service.UpdateAsync(session.Id, updated);

        Assert.NotNull(result);
        Assert.Equal("Updated Title", result!.Title);
        Assert.Equal(200, result.Capacity);
    }

    [Fact]
    public async Task GetByIdAsync_EagerLoadsSpeakerAndRoom()
    {
        // Seed and query use separate context instances against the same in-memory database:
        // sharing one context would let EF's identity resolution auto-wire navigations to
        // already-tracked entities, making IsLoaded true even without Include and hiding the bug.
        var dbName = Guid.NewGuid().ToString();
        int sessionId;
        using (var seedContext = CreateContext(dbName))
        {
            var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace" };
            var room = new Room { Name = "Hall A", Capacity = 100 };
            seedContext.Speakers.Add(speaker);
            seedContext.Rooms.Add(room);
            seedContext.SaveChanges();

            var session = CreateSession(seedContext);
            session.SpeakerId = speaker.Id;
            session.RoomId = room.Id;
            seedContext.SaveChanges();
            sessionId = session.Id;
        }

        using var context = CreateContext(dbName);
        var service = new SessionService(context);
        var result = await service.GetByIdAsync(sessionId);

        Assert.NotNull(result);
        Assert.NotNull(result!.Speaker);
        Assert.NotNull(result.Room);
    }

    [Fact]
    public async Task UpdateAsync_EagerLoadsSpeakerAndRoom()
    {
        var dbName = Guid.NewGuid().ToString();
        int sessionId, speakerId, roomId;
        DateTime startTime, endTime;
        using (var seedContext = CreateContext(dbName))
        {
            var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace" };
            var room = new Room { Name = "Hall A", Capacity = 100 };
            seedContext.Speakers.Add(speaker);
            seedContext.Rooms.Add(room);
            seedContext.SaveChanges();

            var session = CreateSession(seedContext);
            sessionId = session.Id;
            speakerId = speaker.Id;
            roomId = room.Id;
            startTime = session.StartTime;
            endTime = session.EndTime;
        }

        using var context = CreateContext(dbName);
        var service = new SessionService(context);
        var updated = new Session
        {
            Title = "Updated Title",
            StartTime = startTime,
            EndTime = endTime,
            Capacity = 200,
            SpeakerId = speakerId,
            RoomId = roomId
        };

        var result = await service.UpdateAsync(sessionId, updated);

        Assert.NotNull(result);
        Assert.True(context.Entry(result!).Reference(s => s.Speaker).IsLoaded, "Speaker should be eager-loaded via Include");
        Assert.True(context.Entry(result!).Reference(s => s.Room).IsLoaded, "Room should be eager-loaded via Include");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        using var context = CreateContext();
        var service = new SessionService(context);

        var result = await service.UpdateAsync(9999, new Session
        {
            Title = "Ghost",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = 10
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenCancelled()
    {
        // Seed and query use separate context instances: FindAsync resolves from the local
        // change tracker (skipping the store and the CancellationToken check entirely) if the
        // entity is already tracked in the same context that calls UpdateAsync.
        var dbName = Guid.NewGuid().ToString();
        int sessionId;
        DateTime startTime, endTime;
        using (var seedContext = CreateContext(dbName))
        {
            var session = CreateSession(seedContext);
            sessionId = session.Id;
            startTime = session.StartTime;
            endTime = session.EndTime;
        }

        using var context = CreateContext(dbName);
        var service = new SessionService(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UpdateAsync(sessionId, new Session
        {
            Title = "Updated Title",
            StartTime = startTime,
            EndTime = endTime,
            Capacity = 200
        }, cts.Token));
    }
}

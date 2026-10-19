using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrownEvents.Tests;

public class RoomServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRooms()
    {
        using var context = CreateContext();
        context.Rooms.AddRange(
            new Room { Name = "Hall A", Capacity = 100 },
            new Room { Name = "Room 101", Capacity = 50 }
        );
        await context.SaveChangesAsync();

        var service = new RoomService(context);
        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Name == "Hall A");
        Assert.Contains(result, r => r.Name == "Room 101");
    }
}

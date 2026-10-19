using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BrownEvents.Api.Services;

public class RoomService : IRoomService
{
    private readonly AppDbContext _context;

    public RoomService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Room>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Rooms.AsNoTracking().ToListAsync(cancellationToken);
    }
}

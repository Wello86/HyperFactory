using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BrownEvents.Api.Services;

public class SessionService : ISessionService
{
    private readonly AppDbContext _context;

    public SessionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Session?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Speaker/Room are `virtual` (lazy-loading proxies), so FindAsync alone leaves them
        // unloaded and each gets fetched with its own query the moment they're serialized.
        // Include keeps this to a single query, consistent with the fix in ConferenceService.
        return await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Speaker)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Session?> UpdateAsync(int id, Session updated, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Sessions.FindAsync(new object?[] { id }, cancellationToken);
        if (existing is null) return null;

        existing.Title = updated.Title;
        existing.Description = updated.Description;
        existing.StartTime = updated.StartTime;
        existing.EndTime = updated.EndTime;
        existing.Capacity = updated.Capacity;
        existing.SpeakerId = updated.SpeakerId;
        existing.RoomId = updated.RoomId;

        await _context.SaveChangesAsync(cancellationToken);

        // Speaker/Room FKs may have just changed above, so load them explicitly (reflecting the
        // new FK values) rather than leaving them for the lazy-loading proxy to fetch one at a
        // time on serialization.
        await _context.Entry(existing).Reference(s => s.Speaker).LoadAsync(cancellationToken);
        await _context.Entry(existing).Reference(s => s.Room).LoadAsync(cancellationToken);
        return existing;
    }
}

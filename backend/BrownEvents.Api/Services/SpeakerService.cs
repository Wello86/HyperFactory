using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BrownEvents.Api.Services;

public class SpeakerService : ISpeakerService
{
    private readonly AppDbContext _context;

    public SpeakerService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Speaker>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Speakers.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<Speaker?> GetByIdWithSessionsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Speakers
            .AsNoTracking()
            .Include(s => s.Sessions.OrderBy(x => x.StartTime))
                .ThenInclude(x => x.Conference)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Speaker> CreateAsync(Speaker speaker, CancellationToken cancellationToken = default)
    {
        _context.Speakers.Add(speaker);
        await _context.SaveChangesAsync(cancellationToken);
        return speaker;
    }

    public async Task<Speaker?> UpdateAsync(int id, Speaker updated, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Speakers.FindAsync(new object?[] { id }, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.FirstName = updated.FirstName;
        existing.LastName = updated.LastName;
        existing.Bio = updated.Bio;
        existing.Email = updated.Email;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }
}

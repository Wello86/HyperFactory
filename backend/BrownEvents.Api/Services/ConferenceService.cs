using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BrownEvents.Api.Services;

public class ConferenceService : IConferenceService
{
    private readonly AppDbContext _context;

    public ConferenceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Conference>> GetAllAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Conferences.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term) || (c.Description != null && c.Description.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLower();
            query = query.Where(c => c.Status.ToLower() == normalizedStatus);
        }

        if (from.HasValue)
        {
            query = query.Where(c => c.EndDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(c => c.StartDate <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Same N+1-avoidance rationale as before: Include Sessions/Speaker/Room before ToListAsync.
        var data = await query
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Sessions).ThenInclude(s => s.Speaker)
            .Include(c => c.Sessions).ThenInclude(s => s.Room)
            .ToListAsync(cancellationToken);

        return new PagedResult<Conference>
        {
            Data = data,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<Conference?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Sessions/Speaker/Room are all `virtual` (lazy-loading proxies), so without Include,
        // iterating this conference's Sessions collection triggers one lazy-load query per
        // session for Speaker and one for Room (N+1). Eager-load them here so the query count
        // stays constant regardless of session count.
        return await _context.Conferences
            .AsNoTracking()
            .Include(c => c.Sessions).ThenInclude(s => s.Speaker)
            .Include(c => c.Sessions).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Conference> CreateAsync(Conference conference, CancellationToken cancellationToken = default)
    {
        _context.Conferences.Add(conference);
        await _context.SaveChangesAsync(cancellationToken);
        return conference;
    }

    public async Task<Conference?> UpdateAsync(int id, Conference updated, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Conferences.FindAsync(new object?[] { id }, cancellationToken);
        if (existing is null) return null;

        existing.Title = updated.Title;
        existing.Description = updated.Description;
        existing.Location = updated.Location;
        existing.Venue = updated.Venue;
        existing.StartDate = updated.StartDate;
        existing.EndDate = updated.EndDate;
        existing.Status = updated.Status.ToUpper();
        existing.MaxAttendees = updated.MaxAttendees;
        existing.OrganizerName = updated.OrganizerName;
        existing.Website = updated.Website;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<PagedResult<Session>> GetSessionsAsync(int conferenceId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Sessions.AsNoTracking().Where(s => s.ConferenceId == conferenceId);
        var totalCount = await query.CountAsync(cancellationToken);

        // Root cause of the "conference page slow with many sessions" bug: Session.Speaker and
        // Session.Room are `virtual` (lazy-loading proxies enabled on AppDbContext), so without
        // Include, JSON-serializing this list triggers a separate lazy-load query per session for
        // each of those navigations (N+1: 1 + 2*sessionCount queries). Include bounds it to 1 query.
        var data = await query
            .OrderBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(s => s.Speaker)
            .Include(s => s.Room)
            .ToListAsync(cancellationToken);

        return new PagedResult<Session>
        {
            Data = data,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<Session> AddSessionAsync(int conferenceId, Session session, CancellationToken cancellationToken = default)
    {
        session.ConferenceId = conferenceId;
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<List<Registration>> GetRegistrationsAsync(int conferenceId, CancellationToken cancellationToken = default)
    {
        // Attendee is `virtual` (lazy-loading proxy); without Include, RegistrationDto's mapper
        // (which has no null-guard, unlike Session's Speaker/Room) throws when lazy-loading isn't
        // configured, and otherwise triggers one query per registration (N+1).
        return await _context.Registrations
            .AsNoTracking()
            .Where(r => r.ConferenceId == conferenceId)
            .Include(r => r.Attendee)
            .Include(r => r.Conference)
            .ToListAsync(cancellationToken);
    }
}

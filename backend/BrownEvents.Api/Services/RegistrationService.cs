using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BrownEvents.Api.Services;

public class RegistrationService : IRegistrationService
{
    private readonly AppDbContext _context;

    public RegistrationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Registration> RegisterAttendeeAsync(int conferenceId, Attendee attendeeData, CancellationToken cancellationToken = default)
    {
        // Without this transaction, a failure between the two saves below (e.g. the process
        // dying, or the registration insert failing) would leave the attendee row committed
        // with no matching registration — an orphaned attendee that looks registered nowhere.
        // The in-memory provider used by tests doesn't support real transactions, so only
        // start one against a relational database (Npgsql in production).
        using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        var existing = await _context.Attendees.FirstOrDefaultAsync(a => a.Email == attendeeData.Email, cancellationToken);

        Attendee attendee;
        if (existing is null)
        {
            _context.Attendees.Add(attendeeData);
            await _context.SaveChangesAsync(cancellationToken);
            attendee = attendeeData;
        }
        else
        {
            attendee = existing;
        }

        // ToDto() reads registration.Conference directly; a freshly-constructed Registration has
        // no tracked Conference for EF's relationship fixup to attach, so load it explicitly.
        var conference = await _context.Conferences.FindAsync(new object?[] { conferenceId }, cancellationToken);

        var registration = new Registration
        {
            ConferenceId = conferenceId,
            Conference = conference!,
            AttendeeId = attendee.Id,
            Attendee = attendee,
            RegisteredAt = DateTime.UtcNow,
            Status = "CONFIRMED"
        };

        _context.Registrations.Add(registration);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }

        return registration;
    }

    public async Task<IReadOnlyList<Registration>> GetByAttendeeEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLower();

        return await _context.Registrations
            .AsNoTracking()
            .Include(r => r.Attendee)
            .Include(r => r.Conference)
            .Where(r => r.Attendee.Email.ToLower() == normalizedEmail)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AttendeeExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLower();

        return await _context.Attendees.AnyAsync(a => a.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task<RegistrationCancellationResult> CancelRegistrationAsync(int conferenceId, int registrationId, CancellationToken cancellationToken = default)
    {
        using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var registration = await _context.Registrations.FirstOrDefaultAsync(
            r => r.Id == registrationId && r.ConferenceId == conferenceId && r.Status == "CONFIRMED",
            cancellationToken);

        if (registration is null)
        {
            return RegistrationCancellationResult.Failure(RegistrationCancellationOutcome.NotFound);
        }

        registration.Status = "CANCELLED";
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return RegistrationCancellationResult.Success();
    }
}

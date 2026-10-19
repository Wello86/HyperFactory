using BrownEvents.Api.Data;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrownEvents.Api.Services;

public class SessionRegistrationService : ISessionRegistrationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SessionRegistrationService> _logger;

    public SessionRegistrationService(AppDbContext context, ILogger<SessionRegistrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SessionRegistrationResult> RegisterAttendeeAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default)
    {
        using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var normalizedEmail = attendeeData.Email.Trim();
        var existingAttendee = await _context.Attendees
            .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail.ToLower(), cancellationToken);

        Attendee attendee;
        if (existingAttendee is null)
        {
            attendee = new Attendee
            {
                FirstName = attendeeData.FirstName,
                LastName = attendeeData.LastName,
                Email = normalizedEmail,
            };
            _context.Attendees.Add(attendee);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            attendee = existingAttendee;
        }

        var alreadyRegistered = await _context.SessionRegistrations.AnyAsync(
            sr => sr.SessionId == sessionId && sr.AttendeeId == attendee.Id && sr.Status == "CONFIRMED",
            cancellationToken);
        if (alreadyRegistered)
        {
            return SessionRegistrationResult.Failure(SessionRegistrationOutcome.AlreadyRegistered);
        }

        var confirmedCount = await _context.SessionRegistrations.CountAsync(
            sr => sr.SessionId == sessionId && sr.Status == "CONFIRMED", cancellationToken);
        if (confirmedCount >= capacity)
        {
            return SessionRegistrationResult.Failure(SessionRegistrationOutcome.SessionFull);
        }

        var registration = new SessionRegistration
        {
            SessionId = sessionId,
            AttendeeId = attendee.Id,
            RegisteredAt = DateTime.UtcNow,
            Status = "CONFIRMED",
        };

        _context.SessionRegistrations.Add(registration);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return SessionRegistrationResult.Success(registration);
    }

    public async Task<WaitlistJoinResult> JoinWaitlistAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default)
    {
        using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var normalizedEmail = attendeeData.Email.Trim();
        var existingAttendee = await _context.Attendees
            .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail.ToLower(), cancellationToken);

        if (existingAttendee is not null)
        {
            var alreadyConfirmed = await _context.SessionRegistrations.AnyAsync(
                sr => sr.SessionId == sessionId && sr.AttendeeId == existingAttendee.Id && sr.Status == "CONFIRMED",
                cancellationToken);
            if (alreadyConfirmed)
            {
                return WaitlistJoinResult.Failure(WaitlistJoinOutcome.AlreadyRegistered);
            }

            var alreadyWaitlisted = await _context.SessionRegistrations.AnyAsync(
                sr => sr.SessionId == sessionId && sr.AttendeeId == existingAttendee.Id && sr.Status == "WAITLISTED",
                cancellationToken);
            if (alreadyWaitlisted)
            {
                return WaitlistJoinResult.Failure(WaitlistJoinOutcome.AlreadyWaitlisted);
            }
        }

        var confirmedCount = await _context.SessionRegistrations.CountAsync(
            sr => sr.SessionId == sessionId && sr.Status == "CONFIRMED", cancellationToken);
        if (confirmedCount < capacity)
        {
            return WaitlistJoinResult.Failure(WaitlistJoinOutcome.SessionNotFull);
        }

        var attendee = existingAttendee;
        if (attendee is null)
        {
            attendee = new Attendee
            {
                FirstName = attendeeData.FirstName,
                LastName = attendeeData.LastName,
                Email = normalizedEmail,
            };
            _context.Attendees.Add(attendee);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var registration = new SessionRegistration
        {
            SessionId = sessionId,
            AttendeeId = attendee.Id,
            RegisteredAt = DateTime.UtcNow,
            Status = "WAITLISTED",
        };

        _context.SessionRegistrations.Add(registration);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return WaitlistJoinResult.Success(registration);
    }

    public async Task<SessionCancellationResult> CancelRegistrationAsync(int sessionId, int registrationId, CancellationToken cancellationToken = default)
    {
        using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var registration = await _context.SessionRegistrations.FirstOrDefaultAsync(
            sr => sr.Id == registrationId && sr.SessionId == sessionId && sr.Status == "CONFIRMED",
            cancellationToken);

        if (registration is null)
        {
            return SessionCancellationResult.Failure(SessionCancellationOutcome.NotFound);
        }

        registration.Status = "CANCELLED";
        await _context.SaveChangesAsync(cancellationToken);

        var nextWaitlisted = await _context.SessionRegistrations
            .Where(sr => sr.SessionId == sessionId && sr.Status == "WAITLISTED")
            .OrderBy(sr => sr.RegisteredAt)
            .ThenBy(sr => sr.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextWaitlisted is not null)
        {
            nextWaitlisted.Status = "CONFIRMED";
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Promoted attendee {AttendeeId} to confirmed registration for session {SessionId}",
                nextWaitlisted.AttendeeId, sessionId);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return SessionCancellationResult.Success();
    }

    public async Task<SessionRegistrationSummary> GetSummaryAsync(int sessionId, string? email, CancellationToken cancellationToken = default)
    {
        var registeredCount = await _context.SessionRegistrations.CountAsync(
            sr => sr.SessionId == sessionId && sr.Status == "CONFIRMED", cancellationToken);

        var waitlistCount = await _context.SessionRegistrations.CountAsync(
            sr => sr.SessionId == sessionId && sr.Status == "WAITLISTED", cancellationToken);

        int? currentAttendeeRegistrationId = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim();
            currentAttendeeRegistrationId = await _context.SessionRegistrations
                .Where(sr => sr.SessionId == sessionId
                             && sr.Status == "CONFIRMED"
                             && sr.Attendee.Email.ToLower() == normalizedEmail.ToLower())
                .Select(sr => (int?)sr.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new SessionRegistrationSummary
        {
            RegisteredCount = registeredCount,
            WaitlistCount = waitlistCount,
            CurrentAttendeeRegistrationId = currentAttendeeRegistrationId,
        };
    }
}

using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;

namespace BrownEvents.Api.Services;

public enum SessionRegistrationOutcome
{
    Success,
    AlreadyRegistered,
    SessionFull,
}

public class SessionRegistrationResult
{
    public SessionRegistrationOutcome Outcome { get; init; }
    public SessionRegistration? Registration { get; init; }

    public static SessionRegistrationResult Success(SessionRegistration registration) =>
        new() { Outcome = SessionRegistrationOutcome.Success, Registration = registration };

    public static SessionRegistrationResult Failure(SessionRegistrationOutcome outcome) =>
        new() { Outcome = outcome };
}

public enum SessionCancellationOutcome
{
    Success,
    NotFound,
}

public class SessionCancellationResult
{
    public SessionCancellationOutcome Outcome { get; init; }

    public static SessionCancellationResult Success() => new() { Outcome = SessionCancellationOutcome.Success };

    public static SessionCancellationResult Failure(SessionCancellationOutcome outcome) =>
        new() { Outcome = outcome };
}

public enum WaitlistJoinOutcome
{
    Success,
    AlreadyRegistered,
    AlreadyWaitlisted,
    SessionNotFull,
}

public class WaitlistJoinResult
{
    public WaitlistJoinOutcome Outcome { get; init; }
    public SessionRegistration? Registration { get; init; }

    public static WaitlistJoinResult Success(SessionRegistration registration) =>
        new() { Outcome = WaitlistJoinOutcome.Success, Registration = registration };

    public static WaitlistJoinResult Failure(WaitlistJoinOutcome outcome) =>
        new() { Outcome = outcome };
}

public class SessionRegistrationSummary
{
    public int RegisteredCount { get; init; }
    public int WaitlistCount { get; init; }
    public int? CurrentAttendeeRegistrationId { get; init; }
}

public interface ISessionRegistrationService
{
    Task<SessionRegistrationResult> RegisterAttendeeAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default);
    Task<WaitlistJoinResult> JoinWaitlistAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default);
    Task<SessionCancellationResult> CancelRegistrationAsync(int sessionId, int registrationId, CancellationToken cancellationToken = default);
    Task<SessionRegistrationSummary> GetSummaryAsync(int sessionId, string? email, CancellationToken cancellationToken = default);
}

using BrownEvents.Api.Models;

namespace BrownEvents.Api.Services;

public interface IRegistrationService
{
    Task<Registration> RegisterAttendeeAsync(int conferenceId, Attendee attendeeData, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetByAttendeeEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> AttendeeExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<RegistrationCancellationResult> CancelRegistrationAsync(int conferenceId, int registrationId, CancellationToken cancellationToken = default);
}

public enum RegistrationCancellationOutcome
{
    Success,
    NotFound
}

public class RegistrationCancellationResult
{
    public RegistrationCancellationOutcome Outcome { get; init; }

    public static RegistrationCancellationResult Success() => new() { Outcome = RegistrationCancellationOutcome.Success };

    public static RegistrationCancellationResult Failure(RegistrationCancellationOutcome outcome) => new() { Outcome = outcome };
}

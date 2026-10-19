using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using BrownEvents.Api.SwaggerExamples;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.Controllers;

/// <summary>
/// Manages individual conference sessions.
/// </summary>
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _service;
    private readonly ISessionRegistrationService _registrationService;

    public SessionsController(ISessionService service, ISessionRegistrationService registrationService)
    {
        _service = service;
        _registrationService = registrationService;
    }

    /// <summary>
    /// Gets a single session by its id.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="email">Optional attendee email used to check whether that attendee is registered for this session.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The requested session.</response>
    /// <response code="404">No session exists with the given id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, [FromQuery] string? email, CancellationToken cancellationToken)
    {
        var session = await _service.GetByIdAsync(id, cancellationToken);
        if (session is null) return NotFound(SessionNotFound(id));

        var summary = await _registrationService.GetSummaryAsync(id, email, cancellationToken);
        return Ok(session.ToDto(summary.RegisteredCount, summary.WaitlistCount, summary.CurrentAttendeeRegistrationId));
    }

    /// <summary>
    /// Updates an existing session.
    /// </summary>
    /// <param name="id">The id of the session to update.</param>
    /// <param name="session">The updated session data.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The updated session.</response>
    /// <response code="404">No session exists with the given id.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [SwaggerRequestExample(typeof(Session), typeof(SessionExample))]
    public async Task<IActionResult> Update(int id, [FromBody] Session session, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, session, cancellationToken);
        if (updated is null) return NotFound(SessionNotFound(id));
        return Ok(updated.ToDto());
    }

    /// <summary>
    /// Registers an attendee for a session.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="request">The attendee to register.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="201">The created registration.</response>
    /// <response code="404">No session exists with the given id.</response>
    /// <response code="409">The attendee is already registered, or the session is full.</response>
    [HttpPost("{id}/register")]
    [ProducesResponseType(typeof(SessionRegistrationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [SwaggerRequestExample(typeof(AttendeeRegistrationRequestDto), typeof(AttendeeRegistrationRequestExample))]
    public async Task<IActionResult> Register(int id, [FromBody] AttendeeRegistrationRequestDto request, CancellationToken cancellationToken)
    {
        var session = await _service.GetByIdAsync(id, cancellationToken);
        if (session is null) return NotFound(SessionNotFound(id));

        var result = await _registrationService.RegisterAttendeeAsync(id, session.Capacity, request, cancellationToken);

        return result.Outcome switch
        {
            SessionRegistrationOutcome.Success =>
                Created($"api/sessions/{id}/registrations/{result.Registration!.Id}", result.Registration.ToDto()),
            SessionRegistrationOutcome.AlreadyRegistered => Conflict(AlreadyRegistered(id)),
            SessionRegistrationOutcome.SessionFull => Conflict(SessionFull(id)),
            _ => Problem(),
        };
    }

    /// <summary>
    /// Adds an attendee to a session's waitlist once the session is at capacity.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="request">The attendee to add to the waitlist.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="201">The created waitlist entry.</response>
    /// <response code="404">No session exists with the given id.</response>
    /// <response code="409">The attendee is already registered or waitlisted, or the session is not yet full.</response>
    [HttpPost("{id}/waitlist")]
    [ProducesResponseType(typeof(SessionRegistrationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [SwaggerRequestExample(typeof(AttendeeRegistrationRequestDto), typeof(AttendeeRegistrationRequestExample))]
    public async Task<IActionResult> JoinWaitlist(int id, [FromBody] AttendeeRegistrationRequestDto request, CancellationToken cancellationToken)
    {
        var session = await _service.GetByIdAsync(id, cancellationToken);
        if (session is null) return NotFound(SessionNotFound(id));

        var result = await _registrationService.JoinWaitlistAsync(id, session.Capacity, request, cancellationToken);

        return result.Outcome switch
        {
            WaitlistJoinOutcome.Success =>
                Created($"api/sessions/{id}/registrations/{result.Registration!.Id}", result.Registration.ToDto()),
            WaitlistJoinOutcome.AlreadyRegistered => Conflict(AlreadyRegistered(id)),
            WaitlistJoinOutcome.AlreadyWaitlisted => Conflict(AlreadyWaitlisted(id)),
            WaitlistJoinOutcome.SessionNotFull => Conflict(SessionNotFull(id)),
            _ => Problem(),
        };
    }

    /// <summary>
    /// Cancels an attendee's registration for a session.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="registrationId">The registration id to cancel.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="204">The registration was cancelled.</response>
    /// <response code="404">No session exists with the given id, or no active registration exists with the given id.</response>
    [HttpDelete("{id}/registrations/{registrationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRegistration(int id, int registrationId, CancellationToken cancellationToken)
    {
        var session = await _service.GetByIdAsync(id, cancellationToken);
        if (session is null) return NotFound(SessionNotFound(id));

        var result = await _registrationService.CancelRegistrationAsync(id, registrationId, cancellationToken);
        if (result.Outcome == SessionCancellationOutcome.NotFound)
        {
            return NotFound(RegistrationNotFound(id, registrationId));
        }

        return NoContent();
    }

    private static ProblemDetails SessionNotFound(int id) => new()
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = $"No session exists with id {id}.",
    };

    private static ProblemDetails AlreadyRegistered(int sessionId) => new()
    {
        Title = "Conflict",
        Status = StatusCodes.Status409Conflict,
        Detail = $"This attendee is already registered for session {sessionId}.",
    };

    private static ProblemDetails SessionFull(int sessionId) => new()
    {
        Title = "Conflict",
        Status = StatusCodes.Status409Conflict,
        Detail = $"Session {sessionId} has reached its capacity.",
    };

    private static ProblemDetails AlreadyWaitlisted(int sessionId) => new()
    {
        Title = "Conflict",
        Status = StatusCodes.Status409Conflict,
        Detail = $"This attendee is already on the waitlist for session {sessionId}.",
    };

    private static ProblemDetails SessionNotFull(int sessionId) => new()
    {
        Title = "Conflict",
        Status = StatusCodes.Status409Conflict,
        Detail = $"Session {sessionId} is not yet at capacity; use /register instead.",
    };

    private static ProblemDetails RegistrationNotFound(int sessionId, int registrationId) => new()
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = $"No active registration {registrationId} exists for session {sessionId}.",
    };
}

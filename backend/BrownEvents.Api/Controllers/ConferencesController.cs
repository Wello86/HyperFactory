using System.Linq;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using BrownEvents.Api.SwaggerExamples;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.Controllers;

/// <summary>
/// Manages conferences, their sessions, and attendee registrations.
/// </summary>
[ApiController]
[Route("api/conferences")]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class ConferencesController : ControllerBase
{
    private readonly IConferenceService _service;
    private readonly IRegistrationService _registrationService;

    public ConferencesController(IConferenceService service, IRegistrationService registrationService)
    {
        _service = service;
        _registrationService = registrationService;
    }

    /// <summary>
    /// Gets a page of conferences, optionally filtered by keyword, date range, and status.
    /// </summary>
    /// <param name="page">The 1-based page number (default 1).</param>
    /// <param name="pageSize">The number of conferences per page (default 20, capped at 100).</param>
    /// <param name="search">Case-insensitive substring match against title or description. Absent or blank applies no filter.</param>
    /// <param name="from">Only conferences whose date range overlaps this start (EndDate &gt;= from). Absent applies no filter.</param>
    /// <param name="to">Only conferences whose date range overlaps this end (StartDate &lt;= to). Absent applies no filter.</param>
    /// <param name="status">Case-insensitive equality match against the stored status. Absent applies no filter.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">A page of conferences with the total count.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ConferenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var conferences = await _service.GetAllAsync(page, pageSize, search, from, to, status, cancellationToken);
        return Ok(conferences.ToDto(c => c.ToDto()));
    }

    /// <summary>
    /// Gets a single conference by its id.
    /// </summary>
    /// <param name="id">The conference id.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The requested conference.</response>
    /// <response code="404">No conference exists with the given id.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var conference = await _service.GetByIdAsync(id, cancellationToken);
        if (conference is null) return NotFound(ConferenceNotFound(id));
        return Ok(conference.ToDto());
    }

    /// <summary>
    /// Creates a new conference.
    /// </summary>
    /// <param name="conference">The conference to create.</param>
    /// <response code="201">The created conference.</response>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="400">The request body is invalid.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ConferenceDto), StatusCodes.Status201Created)]
    [SwaggerRequestExample(typeof(Conference), typeof(ConferenceExample))]
    public async Task<IActionResult> Create([FromBody] Conference conference, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(conference, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
    }

    /// <summary>
    /// Updates an existing conference.
    /// </summary>
    /// <param name="id">The id of the conference to update.</param>
    /// <param name="conference">The updated conference data.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The updated conference.</response>
    /// <response code="404">No conference exists with the given id.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ConferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [SwaggerRequestExample(typeof(Conference), typeof(ConferenceExample))]
    public async Task<IActionResult> Update(int id, [FromBody] Conference conference, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, conference, cancellationToken);
        if (updated is null) return NotFound(ConferenceNotFound(id));
        return Ok(updated.ToDto());
    }

    /// <summary>
    /// Gets a page of sessions for a conference.
    /// </summary>
    /// <param name="id">The conference id.</param>
    /// <param name="page">The 1-based page number (default 1).</param>
    /// <param name="pageSize">The number of sessions per page (default 20, capped at 100).</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">A page of sessions with the total count.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id}/sessions")]
    [ProducesResponseType(typeof(PagedResult<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var sessions = await _service.GetSessionsAsync(id, page, pageSize, cancellationToken);
        return Ok(sessions.ToDto(s => s.ToDto()));
    }

    /// <summary>
    /// Adds a new session to a conference.
    /// </summary>
    /// <param name="id">The conference id.</param>
    /// <param name="session">The session to add.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="201">The created session.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("{id}/sessions")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status201Created)]
    [SwaggerRequestExample(typeof(Session), typeof(SessionExample))]
    public async Task<IActionResult> AddSession(int id, [FromBody] Session session, CancellationToken cancellationToken)
    {
        var created = await _service.AddSessionAsync(id, session, cancellationToken);
        return CreatedAtAction("GetById", "Sessions", new { id = created.Id }, created.ToDto());
    }

    /// <summary>
    /// Gets all registrations for a conference.
    /// </summary>
    /// <param name="id">The conference id.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The list of registrations.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id}/registrations")]
    [ProducesResponseType(typeof(IEnumerable<RegistrationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegistrations(int id, CancellationToken cancellationToken)
    {
        var registrations = await _service.GetRegistrationsAsync(id, cancellationToken);
        return Ok(registrations.Select(r => r.ToDto()).ToList());
    }

    /// <summary>
    /// Registers an attendee for a conference.
    /// </summary>
    /// <param name="id">The conference id.</param>
    /// <param name="attendee">The attendee to register.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="201">The created registration.</response>
    /// <response code="404">No conference exists with the given id.</response>
    /// <response code="500">An unexpected error occurred while saving the registration.</response>
    [HttpPost("{id}/register")]
    [ProducesResponseType(typeof(RegistrationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [SwaggerRequestExample(typeof(Attendee), typeof(AttendeeExample))]
    public async Task<IActionResult> Register(int id, [FromBody] Attendee attendee, CancellationToken cancellationToken)
    {
        var conference = await _service.GetByIdAsync(id, cancellationToken);
        if (conference is null)
        {
            return NotFound(ConferenceNotFound(id));
        }

        var registration = await _registrationService.RegisterAttendeeAsync(id, attendee, cancellationToken);
        return Created($"api/conferences/{id}/registrations/{registration.Id}", registration.ToDto());
    }

    /// <summary>
    /// Cancels an attendee's registration for a conference.
    /// </summary>
    /// <param name="id">The conference id.</param>
    /// <param name="registrationId">The registration id to cancel.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="204">The registration was cancelled.</response>
    /// <response code="404">No conference exists with the given id, or no active registration exists with the given id.</response>
    [HttpDelete("{id}/registrations/{registrationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRegistration(int id, int registrationId, CancellationToken cancellationToken)
    {
        var conference = await _service.GetByIdAsync(id, cancellationToken);
        if (conference is null) return NotFound(ConferenceNotFound(id));

        var result = await _registrationService.CancelRegistrationAsync(id, registrationId, cancellationToken);
        if (result.Outcome == RegistrationCancellationOutcome.NotFound)
        {
            return NotFound(RegistrationNotFound(id, registrationId));
        }

        return NoContent();
    }

    private static ProblemDetails ConferenceNotFound(int id) => new()
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = $"No conference exists with id {id}.",
    };

    private static ProblemDetails RegistrationNotFound(int conferenceId, int registrationId) => new()
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = $"No active registration {registrationId} exists for conference {conferenceId}.",
    };
}

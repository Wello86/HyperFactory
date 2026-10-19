using System.Linq;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrownEvents.Api.Controllers;

/// <summary>
/// Provides attendee-scoped lookups across conference registrations.
/// </summary>
[ApiController]
[Route("api/attendees")]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class AttendeesController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public AttendeesController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>
    /// Gets all conference registrations for the attendee with the given email.
    /// </summary>
    /// <param name="email">The attendee's email (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The list of registrations for this attendee (possibly empty).</response>
    /// <response code="404">No attendee exists with the given email.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{email}/registrations")]
    [ProducesResponseType(typeof(IEnumerable<RegistrationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRegistrations(string email, CancellationToken cancellationToken)
    {
        if (!await _registrationService.AttendeeExistsAsync(email, cancellationToken))
        {
            return NotFound(AttendeeNotFound(email));
        }

        var registrations = await _registrationService.GetByAttendeeEmailAsync(email, cancellationToken);
        return Ok(registrations.Select(r => r.ToDto()).ToList());
    }

    private static ProblemDetails AttendeeNotFound(string email) => new()
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = $"No attendee found with email '{email}'.",
    };
}

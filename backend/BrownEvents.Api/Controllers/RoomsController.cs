using System.Linq;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrownEvents.Api.Controllers;

/// <summary>
/// Manages conference rooms.
/// </summary>
[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _service;

    public RoomsController(IRoomService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets all rooms.
    /// </summary>
    /// <response code="200">The list of rooms.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var rooms = await _service.GetAllAsync(cancellationToken);
        return Ok(rooms.Select(r => r.ToDto()).ToList());
    }
}

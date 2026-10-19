using System.Linq;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using BrownEvents.Api.SwaggerExamples;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;

namespace BrownEvents.Api.Controllers;

/// <summary>
/// Manages conference speakers.
/// </summary>
[ApiController]
[Route("api/speakers")]
public class SpeakersController : ControllerBase
{
    private readonly ISpeakerService _service;

    public SpeakersController(ISpeakerService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets all speakers.
    /// </summary>
    /// <response code="200">The list of speakers.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SpeakerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var speakers = await _service.GetAllAsync(cancellationToken);
        return Ok(speakers.Select(s => s.ToDto()).ToList());
    }

    /// <summary>
    /// Gets a single speaker by its id, including the sessions they present.
    /// </summary>
    /// <param name="id">The speaker id.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The requested speaker profile.</response>
    /// <response code="404">No speaker exists with the given id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SpeakerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var speaker = await _service.GetByIdWithSessionsAsync(id, cancellationToken);
        if (speaker is null)
        {
            return NotFound(SpeakerNotFound(id));
        }

        return Ok(speaker.ToProfileDto());
    }

    /// <summary>
    /// Creates a new speaker.
    /// </summary>
    /// <param name="speaker">The speaker to create.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="201">The created speaker.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SpeakerDto), StatusCodes.Status201Created)]
    [SwaggerRequestExample(typeof(Speaker), typeof(SpeakerExample))]
    public async Task<IActionResult> Create([FromBody] Speaker speaker, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(speaker, cancellationToken);
        return Created($"api/speakers/{created.Id}", created.ToDto());
    }

    /// <summary>
    /// Updates an existing speaker.
    /// </summary>
    /// <param name="id">The id of the speaker to update.</param>
    /// <param name="speaker">The updated speaker data.</param>
    /// <param name="cancellationToken">Cancellation token from the request.</param>
    /// <response code="200">The updated speaker.</response>
    /// <response code="404">No speaker exists with the given id.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SpeakerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [SwaggerRequestExample(typeof(Speaker), typeof(SpeakerExample))]
    public async Task<IActionResult> Update(int id, [FromBody] Speaker speaker, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, speaker, cancellationToken);
        if (updated is null)
        {
            return NotFound(SpeakerNotFound(id));
        }

        return Ok(updated.ToDto());
    }

    private static ProblemDetails SpeakerNotFound(int id) => new()
    {
        Title = "Not Found",
        Status = StatusCodes.Status404NotFound,
        Detail = $"No speaker exists with id {id}.",
    };
}

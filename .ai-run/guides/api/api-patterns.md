# API Patterns

**Project**: BrownEvents
**Style**: REST-ish JSON over HTTP, ASP.NET Core `[ApiController]` controllers
**Docs**: Swagger/OpenAPI via Swashbuckle, served at `/swagger` (`Program.cs:55-70,92-93`)
**Versioning**: None — every route is unversioned `api/{resource}`

---

## Route Conventions

| Controller | Base route | Source |
|---|---|---|
| `ConferencesController` | `api/conferences` | `Controllers/ConferencesController.cs:15` |
| `SessionsController` | `api/sessions` | `Controllers/SessionsController.cs:14` |
| `SpeakersController` | `api/speakers` | `Controllers/SpeakersController.cs:15` |
| `RoomsController` | `api/rooms` | `Controllers/RoomsController.cs:12` |
| `AttendeesController` | `api/attendees` | `Controllers/AttendeesController.cs` |

Nested resources are sub-routes on the parent, not their own controller: `api/conferences/{id}/sessions`, `api/conferences/{id}/registrations`, `api/conferences/{id}/register`, and `api/conferences/{id}/registrations/{registrationId}` (`DELETE`, cancel) all live on `ConferencesController` (`ConferencesController.cs:119,135,151,168`); `api/sessions/{id}/register`, `api/sessions/{id}/waitlist`, and `api/sessions/{id}/registrations/{registrationId}` live on `SessionsController` (`SessionsController.cs:74,105,136`); `api/attendees/{email}/registrations` lives on `AttendeesController`.

---

## Controller Pattern

Every action: bind → call one service → map to DTO → return. No business logic in controllers.

```csharp
// Source: backend/BrownEvents.Api/Controllers/ConferencesController.cs:63-71
[HttpGet("{id}")]
[ProducesResponseType(typeof(ConferenceDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
{
    var conference = await _service.GetByIdAsync(id, cancellationToken);
    if (conference is null) return NotFound(ConferenceNotFound(id));
    return Ok(conference.ToDto());
}
```

```csharp
// Source: backend/BrownEvents.Api/Controllers/ConferencesController.cs:81-88
[HttpPost]
[ProducesResponseType(typeof(ConferenceDto), StatusCodes.Status201Created)]
public async Task<IActionResult> Create([FromBody] Conference conference, CancellationToken cancellationToken)
{
    var created = await _service.CreateAsync(conference, cancellationToken);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
}
```

Every action documents `<summary>`/`<param>`/`<response>` XML comments (surfaced in Swagger via `IncludeXmlComments`, `Program.cs:65-66`) and declares `[ProducesResponseType]` for each status it can return, plus a class-level `[ProducesResponseType(StatusCodes.Status500InternalServerError)]` on `ConferencesController` (`ConferencesController.cs:16`) covering the global-handler fallback.

---

## Request Body Shape (DTO Asymmetry)

**Responses always go through a `Dto`** (`Dtos/EntityDtoExtensions.cs`). **Request bodies are the raw domain `Model` directly** (e.g. `[FromBody] Conference conference`, `[FromBody] Session session`, `[FromBody] Speaker speaker`) — **except attendee registration**, which uses a dedicated request DTO because the write shape genuinely differs from the persisted entity:

```csharp
// Source: backend/BrownEvents.Api/Dtos/AttendeeRegistrationRequestDto.cs:5-18
public class AttendeeRegistrationRequestDto
{
    [Required] [MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required] [MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
}
```

Used at `SessionsController.cs:79` (`api/sessions/{id}/register`). The conference-level registration endpoint (`ConferencesController.cs:172`) still accepts a raw `Attendee` model — the asymmetry is not fully consistent; when adding a new registration-style endpoint, prefer a dedicated request DTO over the raw model, matching the newer `SessionsController` pattern rather than the older `ConferencesController` one.

---

## Validation

Enforced entirely by `[ApiController]`'s automatic model-state validation against data annotations (`[Required]`, `[MaxLength]`, `[EmailAddress]`) — see [[security-practices]]. No controller calls `ModelState.IsValid` manually; don't add one.

---

## Response Mapping

All mapping goes through static extension methods in one file:

```csharp
// Source: backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs:59-75
public static SessionDto ToDto(this Session session, int registeredCount = 0, int waitlistCount = 0, int? currentAttendeeRegistrationId = null) => new()
{
    Id = session.Id,
    Title = session.Title,
    Speaker = session.Speaker?.ToDto(),
    Room = session.Room?.ToDto(),
    RegisteredCount = registeredCount,
    WaitlistCount = waitlistCount,
    CurrentAttendeeRegistrationId = currentAttendeeRegistrationId,
    // ... remaining fields
};
```

Add one `ToDto()` overload per entity here — never map to a response shape inline in a controller.

---

## Pagination

Only two endpoints paginate, via a generic envelope:

```csharp
// Source: backend/BrownEvents.Api/Models/PagedResult.cs:3-9
public class PagedResult<T>
{
    public List<T> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
```

`GET api/conferences` and `GET api/conferences/{id}/sessions` (`ConferencesController.cs:43-49,119-125`) accept `page`/`pageSize` query params (default `1`/`20`) and return `PagedResult<TDto>` via the generic `ToDto()` mapper (`EntityDtoExtensions.cs:105-111`). Every other list endpoint (`GET api/speakers`) returns a plain `List<TDto>` — **do not assume every list endpoint paginates**. `GET api/conferences` additionally accepts `search`/`from`/`to`/`status` filter query params (`ConferencesController.cs:45-48`), all optional and combinable.

---

## Error Response Shape

Two layers, both RFC7807 `ProblemDetails`:

1. **Domain errors** (404/409): a `private static ProblemDetails {X}NotFound(...)`-style local helper per controller, e.g. `ConferenceNotFound` (`ConferencesController.cs:184-189`), `AlreadyRegistered`/`SessionFull` (`SessionsController.cs:160-173`), `AlreadyWaitlisted`/`SessionNotFull` (`SessionsController.cs:174-188`). Follow this exact local-helper pattern for new domain errors — don't throw a custom exception type.
2. **Unhandled errors** (500): the global exception middleware (`Program.cs:74-90`) — see [[architecture]], [[development-practices]].

### Outcome-Enum Pattern For Multi-Result Writes

When a write can fail in more than one domain-specific way, the service returns a result object with an `Outcome` enum and the controller `switch`es on it — see `SessionsController.cs:86` (`SessionRegistrationOutcome.Success` / `AlreadyRegistered` / `SessionFull`) and `SessionsController.cs:117` (`WaitlistJoinOutcome.Success` / `AlreadyRegistered` / `AlreadyWaitlisted` / `SessionNotFull`). Prefer this over throwing exceptions for expected business outcomes.

---

## Swagger / OpenAPI

Configured in `Program.cs:55-70`: `AddSwaggerGen` with a single `"v1"` doc, XML comments included from the built assembly, `ExampleFilters()` enabled via `Swashbuckle.AspNetCore.Filters`. Request-body examples live in `SwaggerExamples/{X}Example.cs` and are wired per-action with `[SwaggerRequestExample(typeof(Model), typeof(XExample))]` (e.g. `ConferencesController.cs:83`, `SessionsController.cs:57,78`). Add a new `{X}Example.cs` for any new request body type you introduce.

---

## API Versioning

**None.** No `Asp.Versioning`/route-based `v1`/`v2` segments exist. All routes are flat `api/{resource}`. If versioning is ever needed, it would be a project-wide decision, not a per-endpoint addition.

---

## Quick Reference

| Need | Location |
|---|---|
| Add/modify a route | `backend/BrownEvents.Api/Controllers/` |
| Add/modify a response shape | `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs` |
| Add a request DTO | `backend/BrownEvents.Api/Dtos/*RequestDto.cs` |
| Add a Swagger example | `backend/BrownEvents.Api/SwaggerExamples/` |
| Pagination envelope | `backend/BrownEvents.Api/Models/PagedResult.cs` |
| Error response construction | Local `private static ProblemDetails` helper per controller |

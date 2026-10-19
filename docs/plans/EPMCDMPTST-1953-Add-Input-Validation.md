# Add input validation to the API (EPMCDMPTST-1953)

## Context
The API currently accepts any payload. Models used as request bodies (`Conference`, `Session`,
`Speaker`, `Attendee`) are plain EF entities with no data-annotation validation, and none of the
three controllers (`ConferencesController`, `SessionsController`, `SpeakersController`) have
`[ApiController]`, so ASP.NET Core's automatic model-state validation never runs. Bad input (empty
email, no title, empty JSON body) either silently persists or blows up downstream into an
unhelpful 500. We need proper validation so bad requests are rejected with a clear 400 before
reaching the service layer.

## TDD
Yes — write integration tests first (via `WebApplicationFactory<Program>`, following the existing
pattern in `SwaggerDocumentationTests.cs`) that POST invalid payloads and assert `400 Bad Request`.
These will fail today (either 500 or 200/201) and pass once attributes + `[ApiController]` are added.

## Steps

1. **Add data-annotation attributes** to request-body models in `BrownEvents.Api/Models/`:
   - `Conference.cs`: `Title` → `[Required][MaxLength(200)]`.
   - `Session.cs`: `Title` → `[Required][MaxLength(200)]`.
   - `Speaker.cs`: `FirstName`/`LastName` → `[Required][MaxLength(100)]`; `Email` → `[EmailAddress]` (nullable, so no `[Required]`, but validate format when present).
   - `Attendee.cs`: `FirstName`/`LastName` → `[Required][MaxLength(100)]`; `Email` → `[Required][EmailAddress]`.
   - Scope kept to fields called out in the ticket (email, title) plus directly analogous name fields already present — not touching `StartDate`/`EndDate`/`Capacity`/business-rule validation (out of scope).

2. **Add `[ApiController]`** to `ConferencesController`, `SessionsController`, `SpeakersController` so invalid `ModelState` automatically short-circuits to `400` with the built-in `ValidationProblemDetails` (structured, lists each failed field + message) — no custom filter needed since `Program.cs` has no `ApiBehaviorOptions` suppressing it.

3. **Write integration tests** in a new file `BrownEvents.Tests/ValidationTests.cs`, reusing the `WebApplicationFactory<Program>` + in-memory-DB-substitution pattern from `SwaggerDocumentationTests.cs`:
   - `CreateConference_WithEmptyTitle_Returns400WithFieldError`
   - `CreateConference_WithEmptyBody_Returns400`
   - `RegisterAttendee_WithEmptyEmail_Returns400`
   - `RegisterAttendee_WithInvalidEmailFormat_Returns400`
   - `CreateSpeaker_WithMissingFirstName_Returns400`
   Each asserts `response.StatusCode == HttpStatusCode.BadRequest` and that the response body (parsed as `ValidationProblemDetails`) contains the expected field key (e.g. `"Title"`, `"Email"`).

4. Run the new tests red → add attributes/`[ApiController]` → green. Run full `dotnet test BrownEvents.Tests` to check no existing tests regressed (e.g. `ConferenceServiceTests`/`SessionServiceTests` construct models directly, bypassing controller validation, so should be unaffected).

## Out of scope
- Cross-field/business-rule validation (EndDate > StartDate, Capacity > 0, FK existence checks for ConferenceId/SpeakerId/RoomId) — ticket only asks for the [Required]/[MaxLength]/[EmailAddress] shape.
- Global exception-handling middleware for non-validation 500s (e.g. FK violations) — separate concern, not mentioned in acceptance criteria.
- `Room`/`Registration` models — not currently accepted as request bodies by any controller.

## Verification
- `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~ValidationTests"` — new tests pass.
- `dotnet test BrownEvents.Tests` — full suite green, no regressions.

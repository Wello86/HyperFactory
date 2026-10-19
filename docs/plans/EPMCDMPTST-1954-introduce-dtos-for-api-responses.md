# Introduce DTOs for API responses (EPMCDMPTST-1954)

## Context

Every controller action currently returns EF Core entities (or `PagedResult<Entity>`/
`IEnumerable<Entity>`) directly. Because entities have `virtual` navigation properties in both
directions (`Conference.Sessions` ↔ `Session.Conference`, `Speaker.Sessions` ↔ `Session.Speaker`,
`Room.Sessions` ↔ `Session.Room`, `Attendee.Registrations` ↔ `Registration.Attendee`,
`Conference.Registrations` ↔ `Registration.Conference`) and lazy-loading proxies are enabled,
`System.Text.Json` walks the whole object graph on serialization and hits real reference cycles.
The only thing preventing a crash today is `ReferenceHandler.IgnoreCycles` in `Program.cs:37-41`,
which silently drops whatever property closes a cycle — a lossy, implicit workaround, not a
designed contract. It also means any change to `Models/*.cs` (rename a column, add a relation)
immediately changes the wire format.

This change introduces explicit response DTOs so controllers never hand back a tracked entity,
removes `ReferenceHandler.IgnoreCycles` (no longer needed once DTOs have no back-references), and
updates the tests that currently deserialize responses into entity types.

**Scope decision (flagging for confirmation):** "same fields, same shape" is interpreted as the
resource's own scalar fields plus the forward-nested relations the frontend actually consumes
(`Session.Speaker`, `Session.Room`, `Registration.Attendee`) — not the accidental, unbounded
back-reference graph that only exists because nothing stops the serializer from walking
`Conference.Sessions[].Speaker.Sessions[]...`. That graph is never read by the frontend (it
fetches sessions/registrations via their own paged endpoints, not from the nested conference
object — see `frontend/src/pages/ConferenceDetailPage.jsx`), isn't part of the documented shape in
`docs/architecture.md` §4.1–§4.12, and is exactly the bug this ticket exists to remove. DTOs will
therefore **exclude** every collection/back-reference nav property (`Conference.Sessions`,
`Conference.Registrations`, `Speaker.Sessions`, `Room.Sessions`, `Attendee.Registrations`,
`Session.Conference`, `Registration.Conference`) and **include** every scalar field plus
`Session.Speaker`/`Session.Room`/`Registration.Attendee` (flattened, no back-links of their own).

## TDD decision

Partial TDD. Existing tests assert on entity-typed deserialization
(`ApiResponseShapeTests.Create_Conference_Returns201WithLocationAndBody`,
`ValidationTests.GetSeededConferenceIdAsync`) — these will be updated to target the new DTO types
first. I'll also add one new assertion (in `ApiResponseShapeTests`) that the raw JSON body for
`POST /api/conferences` does **not** contain a `sessions`/`registrations` key — this fails against
the current entity-returning controller (red, for the right reason: entity leakage) and passes
once `ConferenceDto` is wired in (green). Service-layer tests (`ConferenceServiceTests`,
`SessionServiceTests`) are untouched — services keep returning entities; only controllers map to
DTOs, so their behavior/contract doesn't change.

## Steps

1. **Add DTO classes** in new folder `backend/BrownEvents.Api/Dtos/` (sibling to `Models/`,
   `Services/`, `Controllers/`, matching the project's flat one-file-per-type convention):
   - `ConferenceDto.cs` — `Id, Title, Description, Location, Venue, StartDate, EndDate, Status, MaxAttendees, OrganizerName, Website`
   - `SpeakerDto.cs` — `Id, FirstName, LastName, Bio, Email`
   - `RoomDto.cs` — `Id, Name, Capacity, Location`
   - `SessionDto.cs` — `Id, Title, Description, StartTime, EndTime, Capacity, ConferenceId, SpeakerId, RoomId, Speaker (SpeakerDto?), Room (RoomDto?)`
   - `AttendeeDto.cs` — `Id, FirstName, LastName, Email`
   - `RegistrationDto.cs` — `Id, RegisteredAt, Status, ConferenceId, AttendeeId, Attendee (AttendeeDto)`

2. **Add mapping extensions** in `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs`: one
   `ToDto()` extension method per entity (manual mapping — no AutoMapper/Mapster is referenced
   anywhere in the repo today, and 6 flat property-copies don't justify adding a dependency), plus
   a small generic `PagedResult<TEntity>.ToDto<TDto>(Func<TEntity,TDto> map)` extension so
   `GetAll`/`GetSessions` don't duplicate the wrapper-copy by hand.

3. **Update controllers** to map before returning, and update `[ProducesResponseType]` attributes
   to the DTO types so Swagger reflects the real contract:
   - `ConferencesController`: `GetAll`, `GetById`, `Create`, `Update`, `GetSessions`, `AddSession`,
     `GetRegistrations`, `Register` — call `.ToDto()` (or the `PagedResult` mapper / `.Select(...)`)
     on the service result before `Ok(...)`/`Created(...)`/`CreatedAtAction(...)`.
   - `SessionsController`: `GetById`, `Update`.
   - `SpeakersController`: `GetAll`, `Create`.
   - Request bodies (`[FromBody] Conference/Session/Speaker/Attendee`) are **not** touched — the
     ticket's goal and acceptance criteria are about responses ("API currently returns database
     entity objects", "No database entity is returned directly from any controller action").
     Binding requests to entities is a separate concern and out of scope here.

4. **Remove the JSON workaround** in `backend/BrownEvents.Api/Program.cs`: delete the
   `.AddJsonOptions(options => { options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; })`
   call (collapsing to plain `builder.Services.AddControllers();`) and the now-unused
   `using System.Text.Json.Serialization;`.

5. **Update tests**:
   - `backend/BrownEvents.Tests/ApiResponseShapeTests.cs`: change
     `response.Content.ReadFromJsonAsync<Conference>()` (line 58) to
     `ReadFromJsonAsync<ConferenceDto>()`; add an assertion on the raw JSON body that `sessions`
     and `registrations` keys are absent (proves no entity/graph leakage).
   - `backend/BrownEvents.Tests/ValidationTests.cs`: change
     `GetFromJsonAsync<PagedResult<Conference>>("/api/conferences")` (line 39) to
     `PagedResult<ConferenceDto>`.
   - `DbContextConcurrencyTests.cs` and `SwaggerDocumentationTests.cs` need no changes (verified —
     neither deserializes into entity types or checks schema names).
   - `ConferenceServiceTests.cs`/`SessionServiceTests.cs` need no changes (services still return
     entities; unaffected by the controller-level DTO mapping).

## Verification

- `dotnet test BrownEvents.Tests` (full suite) from `backend/` — must pass, including the two
  updated integration tests and the new leakage assertion.
- `dotnet format BrownEvents.sln --verify-no-changes` — style check (per CLAUDE.md `using`-order
  rule: `System.*` first, then everything else alphabetically, no blank line between groups).
- Manual sanity check: run the API and confirm swagger.json now shows `ConferenceDto`/`SessionDto`/
  etc. schemas instead of the raw entity schemas, and that `GET /api/conferences/{id}` no longer
  emits `sessions`/`registrations` arrays.

## Out of scope

- Request-body DTOs (create/update payloads still bind to entities) — not required by the
  acceptance criteria, which are about responses.
- AutoMapper/Mapster adoption — noted as a possible future improvement in
  `docs/tech-debt-audit.md` but unnecessary for 6 flat mappings.
- Fixing the underlying over-fetch/lazy-loading performance concerns — out of scope; DTOs stop the
  *serializer* from walking the graph, but service methods still lazy-load `Speaker`/`Room` on
  `Session`, same as before.
- `docs/architecture.md`'s per-endpoint "Back up" bullets (§4.1–§4.12) currently say things like
  "returns `Ok(conference)`" naming the entity type — will be updated in the task-runner skill's
  documentation-check step (Step 9), not as part of this implementation plan.

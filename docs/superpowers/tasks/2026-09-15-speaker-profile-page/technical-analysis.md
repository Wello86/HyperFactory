# Technical Research

**Task**: speaker profile page, backend API endpoint, frontend routing, unit tests
**Generated**: 2026-09-15T00:00:00Z
**Research path**: codegraph

---

## 1. Original Context

EPMCDMPTST-2329 Speaker Profile Page. Goal: Add a speaker profile page. Context: The page shows the speaker's full name, bio, and a list of sessions they are presenting at — with conference name and session time for each. Speaker names in session detail should link to this page. Acceptance Criteria: (1) GET /api/speakers/{id} returns speaker details including their sessions with conference context. (2) Speaker profile page accessible at /speakers/:id. (3) Speaker name in session detail is a clickable link to the speaker profile. (4) Page shows a loading state while fetching and an error state if the request fails. (5) At least one unit test for the speaker service method that builds the session list.

---

## 2. Codebase Findings

### Existing Implementations

Backend:
- `backend/BrownEvents.Api/Controllers/SpeakersController.cs` — `[Route("api/speakers")]`, has `GetAll` (`GET /api/speakers`) and `Create` (`POST /api/speakers`). No `GetById` action exists yet.
- `backend/BrownEvents.Api/Services/SpeakerService.cs` / `ISpeakerService.cs` — only `GetAllAsync` and `CreateAsync`. No method that loads a single speaker with sessions.
- `backend/BrownEvents.Api/Models/Speaker.cs` — `Id`, `FirstName`, `LastName`, `Bio`, `Email`, and `virtual ICollection<Session> Sessions`.
- `backend/BrownEvents.Api/Dtos/SpeakerDto.cs` (referenced, not yet read in full) and `EntityDtoExtensions.cs` — `ToDto(this Speaker speaker)` currently maps only `Id/FirstName/LastName/Bio/Email` — no sessions.
- `backend/BrownEvents.Api/Dtos/SessionDto.cs` — includes `Speaker`, `Room`, `ConferenceId`, `StartTime`/`EndTime`, but no `ConferenceName` field.
- `backend/BrownEvents.Api/Services/ConferenceService.cs` — `GetSessionsAsync`, `GetByIdAsync` eager-load session/speaker/room via `Include`; useful precedent for eager-loading a speaker's sessions with conference.
- `backend/BrownEvents.Api/Data/AppDbContext.cs` — `Speakers` DbSet, `OnModelCreating` configures relationships.

Frontend:
- `frontend/src/pages/SpeakerListPage.jsx` — existing list page: fetch via `getSpeakers()`, `useState`/`useEffect` loading/error pattern, `Link`-based navbar.
- `frontend/src/pages/SessionDetailPage.jsx` — renders speaker name as plain text (`{speaker.firstName} {speaker.lastName}`, lines 140-142), not a link. This is the integration point for AC3.
- `frontend/src/pages/ConferenceDetailPage.jsx` — example of a detail page using a custom hook (`useConference`) + `Navbar` shared component, loading/error branches before render — closest existing pattern for a new detail page (`useSpeaker` would mirror `useConference`).
- `frontend/src/hooks/useConference.js` — hook pattern: `useState`/`useEffect`, calls `getConference(id)`, unwraps `data.data || data`, sets loading/error.
- `frontend/src/api.js` — `request()` helper (fetch wrapper, throws on non-OK), `getConference`, `getSpeakers` (`GET /api/speakers`, no per-id fetch yet).
- `frontend/src/App.jsx` — `BrowserRouter`/`Routes` in one file; routes such as `/conferences/:id` show the `:id` param pattern to follow for `/speakers/:id`.
- `frontend/src/components/SpeakerCard.jsx`, `Navbar` (used by `ConferenceDetailPage`, not yet by `SpeakerListPage`/`SessionDetailPage`, which inline their own `<nav>`).

### Architecture and Layers Affected

- **Backend API layer**: `Controllers/SpeakersController.cs` (needs new `GetById` action).
- **Backend service layer**: `Services/SpeakerService.cs` + `ISpeakerService.cs` (needs a method to load one speaker with sessions/conference).
- **Backend DTO/mapping layer**: `Dtos/SpeakerDto.cs`, `Dtos/EntityDtoExtensions.cs` (speaker `ToDto` needs a sessions collection; session-in-speaker view needs conference name/session time).
- **Frontend routing layer**: `frontend/src/App.jsx` (new `/speakers/:id` route).
- **Frontend page layer**: new `SpeakerProfilePage.jsx` (or similar), following `ConferenceDetailPage.jsx`/`useConference.js` pattern.
- **Frontend API client layer**: `frontend/src/api.js` (needs a `getSpeaker(id)` function).
- **Frontend integration point**: `SessionDetailPage.jsx` speaker name rendering (needs to become a `Link`).
- **Backend test layer**: `backend/BrownEvents.Tests/` (new test file or addition to existing speaker-related tests).

### Integration Points

- `SpeakerService` depends on `AppDbContext` directly (concrete dependency, no repository abstraction — matches `ConferenceService`/`SessionService`).
- `SpeakerDto`/`SessionDto` are cross-referenced (`SessionDto.Speaker` is a `SpeakerDto`); adding `Sessions` to `SpeakerDto` risks a shape decision (whether to reuse `SessionDto` or a narrower session-summary shape) — see Section 6 for the speculative note.
- Frontend `SessionDetailPage` already receives `session.speaker` (an object with `firstName`/`lastName`) from the `GET /api/sessions/{id}` response — linking it to `/speakers/{speaker.id}` requires the DTO to include a speaker `id`, which `SpeakerDto` already has.

### Patterns and Conventions

- Controllers: bind → call one service → map to DTO → return; XML doc comments + `[ProducesResponseType]` per action; 404 via a private `{X}NotFound` `ProblemDetails` helper (see `ConferenceNotFound` in `ConferencesController.cs`).
- All response mapping centralized in `EntityDtoExtensions.cs` — one `ToDto()` overload per entity, never inline in a controller.
- List endpoints return a plain `List<TDto>` unless explicitly paginated (only `GET api/conferences` and `GET api/conferences/{id}/sessions` paginate) — `GET api/speakers/{id}` should not paginate its embedded sessions unless later specified.
- Frontend pages: `useState`/`useEffect` fetch-on-mount, loading/error/empty branches, inline `<nav>` (older pages) vs. shared `Navbar` component (newer `ConferenceDetailPage`) — inconsistent, no single source of truth for the navbar.
- Frontend hooks: one hook per entity (`useConference`, `useConferenceSessions`, etc.) that owns fetch + loading/error state, consumed by the page component.

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/api/api-patterns.md` — route conventions, controller pattern, DTO mapping rules, pagination rules, error response shape (`ProblemDetails` + local `{X}NotFound` helper), Swagger example pattern.
- `.ai-run/guides/testing/testing-patterns.md` — xUnit + EF InMemory conventions, no mocking, `CreateContext()` per test file, `MethodName_Scenario_ExpectedResult` naming, `[Fact]` only (no `[Theory]` precedent).
- Other guides present but not loaded in depth here: `.ai-run/guides/architecture/architecture.md`, `.ai-run/guides/data/database-patterns.md`, `.ai-run/guides/development/development-practices.md`, `.ai-run/guides/security/security-practices.md`, `.ai-run/guides/standards/code-quality.md`, `.ai-run/guides/standards/git-workflow.md`, `.ai-run/guides/quality-gates.md`, `.ai-run/guides/project.md`.

### Architectural Decisions

- API versioning: explicitly none — all routes stay flat `api/{resource}`.
- Request/response DTO asymmetry is a recorded, intentional (if inconsistent) convention: responses always map through `ToDto()`; request bodies are raw domain models except for registration-style endpoints.

### Derived Conventions

- New GET-by-id actions consistently follow `ConferencesController.GetById` / return shape: `Ok(entity.ToDto())` or `NotFound({X}NotFound(id))`.
- New frontend detail pages consistently follow `ConferenceDetailPage` + dedicated `use{Entity}` hook shape.

---

## 4. Testing Landscape

### Existing Coverage

- `backend/BrownEvents.Tests/ValidationTests.cs` — has `CreateSpeaker_WithMissingFirstName_Returns400`, exercises `POST /api/speakers` via `WebApplicationFactory`.
- `backend/BrownEvents.Tests/SessionServiceTests.cs`, `ConferenceServiceTests.cs` — eager-loading assertions for `Speaker`/`Room` navigation properties (pattern precedent for asserting eager-loaded `Sessions` on a `Speaker`).
- No test file exists for `SpeakerService` specifically (no `SpeakerServiceTests.cs`), and no test exercises `GetAllAsync`/`CreateAsync` on `SpeakerService` directly — codegraph reports "no tests found within 3 caller hops" for `SpeakerService.GetAllAsync`/`CreateAsync`.
- No frontend test runner exists in the project at all (confirmed by `testing-patterns.md`: "backend only — no frontend test runner exists").

### Testing Framework and Patterns

- xUnit 2.9.3, EF Core InMemory provider, `WebApplicationFactory<Program>` for HTTP-level tests, `CreateContext()` per-test-file helper with `Guid.NewGuid()` database name, `[Fact]`-only (no `[Theory]`), no mocking (Moq referenced but unused).

### Coverage Gaps

- No existing `SpeakerServiceTests.cs` — a new test class will be net-new, not an addition to covered ground.
- No test currently exercises a speaker-with-sessions query shape anywhere in the codebase (unlike conference/session eager-loading, which has precedent).
- No frontend test coverage exists or is expected (matches project-wide convention, not a gap specific to this task).

---

## 5. Configuration and Environment

### Environment Variables

None found specific to speakers/sessions/conferences in the explored areas; no env vars surfaced by codegraph queries for this feature area.

### Configuration Files

- `backend/BrownEvents.Api/Data/AppDbContext.cs` governs entity configuration (`OnModelCreating`) for `Speaker`/`Session`/`Conference` relationships.
- `frontend/src/api.js` centralizes all API base URL/fetch configuration (`BASE_URL` constant, currently empty string / same-origin).

### Feature Flags and Deployment Concerns

None found. No feature-flag mechanism surfaced anywhere in the explored codebase.

---

## 6. Risk Indicators

- No existing `SpeakerServiceTests.cs` — the required unit test (AC5) has no existing file/pattern to extend; a new test class must be created following the `{ServiceName}Tests.cs` convention.
- Speculative: `SpeakerDto` will likely need a new `Sessions` (or similar) collection property to satisfy AC1, and `EntityDtoExtensions.ToDto(this Speaker)` will need a corresponding overload/parameter — exact shape (reuse `SessionDto` vs. a new narrower per-session-with-conference-name DTO) is a design decision, not yet resolved in the codebase.
- Speculative: the session-in-speaker-profile view needs conference name and session time; today `SessionDto` carries `ConferenceId` but not conference name/title, so either a new lightweight DTO or an extra join/include will be needed — exact form is undetermined.
- `SpeakerService` depends on `AppDbContext` concretely (no interface abstraction over EF); a "build session list" method will likely require an `Include(s => s.Sessions).ThenInclude(...)`-style query akin to `ConferenceService.GetSessionsAsync`, but the join path from `Speaker` to `Conference` (via `Session.ConferenceId`) is not yet exercised anywhere in the code.
- Frontend `SessionDetailPage.jsx` renders speaker info as inline JSX with no id-based link — turning it into a link (AC3) touches shared session-detail rendering, not just the new page.
- Navbar duplication: `SpeakerListPage`/`SessionDetailPage` inline their own `<nav>` while `ConferenceDetailPage` uses a shared `Navbar` component — no single convention to follow for the new page's header.
- No frontend test runner exists in the project — AC4 (loading/error states) has no established automated-verification pattern; verification will likely be manual/visual only, consistent with the rest of the frontend.
- `GetById`-by-id 404 handling for speakers has no existing precedent in `SpeakersController` (only `GetAll`/`Create` exist today) — the `{X}NotFound` helper pattern from `ConferencesController`/`SessionsController` will need to be replicated for speakers for the first time.

---

## 7. Summary for Complexity Assessment

This task touches four layers across both the backend (.NET/EF Core) and frontend (React) codebases of BrownEvents: the API controller layer (`SpeakersController` — a new `GetById` action, first 404-handling code for speakers), the service layer (`SpeakerService`/`ISpeakerService` — a new session-with-conference-context query method), the DTO/mapping layer (`SpeakerDto`, `EntityDtoExtensions`, likely `SessionDto`), and the frontend (a new route in `App.jsx`, a new `SpeakerProfilePage` + `useSpeaker` hook mirroring the existing `ConferenceDetailPage`/`useConference` pattern, plus a modification to `SessionDetailPage` to link the speaker name). Every layer has a strong existing precedent to mirror — `ConferencesController.GetById`, `useConference.js`, `ConferenceDetailPage.jsx` — so the implementation is largely additive-by-analogy rather than novel-design work.

The main technical uncertainty is the shape of the "sessions with conference context" data: no existing DTO carries conference name alongside a session, and no existing service method joins Speaker → Session → Conference in one query, so a design decision (not yet made in the codebase) is needed there. Test coverage for the speaker domain is currently limited to one HTTP-validation test (`ValidationTests.CreateSpeaker_WithMissingFirstName_Returns400`) and no dedicated `SpeakerServiceTests.cs` exists, so the required unit test (AC5) starts from a clean slate but has clear structural precedent (`SessionServiceTests.cs`, `ConferenceServiceTests.cs`) to follow. No frontend test runner exists project-wide, so AC4's loading/error states will be verified by pattern-matching against `SpeakerListPage`/`ConferenceDetailPage`, not by automated test.

Overall risk is moderate-low: the change surface is well-bounded (roughly 5-7 backend files, 2-3 frontend files, 1 new test file), all touched layers have close existing analogues, and the only open design question — the session/conference-context DTO shape — is narrow and localized rather than architectural.

---

## 8. External References

None named by the task.

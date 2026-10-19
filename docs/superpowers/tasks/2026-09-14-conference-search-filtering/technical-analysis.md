# Technical Research

**Task**: conference search filter pagination
**Generated**: 2026-09-14
**Research path**: filesystem

---

## 1. Original Context

Ticket EPMCDMPTST-2326 (BEVN-201): Conference Search and Filtering

Goal: A user can search conferences by keyword (title or description) and filter by date range and status.

Context: The search is available from the conference list page. Results update as filters change. An empty result set shows a helpful message. Filtering works together with pagination.

Acceptance Criteria:
- GET /api/conferences accepts ?search=, ?from=, ?to=, ?status= query params
- Absent params apply no filter — all results returned when no filters active
- Frontend conference list has a search input and date range pickers
- Filters and pagination work together correctly
- Empty state message shown when no results match
- At least two unit tests: one for keyword match, one for date range filter

---

## 2. Codebase Findings

### Existing Implementations

No search or filter capability exists anywhere in the codebase today — this is a greenfield addition on top of existing pagination. Relevant existing pieces that the change sits on top of:

- `backend/BrownEvents.Api/Controllers/ConferencesController.cs:36-42` — `GetAll(int page = 1, int pageSize = 20, CancellationToken)` — the only action mapped to `GET api/conferences`. No query params beyond `page`/`pageSize` exist today.
- `backend/BrownEvents.Api/Services/ConferenceService.cs:16-43` — `GetAllAsync(page, pageSize, cancellationToken)` — builds `totalCount` via `_context.Conferences.CountAsync()` then a separate `Skip/Take` query with `.Include(c => c.Sessions).ThenInclude(...)` for Speaker/Room (N+1 avoidance, per inline comment).
- `backend/BrownEvents.Api/Services/IConferenceService.cs:7` — interface signature for `GetAllAsync`, would need updating if the service signature changes.
- `backend/BrownEvents.Api/Models/Conference.cs` — `Title` (required, max 200), `Description` (nullable string), `StartDate`/`EndDate` (`DateTime`), `Status` (string, default `"UPCOMING"`, stored uppercase — see `ConferenceService.UpdateAsync:76` which does `.ToUpper()` on write).
- `backend/BrownEvents.Api/Models/PagedResult.cs` — generic `Data`/`Page`/`PageSize`/`TotalCount` envelope, reused via `EntityDtoExtensions.ToDto<TEntity,TDto>(Func<TEntity,TDto>)` (`EntityDtoExtensions.cs:86-92`).
- `backend/BrownEvents.Api/Dtos/ConferenceDto.cs` — response shape; no changes needed for filtering itself.
- `frontend/src/pages/ConferenceListPage.jsx` — owns `page`, `conferences`, `totalCount`, `loading`, `error` state; calls `getConferences(page, PAGE_SIZE)` in a `useEffect` keyed on `[page]`; renders `ConferenceCard` grid, a "No conferences found." empty-state `div` (already exists, currently only fires when `conferences.length === 0` with no filters), and `Pagination`.
- `frontend/src/api.js:16-17` — `getConferences = (page = 1, pageSize = 20) => request('/api/conferences?page=...&pageSize=...')` — the only client entry point that would need new query params.
- `frontend/src/components/Pagination.jsx` — pure page-size/count math off `totalCount`/`pageSize`/`page`; no awareness of filters, so it will automatically reflect a filtered `totalCount` if the parent passes the filtered value through (which it already does, since `totalCount` comes from the same paged response).
- `frontend/src/components/ConferenceCard.jsx:17-30` — client-side `statusClass()` mapping for `active`/`upcoming`/`cancelled`/`completed` (lowercase-normalized) — the canonical set of status values in use by the UI today, though not enforced by any backend enum/validation.
- `backend/BrownEvents.Api/Data/DataSeeder.cs` — seed data uses status values `"COMPLETED"` (and by inspection of the `ConferenceCard` mapping, also `ACTIVE`/`UPCOMING`/`CANCELLED` are expected elsewhere in seed data) — useful for picking realistic values in new unit tests.

### Architecture and Layers Affected

- **Controller layer** (`Controllers/ConferencesController.cs`) — `GetAll` action needs new `[FromQuery]` bindings for `search`/`from`/`to`/`status`.
- **Service layer** (`Services/ConferenceService.cs` + `IConferenceService.cs`) — `GetAllAsync` (and its interface) is where filter predicates would be composed against `_context.Conferences` before the existing `Skip/Take`/`CountAsync` pagination logic.
- **Frontend page layer** (`pages/ConferenceListPage.jsx`) — needs new state for search text and date range, wiring into the existing `useEffect`/`getConferences` call.
- **Frontend API client** (`api.js`) — `getConferences` needs additional optional query params.
- No `Dtos/`, `Models/`, or `Data/AppDbContext.cs` changes are implied by anything found — filtering is a read-path query change, not a schema change.

### Integration Points

- `ConferencesController` → `IConferenceService` → `AppDbContext.Conferences` (direct EF Core LINQ, no repository layer — per `[[architecture]]`).
- `ConferenceListPage.jsx` → `hooks/` is *not* used for the conference list today (unlike `useConference.js`/`useConferenceSessions.js` used elsewhere) — `ConferenceListPage.jsx` calls `api.js`'s `getConferences` directly inside its own `useEffect`, which is an inconsistency with the documented convention ("pages/ never call fetch directly... go through a hooks/ function") worth noting but not necessarily this ticket's job to fix.
- `Pagination.jsx` is a dumb presentational component driven entirely by props from `ConferenceListPage.jsx` — filters do not need to touch it directly, only the `totalCount`/`page` values already flowing through.

### Patterns and Conventions

- **Query composition**: no existing example of conditional/optional LINQ filters in this codebase (`ConferenceService`, `SessionService`, `SpeakerService` — none have optional filter params). This is a new pattern for the codebase; nearest analog is the unconditional `.Where(s => s.ConferenceId == conferenceId)` in `GetSessionsAsync` (`ConferenceService.cs:90`).
- **Pagination envelope**: any new filtering must still return `PagedResult<ConferenceDto>` via the existing generic `.ToDto()` — per `[[api-patterns]]`, don't introduce a second response shape.
- **XML doc + `[ProducesResponseType]`**: every controller action in this file documents `<param>`/`<response>` XML comments and declares response types explicitly (`ConferencesController.cs:28-38`) — new query params need matching `<param>` docs.
- **Async naming**: `Async` suffix universal on service methods.
- **No enum for `Status`**: `Conference.Status` is a plain `string`, normalized to uppercase only on `UpdateAsync` (`ConferenceService.cs:76`), not on `CreateAsync` or anywhere else — a `status=` filter comparing against a fixed casing needs to account for this (data seeded as `"COMPLETED"`/likely mixed-case elsewhere is unconfirmed beyond that one seeded value observed).

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/api/api-patterns.md` — route conventions, controller pattern (bind → one service call → `.ToDto()`), DTO asymmetry, pagination envelope, error shape. Directly governs how the new query params and response should be wired.
- `.ai-run/guides/architecture/architecture.md` — layering (`Controllers` → `Services` → `AppDbContext` directly, no repository), "Adding New Features" 7-step recipe (steps 3/5 are the ones this task touches: service query change, controller query-param binding).
- `.ai-run/guides/data/database-patterns.md` — EF Core query patterns (`.Include()`/`.AsNoTracking()`), no migrations (`EnsureCreated()` only), no raw SQL.
- `.ai-run/guides/testing/testing-patterns.md` — xUnit `[Fact]`-only conventions, `CreateContext()`-per-test InMemory DB pattern, naming `MethodName_Scenario_ExpectedResult`. States explicitly: "No `[Theory]`/`[InlineData]` in the suite... New parameterized cases should still use `[Theory]`/`[InlineData]`" if introduced.
- `.ai-run/guides/security/security-practices.md` — confirms no auth exists anywhere (irrelevant risk for this ticket) and that EF Core LINQ is fully parameterized (relevant: any `search` filter using `.Contains()`/`EF.Functions.Like` stays within this safe pattern, no raw SQL to introduce).
- `.ai-run/guides/development/development-practices.md` — naming/formatting/async conventions; no per-controller `try/catch`.
- `.ai-run/guides/project.md` — ticket adapter (Jira, `EPMCDMPTST` prefix), MR adapter (`glab`), confirms no complexity-scoring field configured.

### Architectural Decisions

- No ADR-style documents found. The closest recorded decisions are inline comments: the N+1-avoidance `.Include()` rationale in `ConferenceService.cs:23-26,47-50`, and the `AddScoped`-not-`AddSingleton` comment referenced in `[[database-patterns]]`/`Program.cs:16-21`. Neither directly constrains filtering, but any new query built on `_context.Conferences` must preserve the existing `.Include(c => c.Sessions).ThenInclude(...)` eager-loading before `ToListAsync` to avoid reintroducing N+1.

### Derived Conventions

- Optional filters are not previously modeled in this codebase; the convention to follow is inferred from the guides rather than an existing example: build the `IQueryable<Conference>` base query, conditionally chain `.Where(...)` calls only when a param is non-null/non-empty (so absent params impose no filter, per the acceptance criteria), then apply `.CountAsync()` and `.Skip/Take` exactly as `GetAllAsync` does today.

---

## 4. Testing Landscape

### Existing Coverage

- `backend/BrownEvents.Tests/ConferenceServiceTests.cs` — covers `GetAllAsync` (all conferences, second-page slice, past-last-page empty data, cancellation), `CreateAsync`, `UpdateAsync` (including the `.ToUpper()` status normalization), `GetSessionsAsync`, eager-loading assertions for `Speaker`/`Room` navs. No test in this file (or anywhere in the suite) exercises any filter/search parameter — confirmed via repo-wide grep for "search" in `backend/` matching only `DataSeeder.cs` (unrelated `Any()`/seed-guard usage, not a real hit on "search").
- `ApiResponseShapeTests.cs`, `ValidationTests.cs`, `SwaggerDocumentationTests.cs`, `AttendeePiiExposureTests.cs` — use `WebApplicationFactory<Program>` for full-HTTP-round-trip tests; none currently target `GET api/conferences` query-string behavior.

### Testing Framework and Patterns

- xUnit 2.9.3, `[Fact]` only (no `[Theory]` precedent yet, though the guide recommends introducing `[Theory]`/`[InlineData]` for new parameterized cases like multiple filter combinations).
- EF Core InMemory provider, `CreateContext()` helper per test file with `Guid.NewGuid()` DB name for isolation; no mocking (Moq referenced but unused).
- Arrange (seed `AppDbContext` rows directly) → Act (call service method) → Assert pattern throughout `ConferenceServiceTests.cs`.

### Coverage Gaps

- No existing test for keyword search on `Title`/`Description`.
- No existing test for date-range filtering (`from`/`to` against `StartDate`/`EndDate`).
- No existing test for status filtering.
- No existing test combining filters with pagination (e.g., filtered result set spanning multiple pages).
- No existing frontend test runner at all (`[[testing-patterns]]` states explicitly: "backend only — no frontend test runner exists") — the acceptance criterion's "at least two unit tests" can only be satisfied on the backend service layer; there is no established way to unit-test the new search input / date-range picker UI in this repo.
- No test exercises `ConferencesController.GetAll` at the controller/HTTP level with query params — closest existing pattern (`WebApplicationFactory`) exists in other files but not for this action.

---

## 5. Configuration and Environment

### Environment Variables

- None found related to search/filtering/pagination. The only backend env-adjacent config found is `ConnectionStrings:DefaultConnection` (`appsettings.json`, plaintext dev credentials) and `Cors:AllowedOrigins` — neither relevant to this feature.

### Configuration Files

- `backend/BrownEvents.Api/appsettings.json` / `appsettings.Development.json` — connection string, CORS origins; no feature-flag section exists.
- `frontend/src/api.js:1` — `BASE_URL = ''` (relies on same-origin/dev-proxy); no config toggle relevant to search.

### Feature Flags and Deployment Concerns

- No feature-flag mechanism found anywhere in the codebase (backend or frontend).
- No CI/CD or Dockerfile reference to search/filter/pagination-specific config — `.gitlab-ci.yml` runs `dotnet format`, `dotnet test`, frontend lint/format only; no env-specific deployment concern surfaced for this change.
- Schema: `Database.EnsureCreated()` with no migrations folder — reiterated because any accidental model/column addition (not indicated as needed by this ticket) would require a fresh DB rather than an in-place migration.

---

## 6. Risk Indicators

- **No prior optional/conditional-filter query pattern exists in this codebase** — `ConferenceService`, `SessionService`, `SpeakerService` all build unconditional queries; the new query-composition style for `GetAllAsync` is a genuinely new pattern here, increasing review/design surface even though the underlying EF Core mechanics are standard.
- **`Conference.Status` is an unvalidated free-text string**, normalized to uppercase only in `UpdateAsync` (not on create, not anywhere else) — a `status=` filter needs a defined case-sensitivity/normalization behavior; the four values observed in UI code (`active`/`upcoming`/`cancelled`/`completed`, lowercased for CSS class lookup) are not backed by any backend enum or constant.
- **`ConferenceListPage.jsx` bypasses the documented `hooks/` convention** (calls `api.js` directly from a page-level `useEffect` instead of going through a `use{Concern}.js` hook like every other page) — Speculative: this may or may not matter for this task depending on whether the implementation follows the existing (inconsistent) page-level pattern or the documented hook convention; flagging so the plan can decide rather than silently picking one.
- **No frontend test runner exists at all** — the acceptance criterion "at least two unit tests" is only achievable against the backend service layer; there is no existing infrastructure (no Jest/Vitest/RTL config found in `frontend/package.json`) to unit-test the new search input or date-range pickers themselves.
- **No existing controller-level (`WebApplicationFactory`) test for `GET api/conferences`** — if the plan chooses to test at the HTTP layer (query-string binding) rather than only the service layer, there's no direct precedent for this specific action, only sibling examples on other actions/controllers.
- **Empty-state message already exists but is filter-unaware** — the current "No conferences found." block (`ConferenceListPage.jsx`) fires whenever `conferences.length === 0` regardless of whether that's because there are zero conferences globally or zero matching the active filters; whether that existing copy is sufficient or needs to differ per case is a design decision, not something resolvable from current code.
- **Pagination interacting with filters is unexercised anywhere** — `Pagination.jsx` and `GetAllAsync`'s `Skip/Take` both assume `totalCount` reflects whatever query already ran; no existing test proves total-count/paging math stays correct once a `.Where()` is added ahead of `CountAsync()`.

---

## 7. Summary for Complexity Assessment

This task touches three layers with no schema change: the controller layer (`ConferencesController.GetAll` needs new `[FromQuery]` bindings for `search`/`from`/`to`/`status`), the service layer (`ConferenceService.GetAllAsync` / `IConferenceService` need conditional `.Where()` composition ahead of the existing `CountAsync`/`Skip`/`Take` pagination), and the frontend (`ConferenceListPage.jsx` needs new search-input/date-range-picker state wired into its existing `useEffect`, plus `api.js`'s `getConferences` needs the new optional query params). No `Models/`, `Dtos/`, or `AppDbContext` changes are indicated by anything found — this is a read-path query change layered onto an existing, working pagination implementation (`PagedResult<T>`, `Pagination.jsx`) that already returns `totalCount` correctly for the unfiltered case.

The primary novelty is architectural rather than technical: no service in this codebase currently builds a conditionally-filtered `IQueryable`, so the implementation sets a new pattern rather than following one, even though the underlying EF Core LINQ mechanics (`.Where()`, `.Contains()` for keyword match, `DateTime` range comparisons) are standard and safe (parameterized, no raw SQL). `Conference.Status` being an unvalidated free-text string with inconsistent casing normalization is the one genuine domain-modeling wrinkle a status filter has to resolve.

Test coverage posture is the main risk concentration: there is zero existing coverage for any filter/search behavior, no controller-level (`WebApplicationFactory`) test precedent for this specific action's query-string binding, and no frontend test runner at all in this repository — meaning the two required unit tests are achievable only against `ConferenceServiceTests.cs` at the service layer, and the frontend UI pieces (search input, date pickers, empty-state wording) have no testing safety net beyond manual/visual verification. Combined with the never-before-exercised interaction of filters with the existing pagination math, this is a moderate-complexity, well-bounded change with above-average design ambiguity (status casing, empty-state copy, hook-vs-direct-fetch convention) rather than high technical difficulty.

---

## 8. External References

None named by the task. `task_context` refers only to the Jira ticket EPMCDMPTST-2326 (BEVN-201) itself as the subject of the work, not to an external file, directory, or URL as a source of truth to read.

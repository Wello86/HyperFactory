# Paginate conference list and session list

## Context

The main conference list and the per-conference session list both load every
row unfiltered via `ToListAsync()` with no `Skip`/`Take`, and the frontend
renders the entire array in one pass with no windowing. As data grows this
makes both the API response and the initial render slow. This adds
server-side pagination (`page`/`pageSize` query params, `Skip`/`Take` in EF
Core, total count in the response) and client-side paging UI so only one
page of items is fetched and rendered at a time, including safe behavior
when paging past the last page.

## Backend

**New DTO** — `backend/BrownEvents.Api/Models/PagedResult.cs` (new file):
```csharp
public class PagedResult<T>
{
    public List<T> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
```

**`IConferenceService` / `ConferenceService`**
(`backend/BrownEvents.Api/Services/IConferenceService.cs`,
`backend/BrownEvents.Api/Services/ConferenceService.cs`):
- Change `GetAllAsync()` → `GetAllAsync(int page, int pageSize)` returning
  `Task<PagedResult<Conference>>`. Query: `OrderBy(c => c.Id)` (deterministic
  order required for stable paging), get `CountAsync()` separately, then
  `.Skip((page-1)*pageSize).Take(pageSize)` before the existing `.Include`
  chain, then `.ToListAsync()`. Clamp `page`/`pageSize` to sane minimums
  (page ≥ 1, pageSize ≥ 1, cap pageSize e.g. at 100) inside the service so
  invalid input can't force a full scan or negative Skip.
- Change `GetSessionsAsync(int conferenceId)` →
  `GetSessionsAsync(int conferenceId, int page, int pageSize)` returning
  `Task<PagedResult<Session>>`, same pattern: `OrderBy(s => s.Id)`, count,
  skip/take, then `.Include(Speaker).Include(Room)`.
- Paging past the last page naturally returns an empty `Data` list (no
  exception) — this satisfies the "no error past last page" criterion as
  long as page/pageSize are clamped to positive values first.

**`ConferencesController`**
(`backend/BrownEvents.Api/Controllers/ConferencesController.cs`):
- `GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)` → calls
  the paged service method, returns `Ok(result)` (the `PagedResult<Conference>`
  itself, replacing the bare array — this is a breaking response-shape
  change, acceptable since no other consumer exists besides this repo's own
  frontend, which is updated in this task).
- `GetSessions(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)`
  → calls paged service method, returns `Ok(result)` (replaces the current
  ad hoc `{ data = sessions }` with the richer `PagedResult<Session>`, which
  still exposes `.Data`).
- Update XML doc comments/`ProducesResponseType` for both actions accordingly.

**Tests** (`backend/BrownEvents.Tests/ConferenceServiceTests.cs`):
- Update `GetAllAsync_ReturnsAllConferences` to call the new paged signature
  and assert against `result.Data`/`TotalCount`.
- Update `GetSessionsAsync_ReturnsOnlyConferenceSessions` and
  `GetSessionsAsync_EagerLoadsSpeakerAndRoom` similarly.
- Add new tests: paging returns correct slice for page 2, `TotalCount`
  reflects full set regardless of page, and requesting a page past the last
  page returns an empty `Data` list with no exception.
- Check `SwaggerDocumentationTests.cs` still passes given the new query
  params/response type; adjust if it asserts specific response shapes.

## Frontend

**`frontend/src/api.js`**:
- `getConferences(page = 1, pageSize = 20)` → appends `?page=&pageSize=` to
  the request, returns the parsed `PagedResult` shape as-is (don't unwrap
  `.data` here — let callers use `.data`/`.totalCount`).
- `getConferenceSessions(id, page = 1, pageSize = 20)` → same query params;
  remove the current `.then((res) => res.data || res)` unwrap so callers get
  the full paged envelope (`{ data, page, pageSize, totalCount }`).

**New shared component** — `frontend/src/components/Pagination.jsx`:
- Props: `page`, `pageSize`, `totalCount`, `onPageChange`.
- Renders Prev/Next buttons plus "Page X of Y"; disables Prev on page 1 and
  Next when `page * pageSize >= totalCount`; hides itself (or renders
  nothing) when `totalCount <= pageSize`.

**`frontend/src/pages/ConferenceListPage.jsx`**:
- Add `page` state (default 1), fixed `pageSize` (e.g. 20), `totalCount`
  state.
- `useEffect` depends on `[page]`, calls `getConferences(page, pageSize)`,
  sets `conferences` from `data.data`, `totalCount` from `data.totalCount`.
- Render `<Pagination page={page} pageSize={pageSize} totalCount={totalCount} onPageChange={setPage} />`
  below the `card-grid`.
- If the current page becomes empty because data shrank (e.g. after
  deletion) or a stale page has no items, no special handling is required
  beyond what "no conferences found" already shows — not in scope here.

**`frontend/src/pages/ConferenceDetailPage.jsx`**:
- Add `sessionsPage` state (default 1), fixed `sessionsPageSize`,
  `sessionsTotalCount` state.
- Sessions `useEffect` depends on `[id, sessionsPage]`, calls
  `getConferenceSessions(id, sessionsPage, sessionsPageSize)`, sets
  `sessions` from `data.data`, `sessionsTotalCount` from `data.totalCount`.
- Render `<Pagination .../>` below `<SessionList>` using the sessions
  paging state/setter.
- `SessionList.jsx` itself needs no change — it stays a pure list renderer
  for whatever page of sessions it's given.

## Verification

- Backend: `dotnet test BrownEvents.Tests` — updated + new tests for
  `ConferenceServiceTests` covering paging math, total count, and
  past-last-page behavior; run
  `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~ConferenceServiceTests"`
  for a fast loop, then the full suite.
- Manual: run the app (`docker-compose up --build` or the `run` skill),
  open the conference list, confirm only `pageSize` cards render and
  Prev/Next work; open a conference with more sessions than `pageSize`
  (seed data has 7 sessions total across 3 conferences, so may need to
  temporarily lower `pageSize` to something like 2 while testing, or add
  test data) and confirm session paging works; manually request
  `/api/conferences?page=999&pageSize=20` and confirm `200 OK` with empty
  `data` rather than a 500.
- `npm run lint` / `dotnet format --verify-no-changes` for style.

## Out of scope

- Virtualized/infinite-scroll rendering (classic page-based pagination is
  sufficient to satisfy "does not render all items at once").
- Pagination for `GetRegistrations` or other list endpoints not named in
  the acceptance criteria.
- Persisting page state in the URL query string (kept as in-memory React
  state only).

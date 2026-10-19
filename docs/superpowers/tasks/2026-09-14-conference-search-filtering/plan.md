# Conference Search and Filtering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user search conferences by keyword and filter by date range and status from `GET /api/conferences`, with matching UI on the conference list page, combined correctly with existing pagination.

**Architecture:** Extend the existing unconditional read path — no repository or filter-object pattern. `ConferenceService.GetAllAsync` gains four optional parameters and conditionally chains `.Where(...)` onto its `IQueryable<Conference>` ahead of the existing `Include`/`CountAsync`/`Skip`/`Take`. The controller adds matching `[FromQuery]` bindings. The frontend adds search-text and date-range state to `ConferenceListPage.jsx`, wired through new optional params on `api.js`'s `getConferences`.

**Tech Stack:** ASP.NET Core (C#) + EF Core (InMemory in tests) backend; React (JSX, no TS) frontend; xUnit `[Fact]` tests.

**Spec:** `docs/superpowers/tasks/2026-09-14-conference-search-filtering/spec.md`

**Commit per task using the repository's existing convention.**

## Global Constraints

- `search`: case-insensitive substring match against `Title` OR `Description`. Null/blank `Description` never matches. Blank/whitespace `search` = absent (no filter).
- `status`: case-insensitive equality against the stored `Status` string. No enum, no validation added.
- Date range uses **overlap** semantics against `[StartDate, EndDate]`, not `StartDate` alone: `from` only → `EndDate >= from`; `to` only → `StartDate <= to`; both → `StartDate <= to AND EndDate >= from`.
- All supplied filters combine with AND. All four absent → identical results/count to today's unfiltered behavior.
- Frontend resets to page 1 whenever any filter changes, before re-fetching.
- No `Status` enum or backend validation — stays free-text.
- No EF Core schema/migration change.
- No refactor of `ConferenceListPage.jsx` onto the `hooks/use*.js` pattern — keep its existing direct-`api.js`-call style.
- No new response envelope — `PagedResult<ConferenceDto>` reused as-is.
- No frontend test runner introduced.
- No debounce/throttle on the search input.
- No controller-level (`WebApplicationFactory`) HTTP tests.
- No custom error contract for malformed `from`/`to` — ASP.NET Core's default model-binding 400 stands.
- Empty state: single generic message for both "no conferences at all" and "no conferences match filters" — no copy branching.

---

### Task 1: Service-layer conditional filtering (`ConferenceService.GetAllAsync`)

**Files:**
- Modify: `backend/BrownEvents.Api/Services/IConferenceService.cs:7`
- Modify: `backend/BrownEvents.Api/Services/ConferenceService.cs:16-43`
- Test: `backend/BrownEvents.Tests/ConferenceServiceTests.cs`

**Interfaces:**
- Consumes: existing `PagedResult<Conference>`, `AppDbContext.Conferences`.
- Produces: `Task<PagedResult<Conference>> GetAllAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken cancellationToken = default)` — Task 2 (controller) calls this with named/positional args in the new order.

Test-first: yes — `GetAllAsync_FiltersByKeyword_MatchingTitleOrDescription` and `GetAllAsync_FiltersByDateRange_UsingOverlapSemantics` don't compile yet (no `search`/`from`/`to` parameters exist).

- [ ] **Step 1: Write the two failing tests**

Add to `backend/BrownEvents.Tests/ConferenceServiceTests.cs` (alongside the existing `GetAllAsync_*` tests):

```csharp
[Fact]
public async Task GetAllAsync_FiltersByKeyword_MatchingTitleOrDescription()
{
    using var context = CreateContext();
    context.Conferences.AddRange(
        new Conference { Title = "AI Summit", Description = "Talks on machine learning", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" },
        new Conference { Title = "Cooking Expo", Description = "Recipes and techniques", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" },
        new Conference { Title = "Garden Fair", Description = null, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" }
    );
    await context.SaveChangesAsync();

    var service = new ConferenceService(context);
    var result = await service.GetAllAsync(1, 20, search: "machine");

    Assert.Equal(1, result.TotalCount);
    Assert.Equal("AI Summit", result.Data[0].Title);
}

[Fact]
public async Task GetAllAsync_FiltersByDateRange_UsingOverlapSemantics()
{
    using var context = CreateContext();
    context.Conferences.AddRange(
        new Conference { Title = "Before Window", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 5), Status = "UPCOMING" },
        new Conference { Title = "Overlaps Start", StartDate = new DateTime(2026, 1, 8), EndDate = new DateTime(2026, 1, 20), Status = "UPCOMING" },
        new Conference { Title = "After Window", StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2026, 2, 5), Status = "UPCOMING" }
    );
    await context.SaveChangesAsync();

    var service = new ConferenceService(context);
    var result = await service.GetAllAsync(1, 20, from: new DateTime(2026, 1, 10), to: new DateTime(2026, 1, 31));

    Assert.Equal(1, result.TotalCount);
    Assert.Equal("Overlaps Start", result.Data[0].Title);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/BrownEvents.Tests --filter "FullyQualifiedName~GetAllAsync_FiltersBy"`
Expected: build error — `search`/`from` are not valid arguments for `GetAllAsync` yet.

- [ ] **Step 3: Add the new parameters and conditional query composition**

In `IConferenceService.cs:7`, change the signature to:
```csharp
Task<PagedResult<Conference>> GetAllAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken cancellationToken = default);
```

In `ConferenceService.cs`, replace the `GetAllAsync` method body (`:16-43`) with:

```csharp
public async Task<PagedResult<Conference>> GetAllAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null, string? status = null, CancellationToken cancellationToken = default)
{
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var query = _context.Conferences.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(c => c.Title.ToLower().Contains(term) || (c.Description != null && c.Description.ToLower().Contains(term)));
    }

    if (!string.IsNullOrWhiteSpace(status))
    {
        var normalizedStatus = status.Trim().ToLower();
        query = query.Where(c => c.Status.ToLower() == normalizedStatus);
    }

    if (from.HasValue)
    {
        query = query.Where(c => c.EndDate >= from.Value);
    }

    if (to.HasValue)
    {
        query = query.Where(c => c.StartDate <= to.Value);
    }

    var totalCount = await query.CountAsync(cancellationToken);

    // Same N+1-avoidance rationale as before: Include Sessions/Speaker/Room before ToListAsync.
    var data = await query
        .AsNoTracking()
        .OrderBy(c => c.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Include(c => c.Sessions).ThenInclude(s => s.Speaker)
        .Include(c => c.Sessions).ThenInclude(s => s.Room)
        .ToListAsync(cancellationToken);

    return new PagedResult<Conference>
    {
        Data = data,
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/BrownEvents.Tests --filter "FullyQualifiedName~ConferenceServiceTests"`
Expected: all `ConferenceServiceTests` pass, including the two new tests and the three pre-existing `GetAllAsync_*` tests (which pass no filter args and must still see identical, unfiltered results).

- [ ] **Step 5: Commit**

---

### Task 2: Controller query-param bindings (`ConferencesController.GetAll`)

**Files:**
- Modify: `backend/BrownEvents.Api/Controllers/ConferencesController.cs:28-42`

**Interfaces:**
- Consumes: `IConferenceService.GetAllAsync(int page, int pageSize, string? search, DateTime? from, DateTime? to, string? status, CancellationToken)` from Task 1.
- Produces: `GET /api/conferences?search=&from=&to=&status=` — consumed by Task 3's `api.js`.

Test-first: no — no controller-level HTTP test is required or added (per spec non-goals); this is a thin `[FromQuery]` wiring change with no new branching logic to unit-test in isolation from Task 1's already-tested service logic.

- [ ] **Step 1: Add query params, XML docs, and pass them through**

Replace the `GetAll` action and its doc comment (`ConferencesController.cs:28-42`) with:

```csharp
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
```

Verify: `dotnet build backend/BrownEvents.Api` compiles cleanly (this is the only call site of the old 3-arg positional `GetAllAsync(page, pageSize, cancellationToken)` form, now updated to the new order).

- [ ] **Step 2: Commit**

---

### Task 3: Frontend search and date-range filtering UI

**Files:**
- Modify: `frontend/src/api.js:16-17`
- Modify: `frontend/src/pages/ConferenceListPage.jsx`

**Interfaces:**
- Consumes: `GET /api/conferences?search=&from=&to=&status=` from Task 2.
- Produces: `getConferences(page, pageSize, filters)` where `filters` is `{ search, from, to, status }`, all optional — no other file currently calls `getConferences`, so this is an additive signature change.

Test-first: no — no frontend test runner exists in this repo (per spec non-goals); this UI has no automated safety net beyond the backend tests already covering the filter semantics it calls into.

- [ ] **Step 1: Add optional filters to `getConferences`**

Replace `api.js:16-17` with:

```javascript
export const getConferences = (page = 1, pageSize = 20, filters = {}) => {
  const { search, from, to, status } = filters
  const params = new URLSearchParams({ page, pageSize })
  if (search) params.set('search', search)
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (status) params.set('status', status)
  return request(`/api/conferences?${params.toString()}`)
}
```

- [ ] **Step 2: Add search/date-range state and wire it into the fetch effect**

In `ConferenceListPage.jsx`, add state next to the existing `page`/`totalCount` declarations (`:10-14`):

```jsx
const [search, setSearch] = useState('')
const [fromDate, setFromDate] = useState('')
const [toDate, setToDate] = useState('')
```

Change the `useEffect` (`:16-29`) to depend on the filters and pass them through:

```jsx
useEffect(() => {
  setLoading(true)
  setError(null)
  getConferences(page, PAGE_SIZE, { search, from: fromDate, to: toDate })
    .then((data) => {
      setConferences(Array.isArray(data.data) ? data.data : [])
      setTotalCount(data.totalCount || 0)
      setLoading(false)
    })
    .catch((err) => {
      setError(err.message || 'Failed to load conferences')
      setLoading(false)
    })
}, [page, search, fromDate, toDate])
```

Add change handlers (each resets to page 1 per the spec, before this same render's state update triggers the effect above with the final values):

```jsx
const handleSearchChange = (e) => {
  setSearch(e.target.value)
  setPage(1)
}
const handleFromChange = (e) => {
  setFromDate(e.target.value)
  setPage(1)
}
const handleToChange = (e) => {
  setToDate(e.target.value)
  setPage(1)
}
```

- [ ] **Step 3: Render the search input and date range pickers, and update the empty-state copy**

Insert a filter bar above the `card-grid` div (after the `page-header` div, `:54`):

```jsx
<div className="filters" style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
  <input
    type="text"
    placeholder="Search by title or description"
    value={search}
    onChange={handleSearchChange}
    aria-label="Search conferences"
  />
  <input type="date" value={fromDate} onChange={handleFromChange} aria-label="From date" />
  <input type="date" value={toDate} onChange={handleToChange} aria-label="To date" />
</div>
```

Change the empty-state text at `:72` from `No conferences found.` to `No conferences match your filters.` (single generic message, per spec — no branching on cause).

- [ ] **Step 4: Manually verify in the running app**

Start backend + frontend per the repo's existing dev setup, load the conference list, and confirm: typing in search narrows results and resets to page 1; setting from/to dates narrows results by overlap; clearing all filters restores the full unfiltered, paginated list; an unmatched search shows "No conferences match your filters."

- [ ] **Step 5: Commit**

---

## Negative-constraints check

- No `Status` enum/validation added — Task 1's `status` filter is a plain `.ToLower()` string comparison against the existing free-text column; no enum, no `[Required]`/regex validation introduced anywhere.
- No schema/migration change — Tasks 1–3 touch only service/controller/frontend code; `Conference.cs` and `AppDbContext.cs` are untouched.
- No `hooks/use*.js` refactor — Task 3 keeps `ConferenceListPage.jsx`'s existing direct `api.js` call inside its own `useEffect`; no new file under `hooks/` is created.
- No new response envelope — Task 1/2 continue returning `PagedResult<Conference>` → `.ToDto()` → `PagedResult<ConferenceDto>`, unchanged shape.
- No frontend test runner introduced — Task 3 has no test step; no Jest/Vitest/RTL config or dependency is added.
- No debounce/throttle — Task 3's `handleSearchChange` fires the fetch on every keystroke via the effect dependency, with no timer/debounce utility introduced.
- No controller-level (`WebApplicationFactory`) HTTP tests — Task 2 explicitly adds none; the two required tests are both in Task 1 at the service layer.
- No custom error contract for malformed `from`/`to` — Task 2 relies on ASP.NET Core's default `DateTime?` model-binding 400; no try/catch or custom `ProblemDetails` added for this action.

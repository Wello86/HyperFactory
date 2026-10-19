# Spec: Conference Search and Filtering (EPMCDMPTST-2326 / BEVN-201)

## Goal

Let a user search conferences by keyword (title/description) and filter by date range
and status from the conference list page, combined correctly with existing pagination.

## Approach

Extend the existing unconditional read path rather than introduce a new query
pattern (repository, spec/filter-object, etc.). `ConferenceService.GetAllAsync`
(`backend/BrownEvents.Api/Services/ConferenceService.cs:16-43`) gains four new
optional parameters (`search`, `from`, `to`, `status`) and builds its
`IQueryable<Conference>` by conditionally chaining `.Where(...)` — only when a
param is supplied — ahead of the existing `.Include(...)`, `CountAsync()`, and
`Skip/Take`. `IConferenceService` (`IConferenceService.cs:7`) gets the matching
signature. `ConferencesController.GetAll` (`ConferencesController.cs:36-42`)
adds matching `[FromQuery]` bindings with `<param>` XML docs, per the file's
existing per-action documentation convention.

This was chosen over introducing a filter DTO or a query-object/specification
pattern: no service in this codebase (`ConferenceService`, `SessionService`,
`SpeakerService`) uses either, and a four-parameter method extension matches
the existing signature style with the least new surface.

On the frontend, `ConferenceListPage.jsx` gains local state for search text,
`from`, and `to` dates, and `status`; `api.js`'s `getConferences`
(`api.js:16-17`) gains the same four optional query params appended when
present. The page keeps its existing direct-fetch-in-`useEffect` pattern
(not the `hooks/use*.js` convention used elsewhere) — extending the file's
current pattern is in scope; fixing that pre-existing inconsistency is not.

## Filter semantics (decided, not left to the implementer)

- **search**: case-insensitive substring match against `Title` OR
  `Description`. A `null`/blank `Description` never matches. Empty or
  whitespace-only `search` is treated as absent (no filter).
- **status**: case-insensitive equality against the stored `Status` string.
  No enum or validation is added — `Conference.Status` stays a free-text
  string; the filter's normalization is entirely inside the query.
- **from/to** (date range): filters by overlap with each conference's
  `[StartDate, EndDate]`, not by `StartDate` alone — a conference already
  underway during the requested window should still match.
  - `from` only → `EndDate >= from`
  - `to` only → `StartDate <= to`
  - both → `StartDate <= to AND EndDate >= from`
- All supplied filters combine with AND. Absent params impose no filter;
  with all four absent, behavior is identical to today.
- Changing any filter on the frontend resets to page 1 before re-fetching
  (avoids landing on a now-out-of-range page from the previous filter set).

## Data flow

Browser (search input / date inputs / status control) → `ConferenceListPage`
state → `getConferences(page, pageSize, { search, from, to, status })` →
query string on `GET /api/conferences` → `ConferencesController.GetAll` →
`ConferenceService.GetAllAsync` → filtered `IQueryable<Conference>` →
existing `PagedResult<ConferenceDto>` envelope (unchanged shape) → rendered
grid, `Pagination`, and empty-state block.

## Error handling

No new validation layer is introduced. A malformed `from`/`to` value falls
through to ASP.NET Core's default model-binding behavior for an unparsable
`DateTime?` (the existing framework-level 400), matching this controller's
current lack of custom per-action validation/try-catch.

## Empty state

The existing "No conferences found." block in `ConferenceListPage.jsx`
(currently filter-unaware) becomes the message shown whenever the filtered
page's result set is empty, regardless of cause — no separate copy for
"zero globally" vs. "zero matching filters." Suggested copy: "No
conferences match your filters."

## Testing

Two new `[Fact]` tests in `backend/BrownEvents.Tests/ConferenceServiceTests.cs`,
following its existing seed-`AppDbContext` → call service → assert pattern:

1. Keyword match: seed conferences with distinguishing titles/descriptions,
   call `GetAllAsync` with `search` matching exactly one, assert only that
   conference is returned.
2. Date range: seed conferences with varied `StartDate`/`EndDate`, call
   `GetAllAsync` with `from`/`to`, assert the returned set matches the
   overlap semantics above.

These two satisfy the ticket's "at least two unit tests" criterion at the
service layer, consistent with this repo having no frontend test runner and
no prior controller-level (`WebApplicationFactory`) test for this action.

## Acceptance criteria

- `GET /api/conferences` accepts optional `search`, `from`, `to`, `status`.
- All four absent → identical results/count to current unfiltered behavior.
- `search` matches case-insensitively on `Title` or `Description`.
- `status` matches case-insensitively on the stored `Status` value.
- `from`/`to` filter by date-range overlap as defined above.
- All filters combine (AND) with each other and with `page`/`pageSize`;
  `totalCount` and `Skip`/`Take` reflect the same filtered query.
- Frontend list page has a search input and two date inputs, wired to
  refetch (resetting to page 1) on change.
- Empty filtered result set shows the existing empty-state block with a
  filter-appropriate message.
- Two new backend unit tests (keyword match, date range) pass.

## Non-goals

- No `Status` enum or backend validation — it remains free-text.
- No EF Core schema/migration change; `Conference` model is untouched.
- No refactor of `ConferenceListPage.jsx` onto the `hooks/use*.js` pattern.
- No new response envelope — `PagedResult<ConferenceDto>` is reused as-is.
- No frontend test runner (Jest/Vitest/RTL) introduced.
- No debounce/throttle infrastructure for the search input.
- No controller-level (`WebApplicationFactory`) HTTP tests required.
- No custom error-message contract for malformed `from`/`to` values beyond
  ASP.NET Core's default model-binding response.

## Decisions made on the caller's behalf

- Date range uses interval-overlap semantics, not "`StartDate` within
  range" — a conference already running during the window is included.
- `search` and `status` matching is case-insensitive, since `Status` has no
  enforced casing convention (`ConferenceService.cs:76` only uppercases on
  `UpdateAsync`, never on create) and the UI's own status vocabulary
  (`ConferenceCard.jsx:17-30`) is lowercase.
- A blank/whitespace `search` is treated the same as an absent one.
- Single generic empty-state message for both "no conferences at all" and
  "no conferences match the filters" — no copy branching.
- Frontend keeps `ConferenceListPage.jsx`'s existing direct-`api.js`-call
  pattern rather than introducing a hook, treating that inconsistency as
  pre-existing and out of scope.

## Open risks

- Filter-ahead-of-`CountAsync`/`Skip`/`Take` correctness is exercised by
  the two required tests but not by a combined filter+pagination test;
  a subtle off-by-one in total-count math under filtering could still slip
  through.
- Malformed `from`/`to` query values produce whatever generic 400 ASP.NET
  Core's default binder returns — not verified against this app's usual
  error-response shape.

# Speaker Profile Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `GET /api/speakers/{id}` returning a speaker with their sessions in conference context, plus a `/speakers/:id` profile page linked from session detail.

**Architecture:** A dedicated `SpeakerProfileDto`/`SpeakerSessionDto` pair leaves `SpeakerDto` and `SessionDto` byte-identical. `SpeakerService` eager-loads `Speaker → Sessions → Conference` in one ordered query; the controller maps and returns. Frontend mirrors `ConferenceDetailPage` + `useConference`.

**Tech Stack:** .NET 8 / EF Core (InMemory for tests), xUnit, React + React Router (Vite).

**Spec:** `docs/superpowers/tasks/2026-09-15-speaker-profile-page/spec.md`

## Global Constraints

- `SpeakerDto` (`Dtos/SpeakerDto.cs`) and `SessionDto` are **not modified**. No existing response shape changes.
- Sessions ordered by `StartTime` ascending, **in the service**; all sessions returned, no date filtering, no pagination.
- Mapping lives only in `Dtos/EntityDtoExtensions.cs`, never inline in a controller.
- `ConferenceTitle` is non-nullable and maps from `Conference.Title`. **Never** null-coalesce to a placeholder and never skip a session — a null `Conference` means a dropped eager-load and must fail loudly.
- Backend tests only: xUnit `[Fact]`, EF InMemory via a local `CreateContext()`, no mocking, `MethodName_Scenario_ExpectedResult`. No frontend test runner is added.
- Commit per task using the repository's existing convention.

---

### Task 1: Service query for a speaker with sessions in conference context

**Files:**
- Modify: `backend/BrownEvents.Api/Services/ISpeakerService.cs:5-9`
- Modify: `backend/BrownEvents.Api/Services/SpeakerService.cs:16-26`
- Test: `backend/BrownEvents.Tests/SpeakerServiceTests.cs` (create)

**Interfaces:**
- Produces: `Task<Speaker?> GetByIdWithSessionsAsync(int id, CancellationToken cancellationToken = default)` on `ISpeakerService` — returns `null` for an unknown id; on success `Sessions` is ordered by `StartTime` ascending and every session's `Conference` is loaded.

**Test-first: yes** — two `[Fact]`s that fail with "no method `GetByIdWithSessionsAsync`":
`GetByIdWithSessionsAsync_SpeakerWithSessionsAcrossConferences_ReturnsSessionsOrderedByStartTimeWithConference` and `GetByIdWithSessionsAsync_SpeakerWithNoSessions_ReturnsSpeakerWithEmptySessions`.

- [ ] **Step 1: Write the failing tests.** New file, `namespace BrownEvents.Tests`, private static `CreateContext()` copied from `SessionServiceTests.cs:11-17` (`UseInMemoryDatabase(Guid.NewGuid().ToString())`).
  - Test 1 seeds two real `Conference` rows (`Title = "Alpha Conf"` / `"Beta Conf"`, required `StartDate`/`EndDate`/`Status` as in `SessionServiceTests.cs:29-37`), one `Speaker`, and two `Session`s **seeded out of chronological order** (the later `StartTime` added first) — one per conference. Asserts: result non-null; `Sessions` count 2; `Sessions` `StartTime`s ascending; the first session's `Conference.Title` is the earlier session's conference title. Seed real `Conference` rows, not bare `ConferenceId` values — EF InMemory does not enforce the FK.
  - Test 2 seeds a speaker with no sessions. Asserts: result non-null and `Sessions` empty (not null).
- [ ] **Step 2: Run to verify failure.** `dotnet test backend/BrownEvents.Tests --filter FullyQualifiedName~SpeakerServiceTests` → compile error / FAIL.
- [ ] **Step 3: Add the interface member** to `ISpeakerService`, matching the signature above.
- [ ] **Step 4: Implement in `SpeakerService`** using the existing `_context` field (`SpeakerService.cs:9`). Single query on `_context.Speakers`, `AsNoTracking()`, ordered filtered include `Include(s => s.Sessions.OrderBy(x => x.StartTime)).ThenInclude(x => x.Conference)`, then `FirstOrDefaultAsync(s => s.Id == id, cancellationToken)`. `ConferenceService.GetSessionsAsync` is the eager-loading precedent. One query — do not loop sessions to fetch conferences (no N+1).
- [ ] **Step 5: Run the tests to verify they pass**, then commit.

---

### Task 2: Profile DTOs and mapping

**Files:**
- Create: `backend/BrownEvents.Api/Dtos/SpeakerProfileDto.cs`, `backend/BrownEvents.Api/Dtos/SpeakerSessionDto.cs`
- Modify: `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs` (add beside `ToDto(this Speaker)` at lines 24-31)

**Interfaces:**
- Consumes: the `Speaker` graph from Task 1.
- Produces: `SpeakerProfileDto ToProfileDto(this Speaker speaker)`. Named `ToProfileDto` because `ToDto(this Speaker)` already exists and C# cannot overload on return type alone.

**Test-first: no** — straight field projection with no branches; its output is asserted through the endpoint's acceptance criteria, and inventing a mapping test would duplicate Task 1's coverage.

- [ ] **Step 1: Add the two DTOs** (namespace `BrownEvents.Api.Dtos`):

```csharp
public class SpeakerSessionDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ConferenceId { get; set; }
    public string ConferenceTitle { get; set; } = string.Empty;
}

public class SpeakerProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Email { get; set; }
    public List<SpeakerSessionDto> Sessions { get; set; } = new();
}
```

- [ ] **Step 2: Add `ToProfileDto`** to `EntityDtoExtensions`, copying the speaker's own fields as `ToDto(this Speaker)` does and projecting `speaker.Sessions` in the order received. Map `ConferenceTitle = s.Conference.Title` **directly** — no `?.`, no `??` fallback, no filtering of sessions. Match `StartTime`/`EndTime` types to `Session`'s (see `Dtos/SessionDto.cs`).
- [ ] **Step 3: Build to confirm it compiles**, then commit.

---

### Task 3: `GET /api/speakers/{id}` endpoint

**Files:**
- Modify: `backend/BrownEvents.Api/Controllers/SpeakersController.cs`

**Interfaces:**
- Consumes: `ISpeakerService.GetByIdWithSessionsAsync` (Task 1), `ToProfileDto` (Task 2).
- Produces: `GET /api/speakers/{id}` → 200 `SpeakerProfileDto` | 404 `ProblemDetails`.

**Test-first: no** — the service behavior behind it is covered by Task 1; this action is the established bind→service→map→return shape with no new logic.

- [ ] **Step 1: Add the `GetById` action**, modelled line-for-line on `ConferencesController.cs:60-71`: XML doc comments with `<response>` tags, `[HttpGet("{id}")]`, `[ProducesResponseType(typeof(SpeakerProfileDto), StatusCodes.Status200OK)]`, `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]`, signature `public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)`. Body: call the service, `if (speaker is null) return NotFound(SpeakerNotFound(id));`, else `return Ok(speaker.ToProfileDto());`.
- [ ] **Step 2: Add the private helper** at the bottom of the controller, mirroring `ConferenceNotFound` (`ConferencesController.cs:184-189`):

```csharp
private static ProblemDetails SpeakerNotFound(int id) => new()
{
    Title = "Not Found",
    Status = StatusCodes.Status404NotFound,
    Detail = $"No speaker exists with id {id}.",
};
```

- [ ] **Step 3: Leave `GetAll` and `Create` untouched.** Confirm by diff that neither action's response changed, then commit.

---

### Task 4: Frontend data layer — `getSpeaker` and `useSpeaker`

**Files:**
- Modify: `frontend/src/api.js:54` (add beside `getSpeakers`)
- Create: `frontend/src/hooks/useSpeaker.js`

**Interfaces:**
- Produces: `getSpeaker(id)` → the `SpeakerProfileDto` JSON; `useSpeaker(id)` → `{ speaker, loading, error }`.

**Test-first: no** — no frontend test runner exists in this project and adding one is out of scope (spec Non-Goals).

- [ ] **Step 1: Add the API function** after `getSpeakers`: `export const getSpeaker = (id) => request(`/api/speakers/${id}`)`, using the same `request()` helper.
- [ ] **Step 2: Create the hook**, mirroring `useConference.js` but with stale-result protection. The effect is keyed on `id` and resets state on change, so a previous speaker or a previous error can never render under a new id, and a slow response for an old id cannot overwrite a newer one:

```js
import { useEffect, useState } from 'react'
import { getSpeaker } from '../api'

export default function useSpeaker(id) {
  const [speaker, setSpeaker] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    let active = true
    setSpeaker(null)
    setError(null)
    setLoading(true)
    getSpeaker(id)
      .then((data) => {
        if (!active) return
        setSpeaker(data.data || data)
        setLoading(false)
      })
      .catch((err) => {
        if (!active) return
        setError(err.message || 'Failed to load speaker')
        setLoading(false)
      })
    return () => { active = false }
  }, [id])

  return { speaker, loading, error }
}
```

- [ ] **Step 3: Commit.**

---

### Task 5: Speaker profile page and route

**Files:**
- Create: `frontend/src/pages/SpeakerProfilePage.jsx`
- Modify: `frontend/src/App.jsx:8-21` (import + one route)

**Interfaces:**
- Consumes: `useSpeaker` (Task 4), `SpeakerProfileDto` fields (Task 2, camelCased in JSON: `firstName`, `lastName`, `bio`, `sessions[].title|startTime|endTime|conferenceTitle|conferenceId|sessionId`).

**Test-first: no** — loading and error states are verified by a later manual/browser stage; no frontend test runner exists.

- [ ] **Step 1: Create the page**, following `ConferenceDetailPage.jsx`'s structure: read `id` via `useParams()`, call `useSpeaker(id)`, render the shared `Navbar` component, then branch — loading first, error second, then the loaded render. Loaded render shows `{firstName} {lastName}` as the heading, `bio` when present, and the session list; each session shows its title, its `conferenceTitle`, and its start/end time. When `sessions` is empty, render an explicit empty-state message (e.g. "This speaker has no sessions yet.") rather than a bare heading. Reuse the existing `detail-header` / `detail-section` / `card` class names used by `ConferenceDetailPage.jsx` and `SessionDetailPage.jsx:136-153`. `sessions` arrives already ordered — do not re-sort in the page.
- [ ] **Step 2: Register the route** in `App.jsx`: import the page alongside the existing page imports and add `<Route path="/speakers/:id" element={<SpeakerProfilePage />} />` after the `/speakers` route, matching the `/conferences/:id` form on line 16. Keep `/speakers` before it.
- [ ] **Step 3: Commit.**

---

### Task 6: Guarded speaker link in session detail

**Files:**
- Modify: `frontend/src/pages/SessionDetailPage.jsx:140-142` (and the `Link` import at the top of the file)

**Test-first: no** — rendering-only change with no frontend test runner; the null-speaker path is covered by the guard below and checked in the later verification stage.

- [ ] **Step 1: Wrap the speaker name in a link.** Inside the existing `card__title` div, render `<Link to={`/speakers/${speaker.id}`}>{speaker.firstName} {speaker.lastName}</Link>` **only when `speaker.id` is present**; otherwise keep the current plain-text `{speaker.firstName} {speaker.lastName}`. `Session.SpeakerId` is `int?` and the relationship is `IsRequired(false)`, so a speakerless session is valid: the outer `{speaker && (...)}` guard at line 136 stays exactly as it is, and the page must never emit a link to `/speakers/undefined`.
- [ ] **Step 2: Ensure `Link` is imported** from `react-router-dom` in this file (add it to the existing import if absent).
- [ ] **Step 3: Leave `SpeakerCard.jsx` and `SpeakerListPage.jsx` untouched** — speaker-list linking is a Non-Goal. Commit.

---

## Negative constraints honored

- *No change to `SpeakerDto`/`SessionDto`* → Task 2 creates new DTOs only; Task 3 Step 3 verifies existing responses are unchanged. No task touches either file.
- *No placeholder / no skipped session for a missing `Conference`* → Task 2 Step 2 forbids `?.` and `??`.
- *No pagination, filtering, grouping, or upcoming/past split* → Task 1 returns all sessions ordered; Task 5 Step 1 forbids re-sorting and adds no filters.
- *No speaker-list linking* → Task 6 Step 3.
- *No frontend tests or test runner* → every frontend task is `Test-first: no` for exactly this reason.
- *No nav refactor, no edit/delete endpoints or UI, no auth, no avatar/photo/social links* → no task creates any of these.

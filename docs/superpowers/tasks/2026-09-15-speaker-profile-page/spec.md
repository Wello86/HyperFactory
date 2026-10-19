# Speaker Profile Page — Design Spec

**Ticket**: EPMCDMPTST-2329
**Date**: 2026-09-15
**Complexity**: L (score 20, `complexity-assessment.json`)
**Research**: `technical-analysis.md`

---

## Goal

Give each speaker a public profile page showing their full name, bio, and every session
they are presenting — each session labelled with its conference title and time. Speaker
names in session detail become links to that page.

## Why

A speaker's sessions are currently only discoverable by browsing conferences one at a
time. Nothing in the app answers "what is this person speaking at?", and the speaker name
in session detail is a dead end (`frontend/src/pages/SessionDetailPage.jsx:140-142`).

---

## Approach

### Backend: a dedicated profile DTO

`GET /api/speakers/{id}` returns a new `SpeakerProfileDto` — the speaker's own fields plus
an ordered list of `SpeakerSessionDto`. **`SpeakerDto` and `SessionDto` are not modified.**

This is the load-bearing decision. `SessionDto.Speaker` is a `SpeakerDto`
(`backend/BrownEvents.Api/Dtos/SessionDto.cs:14`), so adding a `Sessions` collection to
`SpeakerDto` would attach a session list to the speaker embedded in *every* session
response, and make that field conditionally populated depending on which endpoint served
it. A separate profile DTO keeps all existing response contracts byte-identical.

`SpeakerSessionDto` is deliberately narrow — it carries only what the page renders, and
notably not `Speaker`, `Room` or `RegisteredCount`:

```
SpeakerSessionDto: SessionId, Title, StartTime, EndTime, ConferenceId, ConferenceTitle
```

`ConferenceTitle` maps from `Conference.Title` (`Models/Conference.cs:11`). The entity has
no `Name` property — `Title` *is* the human-readable conference label the ticket calls
"conference name", and the DTO field is named after the source to keep the mapping obvious.

Mapping lives in `Dtos/EntityDtoExtensions.cs` as a new `ToDto` overload beside the
existing ones — never inline in the controller, per that file's established role.

### Backend: the conference join is mandatory, so do not defend against it

`Session.ConferenceId` is a non-nullable `int` and the relationship is a required FK
(`Data/AppDbContext.cs:24-26` — `HasMany`/`WithOne`/`HasForeignKey` with no
`IsRequired(false)`, and `Session.Conference` is declared `null!`). **A persisted session
therefore always has a conference.** There is no valid data state in which
`ConferenceTitle` is unknown.

Consequently the mapping **must not** null-coalesce `Conference` to a placeholder such as
`"Unknown conference"`, and must not silently skip such a session. If the `Conference`
navigation is null at mapping time, the only possible cause is that the eager-load was
dropped from the query — a programming error that must surface loudly (as a failure)
rather than be laundered into a plausible-looking string in the response. Tests seed real
`Conference` rows rather than bare `ConferenceId` values, since EF InMemory does not
enforce the FK.

`ConferenceTitle` is non-nullable in the DTO, which encodes exactly this guarantee.

### Backend: service and controller

`ISpeakerService`/`SpeakerService` gain one method that loads a speaker with their
sessions and each session's conference in a single query, eager-loading along
`Speaker → Sessions → Conference`. `ConferenceService.GetSessionsAsync` is the precedent
for the eager-loading style; note that this particular join path is exercised nowhere in
the codebase today.

Sessions are ordered by `StartTime` ascending, and **all** sessions are returned —
past and future alike. Ordering happens in the service, not the page, so the ordering is
part of the tested contract. No date filtering: it would make the required unit test
depend on the current clock, and the repo's EF InMemory tests have no clock abstraction to
inject.

`SpeakersController` gains a `GetById` action following `ConferencesController.GetById`:
bind, call one service method, map, return `Ok(...)` or `NotFound(...)`. Unknown ids
return a `ProblemDetails` from a new private `SpeakerNotFound` helper — the first 404
handling in this controller, mirroring `ConferenceNotFound`. XML doc comments and
`[ProducesResponseType]` per the controller convention.

### Frontend: hook

A `useSpeaker(id)` hook mirrors `frontend/src/hooks/useConference.js` — fetch on mount,
own the loading/error state, unwrap the response the same way.

**The effect must be keyed on `id` and reset its state when `id` changes.** React Router
re-renders the same component when only the route param changes, rather than unmounting
it, so a hook that fetches once on mount would keep showing the previous speaker under the
new id. On an `id` change the hook re-runs the fetch and returns to the loading state,
clearing any previously loaded speaker and any previous error. A stale speaker must never
render under a new id — including after a failed fetch, where the error must not persist
into the next successful one. Responses that arrive out of order must not overwrite a
newer id's result.

`api.js` gains a `getSpeaker(id)` alongside the existing `getSpeakers`.

### Frontend: pages and routing

`SpeakerProfilePage.jsx` follows `ConferenceDetailPage.jsx`: the shared `Navbar`
component, then loading and error branches before the main render. A speaker with zero
sessions gets an empty-state message rather than a bare heading.

`App.jsx` gains `/speakers/:id`, matching the existing `/conferences/:id` pattern.

### Frontend: the speaker link must be guarded

In `SessionDetailPage.jsx`, the speaker name becomes a `Link` to `/speakers/{id}`. The id
is already present on `session.speaker`, so no API change is needed.

**A session may legitimately have no speaker.** `Session.SpeakerId` is `int?` and the
relationship is explicitly `.IsRequired(false)` (`Data/AppDbContext.cs:36-39`), so
`session.speaker` can be null in a perfectly valid response. The link is therefore
rendered only when `session.speaker` exists and carries an id; otherwise the page falls
back to its current non-link rendering (or the existing placeholder for a speakerless
session). Session detail must not crash, render an empty link, or emit a link to
`/speakers/undefined`.

### Testing

A new `backend/BrownEvents.Tests/SpeakerServiceTests.cs` — net-new, as no speaker service
test file exists. Structure follows `SessionServiceTests.cs`: xUnit `[Fact]`, EF InMemory
via a local `CreateContext()`, no mocking, `MethodName_Scenario_ExpectedResult` naming.

AC5 asks for "at least one" test; **one is not enough here**, and two `[Fact]`s are
required:

1. **Populated case** — a speaker with sessions across two conferences returns the list
   ordered by start time, each entry carrying the correct `ConferenceTitle`. Ordering and
   conference context are the two things most likely to break silently, so both are
   asserted, and seeding out of chronological order is what makes the ordering assertion
   meaningful.
2. **Empty case** — a speaker with no sessions returns a speaker with an empty session
   list, not null and not an error. Without this, the zero-session path has no automated
   coverage at all: the UI empty state is manual-verification only, so a backend
   regression returning null there would surface as a frontend crash rather than a failing
   test.

AC4's loading and error states are verified manually — no frontend test runner exists in
this project, and adding one is out of scope.

---

## Acceptance Criteria

1. `GET /api/speakers/{id}` returns the speaker's full name, bio, and their sessions, each
   with conference title and session start/end time.
2. `GET /api/speakers/{id}` returns a 404 `ProblemDetails` for an unknown id.
3. The embedded session list is ordered by start time ascending and includes all sessions.
4. A speaker with no sessions returns an empty session list (not null, not a 404).
5. A profile page renders at `/speakers/:id` showing full name, bio, and the session list.
6. The page shows a loading state while fetching and an error state if the request fails.
7. A speaker with no sessions renders an empty-state message.
8. Changing the `:id` route param while the page stays mounted refetches and shows the
   loading state; no stale speaker or stale error from the previous id is ever displayed.
9. The speaker name in session detail links to `/speakers/{speakerId}`, and renders as
   plain non-link text when the session has no speaker.
10. `SpeakerServiceTests` has two `[Fact]`s: sessions-with-conference-context (asserting
    order and title) and the empty-list case.
11. Existing `GET /api/speakers`, `POST /api/speakers` and all session responses are
    unchanged.

## Non-Goals

- **No change to `SpeakerDto` or `SessionDto`** — the whole point of the profile DTO.
- **No speaker-list linking** — `SpeakerCard`/`SpeakerListPage` are untouched; only
  session detail links to the profile.
- No pagination of the embedded session list (see Risks — assessed and accepted).
- No filtering, grouping, or upcoming/past split of the session list.
- No frontend automated tests, and no frontend test runner.
- No refactor of the existing inline-`<nav>` duplication outside the new page.
- No speaker edit or delete endpoints or UI.
- No auth or authorization on the new endpoint (the project has none).
- No speaker avatar, photo, or social links.

## Risks

- **Unbounded session list — assessed and accepted.** The embedded list is never
  paginated, so a speaker with very many sessions produces a proportionally large response
  payload and a long, unbroken page. Accepted for now: only two endpoints in the codebase
  paginate at all (`GET api/conferences` and its sessions), realistic speaker session
  counts are in the single or low double digits, and the profile page is read-mostly and
  low-traffic. Revisit if a speaker's list grows past roughly a screen or two, or if the
  endpoint shows up as slow — the fix is to paginate the embedded list, which would be a
  breaking change to `SpeakerProfileDto`'s shape and so is better decided before more
  clients depend on it.
- The `Speaker → Session → Conference` join path is new to this codebase, so the
  eager-loading has no exact precedent; verify one query, not N+1.
- Loading and error states rest on pattern-matching `ConferenceDetailPage` rather than on
  an automated check, since no frontend test runner exists.

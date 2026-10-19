# Session Create/Edit Forms — Design Spec

**Ticket**: EPMCDMPTST-2332 (BEVN-207)
**Status**: Draft — pending review
**Date**: 2026-09-16

## 1. Summary

Conference organizers can create a new session from the conference detail page and edit an
existing session from the session detail page, mirroring the existing Conference create/edit
form pattern. Both forms validate required fields client-side, redirect to the relevant detail
page on success, and handle loading/error states the same way the Conference forms already do.

## 2. Scope

**In scope**: a `SessionCreatePage` and `SessionEditPage`, the `api.js`/route/entry-point wiring
they need, and one small new backend read endpoint (`GET /api/rooms`) so the Room field can be
populated the same way the Speaker field already is.

**Out of scope**:
- Room creation/management UI — `GET /api/rooms` is read-only; there is no ticket requirement to
  create or edit rooms.
- A backend `[Required]`/range annotation on `Session.Capacity` — validated client-side only (see
  §3). Adding a matching server-side annotation was considered and rejected to keep this ticket
  frontend-only, consistent with the technical research's scope conclusion.
- Session deletion.
- Authentication/authorization — matches the app's current no-auth baseline
  (`security-practices.md`).

## 3. Decisions

These were resolved during brainstorming and drive the design below:

| Decision | Choice | Why |
|---|---|---|
| Room field data source | Add `GET /api/rooms` + `getRooms()`, populate a `<select>` | No rooms-listing endpoint exists today; mirroring the existing Speaker picker (`getSpeakers()` + `<select>`) is the smallest correct fix and keeps `RoomId` referentially valid, unlike free text |
| Capacity validation | Client-side only (`validate()` requires a positive integer) | `Session.Capacity` has no backend annotation; adding one would expand this ticket beyond the frontend, and the AC's "validate required fields before submission" is satisfiable client-side |
| Session detail/time fields | Required client-side (Title, StartTime, EndTime, EndTime ≥ StartTime), same shape as `ConferenceCreatePage.jsx`/`ConferenceEditPage.jsx`'s `validate()` | Mirrors the sibling form's existing convention exactly rather than inventing a different validation shape for Sessions |

## 4. Architecture

No new subsystem. One new thin vertical is added on the backend (`Room` read-only, mirroring the
existing `Speaker` read-only list endpoint), and the frontend gets a new page/route pair per
create/edit, following the existing `pages → hooks/api.js → backend REST` chain already
established for Conferences.

```
Frontend                              Backend
--------                              -------
SessionCreatePage.jsx                 ConferencesController
  -> api.js: addSession(confId, ..)     POST /api/conferences/{id}/sessions   (existing)
  -> api.js: getSpeakers()            SessionsController
  -> api.js: getRooms()  ------------>   GET /api/sessions/{id}               (existing)
                                          PUT /api/sessions/{id}               (existing)
SessionEditPage.jsx                   RoomsController (new)
  -> api.js: getSession(id)             GET /api/rooms  -> RoomService.GetAllAsync (new)
  -> api.js: updateSession(id, ..)
  -> api.js: getSpeakers()
  -> api.js: getRooms()

ConferenceSessionsSection.jsx  --"Add Session"-->  /conferences/:id/sessions/new
SessionDetailPage.jsx          --"Edit"---------->  /conferences/:id/sessions/:sessionId/edit
```

## 5. Backend Components

### 5.1 `IRoomService` / `RoomService` (new)

Mirrors `ISpeakerService`/`SpeakerService` exactly — a single read method, no write methods (out
of scope per §2):

```csharp
public interface IRoomService
{
    Task<List<Room>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class RoomService : IRoomService
{
    public async Task<List<Room>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Rooms.AsNoTracking().ToListAsync(cancellationToken);
}
```

### 5.2 `RoomsController` (new)

```
GET /api/rooms
```

Mirrors `SpeakersController.GetAll` exactly: `Ok(rooms.Select(r => r.ToDto()).ToList())`.
`RoomDto` and `EntityDtoExtensions.ToDto(Room)` already exist (`Dtos/RoomDto.cs`,
`Dtos/EntityDtoExtensions.cs:51`) — no DTO changes needed.

### 5.3 `Program.cs`

Register `IRoomService`/`RoomService` with `AddScoped`, same line shape as the existing
`ISpeakerService`/`SpeakerService` registration.

## 6. Frontend Components

### 6.1 `api.js` additions

```js
export const getRooms = () => request('/api/rooms')

export const addSession = (conferenceId, data) =>
  request(`/api/conferences/${conferenceId}/sessions`, { method: 'POST', body: JSON.stringify(data) })

export const updateSession = (id, data) =>
  request(`/api/sessions/${id}`, { method: 'PUT', body: JSON.stringify(data) })
```

### 6.2 `pages/SessionCreatePage.jsx` (new)

Route: `/conferences/:id/sessions/new`. Structurally identical to `ConferenceCreatePage.jsx`:

- `formData`: `title`, `description`, `startTime`, `endTime`, `capacity`, `speakerId`, `roomId`.
- On mount, fetches `getSpeakers()` and `getRooms()` in parallel to populate two `<select>`
  dropdowns (both optional — `speakerId`/`roomId` are nullable on `Session`, so an empty selection
  submits `null`).
- `validate()`: Title required (matches backend `[Required]`), StartTime required, EndTime
  required, EndTime ≥ StartTime (same four checks as `ConferenceCreatePage.validate()`, applied to
  session fields), Capacity required and a positive integer (per §3 decision).
- `handleSubmit`: `addSession(conferenceId, formData)` → on success, `navigate(`/conferences/${conferenceId}`)`
  (redirect to the conference detail page, since the session was created from there); on failure,
  sets `error` from `err.message` and stops loading — same shape as
  `ConferenceCreatePage.handleSubmit`.
- `loading`/`error` state pair, no separate fetch state needed (nothing to load before the form
  renders except the speaker/room option lists).

### 6.3 `pages/SessionEditPage.jsx` (new)

Route: `/conferences/:id/sessions/:sessionId/edit`. Structurally identical to
`ConferenceEditPage.jsx`:

- On mount, fetches `getSession(sessionId)` to populate `formData`, plus `getSpeakers()`/`getRooms()`
  for the dropdowns — three parallel fetches, `loading`/`error` cover this initial load.
- Same `formData` shape and `validate()` as §6.2.
- `handleSubmit`: `updateSession(sessionId, formData)` → on success,
  `navigate(`/conferences/${conferenceId}/sessions/${sessionId}`)` (redirect to the session detail
  page); on failure, sets `submitError` and stops `submitting` — mirrors
  `ConferenceEditPage.handleSubmit`'s `submitting`/`submitError` pair, kept separate from the
  initial-load `loading`/`error` pair.
- `Cancel` link back to the session detail page, matching `ConferenceEditPage.jsx`'s `Cancel`
  link back to the conference detail page.

### 6.4 `App.jsx`

Add two routes, in the same position as the existing session-detail route:

```jsx
<Route path="/conferences/:id/sessions/new" element={<SessionCreatePage />} />
<Route path="/conferences/:id/sessions/:sessionId/edit" element={<SessionEditPage />} />
```

### 6.5 Entry points

- **`components/ConferenceSessionsSection.jsx`**: add an "Add Session" link
  (`to={`/conferences/${conferenceId}/sessions/new`}`) next to the existing
  `<h2 className="detail-section__title">Sessions</h2>` heading. `conferenceId` is already a prop
  on this component — no new prop threading needed.
- **`pages/SessionDetailPage.jsx`**: add an "Edit" link
  (`to={`/conferences/${id}/sessions/${sessionId}/edit`}`) next to the existing
  "← Back to Conference" link in the action row (both `id` and `sessionId` are already destructured
  from `useParams()` in this file).

## 7. Error Handling

Identical to the Conference forms: `api.js`'s `request()` already throws with the server's
`detail` message on any non-2xx response; both new pages catch that into `error`/`submitError`
state and render it inline, the same way `ConferenceCreatePage.jsx`/`ConferenceEditPage.jsx` do.
A disabled submit button plus a loading label (e.g. "Saving...") covers the in-flight state; no
new error-handling pattern is introduced.

## 8. Testing

- **`RoomServiceTests.cs`** (new, mirrors `SpeakerServiceTests.cs`): `GetAllAsync` returns all
  seeded rooms.
- **`ConferenceServiceTests.cs`** (extend): add the missing `AddSessionAsync` test (sets
  `ConferenceId` from the route parameter, persists, returns the created session) — currently
  uncovered, and this ticket is what first exercises that path from the UI.
- **Frontend**: no automated tests — no frontend test runner exists in this repository today (a
  project-wide gap, not specific to this feature, per `testing-patterns.md`).

## 9. Non-Goals

- Room creation/editing UI or endpoints (see §2).
- A backend annotation for `Session.Capacity` (see §2, §3).
- Session deletion.
- Pagination or filtering on `GET /api/rooms` — the existing `GET /api/speakers` endpoint it
  mirrors has none either, and the ticket doesn't call for it.
- Authentication/authorization on the new endpoint (matches the app's current no-auth baseline).

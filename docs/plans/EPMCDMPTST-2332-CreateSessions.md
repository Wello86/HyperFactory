# Session Create/Edit Forms (EPMCDMPTST-2332 / BEVN-207)

## Context

Conferences can be created/edited from the UI, but sessions cannot — there's no form to add a
session to a conference or edit an existing session's title, speaker, room, time, or capacity
without calling the API directly. There's also no read endpoint for Rooms, so a Room picker has
no data source (unlike Speaker, which already has `GET /api/speakers` + `getSpeakers()`).

Goal: mirror the existing `ConferenceCreatePage.jsx`/`ConferenceEditPage.jsx` pattern for
Sessions, adding one small new backend endpoint (`GET /api/rooms`) so the Room field has data to
populate. All research for this plan has already been verified against the current code.

Acceptance Criteria:
1. A form to create a new session is accessible from the conference detail page.
2. A form to edit an existing session is accessible from the session detail page.
3. Both forms validate required fields before submission.
4. After a successful create or edit, the user is redirected to the relevant conference/session detail page.
5. Both forms handle loading and error states gracefully.

## TDD Strategy

- **Backend: Yes.** Two tests, written first:
  1. `backend/BrownEvents.Tests/RoomServiceTests.cs` (new) — `GetAllAsync_ReturnsAllRooms`: seed 2 `Room`s, assert `RoomService.GetAllAsync()` returns both. Fails first because `RoomService`/`IRoomService` don't exist yet.
  2. `backend/BrownEvents.Tests/ConferenceServiceTests.cs` (extend) — `AddSessionAsync_PersistsSession_AndSetsConferenceIdFromRoute`: create a `Conference`, call `service.AddSessionAsync(conf.Id, new Session { ..., ConferenceId = 999 })`, assert `result.ConferenceId == conf.Id` (not 999). Pins existing untested behavior (`ConferenceService.cs:137`) — should pass immediately, no prod code change needed.
- **Frontend: No.** No frontend test runner exists in this repo (confirmed project-wide gap, out of scope to introduce here).

## Approach

Backend is otherwise unchanged — `POST /api/conferences/{id}/sessions` and `PUT /api/sessions/{id}`
already exist and work (`ConferencesController.AddSession`, `SessionsController.Update`). Only
the Room read endpoint is new. The frontend gets two new pages, three new `api.js` functions, two
new routes, and one new entry-point link each on `ConferenceSessionsSection.jsx` and
`SessionDetailPage.jsx`.

### Backend

1. **`backend/BrownEvents.Api/Services/IRoomService.cs`** (new) — mirrors `ISpeakerService.cs`:
   ```csharp
   public interface IRoomService { Task<List<Room>> GetAllAsync(CancellationToken cancellationToken = default); }
   ```
2. **`backend/BrownEvents.Api/Services/RoomService.cs`** (new) — mirrors `SpeakerService.cs`: constructor-injects `AppDbContext`, `GetAllAsync => await _context.Rooms.AsNoTracking().ToListAsync(cancellationToken)`.
3. **`backend/BrownEvents.Api/Controllers/RoomsController.cs`** (new) — mirrors `SpeakersController.GetAll`: `[ApiController][Route("api/rooms")]`, `[HttpGet]`, `return Ok(rooms.Select(r => r.ToDto()).ToList())`. Reuses existing `RoomDto`/`EntityDtoExtensions.ToDto(Room)` (`Dtos/EntityDtoExtensions.cs:51`) — no DTO changes.
4. **`backend/BrownEvents.Api/Program.cs`** — add `builder.Services.AddScoped<IRoomService, RoomService>();` right after the existing `ISpeakerService` registration (line 36).
5. Tests per TDD Strategy above.

### Frontend

6. **`frontend/src/api.js`** (append after line 71, the existing `getSpeaker` export):
   ```js
   export const getRooms = () => request('/api/rooms')
   export const addSession = (conferenceId, data) => request(`/api/conferences/${conferenceId}/sessions`, { method: 'POST', body: JSON.stringify(data) })
   export const updateSession = (id, data) => request(`/api/sessions/${id}`, { method: 'PUT', body: JSON.stringify(data) })
   ```
7. **`frontend/src/pages/SessionCreatePage.jsx`** (new, route `/conferences/:id/sessions/new`) — structurally identical to `ConferenceCreatePage.jsx`:
   - `formData`: `{ title, description, startTime, endTime, capacity, speakerId, roomId }`.
   - On mount, `Promise.all([getSpeakers(), getRooms()])` to populate two `<select>`s (blank option = `null`).
   - `validate()`: Title/StartTime/EndTime required, EndTime ≥ StartTime, Capacity required positive integer.
   - `handleSubmit`: `addSession(conferenceId, { ...formData, capacity: Number(...), speakerId: formData.speakerId ? Number(...) : null, roomId: formData.roomId ? Number(...) : null })` → on success `navigate('/conferences/${conferenceId}')`; on failure set `error`, clear `loading`.
8. **`frontend/src/pages/SessionEditPage.jsx`** (new, route `/conferences/:id/sessions/:sessionId/edit`) — structurally identical to `ConferenceEditPage.jsx`:
   - On mount, `Promise.all([getSession(sessionId), getSpeakers(), getRooms()])`; map session into `formData` (`startTime`/`endTime` sliced to 16 chars for `datetime-local`, `speakerId`/`roomId` `?? ''`).
   - Same `validate()` as Step 7. Separate `submitting`/`submitError` pair (distinct from initial `loading`/`error`), matching `ConferenceEditPage.jsx`.
   - `handleSubmit`: `updateSession(sessionId, {...})` → on success `navigate('/conferences/${conferenceId}/sessions/${sessionId}')`.
9. **`frontend/src/App.jsx`** — import both new pages; add routes right after the existing session-detail route (line 20):
   ```jsx
   <Route path="/conferences/:id/sessions/new" element={<SessionCreatePage />} />
   <Route path="/conferences/:id/sessions/:sessionId/edit" element={<SessionEditPage />} />
   ```
10. **`frontend/src/components/ConferenceSessionsSection.jsx`** (line 17) — wrap the `<h2>Sessions</h2>` in a flex row with a new `<Link to={\`/conferences/${conferenceId}/sessions/new\`} className="btn btn--primary">Add Session</Link>`.
11. **`frontend/src/pages/SessionDetailPage.jsx`** (lines 190-193) — add `<Link to={\`/conferences/${id}/sessions/${sessionId}/edit\`} className="btn btn--secondary" style={{ marginLeft: '12px' }}>Edit</Link>` right after the existing "← Back to Conference" link, same container. `id`/`sessionId` are already destructured from `useParams()`.

## Scope Boundaries

- Room creation/editing UI or endpoints: out of scope — `GET /api/rooms` is read-only.
- No backend `[Required]`/range annotation on `Session.Capacity` — client-side validation only.
- No session deletion, no auth, no pagination on `/api/rooms`, no frontend test framework introduction.

## Verification

- `dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj -c Release --logger "console;verbosity=normal"` — confirm `RoomServiceTests` and the new `AddSessionAsync` test pass, plus full suite green.
- `dotnet build backend/BrownEvents.sln -c Release` and `dotnet format backend/BrownEvents.sln --verify-no-changes`.
- From `frontend/`: `npm run lint && npm run format:check && npm run build`.
- Manual: start both dev servers; from a conference detail page click "Add Session", submit blank (expect inline validation, no network call), submit EndTime < StartTime (expect error), submit a valid session (expect redirect + session appears in list); open the new session, click "Edit", change the title, submit (expect redirect to session detail with new title); verify `GET /api/rooms` returns seeded rooms via curl/Swagger.

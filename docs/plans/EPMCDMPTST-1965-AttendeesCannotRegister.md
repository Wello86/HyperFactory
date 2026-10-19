# EPMCDMPTST-1965: Session-level registration

## Context

Attendees can already register for a conference, but `Session` (which has a `Capacity`
field) has no registration concept at all — no way to sign up for an individual session,
no enforcement of capacity, and no way to see who's registered. This ticket adds
register/cancel for sessions, exposes a registration-count-vs-capacity display, and shows
whether the current attendee is registered.

There is no login/auth system in this app (conference registration is guest-checkout
style: name/email typed fresh each time, nothing persisted). Per product decision, "who
is the current attendee" for the session page will be answered by **persisting the
attendee's email in the browser's `localStorage`** after a successful session
registration — no real auth is being introduced. This is a deliberate, documented
limitation (shared per-browser, not per-person) rather than a gap.

## Backend

### New entity — mirrors the existing `Registration` (conference) pattern

`backend/BrownEvents.Api/Models/SessionRegistration.cs` (new):
```csharp
public class SessionRegistration
{
    public int Id { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "CONFIRMED"; // CONFIRMED | CANCELLED

    public int SessionId { get; set; }
    public int AttendeeId { get; set; }

    public virtual Session Session { get; set; } = null!;
    public virtual Attendee Attendee { get; set; } = null!;
}
```
Cancellation is a soft-delete (`Status = "CANCELLED"`), not a row delete. Every count/lookup
query filters on `Status == "CONFIRMED"`, so cancelling frees the slot and re-registration
after cancellation is allowed (not treated as a duplicate).

- `Models/Session.cs` and `Models/Attendee.cs`: add
  `public virtual ICollection<SessionRegistration> SessionRegistrations { get; set; } = new List<SessionRegistration>();`
  (alongside `Attendee.Registrations`, which stays conference-only).
- `Data/AppDbContext.cs`: add `DbSet<SessionRegistration> SessionRegistrations => Set<SessionRegistration>();`
  and, in `OnModelCreating`, configure `.ToTable("session_registrations")` with
  `HasOne(Session).WithMany(SessionRegistrations)` / `HasOne(Attendee).WithMany(SessionRegistrations)`,
  matching the existing `Registration` config exactly (no unique index — the codebase has no
  DB-level dedup anywhere; duplicate-prevention is app-level, same as today).
- No migrations exist (`Database.EnsureCreated()`), so anyone testing locally needs a fresh/dropped
  dev DB for `session_registrations` to appear.

### DTOs — `Dtos/`

- `SessionRegistrationDto.cs` (new): mirrors `RegistrationDto` — `Id, RegisteredAt, Status, SessionId, AttendeeId, AttendeeDto Attendee`.
- `SessionDto.cs`: add `public int RegisteredCount { get; set; }` and `public int? CurrentAttendeeRegistrationId { get; set; }`.
- `EntityDtoExtensions.cs`:
  - Add `ToDto(this SessionRegistration registration)` mirroring `ToDto(this Registration)`.
  - Change `ToDto(this Session session)` to `ToDto(this Session session, int registeredCount = 0, int? currentAttendeeRegistrationId = null)` — default params keep every existing call site (`ConferencesController` session list/add) compiling unchanged; those call sites will show `RegisteredCount = 0`, which is acceptable since the AC only requires accuracy on the session detail page.

### Error handling — no exceptions, no `Result<T>` exists in this codebase; introduce one small result type per operation

`Services/ISessionRegistrationService.cs` (new — interface + supporting types in one file):
```csharp
public enum SessionRegistrationOutcome { Success, AlreadyRegistered, SessionFull }
public class SessionRegistrationResult
{
    public SessionRegistrationOutcome Outcome { get; init; }
    public SessionRegistration? Registration { get; init; }
    public static SessionRegistrationResult Success(SessionRegistration r) => new() { Outcome = SessionRegistrationOutcome.Success, Registration = r };
    public static SessionRegistrationResult Failure(SessionRegistrationOutcome o) => new() { Outcome = o };
}

public enum SessionCancellationOutcome { Success, NotFound }
public class SessionCancellationResult
{
    public SessionCancellationOutcome Outcome { get; init; }
    public static SessionCancellationResult Success() => new() { Outcome = SessionCancellationOutcome.Success };
    public static SessionCancellationResult Failure(SessionCancellationOutcome o) => new() { Outcome = o };
}

public class SessionRegistrationSummary
{
    public int RegisteredCount { get; init; }
    public int? CurrentAttendeeRegistrationId { get; init; }
}

public interface ISessionRegistrationService
{
    Task<SessionRegistrationResult> RegisterAttendeeAsync(int sessionId, int capacity, Attendee attendeeData, CancellationToken cancellationToken = default);
    Task<SessionCancellationResult> CancelRegistrationAsync(int sessionId, int registrationId, CancellationToken cancellationToken = default);
    Task<SessionRegistrationSummary> GetSummaryAsync(int sessionId, string? email, CancellationToken cancellationToken = default);
}
```
`RegisterAttendeeAsync` takes `capacity` as a param — the controller already loads the `Session`
for its 404 check, so pass `session.Capacity` through instead of re-querying.

Design call: "cancel a nonexistent registration" and "cancel an already-cancelled one" both
return `NotFound` — there's no `AlreadyCancelled` state. Acceptable since the cancel button only
ever appears when the frontend already believes there's an active registration.

### `Services/SessionRegistrationService.cs` (new)

Follows the exact transaction-guard pattern from `RegistrationService.RegisterAttendeeAsync`
(`using var transaction = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(...) : null;`,
commit only if not null — required per CLAUDE.md for any new multi-step write):

- `RegisterAttendeeAsync`: within the transaction — find/create `Attendee` by email (trim + case-insensitive
  match, scoped to this new code path only; the existing conference-level `RegistrationService` stays
  exact-match, intentionally not touched) → check `AnyAsync` for an existing `CONFIRMED`
  `SessionRegistration` for `(sessionId, attendee.Id)` → return `AlreadyRegistered` if found → `CountAsync`
  `CONFIRMED` registrations for the session, return `SessionFull` if `>= capacity` → otherwise insert a
  `CONFIRMED` `SessionRegistration`, save, commit, return `Success`.
- `CancelRegistrationAsync`: within a transaction — find the `SessionRegistration` matching
  `(id == registrationId, SessionId == sessionId, Status == "CONFIRMED")`; if none, return `NotFound`;
  else set `Status = "CANCELLED"`, save, commit, return `Success`.
- `GetSummaryAsync`: `CountAsync` `CONFIRMED` registrations for `RegisteredCount`; if `email` is provided,
  look up a `CONFIRMED` registration by matching `Attendee.Email` (trim + case-insensitive) for
  `CurrentAttendeeRegistrationId`.

Note: concurrent registrations near the capacity boundary can both pass the `CountAsync` check
before either commits (no unique constraint or row lock) — this mirrors the app's existing
accepted-risk style (no DB-level dedup anywhere today), not a regression introduced here.

### DI — `Program.cs`

Add `builder.Services.AddScoped<ISessionRegistrationService, SessionRegistrationService>();`
next to the existing `IRegistrationService` line.

### `Controllers/SessionsController.cs`

- Inject `ISessionRegistrationService` alongside the existing `ISessionService`.
- Extend `GetById` to accept `[FromQuery] string? email`, call `_registrationService.GetSummaryAsync(id, email, ct)`
  after the existing 404 check, and return `Ok(session.ToDto(summary.RegisteredCount, summary.CurrentAttendeeRegistrationId))`.
- Add `POST {id}/register` (`[FromBody] Attendee attendee`) — same shape as
  `ConferencesController.Register`: 404 via existing `SessionNotFound` if session missing, else call
  `RegisterAttendeeAsync(id, session.Capacity, attendee, ct)` and switch on `Outcome`:
  `Success` → `Created($"api/sessions/{id}/registrations/{registration.Id}", registration.ToDto())`;
  `AlreadyRegistered`/`SessionFull` → `Conflict(ProblemDetails)` (new private helpers, same style as
  `SessionNotFound`).
- Add `DELETE {id}/registrations/{registrationId}` — 404 if session missing, else call
  `CancelRegistrationAsync`; `NotFound(ProblemDetails)` if outcome is `NotFound`, else `NoContent()`.

### Tests — `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs` (new)

Same conventions as `RegistrationServiceTests.cs` (real `AppDbContext` on `UseInMemoryDatabase`,
no mocking). Add a `CreateSession(context, capacity)` helper seeding a `Conference` + `Session`.
Cover, at minimum:
1. New attendee registers successfully when capacity available.
2. Registering with an existing attendee's email reuses that `Attendee` row.
3. Registering twice with the same email returns `AlreadyRegistered` (only one row created).
4. Registering past capacity returns `SessionFull`.
5. Cancelling a registration frees the slot — register (capacity 1), cancel, register a different
   attendee → `Success` (this single test satisfies the AC's "covers registration and cancellation
   service logic" most directly).
6. Cancelling a nonexistent/already-cancelled registration returns `NotFound`.
7. `GetSummaryAsync` returns the right count and matches email case-insensitively.
8. Cancellation-token handling mirrors the existing `RegisterAttendeeAsync_ThrowsWhenCancelled` test.

### Docs

- `erd.md`: add a `SESSION_REGISTRATION` entity block + `SESSION ||--o{ SESSION_REGISTRATION` relationship,
  mirroring the existing `REGISTRATION` entry.
- `docs/architecture.md`: add the new service/model to their respective lists/tables, and a short
  request-flow note for the two new endpoints plus the `email` query param on `GET /api/sessions/{id}`.

## Frontend

### Identity — `frontend/src/utils/attendeeIdentity.js` (new)

`getStoredAttendeeEmail()` / `setStoredAttendeeEmail(email)` wrapping `localStorage` in try/catch
(first localStorage usage in this codebase — some private-browsing modes throw, not just return
null). Key: `brownevents.attendeeEmail`.

### `frontend/src/api.js`

- Fix `request()` to handle `204 No Content` (`if (res.status === 204) return null` before the
  `res.json()` call) — needed for the new cancel endpoint.
- `getSession(id, email)` — append `?email=...` (URL-encoded) when an email is supplied.
- Add `registerForSession(id, data)` → `POST /api/sessions/{id}/register`.
- Add `cancelSessionRegistration(sessionId, registrationId)` → `DELETE /api/sessions/{sessionId}/registrations/{registrationId}`.

### Register form — new dedicated component, not a generalized `RegistrationModal`

`frontend/src/components/SessionRegisterModal.jsx` (new): copy `RegistrationModal.jsx`'s structure
(`.modal-overlay`/`.modal`/`.form-group`/`.error-container`/`.success-box`) but trimmed to
**firstName, lastName, email only** — no phone/company, since this is a lightweight secondary
action and those fields aren't used by session registration logic. Keeping it a separate component
avoids risking a regression in the working conference-registration flow (`RegistrationModal.jsx` +
`useRegistrationForm.js`) for a small amount of duplicated JSX.

### `frontend/src/hooks/useSessionRegistration.js` (new)

Owns: fetching the session via `getSession(sessionId, storedEmail)`, `register(formData)` (calls
`registerForSession`, then `setStoredAttendeeEmail(formData.email)`, then reloads),
`cancel()` (calls `cancelSessionRegistration(sessionId, session.currentAttendeeRegistrationId)`,
then reloads). Exposes `session, loading, error, actionLoading, actionError, register, cancel`.
Always reloads (refetches session) after a successful register/cancel so the UI reflects the
server's state rather than an optimistic guess.

### `frontend/src/pages/SessionDetailPage.jsx`

Replace the local `useState`/`useEffect`/`getSession` block with `useSessionRegistration(sessionId)`
(matches the `useConference`-style hook convention already used elsewhere). Replace the static,
non-session-aware `<Link to=".../register">Register</Link>` at the bottom of the page with:

- Always: `👥 {registeredCount} / {capacity} registered` (fix the existing display bug where
  `{session?.capacity && ...}` hides the line entirely when `capacity === 0` — use
  `typeof session?.capacity === 'number'` instead, since 0 is now a meaningful enforced value).
- If `currentAttendeeRegistrationId` is set: `badge badge--active` "You're registered" + a
  `btn btn--secondary` "Cancel Registration" button (disabled + text-swapped while `actionLoading`).
- Else if `registeredCount >= capacity`: `badge badge--cancelled` "Session Full" indicator, no
  register button.
- Else: `btn btn--primary` "Register for Session" button opening `SessionRegisterModal`.
- Render `actionError` via the existing `.error-container` pattern (covers races/stale UI state —
  the backend's 409 is the source of truth).

## Verification

1. Backend: `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~SessionRegistrationServiceTests"`
   — all new tests green; then full `dotnet test BrownEvents.Tests` to confirm no regressions.
2. `dotnet format BrownEvents.sln --verify-no-changes` (style check — note per CLAUDE.md that
   `dotnet`/`lefthook` aren't available in this environment, so this can't be run locally; rely on
   careful manual adherence to the using-directive ordering rule when editing files).
3. Manual backend smoke test via Swagger (`docker-compose up --build`, or `dotnet run` +
   `docker-compose up postgres -d`): register for a session (201), register the same email again
   (409 AlreadyRegistered), fill a session to capacity and try one more (409 SessionFull), cancel
   (204), cancel again (404), `GET /api/sessions/{id}?email=...` reflects `registeredCount` and
   `currentAttendeeRegistrationId` correctly. Note: `EnsureCreated()` won't add the new table to an
   already-created dev DB — drop/recreate it first.
4. Frontend: `npm run dev`, open a session detail page — register, confirm the count and "You're
   registered" badge update and persist across a page refresh (localStorage), cancel and confirm the
   slot frees and the button reverts, try registering the same email twice to see the 409 surfaced
   as an inline error, and fill a session to capacity to confirm the "Session Full" state.
5. `npm run lint` / `npm run format:check` on the frontend changes.

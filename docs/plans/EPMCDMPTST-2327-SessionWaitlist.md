# Implementation Plan: Session Waitlist

## 1. Context, Objective & Acceptance Criteria

- **Problem Summary:** `SessionRegistrationService.RegisterAttendeeAsync` rejects registration outright with `SessionFull` once a session's capacity is reached (`backend/BrownEvents.Api/Services/SessionRegistrationService.cs:54-57`). There is no way for an attendee to queue for a spot, and no automatic backfill when a confirmed attendee cancels (`CancelRegistrationAsync`, lines 78-102, has no promotion logic).
- **Goal:** Attendees can join a waitlist once a session is full. When a confirmed attendee cancels, the earliest-joined waitlisted attendee is automatically promoted to `"CONFIRMED"`, the promotion is logged at INFO level, and both the API and the session detail UI surface a live waitlist count.
- **Acceptance Criteria:**
  1. `POST /api/sessions/{id}/waitlist` adds an attendee to the waitlist when the session is at capacity, returning 201.
  2. `POST /api/sessions/{id}/waitlist` returns 409 without side effects when the session is not yet at capacity.
  3. `POST /api/sessions/{id}/register` on a full session still returns 409 and never creates a waitlist row (unchanged behavior, re-verified by test).
  4. Cancelling a confirmed registration automatically promotes the earliest-by-`RegisteredAt` waitlisted attendee to `"CONFIRMED"`.
  5. Promotion emits exactly one `LogInformation` entry containing the attendee ID and session ID.
  6. `GET /api/sessions/{id}` response includes `registeredCount`, `capacity`, and `waitlistCount`.
  7. `SessionDetailPage` renders capacity and waitlist count, and offers a "Join Waitlist" affordance when full.
  8. Unit tests cover: joining the waitlist, cancel-triggers-promotion, and waitlist FIFO ordering with 2+ waitlisted attendees.

## 2. TDD Strategy

- **TDD Applies:** Yes — backend only. `SessionRegistrationServiceTests.cs` already has a strong `[Fact]`-per-scenario convention (no mocking, real `AppDbContext` InMemory instance) to extend directly; every acceptance criterion involving service behavior has a clear, checkable pre/post state.
- **TDD does not apply:** Frontend (`SessionDetailPage.jsx`, `useSessionRegistration.js`, `api.js`). No test runner/config exists anywhere under `frontend/` (`package.json` has no `test` script, no `*.test.jsx` files exist) and adding one is out of scope for this ticket. Frontend correctness will be verified manually (Section 7).
- **Initial Test Cases** (write first, confirm they fail for the right reason, then implement):
  1. `JoinWaitlistAsync_AddsWaitlistedRegistration_WhenSessionAtCapacity` — session at capacity 1 with one confirmed attendee; joining waitlist returns `Success` and inserts a `SessionRegistration` with `Status == "WAITLISTED"`.
  2. `JoinWaitlistAsync_ReturnsSessionNotFull_WhenCapacityNotReached` — session with capacity 5, 0 confirmed; join attempt returns `SessionNotFull`, no row inserted.
  3. `JoinWaitlistAsync_ReturnsAlreadyRegistered_WhenAttendeeAlreadyConfirmed` — attendee already has a `"CONFIRMED"` row for the session; join attempt returns `AlreadyRegistered`.
  4. `JoinWaitlistAsync_ReturnsAlreadyWaitlisted_WhenAttendeeAlreadyWaitlisted` — attendee already has a `"WAITLISTED"` row; second join attempt returns `AlreadyWaitlisted`.
  5. `CancelRegistrationAsync_PromotesEarliestWaitlistedAttendee_InFifoOrder` — seed 2+ waitlisted attendees with distinct `RegisteredAt` timestamps out of insertion order; cancelling the confirmed attendee promotes the one with the earliest `RegisteredAt` to `"CONFIRMED"`, the other stays `"WAITLISTED"`.
  6. `CancelRegistrationAsync_LogsInformation_WhenPromotingWaitlistedAttendee` — using a capturing test logger (or `NullLogger` + a spy), assert exactly one `LogInformation` call containing the promoted attendee's id and the session id.
  7. `CancelRegistrationAsync_DoesNotPromote_WhenNoWaitlistExists` — regression: existing behavior (`CancelRegistrationAsync_FreesSlot_ForSubsequentRegistration`) must still pass unchanged; add an explicit assertion that no promotion/log occurs when the waitlist is empty.
  8. `GetSummaryAsync_IncludesWaitlistCount` — seed 1 confirmed + 2 waitlisted; `GetSummaryAsync` returns `WaitlistCount == 2`.
  9. Existing test `RegisterAttendeeAsync_ReturnsSessionFull_WhenCapacityReached` (line 100) must still pass unmodified, plus a new assertion that no `"WAITLISTED"` row was created by a plain `/register` call on a full session (criterion 3).

## 3. Affected Files

- `backend/BrownEvents.Api/Services/ISessionRegistrationService.cs`: Modify
- `backend/BrownEvents.Api/Services/SessionRegistrationService.cs`: Modify
- `backend/BrownEvents.Api/Controllers/SessionsController.cs`: Modify
- `backend/BrownEvents.Api/Dtos/SessionDto.cs`: Modify
- `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs`: Modify
- `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs`: Modify
- `frontend/src/api.js`: Modify
- `frontend/src/hooks/useSessionRegistration.js`: Modify
- `frontend/src/components/SessionRegisterModal.jsx`: Modify
- `frontend/src/pages/SessionDetailPage.jsx`: Modify
- `backend/BrownEvents.Api/Models/SessionRegistration.cs`: No change (reusing existing `Status` string field — see Section 6)

## 4. Proposed Changes (Ordered Steps)

### Step 1: `backend/BrownEvents.Api/Services/ISessionRegistrationService.cs`
- **Target Function/Module:** Outcome/result types and interface (currently lines 6-52).
- **Changes:**
  - After `SessionCancellationResult` (line 39), add:
    ```csharp
    public enum WaitlistJoinOutcome
    {
        Success,
        AlreadyRegistered,
        AlreadyWaitlisted,
        SessionNotFull,
    }

    public class WaitlistJoinResult
    {
        public WaitlistJoinOutcome Outcome { get; init; }
        public SessionRegistration? Registration { get; init; }

        public static WaitlistJoinResult Success(SessionRegistration registration) =>
            new() { Outcome = WaitlistJoinOutcome.Success, Registration = registration };

        public static WaitlistJoinResult Failure(WaitlistJoinOutcome outcome) =>
            new() { Outcome = outcome };
    }
    ```
  - Extend `SessionRegistrationSummary` (line 41-45) with `public int WaitlistCount { get; init; }`.
  - Add to `ISessionRegistrationService` (line 47-52): `Task<WaitlistJoinResult> JoinWaitlistAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default);` — signature mirrors `RegisterAttendeeAsync` exactly (same parameter shape/order).

### Step 2: `backend/BrownEvents.Api/Services/SessionRegistrationService.cs`
- **Target Function/Module:** Constructor, new `JoinWaitlistAsync`, `CancelRegistrationAsync` (lines 78-102), `GetSummaryAsync` (lines 104-126).
- **Changes:**
  - Inject `ILogger<SessionRegistrationService>` — the first `ILogger<T>` in this codebase (confirmed: `LogError` in `Program.cs:77-78` is the only existing logging call anywhere in the backend). Constructor becomes:
    ```csharp
    private readonly AppDbContext _context;
    private readonly ILogger<SessionRegistrationService> _logger;

    public SessionRegistrationService(AppDbContext context, ILogger<SessionRegistrationService> logger)
    {
        _context = context;
        _logger = logger;
    }
    ```
    No DI registration change needed — ASP.NET Core's default host already registers `ILogger<T>` for any injected type; `Program.cs:38`'s `AddScoped<ISessionRegistrationService, SessionRegistrationService>()` is unaffected.
  - Add new method `JoinWaitlistAsync`, mirroring `RegisterAttendeeAsync`'s structure (attendee resolve/create → guarded transaction → checks → insert):
    ```csharp
    public async Task<WaitlistJoinResult> JoinWaitlistAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default)
    {
        using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var normalizedEmail = attendeeData.Email.Trim();
        var existingAttendee = await _context.Attendees
            .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail.ToLower(), cancellationToken);

        Attendee attendee;
        if (existingAttendee is null)
        {
            attendee = new Attendee
            {
                FirstName = attendeeData.FirstName,
                LastName = attendeeData.LastName,
                Email = normalizedEmail,
            };
            _context.Attendees.Add(attendee);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            attendee = existingAttendee;
        }

        var alreadyConfirmed = await _context.SessionRegistrations.AnyAsync(
            sr => sr.SessionId == sessionId && sr.AttendeeId == attendee.Id && sr.Status == "CONFIRMED",
            cancellationToken);
        if (alreadyConfirmed)
        {
            return WaitlistJoinResult.Failure(WaitlistJoinOutcome.AlreadyRegistered);
        }

        var alreadyWaitlisted = await _context.SessionRegistrations.AnyAsync(
            sr => sr.SessionId == sessionId && sr.AttendeeId == attendee.Id && sr.Status == "WAITLISTED",
            cancellationToken);
        if (alreadyWaitlisted)
        {
            return WaitlistJoinResult.Failure(WaitlistJoinOutcome.AlreadyWaitlisted);
        }

        var confirmedCount = await _context.SessionRegistrations.CountAsync(
            sr => sr.SessionId == sessionId && sr.Status == "CONFIRMED", cancellationToken);
        if (confirmedCount < capacity)
        {
            return WaitlistJoinResult.Failure(WaitlistJoinOutcome.SessionNotFull);
        }

        var registration = new SessionRegistration
        {
            SessionId = sessionId,
            AttendeeId = attendee.Id,
            RegisteredAt = DateTime.UtcNow,
            Status = "WAITLISTED",
        };

        _context.SessionRegistrations.Add(registration);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return WaitlistJoinResult.Success(registration);
    }
    ```
  - Extend `CancelRegistrationAsync` (after line 94's `SaveChangesAsync`, before the commit at lines 96-99) with promotion logic inside the existing guarded transaction:
    ```csharp
    registration.Status = "CANCELLED";
    await _context.SaveChangesAsync(cancellationToken);

    var nextWaitlisted = await _context.SessionRegistrations
        .Where(sr => sr.SessionId == sessionId && sr.Status == "WAITLISTED")
        .OrderBy(sr => sr.RegisteredAt)
        .ThenBy(sr => sr.Id)
        .FirstOrDefaultAsync(cancellationToken);

    if (nextWaitlisted is not null)
    {
        nextWaitlisted.Status = "CONFIRMED";
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Promoted attendee {AttendeeId} to confirmed registration for session {SessionId}",
            nextWaitlisted.AttendeeId, sessionId);
    }

    if (transaction is not null)
    {
        await transaction.CommitAsync(cancellationToken);
    }
    ```
  - Extend `GetSummaryAsync` (lines 104-126) with a `waitlistCount` query and field:
    ```csharp
    var waitlistCount = await _context.SessionRegistrations.CountAsync(
        sr => sr.SessionId == sessionId && sr.Status == "WAITLISTED", cancellationToken);
    ```
    add `WaitlistCount = waitlistCount,` to the returned `SessionRegistrationSummary`.

### Step 3: `backend/BrownEvents.Api/Dtos/SessionDto.cs`
- **Target Function/Module:** `SessionDto` class (lines 3-18).
- **Changes:** Add `public int WaitlistCount { get; set; }` alongside the existing `RegisteredCount` (line 16).

### Step 4: `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs`
- **Target Function/Module:** `ToDto(this Session session, ...)` (lines 41-56).
- **Changes:** Add a `waitlistCount` parameter and mapping:
  ```csharp
  public static SessionDto ToDto(this Session session, int registeredCount = 0, int waitlistCount = 0, int? currentAttendeeRegistrationId = null) => new()
  {
      ...
      RegisteredCount = registeredCount,
      WaitlistCount = waitlistCount,
      CurrentAttendeeRegistrationId = currentAttendeeRegistrationId,
  };
  ```
  All other call sites (`ConferencesController.cs:124` via `s.ToDto()`, `SessionsController.cs:62` via `updated.ToDto()`) use the parameterless defaults and are unaffected.

### Step 5: `backend/BrownEvents.Api/Controllers/SessionsController.cs`
- **Target Function/Module:** `GetById` (lines 34-44), new `JoinWaitlist` action, `ProblemDetails` helpers (lines 121-147).
- **Changes:**
  - Update the `GetById` call site (line 43) to pass the new count:
    `return Ok(session.ToDto(summary.RegisteredCount, summary.WaitlistCount, summary.CurrentAttendeeRegistrationId));`
  - Add a new action, placed after `Register` (after line 94), mirroring its structure:
    ```csharp
    [HttpPost("{id}/waitlist")]
    [ProducesResponseType(typeof(SessionRegistrationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [SwaggerRequestExample(typeof(AttendeeRegistrationRequestDto), typeof(AttendeeRegistrationRequestExample))]
    public async Task<IActionResult> JoinWaitlist(int id, [FromBody] AttendeeRegistrationRequestDto request, CancellationToken cancellationToken)
    {
        var session = await _service.GetByIdAsync(id, cancellationToken);
        if (session is null) return NotFound(SessionNotFound(id));

        var result = await _registrationService.JoinWaitlistAsync(id, session.Capacity, request, cancellationToken);

        return result.Outcome switch
        {
            WaitlistJoinOutcome.Success =>
                Created($"api/sessions/{id}/registrations/{result.Registration!.Id}", result.Registration.ToDto()),
            WaitlistJoinOutcome.AlreadyRegistered => Conflict(AlreadyRegistered(id)),
            WaitlistJoinOutcome.AlreadyWaitlisted => Conflict(AlreadyWaitlisted(id)),
            WaitlistJoinOutcome.SessionNotFull => Conflict(SessionNotFull(id)),
            _ => Problem(),
        };
    }
    ```
  - Add two new `ProblemDetails` helpers alongside `AlreadyRegistered`/`SessionFull` (lines 128-140):
    ```csharp
    private static ProblemDetails AlreadyWaitlisted(int sessionId) => new()
    {
        Title = "Conflict",
        Status = StatusCodes.Status409Conflict,
        Detail = $"This attendee is already on the waitlist for session {sessionId}.",
    };

    private static ProblemDetails SessionNotFull(int sessionId) => new()
    {
        Title = "Conflict",
        Status = StatusCodes.Status409Conflict,
        Detail = $"Session {sessionId} is not yet at capacity; use /register instead.",
    };
    ```

### Step 6: `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs`
- **Target Function/Module:** All 11 existing `new SessionRegistrationService(context)` call sites; new `[Fact]`s.
- **Changes:**
  - Add `using Microsoft.Extensions.Logging.Abstractions;` and update every `new SessionRegistrationService(context)` call to `new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance)` (11 call sites: lines 51, 73, 88, 105, 134, 147, 166, 178, 194, 212, 229).
  - Add the 9 new `[Fact]`s listed in Section 2. For the log-assertion test (`CancelRegistrationAsync_LogsInformation_WhenPromotingWaitlistedAttendee`), use a small in-file fake `ILogger<SessionRegistrationService>` (record calls to `Log<TState>`) rather than a new package dependency — `Moq` is referenced but unused elsewhere in the suite and there's no mocking precedent to extend, so a minimal hand-written fake matches existing conventions better than introducing `Mock<ILogger<T>>` usage for the first time.
  - Add a `CreateWaitlistedRegistration` test helper (mirrors `CreateSession`) to seed a `SessionRegistration` with `Status = "WAITLISTED"` and a specific `RegisteredAt`, for the FIFO-ordering test.

### Step 7: `frontend/src/api.js`
- **Target Function/Module:** New export alongside `registerForSession`/`cancelSessionRegistration` (lines 48-52).
- **Changes:** Add:
  ```js
  export const joinWaitlist = (sessionId, data) =>
    request(`/api/sessions/${sessionId}/waitlist`, { method: 'POST', body: JSON.stringify(data) })
  ```

### Step 8: `frontend/src/hooks/useSessionRegistration.js`
- **Target Function/Module:** Import list (line 2), new `joinWaitlist` action alongside `register` (lines 30-44), return statement (line 59).
- **Changes:**
  - Import: `import { cancelSessionRegistration, getSession, joinWaitlist as joinWaitlistRequest, registerForSession } from '../api'`
  - Add:
    ```js
    async function joinWaitlist(formData) {
      setActionLoading(true)
      setActionError(null)
      try {
        await joinWaitlistRequest(sessionId, formData)
        setStoredAttendeeEmail(formData.email)
        await load()
        return true
      } catch (err) {
        setActionError(err.message || 'Joining the waitlist failed. Please try again.')
        return false
      } finally {
        setActionLoading(false)
      }
    }
    ```
  - Update return statement: `return { session, loading, error, actionLoading, actionError, register, cancel, joinWaitlist }`

### Step 9: `frontend/src/components/SessionRegisterModal.jsx`
- **Target Function/Module:** Header (line 19) and submit button (lines 61-63).
- **Changes:** Add optional `title`/`submitLabel`/`submitLoadingLabel` props with defaults matching current text, so the same modal serves both flows without hardcoding "Register":
  ```jsx
  export default function SessionRegisterModal({
    sessionTitle,
    title,
    submitLabel = 'Register',
    submitLoadingLabel = 'Registering...',
    formData,
    loading,
    error,
    onChange,
    onSubmit,
    onClose,
  }) {
    ...
    <h2 className="modal__title">{title ?? `Register for ${sessionTitle}`}</h2>
    ...
    <button type="submit" className="btn btn--primary" disabled={loading}>
      {loading ? submitLoadingLabel : submitLabel}
    </button>
  ```

### Step 10: `frontend/src/pages/SessionDetailPage.jsx`
- **Target Function/Module:** Hook destructure (line 10), modal-open state/handler (lines 12-18), `handleSubmit` (lines 29-33), meta line (lines 125-129), action area (lines 183-212), modal render (lines 216-226).
- **Changes:**
  - Destructure `joinWaitlist` from the hook alongside `register`/`cancel` (line 10).
  - Add `const [modalMode, setModalMode] = useState('register')` and change `openModal` to `openModal(mode) { setFormData(EMPTY_FORM); setModalMode(mode); setIsModalOpen(true) }`.
  - `handleSubmit` dispatches by mode: `const success = modalMode === 'waitlist' ? await joinWaitlist(formData) : await register(formData)`.
  - Meta line (125-129): add waitlist count next to capacity:
    ```jsx
    {typeof session?.capacity === 'number' && (
      <span>
        👥 {session.registeredCount ?? 0} / {session.capacity} registered
        {session.waitlistCount > 0 && ` · ${session.waitlistCount} waitlisted`}
      </span>
    )}
    ```
  - Action area (198-212): replace the static "Session Full" badge branch with a "Join Waitlist" button:
    ```jsx
    ) : session?.registeredCount >= session?.capacity ? (
      <button
        type="button"
        className="btn btn--primary"
        style={{ marginLeft: '12px' }}
        onClick={() => openModal('waitlist')}
        disabled={actionLoading}
      >
        Join Waitlist
      </button>
    ) : (
      <button
        type="button"
        className="btn btn--primary"
        style={{ marginLeft: '12px' }}
        onClick={() => openModal('register')}
        disabled={actionLoading}
      >
        Register for Session
      </button>
    )}
    ```
  - Modal render (216-226): pass mode-aware props:
    ```jsx
    <SessionRegisterModal
      sessionTitle={session?.title}
      title={modalMode === 'waitlist' ? `Join Waitlist for ${session?.title}` : undefined}
      submitLabel={modalMode === 'waitlist' ? 'Join Waitlist' : 'Register'}
      submitLoadingLabel={modalMode === 'waitlist' ? 'Joining...' : 'Registering...'}
      formData={formData}
      loading={actionLoading}
      error={actionError}
      onChange={handleChange}
      onSubmit={handleSubmit}
      onClose={closeModal}
    />
    ```

## 5. Scope Boundaries & Assumptions

- **Out of Scope:**
  - Voluntarily leaving the waitlist (no cancel-waitlist-entry endpoint) — not required by any acceptance criterion; `CancelRegistrationAsync`'s existing query already scopes to `Status == "CONFIRMED"`, so a waitlist-entry id 404s there with zero new code.
  - Any maximum waitlist size/cap — waitlist is unbounded.
  - Notifying the promoted attendee (email/push) — no notification infrastructure exists.
  - Any change to `/register`'s existing capacity check beyond the regression test in acceptance criterion 3.
  - Migrations tooling — this design deliberately avoids a schema change (new column) so no coordination with `Database.EnsureCreated()` is needed.
  - Frontend automated tests — no test runner exists for `frontend/`; adding one is out of scope.
  - `ConferencesController.cs`'s session-list endpoint (line 124) — it already renders `RegisteredCount = 0` by omission (parameterless `ToDto()`); waitlist count is likewise omitted there, consistent with current behavior, since no acceptance criterion covers the list view.
- **Open Questions & Assumptions:**
  - `spec.md` (from the earlier brainstorming step) states the waitlist route has "no request body." This plan assumes that was imprecise and instead has `POST /api/sessions/{id}/waitlist` accept the same `AttendeeRegistrationRequestDto` body as `/register` — a brand-new attendee joining the waitlist directly (session was already full when they first looked) still needs to supply `firstName`/`lastName`/`email`, and `JoinWaitlistAsync`'s signature must mirror `RegisterAttendeeAsync`'s (which requires this DTO) to reuse the identical attendee-resolution logic. Flagging this explicitly since it's a deviation from the prior spec document.
  - Assuming FIFO tiebreak on `Id` ascending (not just `RegisteredAt`) is acceptable, since `RegisteredAt` has only millisecond-ish resolution and two waitlist joins could theoretically collide — matches `spec.md`'s stated design.

## 6. Edge Cases & Risk Analysis

- **Potential Risks:**
  - First-ever `ILogger<T>` injection in this codebase (`development-practices.md` explicitly calls this "a departure from current practice") — low risk technically since ASP.NET Core wires it automatically, but worth flagging in review since it sets a new precedent other services may follow.
  - Concurrent cancel + join-waitlist race is bounded only by the existing guarded-transaction pattern, which itself no-ops under the EF InMemory provider used in tests — behavior under real Npgsql concurrency is unverified by unit tests. This matches the existing risk profile of `RegisterAttendeeAsync`/`CancelRegistrationAsync` and is not a new gap introduced here.
  - Reusing `Status = "WAITLISTED"` as a bare string (no enum/constant) matches the existing pattern exactly (`"CONFIRMED"`/`"CANCELLED"` are also bare strings) but means a typo anywhere would silently misbehave rather than fail to compile — consistent with existing code, not introducing new risk.
- **Breaking Changes:** No. `SessionDto.WaitlistCount` and the `ToDto` extra parameter are additive; existing callers using positional defaults or the parameterless overload are unaffected. The `SessionRegistrationService` constructor gains a required parameter, so every direct instantiation (tests only — no other `new SessionRegistrationService(...)` call site outside `Program.cs`'s DI registration) must be updated; Section 4 Step 6 covers all 11.
- **New Dependencies:** None.

## 7. Verification & Testing Plan

- **Automated Tests:**
  - `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs` — 9 new `[Fact]`s (Section 2) plus updated constructor calls at all 11 existing sites.
  - Full existing suite must still pass unmodified in behavior (`RegisterAttendeeAsync_ReturnsSessionFull_WhenCapacityReached`, `CancelRegistrationAsync_FreesSlot_ForSubsequentRegistration`, etc.).
- **Manual Checks:**
  1. `dotnet run` the API; create a session with `capacity: 1` via `PUT /api/sessions/{id}`.
  2. Register one attendee via `POST /api/sessions/{id}/register` → 201.
  3. Attempt `POST /api/sessions/{id}/waitlist` for a second attendee → 201, `Status == "WAITLISTED"` in the DB.
  4. `GET /api/sessions/{id}` → confirm `waitlistCount: 1`.
  5. `DELETE /api/sessions/{id}/registrations/{firstRegistrationId}` → confirm the console/log output shows the `LogInformation` promotion line with the correct attendee/session ids, and a subsequent `GET` shows the second attendee's registration now `"CONFIRMED"` and `waitlistCount: 0`.
  6. In the frontend (`npm run dev`), open the session detail page for a full session: confirm the "Join Waitlist" button renders instead of the static "Session Full" badge, the waitlist count displays next to capacity, and submitting the modal succeeds and refreshes the counts.
  7. Confirm `POST /register` on a still-full session still returns 409 and does not create a `"WAITLISTED"` row (criterion 3).
- **Verification Commands:**
  - `dotnet build backend/BrownEvents.sln -c Release`
  - `dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj -c Release --logger "console;verbosity=normal"`
  - `dotnet format backend/BrownEvents.sln --verify-no-changes`
  - `npm run lint && npm run format:check && npm run build` (from `frontend/`)

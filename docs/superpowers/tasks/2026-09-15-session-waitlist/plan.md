# Session Waitlist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an attendee join a FIFO waitlist once a session's capacity is reached, with automatic
promotion of the earliest waitlisted attendee when a confirmed attendee cancels, and a live
waitlist count surfaced in both the API and the `SessionDetailPage` UI.

**Architecture:** No schema change — reuse `SessionRegistration.Status` (a bare string column) with
a new `"WAITLISTED"` value alongside `"CONFIRMED"`/`"CANCELLED"`. `SessionRegistrationService`
gains `JoinWaitlistAsync`, mirroring `RegisterAttendeeAsync`'s attendee-resolve/dedup/transaction
structure, and `CancelRegistrationAsync` gains promotion-on-cancel logic. `SessionsController`
gains `POST /api/sessions/{id}/waitlist`. The frontend adds a "Join Waitlist" action to
`SessionDetailPage`, reusing the existing register modal with mode-aware copy.

**Tech Stack:** ASP.NET Core (C#) + EF Core (InMemory in tests) backend; React (JSX, no TS)
frontend; xUnit `[Fact]` tests.

**Spec:** `docs/superpowers/tasks/2026-09-15-session-waitlist/spec.md`

**Detailed reference:** `docs/plans/EPMCDMPTST-2327-SessionWaitlist.md` (full line-level diffs and
rationale for every step below).

**Commit per task using the repository's existing convention.**

## Global Constraints

- No schema/migration change — `SessionRegistration.cs` is untouched; `"WAITLISTED"` is a new value
  for the existing `Status` string.
- No maximum waitlist size — unbounded.
- No voluntary leave-waitlist endpoint.
- No notification (email/push) to a promoted attendee — no such infrastructure exists.
- No change to `/register`'s existing capacity check beyond a regression test confirming it still
  creates no waitlist row on a full session.
- No frontend automated test runner introduced — frontend changes are verified manually.
- `ILogger<SessionRegistrationService>` is the first per-service logger in this codebase — a
  deliberate, scoped exception to the prior "no service/controller logs individually" convention.

---

### Task 1: Waitlist outcome types and service contract

**Files:**
- Modify: `backend/BrownEvents.Api/Services/ISessionRegistrationService.cs`

**Interfaces:**
- Produces: `WaitlistJoinOutcome` enum (`Success`, `AlreadyRegistered`, `AlreadyWaitlisted`,
  `SessionNotFull`), `WaitlistJoinResult` class, an extended `SessionRegistrationSummary` with
  `WaitlistCount`, and the `JoinWaitlistAsync` method signature — consumed by Task 2's
  implementation and Task 4's controller action.

- [ ] **Step 1: Add the outcome/result types**

After `SessionCancellationResult`, add:

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

- [ ] **Step 2: Extend the summary type and interface**

Add `public int WaitlistCount { get; init; }` to `SessionRegistrationSummary`, and add to
`ISessionRegistrationService`:

```csharp
Task<WaitlistJoinResult> JoinWaitlistAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Commit**

---

### Task 2: `SessionRegistrationService` — join, promote, summarize

**Files:**
- Modify: `backend/BrownEvents.Api/Services/SessionRegistrationService.cs`
- Test: `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs`

**Interfaces:**
- Consumes: `WaitlistJoinResult`/`WaitlistJoinOutcome` from Task 1.
- Produces: `JoinWaitlistAsync` — called by Task 4's controller action.

Test-first: yes — extend `SessionRegistrationServiceTests.cs` directly (no mocking, real
`AppDbContext` InMemory instance per the existing convention).

- [ ] **Step 1: Write the failing tests**

Add to `SessionRegistrationServiceTests.cs`:

- `JoinWaitlistAsync_AddsWaitlistedRegistration_WhenSessionAtCapacity`
- `JoinWaitlistAsync_ReturnsSessionNotFull_WhenCapacityNotReached` — asserts **zero**
  `SessionRegistration` rows **and zero** `Attendee` rows inserted (a brand-new attendee whose join
  is rejected must not leave an orphan `Attendee` behind).
- `JoinWaitlistAsync_ReturnsAlreadyRegistered_WhenAttendeeAlreadyConfirmed`
- `JoinWaitlistAsync_ReturnsAlreadyWaitlisted_WhenAttendeeAlreadyWaitlisted`
- `CancelRegistrationAsync_PromotesEarliestWaitlistedAttendee_InFifoOrder` — seed 2+ waitlisted
  attendees with distinct `RegisteredAt` timestamps out of insertion order.
- `CancelRegistrationAsync_LogsInformation_WhenPromotingWaitlistedAttendee` — via an in-file fake
  `ILogger<SessionRegistrationService>` (no new mocking dependency).
- `CancelRegistrationAsync_DoesNotPromote_WhenNoWaitlistExists`
- `GetSummaryAsync_IncludesWaitlistCount`

Also update the existing `RegisterAttendeeAsync_ReturnsSessionFull_WhenCapacityReached` test to
assert no `"WAITLISTED"` row was created by a plain `/register` call on a full session.

- [ ] **Step 2: Run tests to verify they fail**

`dotnet test backend/BrownEvents.Tests --filter "FullyQualifiedName~SessionRegistrationServiceTests"`
— expect build errors (`JoinWaitlistAsync` doesn't exist yet, constructor now needs a logger).

- [ ] **Step 3: Inject `ILogger<T>` and implement `JoinWaitlistAsync`**

Constructor becomes:

```csharp
private readonly AppDbContext _context;
private readonly ILogger<SessionRegistrationService> _logger;

public SessionRegistrationService(AppDbContext context, ILogger<SessionRegistrationService> logger)
{
    _context = context;
    _logger = logger;
}
```

`JoinWaitlistAsync` mirrors `RegisterAttendeeAsync`'s structure, with one important ordering
difference: the attendee lookup is read-only first, the already-registered / already-waitlisted /
session-not-full checks all run before anything is written, and only after every check passes is a
new `Attendee` actually created and the `SessionRegistration` inserted:

```csharp
public async Task<WaitlistJoinResult> JoinWaitlistAsync(int sessionId, int capacity, AttendeeRegistrationRequestDto attendeeData, CancellationToken cancellationToken = default)
{
    using var transaction = _context.Database.IsRelational()
        ? await _context.Database.BeginTransactionAsync(cancellationToken)
        : null;

    var normalizedEmail = attendeeData.Email.Trim();
    var existingAttendee = await _context.Attendees
        .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail.ToLower(), cancellationToken);

    if (existingAttendee is not null)
    {
        var alreadyConfirmed = await _context.SessionRegistrations.AnyAsync(
            sr => sr.SessionId == sessionId && sr.AttendeeId == existingAttendee.Id && sr.Status == "CONFIRMED",
            cancellationToken);
        if (alreadyConfirmed)
        {
            return WaitlistJoinResult.Failure(WaitlistJoinOutcome.AlreadyRegistered);
        }

        var alreadyWaitlisted = await _context.SessionRegistrations.AnyAsync(
            sr => sr.SessionId == sessionId && sr.AttendeeId == existingAttendee.Id && sr.Status == "WAITLISTED",
            cancellationToken);
        if (alreadyWaitlisted)
        {
            return WaitlistJoinResult.Failure(WaitlistJoinOutcome.AlreadyWaitlisted);
        }
    }

    var confirmedCount = await _context.SessionRegistrations.CountAsync(
        sr => sr.SessionId == sessionId && sr.Status == "CONFIRMED", cancellationToken);
    if (confirmedCount < capacity)
    {
        return WaitlistJoinResult.Failure(WaitlistJoinOutcome.SessionNotFull);
    }

    var attendee = existingAttendee;
    if (attendee is null)
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

- [ ] **Step 4: Add promotion-on-cancel to `CancelRegistrationAsync`**

Inside the existing guarded transaction, after marking the cancelled row `"CANCELLED"`:

```csharp
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
```

- [ ] **Step 5: Add `WaitlistCount` to `GetSummaryAsync`**

```csharp
var waitlistCount = await _context.SessionRegistrations.CountAsync(
    sr => sr.SessionId == sessionId && sr.Status == "WAITLISTED", cancellationToken);
```

add `WaitlistCount = waitlistCount,` to the returned `SessionRegistrationSummary`.

- [ ] **Step 6: Update every direct `new SessionRegistrationService(context)` call site**

All existing call sites in `SessionRegistrationServiceTests.cs` need
`new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance)`.

- [ ] **Step 7: Run tests to verify they pass**

`dotnet test backend/BrownEvents.Tests --filter "FullyQualifiedName~SessionRegistrationServiceTests"`

- [ ] **Step 8: Commit**

---

### Task 3: DTO and mapping

**Files:**
- Modify: `backend/BrownEvents.Api/Dtos/SessionDto.cs`
- Modify: `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs`

**Interfaces:**
- Consumes: `SessionRegistrationSummary.WaitlistCount` from Task 2.
- Produces: `SessionDto.WaitlistCount` — consumed by Task 4's controller and Task 5's frontend.

- [ ] **Step 1: Add `WaitlistCount` to `SessionDto`**

`public int WaitlistCount { get; set; }` alongside the existing `RegisteredCount`.

- [ ] **Step 2: Thread it through `ToDto`**

```csharp
public static SessionDto ToDto(this Session session, int registeredCount = 0, int waitlistCount = 0, int? currentAttendeeRegistrationId = null) => new()
{
    // ...existing mappings...
    RegisteredCount = registeredCount,
    WaitlistCount = waitlistCount,
    CurrentAttendeeRegistrationId = currentAttendeeRegistrationId,
};
```

All other call sites (`ConferencesController`, `SessionsController`'s update action) use the
parameterless defaults and are unaffected — additive change only.

- [ ] **Step 3: Commit**

---

### Task 4: `POST /api/sessions/{id}/waitlist` endpoint

**Files:**
- Modify: `backend/BrownEvents.Api/Controllers/SessionsController.cs`

**Interfaces:**
- Consumes: `JoinWaitlistAsync` from Task 2, `SessionDto.WaitlistCount` from Task 3.
- Produces: `POST /api/sessions/{id}/waitlist` — consumed by Task 5's `api.js`.

Test-first: no — this is a thin controller action delegating to Task 2's already-tested service
method, matching the no-controller-HTTP-test convention used for `/register`.

- [ ] **Step 1: Pass `WaitlistCount` through `GetById`**

`return Ok(session.ToDto(summary.RegisteredCount, summary.WaitlistCount, summary.CurrentAttendeeRegistrationId));`

- [ ] **Step 2: Add the `JoinWaitlist` action**

Placed after `Register`, mirroring its structure:

```csharp
[HttpPost("{id}/waitlist")]
[ProducesResponseType(typeof(SessionRegistrationDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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

- [ ] **Step 3: Add the two new `ProblemDetails` helpers**

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

- [ ] **Step 4: Verify build**

`dotnet build backend/BrownEvents.sln -c Release`

- [ ] **Step 5: Commit**

---

### Task 5: Frontend — join-waitlist action and UI

**Files:**
- Modify: `frontend/src/api.js`
- Modify: `frontend/src/hooks/useSessionRegistration.js`
- Modify: `frontend/src/components/SessionRegisterModal.jsx`
- Modify: `frontend/src/pages/SessionDetailPage.jsx`

**Interfaces:**
- Consumes: `POST /api/sessions/{id}/waitlist` from Task 4.

Test-first: no — no frontend test runner exists in this repo; verified manually.

- [ ] **Step 1: Add `joinWaitlist` to `api.js`**

```javascript
export const joinWaitlist = (sessionId, data) =>
  request(`/api/sessions/${sessionId}/waitlist`, { method: 'POST', body: JSON.stringify(data) })
```

- [ ] **Step 2: Add the `joinWaitlist` action to `useSessionRegistration.js`**

```javascript
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

Return it alongside `register`/`cancel`.

- [ ] **Step 3: Make `SessionRegisterModal` mode-aware**

Add optional `title`/`submitLabel`/`submitLoadingLabel` props with defaults matching the current
"Register" copy, so the same modal serves both flows without duplicating markup.

- [ ] **Step 4: Wire the "Join Waitlist" action into `SessionDetailPage`**

- Track a `modalMode` (`'register' | 'waitlist'`) alongside the existing modal-open state.
- `handleSubmit` dispatches by mode: `joinWaitlist(formData)` vs `register(formData)`.
- Replace the static "Session Full" badge with a "Join Waitlist" button when
  `registeredCount >= capacity`.
- Show `· N waitlisted` next to the capacity line when `waitlistCount > 0`.

- [ ] **Step 5: Manually verify in the running app**

Per `docs/plans/EPMCDMPTST-2327-SessionWaitlist.md` §7: create a capacity-1 session, register one
attendee, join the waitlist with a second, confirm `GET` shows `waitlistCount: 1`, cancel the first
registration and confirm the log line and the second attendee's promotion to `CONFIRMED`.

- [ ] **Step 6: Commit**

---

## Negative-constraints check

- No schema/migration change — `SessionRegistration.cs` and `AppDbContext.cs` are untouched.
- No waitlist cap — `JoinWaitlistAsync` has no size check anywhere in Task 2.
- No leave-waitlist endpoint — no new route added for removing a `WAITLISTED` row.
- No notification integration — Task 2's promotion path only logs; no email/push call added.
- No frontend test runner — Task 5 has no test step; no Jest/Vitest/RTL dependency added.
- `/register`'s capacity check is unchanged — Task 2 only adds a regression assertion, no logic
  change to `RegisterAttendeeAsync`'s existing capacity branch.

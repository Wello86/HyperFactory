# Spec: Session Waitlist (EPMCDMPTST-2327)

## Goal

Let an attendee join a FIFO waitlist once a session is at capacity, instead of being turned away
outright. When a confirmed attendee cancels their registration, the earliest-joined waitlisted
attendee is automatically promoted to `"CONFIRMED"`. Both the API and the session detail UI
surface a live waitlist count.

## Approach

Reuse the existing `SessionRegistration.Status` string column with a new value, `"WAITLISTED"`,
alongside the existing `"CONFIRMED"`/`"CANCELLED"` values — no schema change or migration needed.

- `SessionRegistrationService` gains `JoinWaitlistAsync(sessionId, capacity, attendeeData, ct)`,
  mirroring `RegisterAttendeeAsync`'s attendee-resolve/dedup/transaction structure
  (`backend/BrownEvents.Api/Services/SessionRegistrationService.cs`).
- `CancelRegistrationAsync` gains promotion logic: after a successful cancellation, the earliest
  `WAITLISTED` registration (ordered by `RegisteredAt`, tiebreak `Id` ascending) is promoted to
  `CONFIRMED` inside the same guarded transaction, and one `LogInformation` call is emitted.
- `GetSummaryAsync` gains a `WaitlistCount` field.
- `SessionsController` gains `POST /api/sessions/{id}/waitlist`, reusing the existing
  `AttendeeRegistrationRequestDto` request body — a brand-new attendee joining the waitlist
  directly still needs to supply `firstName`/`lastName`/`email`.
- Frontend: `SessionDetailPage` shows a "Join Waitlist" button when the session is full (in place
  of the old static "Session Full" badge) and displays the waitlist count next to capacity.

## Decided semantics

- **Join-waitlist eligibility**: only allowed once `confirmedCount >= capacity`. Attempting to join
  while the session is not yet full returns `SessionNotFull` (409) with no side effects.
- **Dedup**: an attendee who already has a `CONFIRMED` row for the session gets `AlreadyRegistered`;
  one who already has a `WAITLISTED` row gets `AlreadyWaitlisted`. Both are checked before the
  capacity check, mirroring `RegisterAttendeeAsync`'s existing dedup-before-capacity ordering.
- **Promotion order**: FIFO by `RegisteredAt` ascending, tiebreak `Id` ascending. `RegisteredAt`
  alone has only millisecond-ish resolution, so two waitlist joins could theoretically collide —
  the `Id` tiebreak resolves that deterministically.
- **No voluntary leave-waitlist**: there is no endpoint to remove oneself from the waitlist without
  cancelling nothing (a waitlisted attendee has no `CONFIRMED` row to cancel). Not required by any
  acceptance criterion.
- **No waitlist cap**: the waitlist is unbounded.
- **No notification on promotion**: no email/push infrastructure exists in this codebase; promotion
  is only observable via the `LogInformation` call and the next `GET`.

## Data flow

1. Attendee submits the "Join Waitlist" form → `POST /api/sessions/{id}/waitlist` with
   `{ firstName, lastName, email }`.
2. `SessionsController.JoinWaitlist` loads the session (404 if missing), then calls
   `SessionRegistrationService.JoinWaitlistAsync(id, session.Capacity, request, ct)`.
3. `JoinWaitlistAsync` resolves/creates the `Attendee` by email, runs the
   already-registered/already-waitlisted/not-full checks, and — only if all pass — inserts a
   `SessionRegistration` with `Status = "WAITLISTED"`.
4. On cancellation (`DELETE /api/sessions/{id}/registrations/{registrationId}`), after marking the
   cancelled row `"CANCELLED"`, `CancelRegistrationAsync` queries the earliest `WAITLISTED` row for
   the session and promotes it to `"CONFIRMED"`, logging the promotion.
5. `GET /api/sessions/{id}` returns `registeredCount`, `capacity`, and `waitlistCount` via
   `GetSummaryAsync` → `SessionDto`.

## Error handling

- `JoinWaitlist` returns `404` (`ProblemDetails`) if the session doesn't exist.
- `409 Conflict` (`ProblemDetails`) for `AlreadyRegistered`, `AlreadyWaitlisted`, and `SessionNotFull`
  — same shape as the existing `/register` conflict responses.
- No new validation beyond what `AttendeeRegistrationRequestDto` already enforces for `/register`.

## Testing

- Backend only, extending `SessionRegistrationServiceTests.cs`'s existing `[Fact]`-per-scenario,
  no-mocking, real `AppDbContext` InMemory convention:
  - Joining the waitlist when at capacity succeeds.
  - Joining the waitlist when not at capacity returns `SessionNotFull` with **zero** rows inserted
    (neither `SessionRegistration` nor a new orphan `Attendee`).
  - Already-confirmed and already-waitlisted attendees are rejected.
  - Cancelling promotes the earliest waitlisted attendee in FIFO order; a second waitlisted
    attendee stays `WAITLISTED`.
  - Cancelling with no waitlist present does not log and does not error.
  - Promotion logs exactly one `LogInformation` entry containing the attendee ID and session ID.
  - `GetSummaryAsync` includes the correct `WaitlistCount`.
  - The existing full-session `/register` rejection (`SessionFull`) still passes unmodified, and
    still creates no `WAITLISTED` row.
- Frontend has no automated test runner in this repo — verified manually (see
  `docs/plans/EPMCDMPTST-2327-SessionWaitlist.md` §7 for the manual check script).

## Acceptance criteria

1. `POST /api/sessions/{id}/waitlist` adds an attendee to the waitlist when the session is at
   capacity, returning 201.
2. `POST /api/sessions/{id}/waitlist` returns 409 without side effects when the session is not yet
   at capacity.
3. `POST /api/sessions/{id}/register` on a full session still returns 409 and never creates a
   waitlist row (unchanged behavior, re-verified by test).
4. Cancelling a confirmed registration automatically promotes the earliest-by-`RegisteredAt`
   waitlisted attendee to `"CONFIRMED"`.
5. Promotion emits exactly one `LogInformation` entry containing the attendee ID and session ID.
6. `GET /api/sessions/{id}` response includes `registeredCount`, `capacity`, and `waitlistCount`.
7. `SessionDetailPage` renders capacity and waitlist count, and offers a "Join Waitlist" affordance
   when full.
8. Unit tests cover: joining the waitlist, cancel-triggers-promotion, and waitlist FIFO ordering
   with 2+ waitlisted attendees.

## Non-goals

- No maximum waitlist size/cap.
- No voluntary leave-waitlist endpoint.
- No notification (email/push) to the promoted attendee.
- No schema/migration change — the design deliberately reuses the existing `Status` string column.
- No frontend automated test runner.
- No change to `ConferencesController`'s session-list endpoint — it already omits `RegisteredCount`
  by using the parameterless `ToDto()` overload; `WaitlistCount` is likewise omitted there for
  consistency, since no acceptance criterion covers the list view.

## Decisions made on the caller's behalf

- The waitlist route accepts the same `AttendeeRegistrationRequestDto` body as `/register`, rather
  than no body — a brand-new attendee whose first encounter with the session is finding it full
  still needs to supply their name/email to join the waitlist. `JoinWaitlistAsync`'s signature
  mirrors `RegisterAttendeeAsync`'s exactly so the attendee-resolution logic can be reused as-is.
- FIFO tiebreak uses `Id` ascending (not `RegisteredAt` alone), since `RegisteredAt` has only
  millisecond-ish resolution and two joins could otherwise collide.
- `ILogger<SessionRegistrationService>` is constructor-injected — the first per-service logger in
  this codebase. ASP.NET Core's default host wires `ILogger<T>` automatically, so no DI
  registration change was needed beyond the constructor signature.

## Open risks

- Concurrent cancel + join-waitlist races are bounded only by the existing guarded-transaction
  pattern (`Database.IsRelational()` gate), which itself no-ops under the EF InMemory provider used
  in tests. Behavior under real relational-database concurrency is unverified by unit tests — this
  matches the pre-existing risk profile of `RegisterAttendeeAsync`/`CancelRegistrationAsync` and is
  not a new gap introduced here.
- `Status = "WAITLISTED"` is a bare string (no enum/constant), matching the existing
  `"CONFIRMED"`/`"CANCELLED"` convention exactly — a typo anywhere would silently misbehave rather
  than fail to compile.
- See `code-review-deferred.md` for the three non-blocking follow-ups identified during MR review.

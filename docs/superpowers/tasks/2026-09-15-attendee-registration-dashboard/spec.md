# Attendee Registration Dashboard — Design Spec

**Ticket**: EPMCDMPTST-2328 (BEVN-203)
**Status**: Draft — pending review
**Date**: 2026-09-15

## 1. Summary

An attendee can look up all of their conference registrations in one place by entering their
email on a new `/dashboard` page. Each registration shows the conference name, dates, and status.
Confirmed registrations can be cancelled from the dashboard; cancelling refreshes the list.

## 2. Scope

**In scope**: conference-level `Registration` rows only (the entity backing
`POST /api/conferences/{id}/register`).

**Out of scope**: session-level `SessionRegistration` rows. The ticket's phrase "all their
conference registrations" reads literally as conference-level registrations, and the two models
are otherwise unrelated in this codebase (separate entities, separate services, no shared query
today) — aggregating both would mean building a cross-model abstraction that isn't needed for this
scope.

One consequence of this scope decision: the ticket also says "Cancellation triggers the same
waitlist promotion logic as BEVN-202 if that task is complete." Waitlist promotion
(`SessionRegistrationService.CancelRegistrationAsync`, the BEVN-202 logic) only exists for
`SessionRegistration` — conference-level `Registration` has no `WAITLISTED` status, no capacity
check, and no cancel path at all today. Since this dashboard only surfaces conference-level
registrations, that clause does not apply: cancelling a conference-level registration is a plain
status flip to `CANCELLED`, with nothing to promote.

## 3. Decisions

These were resolved during brainstorming and drive the design below:

| Decision | Choice | Why |
|---|---|---|
| Aggregation scope | Conference-level `Registration` only | Matches the literal ticket wording; avoids building a cross-model abstraction for two otherwise-unrelated entities |
| Cancel endpoint location | Nested under conferences: `DELETE /api/conferences/{id}/registrations/{registrationId}` | Matches the existing `SessionsController` nested-resource convention (`api-patterns.md`) exactly |
| Unknown email behavior | `404 Not Found` when the email matches no `Attendee` | Distinguishes "no such attendee" from "attendee with zero registrations" using the app's existing per-entity 404 helper pattern |
| Backend logic placement | Extend `IRegistrationService` / `RegistrationService` | Keeps all conference-`Registration` behavior in one service, mirroring how `ISessionRegistrationService` already owns register+cancel+query for its model; avoids a new service class for logic that is really more `RegistrationService` behavior |

## 4. Architecture

No new subsystem or cross-model abstraction is introduced. The change extends the existing
conference-registration vertical (`RegistrationService` → `ConferencesController`) and adds one
new thin controller (`AttendeesController`) for the email-keyed read, plus the standard frontend
page/hook/route/api additions.

```
Frontend                          Backend
--------                          -------
DashboardPage.jsx                 AttendeesController
  -> useAttendeeDashboard.js        GET /api/attendees/{email}/registrations
       -> api.js                      -> RegistrationService.GetByAttendeeEmailAsync
            -> GET  .../registrations
            -> DELETE .../registrations/{id}
                                   ConferencesController
                                     DELETE /api/conferences/{id}/registrations/{registrationId}
                                       -> RegistrationService.CancelRegistrationAsync
```

## 5. Backend Components

### 5.1 `IRegistrationService` / `RegistrationService`

Add two methods:

```csharp
Task<IReadOnlyList<Registration>> GetByAttendeeEmailAsync(string email, CancellationToken cancellationToken = default);
Task<RegistrationCancellationResult> CancelRegistrationAsync(int conferenceId, int registrationId, CancellationToken cancellationToken = default);
```

- `GetByAttendeeEmailAsync` — case-insensitive match on `Attendee.Email` (same normalization as
  `SessionRegistrationService.GetSummaryAsync`: `.Trim()` the input, compare via `.ToLower()`),
  `.Include(r => r.Conference)` to satisfy the DTO's conference name/dates, ordered by
  `RegisteredAt` descending (most recent first — no explicit ordering requirement in the ticket, so
  this is a reasonable default consistent with "dashboard" framing). Returns an empty list when the
  attendee exists but has no registrations; the caller (`AttendeesController`) is responsible for
  the attendee-existence check that produces a 404.
- `CancelRegistrationAsync` — mirrors `SessionRegistrationService.CancelRegistrationAsync`'s
  shape: look up the `Registration` by `conferenceId` + `registrationId` where
  `Status == "CONFIRMED"`; if not found, return `RegistrationCancellationOutcome.NotFound`;
  otherwise set `Status = "CANCELLED"`, save, and return `Success`. No waitlist step (see §2).

New result types in `IRegistrationService.cs`, following `ISessionRegistrationService.cs`'s
existing pattern:

```csharp
public enum RegistrationCancellationOutcome { Success, NotFound }

public class RegistrationCancellationResult
{
    public RegistrationCancellationOutcome Outcome { get; init; }
    public static RegistrationCancellationResult Success() => new() { Outcome = RegistrationCancellationOutcome.Success };
    public static RegistrationCancellationResult Failure(RegistrationCancellationOutcome outcome) => new() { Outcome = outcome };
}
```

### 5.2 `AttendeesController` (new)

```
GET /api/attendees/{email}/registrations
```

- Looks up the `Attendee` by case-insensitive email match. No match → 404 via a local
  `AttendeeNotFound(email)` `ProblemDetails` helper, following the existing per-controller
  404-helper convention (e.g. `ConferencesController.ConferenceNotFound`).
- Match found → calls `RegistrationService.GetByAttendeeEmailAsync`, maps each `Registration` to
  the extended `RegistrationDto` (§5.4), returns 200 with the list (possibly empty).

### 5.3 `ConferencesController` — new endpoint

```
DELETE /api/conferences/{id}/registrations/{registrationId}
```

- Calls `RegistrationService.CancelRegistrationAsync(id, registrationId, cancellationToken)`.
- Switches on the outcome: `Success` → 204 No Content; `NotFound` → 404 via a local
  `RegistrationNotFound` helper. Same shape as `SessionsController.CancelRegistration`.

### 5.4 DTO changes

Extend `RegistrationDto` (`Dtos/RegistrationDto.cs` + its mapping in
`EntityDtoExtensions.ToDto()`) to include the conference's name and start/end dates alongside the
existing fields. This requires the new query's `.Include(r => r.Conference)` from §5.1 — no schema
or migration changes (`EnsureCreated()`-only database, per `database-patterns.md`).

## 6. Frontend Components

- **`pages/DashboardPage.jsx`** (new) — email input (prefilled from
  `getStoredAttendeeEmail()` if set), submit triggers `load(email)`. Renders:
  - a list row per registration: conference name, start/end dates, status;
  - a "Cancel" button on `CONFIRMED` rows only; `CANCELLED` rows render read-only;
  - an empty state (no rows) when the list is empty **or** the lookup 404s (see §7).
- **`hooks/useAttendeeDashboard.js`** (new) — `load(email)` and `cancel(conferenceId,
  registrationId)`, where `cancel` calls the DELETE endpoint then re-invokes `load` to refresh the
  list (mirrors the load/mutate/refetch shape already used by `useSessionRegistration.js`). On a
  successful `load`, stores the email via `setStoredAttendeeEmail` (same "remember last-used email"
  behavior `useSessionRegistration.js` already has).
- **`api.js`** — add `getAttendeeRegistrations(email)` (`GET
  /api/attendees/{email}/registrations`). Cancel reuses the pre-existing `deleteRegistration(conferenceId,
  registrationId)` export, which already targets `DELETE /api/conferences/{id}/registrations/{registrationId}`.
- **`App.jsx`** — register the `/dashboard` route.

## 7. Error Handling

- **Unknown email vs. zero registrations**: the backend distinguishes these (404 vs. 200 + empty
  array, per §3), but the frontend does not need to branch on which one it got — both cases render
  the same empty-state UI, satisfying the ticket's "empty state shown when no registrations found
  for the email" without extra UI logic.
- **Cancel failure**: a `DELETE` against an already-cancelled or nonexistent registration returns
  404; the hook surfaces a small inline error message rather than crashing the page. The list is
  not optimistically mutated — the UI reflects only what a successful reload confirms.
- **No new auth/validation surface**: this endpoint is unauthenticated, consistent with the rest of
  the app's no-auth baseline (`security-practices.md`). It intentionally returns attendee PII
  (name, email is the lookup key) the same way existing attendee-scoped endpoints do (per
  `AttendeePiiExposureTests.cs`), which is not a new exposure — it matches established, tested
  posture.

## 8. Testing

- **`RegistrationServiceTests.cs`** (extend): `GetByAttendeeEmailAsync` — found with one
  registration, found with multiple across different conferences, case-insensitive email match,
  no match returns empty list. `CancelRegistrationAsync` — success (status flips to `CANCELLED`),
  not-found (wrong id / wrong conference / already-cancelled).
- **New `AttendeesControllerTests.cs`** (`WebApplicationFactory`-based, following
  `AttendeePiiExposureTests.cs`'s style): 404 for unknown email, 200 + empty array for a known
  attendee with no registrations, 200 with correct conference name/dates/status for a known
  attendee with registrations.
- **`ConferencesController`** cancel endpoint: HTTP-level success (204) and not-found (404) cases,
  added alongside existing `ConferencesController`-adjacent tests.
- **Frontend**: no automated tests — no frontend test runner exists in this repository today (a
  project-wide gap, not specific to this feature).

## 9. Non-Goals

- Aggregating `SessionRegistration` rows into this dashboard (see §2).
- Any waitlist/capacity behavior for conference-level registrations (none exists today; out of
  scope per §2).
- Pagination on the attendee-registrations list (the ticket doesn't call for it, and per
  `api-patterns.md` list endpoints aren't assumed to paginate by default).
- Authentication/authorization on the new endpoints (matches the app's current no-auth baseline).

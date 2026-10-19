# Technical Research

**Task**: attendee registration dashboard
**Generated**: 2026-09-15T00:00:00Z
**Research path**: codegraph

---

## 1. Original Context

EPMCDMPTST-2328 — BEVN-203 Attendee Registration Dashboard.

Goal: An attendee can view all their conference registrations in one place.
Context: The dashboard shows each registration with conference name, dates, status, and a cancel button. Cancellation triggers the same waitlist promotion logic as BEVN-202 if that task is complete.

Acceptance Criteria:
- GET /api/attendees/{email}/registrations returns all registrations with conference context
- Dashboard page accessible at /dashboard with an email input to look up registrations
- Each registration shows conference name, dates, status
- Confirmed registrations have a cancel button; cancelled ones are read-only
- Cancellation refreshes the list
- Empty state shown when no registrations found for the email

---

## 2. Codebase Findings

### Existing Implementations

Backend — two independent registration models exist, with no shared abstraction between them:

- `backend/BrownEvents.Api/Models/Registration.cs` — conference-level registration join entity (`Id`, `RegisteredAt`, `Status`, `ConferenceId`, `AttendeeId`, `Conference` nav, `Attendee` nav).
- `backend/BrownEvents.Api/Models/SessionRegistration.cs` — session-level registration join entity (`Id`, `RegisteredAt`, `Status`, `SessionId`, `AttendeeId`, `Session` nav, `Attendee` nav). `Session` in turn has `ConferenceId`/`Conference` nav (`backend/BrownEvents.Api/Models/Session.cs`).
- `backend/BrownEvents.Api/Models/Attendee.cs` — `Id`, `FirstName`, `LastName`, `Email` (`[EmailAddress]`, `[Required]`), `Registrations` collection, `SessionRegistrations` collection.
- `backend/BrownEvents.Api/Services/IRegistrationService.cs` / `RegistrationService.cs` — only `RegisterAttendeeAsync(conferenceId, attendeeData)`. No cancel, no waitlist, no lookup-by-email method exists on this service.
- `backend/BrownEvents.Api/Services/ISessionRegistrationService.cs` / `SessionRegistrationService.cs` — `RegisterAttendeeAsync`, `JoinWaitlistAsync`, `CancelRegistrationAsync`, `GetSummaryAsync`. `GetSummaryAsync(sessionId, email)` already does a case-insensitive email lookup (`sr.Attendee.Email.ToLower() == normalizedEmail.ToLower()`) but scoped to one session, not across an attendee's full history.
- `backend/BrownEvents.Api/Services/SessionRegistrationService.cs:149-188` `CancelRegistrationAsync` — sets the cancelled registration's `Status = "CANCELLED"`, then queries the earliest `WAITLISTED` registration for the same session (`OrderBy(RegisteredAt).ThenBy(Id)`) and promotes it to `"CONFIRMED"`, logging via `ILogger<T>.LogInformation`. This is the waitlist-promotion logic the ticket refers to as "BEVN-202" — it is present and covered by tests (see Testing Landscape). It only exists for `SessionRegistration`; `Registration` (conference-level) has no equivalent cancel/promotion path at all.
- `backend/BrownEvents.Api/Controllers/ConferencesController.cs:151-157` `GetRegistrations(id)` — returns all `Registration`s for one conference, not scoped by attendee.
- `backend/BrownEvents.Api/Controllers/SessionsController.cs:136-151` `CancelRegistration(id, registrationId)` — `DELETE api/sessions/{id}/registrations/{registrationId}`, calls `ISessionRegistrationService.CancelRegistrationAsync`.
- No `AttendeesController` exists anywhere in `backend/BrownEvents.Api/Controllers/`. No endpoint keyed by attendee email exists at all — every current lookup is keyed by conference id or session id, with email used only as an optional filter (`SessionsController.GetById(id, [FromQuery] string? email)`).
- `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs` — one `ToDto()` extension per entity (`RegistrationDto`, `SessionRegistrationDto`, `ConferenceDto`, `SessionDto`, etc.). Neither `RegistrationDto` nor `SessionRegistrationDto` currently carries conference name/dates — `RegistrationDto` embeds `Attendee` but not `Conference`; `SessionRegistrationDto` embeds `Attendee` but not `Session`/`Conference`.

Frontend:

- `frontend/src/App.jsx` — route table; no `/dashboard` route exists. Existing routes: `/`, `/conferences/new`, `/conferences/:id`, `/conferences/:id/edit`, `/conferences/:id/sessions/:sessionId`, `/conferences/:id/register`, `/speakers`, `/speakers/:id`.
- `frontend/src/api.js` — thin `fetch` wrapper (`request()`), one named export per endpoint. No attendee-scoped export exists.
- `frontend/src/hooks/useSessionRegistration.js` — existing hook pattern for session-scoped load/register/joinWaitlist/cancel, with `load()` re-fetching after a mutating action — the closest existing precedent for "cancellation refreshes the list."
- `frontend/src/utils/attendeeIdentity.js` — `getStoredAttendeeEmail`/`setStoredAttendeeEmail` wrap `localStorage` under key `brownevents.attendeeEmail`, already used by `useSessionRegistration.js` to remember the last-used email after a successful register/waitlist action.
- No existing frontend page named `Dashboard*`; `codegraph_explore("dashboard")` returned no results.

### Architecture and Layers Affected

Per `.ai-run/guides/architecture/architecture.md`, the stack is a thin-controller/fat-service backend with no repository layer, plus a React SPA where `pages/` → `hooks/` → `api.js`, never `fetch` directly from a page. Layers this task touches:

- **Backend Controllers** (`backend/BrownEvents.Api/Controllers/`) — needs a new attendee-scoped read endpoint and a way to trigger the existing cancel path.
- **Backend Services** (`backend/BrownEvents.Api/Services/`) — needs a query that aggregates an attendee's registrations by email; existing services are split per registration type (`RegistrationService` for conference-level, `SessionRegistrationService` for session-level) with no combined query today.
- **Backend Dtos** (`backend/BrownEvents.Api/Dtos/`) — existing `RegistrationDto`/`SessionRegistrationDto` do not surface conference name/dates.
- **Frontend pages/hooks/api** — new page, new hook, new `api.js` export, new route registration.

### Integration Points

- Frontend `api.js` → backend `api/...` routes, same-origin/dev-proxy (`BASE_URL = ''`).
- `SessionRegistration` → `Session` → `Conference` (two-hop nav) vs. `Registration` → `Conference` (one-hop nav) — any attendee-registrations query that spans both registration types has to eager-load two different nav paths to reach "conference name, dates."
- Waitlist promotion integration: cancelling a `SessionRegistration` already cascades into promoting the next `WAITLISTED` row for that session — any new cancel entry point that reuses `CancelRegistrationAsync` inherits this for free; `Registration` (conference-level) has no such cascade to inherit.

### Patterns and Conventions

- Outcome-enum + `switch` in the controller for multi-result writes (`SessionRegistrationOutcome`, `SessionCancellationOutcome`, `WaitlistJoinOutcome` in `ISessionRegistrationService.cs`) rather than exceptions.
- `Database.IsRelational() ? BeginTransactionAsync() : null` guard around any multi-`SaveChangesAsync()` write (`database-patterns.md`), used in `RegistrationService`, `SessionRegistrationService`, `DataSeeder`.
- Every list-returning service method `.Include()`s/`.ThenInclude()`s the nav properties its DTO mapping reads, to avoid N+1 against `UseLazyLoadingProxies()`.
- Responses always map through `EntityDtoExtensions.ToDto()`; request bodies are raw domain models except attendee registration, which uses a dedicated `*RequestDto` — `api-patterns.md` explicitly recommends the request-DTO style for any *new* registration-style endpoint.
- Local `private static ProblemDetails {X}NotFound(...)` helpers per controller for domain 404s, not custom exception types.
- Frontend: one hook per page/concern in `hooks/`, page components never call `fetch` directly.

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/architecture/architecture.md` — layering, component structure, "Adding New Features" checklist (entity → DbSet → service → DI → controller → DTO mapping → Swagger example).
- `.ai-run/guides/api/api-patterns.md` — route conventions, nested-resource-on-parent-controller convention, request-DTO asymmetry note, outcome-enum pattern, pagination envelope (only two endpoints paginate; list endpoints are not assumed to paginate by default).
- `.ai-run/guides/data/database-patterns.md` — transaction guard pattern, eager-loading rule, write-ordering rule (validate before persisting a new entity), no-migrations caveat (`EnsureCreated()` only).
- `.ai-run/guides/testing/testing-patterns.md` — xUnit + EF InMemory conventions, no mocking, `WebApplicationFactory` for HTTP-level tests.
- `.ai-run/guides/security/security-practices.md` — no auth/authorization anywhere; email-based lookup is therefore unauthenticated by design in this codebase, consistent with every other endpoint.

### Architectural Decisions

- No ADR files found; conventions live entirely in the `.ai-run/guides/` documents above, which are treated as current and authoritative (the security guide explicitly calls out one place where an older doc/CLAUDE.md was stale and instructs trusting the guide over it).

### Derived Conventions

- The email-matching convention used in `SessionRegistrationService.GetSummaryAsync` (`Attendee.Email.ToLower() == normalizedEmail.ToLower()`, with `.Trim()` on the input) is the established pattern for case-insensitive email lookups and should be treated as the precedent for any new email-keyed query.

---

## 4. Testing Landscape

### Existing Coverage

- `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs` — covers `RegisterAttendeeAsync`, `JoinWaitlistAsync`, `CancelRegistrationAsync` (including `CancelRegistrationAsync_PromotesEarliestWaitlistedAttendee_InFifoOrder` and `CancelRegistrationAsync_LogsInformation_WhenPromotingWaitlistedAttendee` — the BEVN-202 waitlist-promotion behavior is under test), and `GetSummaryAsync` (including case-insensitive email matching).
- `backend/BrownEvents.Tests/RegistrationServiceTests.cs` — covers `RegisterAttendeeAsync` only (create/reuse-by-email/cancellation-token-throws); no cancel or query-by-email coverage for conference-level registrations.
- `backend/BrownEvents.Tests/AttendeePiiExposureTests.cs` — `WebApplicationFactory`-based; asserts attendee PII (first/last name, email) does *not* leak on public conference/session endpoints but *does* appear on attendee-scoped endpoints (`GET /api/conferences/{id}/registrations`, register responses). Directly relevant precedent for a new attendee-email-keyed endpoint: it will need to intentionally return PII (email is the lookup key), and any test suite addition should follow this file's marker-constant pattern to assert the right data appears/doesn't appear.
- No test file references "dashboard" or an attendee-email-keyed registrations endpoint — `codegraph_explore("test attendee registration dashboard")` surfaced only the two service test files above.

### Testing Framework and Patterns

xUnit 2.9.3, EF Core InMemory provider, one `CreateContext()` helper per test file with a fresh `Guid.NewGuid()` database name, `Arrange/Act/Assert` structure, `[Fact]` only (no `[Theory]` in the suite), `WebApplicationFactory<Program>` for HTTP-round-trip tests. No frontend test runner exists in this repository.

### Coverage Gaps

- No test exercises a query spanning both `Registration` and `SessionRegistration` for one attendee.
- No test exists for cancelling a conference-level `Registration` (the service method doesn't exist).
- No frontend tests exist at all (project-wide gap, not specific to this task).
- No existing route/page named `dashboard` to regress against.

---

## 5. Configuration and Environment

### Environment Variables

None specific to this feature area. The only backend secret is `ConnectionStrings:DefaultConnection` (PostgreSQL), unrelated to attendee/registration logic.

### Configuration Files

- `appsettings.json` — DB connection string, `Cors:AllowedOrigins` (currently `["http://localhost:5173"]`), unrelated to this feature beyond being the standard app config.
- No feature-specific config file found for registrations or dashboards.

### Feature Flags and Deployment Concerns

None found — no feature-flag mechanism exists anywhere in the codebase, and no deployment manifest references attendee/registration/dashboard behavior specifically.

---

## 6. Risk Indicators

- **Two disjoint registration models, one requested endpoint.** `Registration` (conference-level) and `SessionRegistration` (session-level) are separate entities with separate services and no shared query surface. The ticket's "all their conference registrations" is ambiguous as to whether it means conference-level `Registration` rows, session-level `SessionRegistration` rows (whose parent session belongs to a conference), or both combined — this is a specification gap, not something this research resolves.
- **Waitlist promotion only exists on one of the two models.** `CancelRegistrationAsync`'s FIFO promotion (the "BEVN-202 logic" the ticket references) lives entirely in `SessionRegistrationService` and has no equivalent in `RegistrationService`. Speculative: if the dashboard's cancel button must apply to conference-level `Registration` rows too, that model has no cancel method at all today and none of its current test suite exercises one — this would be new behavior, not an extension of tested code.
- **No attendee-by-email query precedent at the "all registrations" scope.** The only existing email-keyed lookup (`GetSummaryAsync`) is scoped to a single session and returns a count/id, not a hydrated list with conference context — a new query pattern, though the case-insensitive matching convention is established.
- **N+1 / eager-loading risk on a new list query.** Per `database-patterns.md`, any new list-returning method must `.Include()` the nav chain its DTO needs; a query spanning `SessionRegistration → Session → Conference` needs a two-hop `.ThenInclude()`, distinct from the one-hop `Registration → Conference` chain already used in `ConferenceService.GetRegistrationsAsync`.
- **No `AttendeesController` and no `/dashboard` route exist** — both are net-new surfaces with no local precedent to diff against, only analogous patterns (`SessionsController`'s outcome-enum style, `useSessionRegistration.js`'s load/mutate/refetch hook style).
- **No authentication anywhere in the app** — an email-keyed "look up anyone's registrations" endpoint is consistent with the app's current no-auth baseline (per `security-practices.md`) but is also, by that same baseline, unauthenticated PII exposure by design; `AttendeePiiExposureTests.cs` establishes that attendee-scoped endpoints are expected to expose PII, so this is consistent with existing posture rather than a new hole.
- **No coverage gap specific to this task beyond what's listed above** — all five codegraph research dimensions returned results; the only empty result was the literal keyword `"dashboard"`, which is expected since no such page/route exists yet.

---

## 7. Summary for Complexity Assessment

This task spans all four backend layers (Controllers, Services, Dtos, and implicitly the two existing Models/Registration entities) plus the full frontend stack (a new page, a new hook, a new `api.js` export, and a new route in `App.jsx`). The backend has two independent, non-unified registration models — conference-level `Registration` and session-level `SessionRegistration` — each with its own service and no existing query that spans both by attendee email; the requested `GET /api/attendees/{email}/registrations` endpoint has no direct precedent to extend, only analogous patterns (`GetSummaryAsync`'s case-insensitive email match, `ConferenceService.GetRegistrationsAsync`'s eager-loading style, the outcome-enum controller pattern). The cancel/waitlist-promotion logic referenced as "BEVN-202" is real, already implemented, and covered by tests — but only for `SessionRegistration`; `RegistrationService` has no cancel method at all today.

Test coverage posture is solid for the piece that already exists (session registration, cancellation, waitlist promotion, and PII-exposure expectations at the HTTP level) but has zero coverage for anything resembling a cross-model, email-keyed aggregate query, and zero coverage for cancelling a conference-level `Registration`. The frontend has strong structural precedent (hook-per-concern, page-never-calls-fetch-directly, load/mutate/refetch pattern, stored-email utility already used for exactly this kind of "remember the attendee's email" flow) but no dashboard-shaped page, no empty-state precedent to copy verbatim, and no `/dashboard` route.

The primary complexity driver is the ambiguity in which registration model(s) the new endpoint must aggregate, compounded by the asymmetric maturity of the two services (one has cancel + waitlist promotion + tests, the other has neither). That ambiguity, plus the two-hop vs. one-hop eager-loading difference between the two models, is the main technical-novelty and file-change-surface risk for this task; the no-auth/PII-exposure posture is not a novel risk since it matches the app's established, tested baseline.

---

## 8. External References

None named by the task. The task_context is a ticket description (EPMCDMPTST-2328 / BEVN-203) with no file paths or external URLs cited as sources of truth.

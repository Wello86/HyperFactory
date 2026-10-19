# Implementation Plan: Attendee Registration Dashboard

## 1. Context, Objective & Acceptance Criteria
- **Problem Summary:** Attendees have no single place to see all their conference registrations or cancel one. The design spec (`docs/superpowers/specs/2026-09-15-attendee-registration-dashboard-design.md`) scopes this to conference-level `Registration` rows only (not session-level `SessionRegistration`), resolving the ticket's ambiguity.
- **Goal:** An attendee enters their email on a new `/dashboard` page and sees every conference they're registered for (name, dates, status), with a working Cancel action on confirmed rows.
- **Acceptance Criteria:**
  1. `GET /api/attendees/{email}/registrations` returns 404 when no `Attendee` matches the email (case-insensitive); returns 200 with a (possibly empty) list of registrations, each including conference title/start/end date, when a match exists.
  2. `DELETE /api/conferences/{id}/registrations/{registrationId}` flips a `CONFIRMED` registration to `CANCELLED` (204) or returns 404 if the registration/conference doesn't match or is already cancelled.
  3. `/dashboard` page: email input (prefilled from stored attendee email), submit loads registrations, renders conference name/dates/status per row.
  4. `CONFIRMED` rows show a Cancel button; `CANCELLED` rows are read-only.
  5. Cancelling refreshes the list from the server (no optimistic mutation).
  6. Empty state renders when the list is empty or the lookup 404s.

## 2. TDD Strategy
- **TDD Applies:** Yes, for all backend service/controller changes — existing xUnit + EF InMemory infra and clear expected outcomes support red→green.
  Frontend page/hook: No — this repo has no frontend test runner (confirmed project-wide gap); verified manually instead.
- **Initial Test Cases (write first, confirm they fail, then implement):**
  - `RegistrationServiceTests.cs`:
    - `GetByAttendeeEmailAsync_ReturnsRegistration_WhenAttendeeHasOne`
    - `GetByAttendeeEmailAsync_ReturnsAllRegistrations_AcrossMultipleConferences`
    - `GetByAttendeeEmailAsync_MatchesEmailCaseInsensitively`
    - `GetByAttendeeEmailAsync_ReturnsEmptyList_WhenAttendeeHasNoRegistrations`
    - `AttendeeExistsAsync_ReturnsTrue_WhenEmailMatchesCaseInsensitively`
    - `AttendeeExistsAsync_ReturnsFalse_WhenNoAttendeeMatches`
    - `CancelRegistrationAsync_SetsStatusToCancelled_OnSuccess`
    - `CancelRegistrationAsync_ReturnsNotFound_WhenRegistrationIdDoesNotExist`
    - `CancelRegistrationAsync_ReturnsNotFound_WhenConferenceIdDoesNotMatch`
    - `CancelRegistrationAsync_ReturnsNotFound_WhenAlreadyCancelled`
  - `AttendeesControllerTests.cs` (new):
    - `GetRegistrations_Returns404_ForUnknownEmail`
    - `GetRegistrations_Returns200WithEmptyArray_ForKnownAttendeeWithNoRegistrations`
    - `GetRegistrations_Returns200WithConferenceContext_ForKnownAttendeeWithRegistrations`
  - `ConferencesControllerTests` (existing or new file — see §3):
    - `CancelRegistration_Returns204_OnSuccess`
    - `CancelRegistration_Returns404_WhenRegistrationNotFound`

## 3. Affected Files
- `backend/BrownEvents.Api/Services/IRegistrationService.cs`: Modify
- `backend/BrownEvents.Api/Services/RegistrationService.cs`: Modify
- `backend/BrownEvents.Api/Controllers/AttendeesController.cs`: Create
- `backend/BrownEvents.Api/Controllers/ConferencesController.cs`: Modify
- `backend/BrownEvents.Api/Dtos/RegistrationDto.cs`: Modify
- `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs`: Modify
- `backend/BrownEvents.Tests/RegistrationServiceTests.cs`: Modify
- `backend/BrownEvents.Tests/AttendeesControllerTests.cs`: Create
- `backend/BrownEvents.Tests/ConferencesControllerTests.cs`: Create (no existing controller-level test file for `ConferencesController` cancel path — check at implementation time whether an existing file already covers `ConferencesController` HTTP tests and extend that instead if found)
- `frontend/src/api.js`: Modify
- `frontend/src/hooks/useAttendeeDashboard.js`: Create
- `frontend/src/pages/DashboardPage.jsx`: Create
- `frontend/src/App.jsx`: Modify

## 4. Proposed Changes (Ordered Steps)

### Step 1: `backend/BrownEvents.Api/Services/IRegistrationService.cs`
- **Target Function/Module:** Interface + new result types
- **Changes:**
  - Add `Task<IReadOnlyList<Registration>> GetByAttendeeEmailAsync(string email, CancellationToken cancellationToken = default);`
  - Add `Task<bool> AttendeeExistsAsync(string email, CancellationToken cancellationToken = default);` — resolves how `AttendeesController` distinguishes "no such attendee" (404) from "attendee exists, zero registrations" (200 + `[]`), since `GetByAttendeeEmailAsync` alone (a query rooted on `Registration`) can't tell those two cases apart.
  - Add `Task<RegistrationCancellationResult> CancelRegistrationAsync(int conferenceId, int registrationId, CancellationToken cancellationToken = default);`
  - Add `public enum RegistrationCancellationOutcome { Success, NotFound }`
  - Add `RegistrationCancellationResult` class mirroring `SessionCancellationResult` in `ISessionRegistrationService.cs` (`Outcome` init property, static `Success()`/`Failure(outcome)` factories).

### Step 2: `backend/BrownEvents.Api/Services/RegistrationService.cs`
- **Target Function/Module:** New method implementations
- **Changes:**
  - `GetByAttendeeEmailAsync(email, ct)`: normalize `email.Trim()`; query `_context.Registrations.Include(r => r.Conference).Where(r => r.Attendee.Email.ToLower() == normalizedEmail.ToLower()).OrderByDescending(r => r.RegisteredAt)`, `.ToListAsync(ct)`, cast to `IReadOnlyList<Registration>`. Mirrors `SessionRegistrationService.GetSummaryAsync`'s case-insensitive match convention.
  - `AttendeeExistsAsync(email, ct)`: normalize the same way; `_context.Attendees.AnyAsync(a => a.Email.ToLower() == normalizedEmail.ToLower(), ct)`.
  - `CancelRegistrationAsync(conferenceId, registrationId, ct)`: query `_context.Registrations.FirstOrDefaultAsync(r => r.Id == registrationId && r.ConferenceId == conferenceId && r.Status == "CONFIRMED", ct)`; if null, return `RegistrationCancellationResult.Failure(RegistrationCancellationOutcome.NotFound)`; else set `Status = "CANCELLED"`, wrap the save in the existing transaction guard (`_context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(ct) : null`), `SaveChangesAsync(ct)`, commit if a transaction was opened, return `RegistrationCancellationResult.Success()`. No waitlist-promotion step (per design spec §2/§5.1 — conference-level `Registration` has no `WAITLISTED` status).

### Step 3: `backend/BrownEvents.Api/Dtos/RegistrationDto.cs`
- **Target Function/Module:** DTO shape
- **Changes:** Add `ConferenceTitle` (string), `ConferenceStartDate` (DateTime), `ConferenceEndDate` (DateTime) properties, named to match `ConferenceDto`'s existing `Title`/`StartDate`/`EndDate`.

### Step 4: `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs`
- **Target Function/Module:** `ToDto(this Registration registration)` (currently lines 85-93)
- **Changes:** Populate the three new fields from `registration.Conference.Title` / `.StartDate` / `.EndDate`. Requires the caller to have eager-loaded `Conference` (guaranteed by Step 2's `.Include`).

### Step 5: `backend/BrownEvents.Api/Controllers/AttendeesController.cs` (new)
- **Target Function/Module:** New thin controller
- **Changes:**
  - `[ApiController] [Route("api/attendees")]`, constructor injects `IRegistrationService`.
  - `[HttpGet("{email}/registrations")] GetRegistrations(string email, CancellationToken ct)`:
    - `if (!await _registrationService.AttendeeExistsAsync(email, ct)) return AttendeeNotFound(email);`
    - `var registrations = await _registrationService.GetByAttendeeEmailAsync(email, ct);`
    - `return Ok(registrations.Select(r => r.ToDto()).ToList());`
  - Add local `private static ProblemDetails AttendeeNotFound(string email) => new() { Title = "Attendee not found", Detail = $"No attendee found with email '{email}'.", Status = StatusCodes.Status404NotFound };` mirroring `ConferencesController.ConferenceNotFound`'s shape, wrapped in `NotFound(...)` at the call site (match existing helper's exact return convention once re-checked against `ConferenceNotFound`).

### Step 6: `backend/BrownEvents.Api/Controllers/ConferencesController.cs`
- **Target Function/Module:** New endpoint alongside existing `GetRegistrations`/`Register`
- **Changes:**
  - Add `[HttpDelete("{id}/registrations/{registrationId}")] CancelRegistration(int id, int registrationId, CancellationToken ct)`:
    - Confirm the conference exists (reuse existing conference-lookup/404 pattern already in this controller) — if not, return `ConferenceNotFound(id)`.
    - `var result = await _registrationService.CancelRegistrationAsync(id, registrationId, ct);`
    - `switch (result.Outcome) { case RegistrationCancellationOutcome.NotFound: return RegistrationNotFound(...); default: return NoContent(); }`
  - Add local `private static ProblemDetails RegistrationNotFound(int registrationId)` 404 helper, mirroring `SessionsController.CancelRegistration`'s equivalent helper.

### Step 7: `backend/BrownEvents.Tests/RegistrationServiceTests.cs`
- **Target Function/Module:** New `[Fact]` methods using existing `CreateContext()`/`CreateConference()` helpers
- **Changes:** Add the 10 test cases listed in §2. Use `Guid.NewGuid()` emails/attendees per test to avoid cross-test collisions, consistent with existing tests in this file.

### Step 8: `backend/BrownEvents.Tests/AttendeesControllerTests.cs` (new)
- **Target Function/Module:** `WebApplicationFactory<Program>`-based HTTP tests
- **Changes:** Mirror `AttendeePiiExposureTests.cs`'s `CreateFactory()` (fresh InMemory DB per test) and HTTP-seeding helper pattern. Implement the 3 test cases from §2: 404 unknown email; 200 + `[]` for a seeded attendee with no registrations (seed the attendee via the existing register flow then... note: there is no direct "create attendee without registration" endpoint today — seed by registering to one conference then cancelling it via the new DELETE endpoint, OR seed an attendee directly through `AppDbContext` in the test if the factory setup allows swapping in a pre-seeded context, matching whatever seeding mechanism `AttendeePiiExposureTests.cs` already uses); 200 with correct conference title/dates/status for a seeded attendee with an active registration.

### Step 9: `backend/BrownEvents.Tests/ConferencesControllerTests.cs` (new, unless an existing file is found to extend)
- **Target Function/Module:** HTTP-level tests for the new DELETE endpoint
- **Changes:** `WebApplicationFactory`-based; 204 on successful cancel of a seeded CONFIRMED registration; 404 when the registration id doesn't exist under that conference.

### Step 10: `frontend/src/api.js`
- **Target Function/Module:** New export only (existing `deleteRegistration(conferenceId, registrationId)` at line ~51-52 is reused as-is for cancel — it already targets the exact route this feature needs and has no current callers)
- **Changes:** Add `export const getAttendeeRegistrations = (email) => request(`/api/attendees/${encodeURIComponent(email)}/registrations`);` following the existing `request()` wrapper convention.

### Step 11: `frontend/src/hooks/useAttendeeDashboard.js` (new)
- **Target Function/Module:** New hook, mirroring `useSessionRegistration.js`'s load/mutate/refetch shape
- **Changes:**
  - State: `registrations` (array), `loading`, `error`.
  - `load(email)`: calls `getAttendeeRegistrations(email)`; on success sets `registrations` and calls `setStoredAttendeeEmail(email)` (from `frontend/src/utils/attendeeIdentity.js`); on a 404-shaped error, set `registrations = []` rather than surfacing an error (per design spec §7 — 404 and empty-list both render the same empty state); on any other error, set `error`.
  - `cancel(conferenceId, registrationId, email)`: calls `deleteRegistration(conferenceId, registrationId)`, then re-invokes `load(email)` to refresh; on failure sets an inline `error` without mutating `registrations` optimistically.

### Step 12: `frontend/src/pages/DashboardPage.jsx` (new)
- **Target Function/Module:** New page component
- **Changes:** Email `<input>` prefilled via `getStoredAttendeeEmail()` on mount; submit calls `load(email)` from `useAttendeeDashboard`. Renders one row per registration: conference title, formatted start/end dates, status badge (reuse `badge badge--upcoming`-style classes from `RegistrationsSection.jsx` for visual consistency). `CONFIRMED` rows get a Cancel button wired to `cancel(conferenceId, registrationId, email)`; `CANCELLED` rows render without a button. Empty state block shown when `registrations.length === 0` (covers both zero-registrations and 404 cases per the hook's normalization in Step 11).

### Step 13: `frontend/src/App.jsx`
- **Target Function/Module:** Route table
- **Changes:** Import `DashboardPage` and add `<Route path="/dashboard" element={<DashboardPage />} />` alongside the existing routes.

## 5. Scope Boundaries & Assumptions
- **Out of Scope:** Aggregating `SessionRegistration` rows; any waitlist/capacity behavior for conference-level `Registration` (BEVN-202's promotion logic stays scoped to `SessionRegistration`); pagination on the attendee-registrations list; authentication/authorization on any new endpoint (matches this app's no-auth baseline).
- **Open Questions & Assumptions:**
  - Assumes `AppDbContext` exposes a `DbSet<Attendee> Attendees` (standard naming, not yet re-verified at plan time — confirm during Step 2 implementation).
  - Assumes no existing `ConferencesController`-focused HTTP test file exists to extend for Step 9; if one is found at implementation time, extend it instead of creating a new file.
  - `AttendeesControllerTests.cs`'s "known attendee, zero registrations" seed case (Step 8) has no direct seeding endpoint today; implementation will pick the least-invasive seeding approach available in the existing `WebApplicationFactory` test setup (register-then-cancel, or direct context seeding) — exact mechanism decided during implementation, not before.

## 6. Edge Cases & Risk Analysis
- **Potential Risks:**
  - N+1 / missing eager-load: `GetByAttendeeEmailAsync` must `.Include(r => r.Conference)` or the DTO mapping will trigger lazy-load queries per row against `UseLazyLoadingProxies()`.
  - Case sensitivity: both the new `AttendeeExistsAsync` and `GetByAttendeeEmailAsync` must use the same `.ToLower()` comparison, or a 200-with-empty-list could contradict a 404 (or vice versa) for emails differing only in case.
  - Route encoding: raw emails contain `@`/`.`; `getAttendeeRegistrations` must `encodeURIComponent` the email in the frontend, and ASP.NET route binding for `{email}` on the backend must be verified to accept it unescaped/escaped consistently (test with a real email in Step 8).
- **Breaking Changes:** No — purely additive (new endpoints, new DTO fields, new frontend page/route). Existing `RegistrationDto` consumers gain fields, they don't lose any.
- **New Dependencies:** None.

## 7. Verification & Testing Plan
- **Automated Tests:**
  - `backend/BrownEvents.Tests/RegistrationServiceTests.cs` (extended, §2/§7 test list)
  - `backend/BrownEvents.Tests/AttendeesControllerTests.cs` (new)
  - `backend/BrownEvents.Tests/ConferencesControllerTests.cs` (new, or extension of an existing file if found)
- **Manual Checks:**
  1. Start backend (`dotnet run` in `backend/BrownEvents.Api`) and frontend (`npm run dev` in `frontend/`).
  2. Register an attendee to a conference via the existing UI flow; note the email used.
  3. Visit `/dashboard`, enter that email, confirm the registration appears with correct conference name/dates/status = Confirmed, and a Cancel button is present.
  4. Click Cancel; confirm the row updates to Cancelled with no button, and the list reflects a fresh server read (not just a local state flip).
  5. Enter an email with no registrations (or a never-used email) and confirm the empty state renders, with no unhandled error in the browser console.
  6. Enter the same email with different casing and confirm the same results appear (case-insensitivity).
- **Verification Commands:**
  - `dotnet format backend/BrownEvents.sln --verify-no-changes`
  - `dotnet build backend/BrownEvents.sln -c Release`
  - `dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj -c Release --logger "console;verbosity=normal"`
  - `npm run lint && npm run format:check` (from `frontend/`)
  - `npm run build` (from `frontend/`)

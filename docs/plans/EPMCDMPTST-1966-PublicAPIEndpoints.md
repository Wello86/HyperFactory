# Implementation Plan: BEVN-120 Public API Endpoints Expose Attendee Data

## 1. Context, Objective & Acceptance Criteria
- **Problem Summary:** The ticket reports that some publicly accessible API endpoints include
  attendee names and email addresses in their responses, and that this data should only appear
  in attendee-scoped views, not in public conference or session payloads.

  A full audit of the current branch (`EPMCDMPTST-1966-PublicAPIEndpoints`) found this is
  **already fixed** by the existing DTO layer, not something that needs a new fix:
  - `Controllers/ConferencesController.cs` and `Controllers/SessionsController.cs` never return
    raw EF entities — every action maps through `Dtos/EntityDtoExtensions.cs`'s `ToDto()`
    extension methods before returning `Ok(...)`/`Created(...)`/`CreatedAtAction(...)`.
  - `ConferenceDto` (`Dtos/ConferenceDto.cs`) and `SessionDto` (`Dtos/SessionDto.cs`) have **no**
    attendee/registration fields at all: no nested `Sessions`, no `Attendee`, no `Registrations`.
    `SessionDto.RegisteredCount` is a plain `int` count; `CurrentAttendeeRegistrationId` is an
    `int?` id, not attendee data.
  - The only place `AttendeeDto` (`FirstName`/`LastName`/`Email`) is attached is inside
    `RegistrationDto` and `SessionRegistrationDto` (`Dtos/RegistrationDto.cs`,
    `Dtos/SessionRegistrationDto.cs`), which are returned only by the attendee-scoped
    registration endpoints:
    - `GET /api/conferences/{id}/registrations`
    - `POST /api/conferences/{id}/register` (response body)
    - `POST /api/sessions/{id}/register` (response body)

    These are exactly the "attendee-scoped views" the ticket says PII should stay confined to,
    and the legitimate feature (`frontend/src/components/RegistrationsSection.jsx`, rendered on
    the conference detail page) that must not regress.

  So acceptance criteria 1-4 already hold in the current code. There is one existing test
  (`ApiResponseShapeTests.cs`, lines 65-66) that asserts `sessions`/`registrations` *keys* are
  absent from the conference JSON, but nothing asserts actual attendee name/email *values* are
  absent anywhere in public responses — that is acceptance criterion 5, and the one concrete gap.

- **Goal:** Add a regression test that proves, against real seeded attendee data (not just a
  shape check), that public conference/session endpoints never leak attendee PII, while
  confirming the attendee-scoped registration endpoints still expose it correctly. No production
  code change is expected — if the new tests unexpectedly fail, that reveals a real gap in this
  audit which must then be fixed in the DTO/mapping layer before the task is done.

- **Acceptance Criteria:**
  1. Conference list and detail endpoints do not include attendee names or email addresses.
  2. Session list and detail endpoints do not include attendee names or email addresses.
  3. Attendee-specific data remains accessible through attendee-scoped endpoints.
  4. No regression in features that legitimately display attendee data.
  5. At least one test verifies that public response shapes do not contain PII fields.

## 2. TDD Strategy
- **TDD Applies:** Yes, in a "regression-lock" shape rather than classic red-green. Because the
  audit above shows the boundary already holds in current code, the new tests are expected to
  pass immediately with no implementation step. If any assertion unexpectedly fails, that is
  treated as a genuine bug: fix `Dtos/EntityDtoExtensions.cs`/the relevant DTO, re-run, confirm
  green — do not weaken or delete the failing assertion to make it pass.
- **Initial Test Cases** (all in a new `AttendeePiiExposureTests` class):
  1. `PublicConferenceAndSessionEndpoints_DoNotContainAttendeeMarkers` — seed a conference, a
     session on it, a conference-level registration (marker attendee A), and a session-level
     registration (marker attendee B); assert none of `GET /api/conferences`,
     `GET /api/conferences/{id}`, `GET /api/conferences/{id}/sessions`,
     `GET /api/sessions/{id}` contain attendee A's or B's first name, last name, or email
     substrings anywhere in the raw JSON body.
  2. `AttendeeScopedEndpoints_StillExposeAttendeeDetails` — using the same seeded data, assert
     `POST /api/conferences/{id}/register`'s response body contains attendee A's marker strings,
     and `GET /api/conferences/{id}/registrations` contains attendee A's marker strings. For the
     session register assertion, register a *third*, distinct attendee (not attendee B) and assert
     `POST /api/sessions/{id}/register`'s response body contains that attendee's marker strings —
     reusing attendee B's email would hit `SessionRegistrationService`'s `AlreadyRegistered` conflict
     (409) for a duplicate attendee/session pair. This is the regression guard for AC3/AC4 — it
     fails if a future fix strips PII too aggressively.

## 3. Affected Files
- `backend/BrownEvents.Tests/AttendeePiiExposureTests.cs`: Create

## 4. Proposed Changes (Ordered Steps)
### Step 1: `backend/BrownEvents.Tests/AttendeePiiExposureTests.cs`
- **Target Function/Module:** New test class `AttendeePiiExposureTests` in namespace
  `BrownEvents.Tests`.
- **Changes:**
  - Add a private `static WebApplicationFactory<Program> CreateFactory()` helper that swaps in a
    fresh `InMemoryDatabase` per test, copying the exact pattern already used in
    `ApiResponseShapeTests.CreateFactory()`/`SwaggerDocumentationTests` (remove the existing
    `AppDbContext` service descriptor, register a new scoped `AppDbContext` backed by
    `UseInMemoryDatabase($"AttendeePiiExposureTestsDb-{Guid.NewGuid()}")`). No shared test base
    class exists in this project, so this duplication matches the established convention.
  - Add `[Fact] async Task PublicConferenceAndSessionEndpoints_DoNotContainAttendeeMarkers()`:
    1. `POST /api/conferences` with a `Conference` body (`Title`, `StartDate`, `EndDate`) →
       deserialize the `ConferenceDto` to get its `Id`.
    2. `POST /api/conferences/{id}/sessions` with a `Session` body (`Title`, `StartTime`,
       `EndTime`, `Capacity`) → deserialize the `SessionDto` to get its `Id`.
    3. `POST /api/conferences/{id}/register` with an `Attendee` body
       `{ FirstName = "PiiConferenceFirst", LastName = "PiiConferenceLast", Email = "pii-conference-marker@example.com" }`.
    4. `POST /api/sessions/{sessionId}/register` with an `AttendeeRegistrationRequestDto` body
       `{ FirstName = "PiiSessionFirst", LastName = "PiiSessionLast", Email = "pii-session-marker@example.com" }`.
    5. For each of `GET /api/conferences`, `GET /api/conferences/{id}`,
       `GET /api/conferences/{id}/sessions`, `GET /api/sessions/{sessionId}`: read the raw body
       string and `Assert.DoesNotContain` for each of the four marker substrings
       (`"PiiConferenceFirst"`, `"PiiConferenceLast"`, `"pii-conference-marker@example.com"`,
       `"PiiSessionFirst"`, `"PiiSessionLast"`, `"pii-session-marker@example.com"` — all six,
       since a session-list/detail response could in principle leak either attendee).
  - Add `[Fact] async Task AttendeeScopedEndpoints_StillExposeAttendeeDetails()`:
    1. Repeat the same 4-step seed sequence as above (own factory/db instance, independent of the
       first test).
    2. `Assert.Contains("PiiConferenceFirst", ...)` (and last name + email) in the raw body of the
       `POST /api/conferences/{id}/register` response.
    3. `Assert.Contains("PiiSessionFirst", ...)` (and last name + email) in the raw body of the
       `POST /api/sessions/{sessionId}/register` response.
    4. `Assert.Contains("PiiConferenceFirst", ...)` (and last name + email) in the raw body of
       `GET /api/conferences/{id}/registrations`.
  - Use `using System.Net.Http.Json;`/`System.Text.Json` for (de)serialization and
    `Microsoft.AspNetCore.Mvc.Testing`/`Microsoft.EntityFrameworkCore`/
    `Microsoft.EntityFrameworkCore.Storage`/`Microsoft.Extensions.DependencyInjection`/`Xunit` for
    the harness — same usings as `ApiResponseShapeTests.cs`, ordered per `.editorconfig`
    (`System.*` first, alphabetical, no blank line between groups).

## 5. Scope Boundaries & Assumptions
- **Out of Scope:**
  - Adding authentication/authorization — none exists anywhere in this app
    (`docs/cross-cutting-notes.md` confirms no `[Authorize]`/identity middleware), and the ticket
    does not ask for it. "Attendee-scoped endpoint" is read as "the dedicated registration
    endpoints," not "requires login."
  - `SpeakerDto.Email` (the speaker's own contact email, nested in `SessionDto.Speaker`) — this is
    speaker PII, not attendee PII, and isn't named in the ticket.
  - The `?email=` query parameter on `GET /api/sessions/{id}` (used to compute
    `CurrentAttendeeRegistrationId`) — a minor existence-oracle side channel (lets a caller check
    whether a given email is registered), not a name/email-*in-response* leak, so it's outside
    what AC1/AC2/AC5 ask for.
  - Relocating, renaming, or adding auth to the registration endpoints — AC3 requires they keep
    working exactly as they do today.
  - Any frontend change — `RegistrationsSection.jsx` already only calls the attendee-scoped
    `GET /api/conferences/{id}/registrations` endpoint, so it needs no changes.
- **Open Questions & Assumptions:**
  - Assuming "attendee-scoped endpoints" in AC3 refers to the existing registration endpoints
    listed in Section 1, since there is no other concept of attendee scoping (no auth, no
    per-attendee session) in this codebase.
  - Assuming no production code changes will be needed; Section 2 documents the fallback plan
    (fix `Dtos/EntityDtoExtensions.cs`) if that assumption turns out to be wrong once the new
    tests run.

## 6. Edge Cases & Risk Analysis
- **Potential Risks:**
  - False confidence: a substring-based assertion could miss a leak if the PII is encoded/escaped
    differently (e.g. HTML-escaped email) — mitigated by asserting against the exact strings the
    API itself accepted as input, and by using `System.Text.Json`'s default (non-HTML-escaping)
    output which is what `ApiResponseShapeTests.cs` already relies on.
  - Test data leakage between the two new tests or with other test classes: avoided by each test
    creating its own `WebApplicationFactory`/InMemory database instance (per-test `Guid`-named DB,
    matching existing convention), so no shared/mutable state.
- **Breaking Changes:** No — this plan only adds a new test file.
- **New Dependencies:** None — reuses `Microsoft.AspNetCore.Mvc.Testing`,
  `Microsoft.EntityFrameworkCore.InMemory`, and `xunit`, all already referenced by
  `BrownEvents.Tests.csproj`.

## 7. Verification & Testing Plan
- **Automated Tests:** New `backend/BrownEvents.Tests/AttendeePiiExposureTests.cs` (2 test
  methods, Section 2); full existing suite re-run to confirm no regressions.
- **Manual Checks:** None required — this is a pure backend API contract change (test-only), and
  the frontend `RegistrationsSection.jsx` behavior is unchanged. If desired, `docker-compose up
  postgres -d && dotnet run --project BrownEvents.Api` followed by hitting
  `GET /api/conferences`, `GET /api/conferences/{id}`, `GET /api/conferences/{id}/sessions`,
  `GET /api/sessions/{id}` via Swagger (`:5000/swagger`) can be used to eyeball that no attendee
  fields appear, as a sanity check alongside the automated tests.
- **Verification Commands:**
  ```bash
  dotnet test backend/BrownEvents.Tests --filter "FullyQualifiedName~AttendeePiiExposureTests"
  dotnet test backend/BrownEvents.Tests
  dotnet format backend/BrownEvents.sln --verify-no-changes
  ```
  (`dotnet` was confirmed available and working in this environment via `dotnet --version`
  (10.0.302) and `dotnet test --list-tests`, despite a stale note in `CLAUDE.md` claiming it
  isn't installed.)

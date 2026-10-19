# Implementation Plan: Fix App Crash Under Concurrent Load (Singleton `AppDbContext`)

## 1. Context & Objective
- **Problem Summary:** A load test against BrownEvents throws errors and makes the app unusable once requests are processed concurrently, while single-user testing works fine. Root cause, confirmed by reading `Program.cs` directly: `AppDbContext` is registered with `AddSingleton` (`backend/BrownEvents.Api/Program.cs:15-22`), so one instance is created at startup and reused for the entire app lifetime. EF Core's `DbContext` (its `ChangeTracker` and underlying Npgsql connection) is explicitly **not thread-safe**. All four services — `ConferenceService`, `SessionService`, `SpeakerService`, `RegistrationService` (`backend/BrownEvents.Api/Services/`) — are correctly registered `AddScoped` (`Program.cs:24-27`) and take `AppDbContext` via constructor injection, but because the *context itself* is a singleton, every concurrent HTTP request across every scoped service ends up sharing the exact same `ChangeTracker`/connection. Under single-user testing, calls never overlap, so nothing fails; under load-test concurrency, overlapping calls race on the shared context and throw `InvalidOperationException: A second operation was started on this context before a previous operation completed` (and/or Npgsql connection-state exceptions). No locking, semaphore, or `IDbContextFactory` usage exists anywhere in the codebase (confirmed via search) — this is a pure DI-lifetime defect, not a partially-mitigated one.
- **Goal:** Each HTTP request gets its own `AppDbContext` (matching how the scoped services that consume it already expect to be used), eliminating the concurrency crash, with a regression test proving the fix and root-cause documentation left in code.

## 2. Affected Files
- `backend/BrownEvents.Api/Program.cs`: Modify
- `backend/BrownEvents.Tests/DbContextConcurrencyTests.cs`: Create
- `CLAUDE.md`: Modify
- `docs/architecture.md`: Modify
- `docs/cross-cutting-notes.md`: Modify

## 3. Proposed Changes

### File: `backend/BrownEvents.Api/Program.cs`
- **Target Function/Module:** `AppDbContext` DI registration, `Program.cs:15-22`
- **Changes:**
  - Change only the registration's lifetime call from `builder.Services.AddSingleton<AppDbContext>(sp => ...)` to `builder.Services.AddScoped<AppDbContext>(sp => ...)`. Keep the existing factory-lambda body as-is (`UseNpgsql`, `UseLazyLoadingProxies`, `EnableSensitiveDataLogging` unchanged).
  - Do **not** switch to `builder.Services.AddDbContext<AppDbContext>(...)`. `AddDbContext` registers Scoped by default too, so the behavioral outcome is identical, but it would restructure the options-building code (`Action<DbContextOptionsBuilder>` vs the current manual `DbContextOptionsBuilder<AppDbContext>()`) for zero functional gain — a bigger diff to review for the same fix.
  - Add a comment immediately above the registration (this is the acceptance-criterion-#1 deliverable — root cause documented in code) explaining:
    - Why `Singleton` was wrong: `DbContext`/`ChangeTracker`/the underlying Npgsql connection aren't thread-safe; a singleton meant every concurrent request shared one instance, causing `InvalidOperationException` ("a second operation started on this context before a previous operation completed") under load.
    - Why `Scoped` is correct: ASP.NET Core creates a new DI scope per HTTP request, so each request gets its own `AppDbContext`, matching how the already-`Scoped` services (`ConferenceService`, etc.) expect to consume it, and matching EF Core's documented default for web apps.
  - No other production file needs to change — `Program.cs:71-77`'s startup seeding already uses `app.Services.CreateScope()` correctly, and the four services/controllers are indifferent to `AppDbContext`'s registered lifetime; they just consume whatever instance DI hands them.

### File: `backend/BrownEvents.Tests/DbContextConcurrencyTests.cs`
- **Target Function/Module:** New test class (regression test for the concurrency fix)
- **Changes:**
  - Follow the existing `WebApplicationFactory<Program>` pattern already used in `SwaggerDocumentationTests.cs` (remove the app's `AppDbContext` descriptor, re-register with `UseInMemoryDatabase` inside `builder.ConfigureServices`), but register the replacement as `AddScoped` — this test verifies the *fixed* app behaves correctly under concurrency, not the raw DI API.
  - Seed a few conference rows first (via a scope, before firing concurrent calls) so read endpoints have real data.
  - Fire a burst of concurrent requests with `Task.WhenAll` (~20-50), mixing:
    - `GET /api/conferences` (exercises `ToListAsync`)
    - `GET /api/conferences/{id}` (exercises the `.FindAsync(id).Result` path — good, since blocking calls are where shared-context contention is most likely to surface)
    - `POST /api/conferences/{id}/register` or `POST /api/conferences` (exercises `SaveChangesAsync` — write contention is the realistic crash trigger)
  - Assert every response is a success status code and no request throws — collect and assert on the full list of status codes so a single failing request is visible in the failure output, not just "something threw somewhere."
  - EF Core's InMemory provider will still reproduce this: the thread-safety guard that gets violated lives in core `DbContext`/`ChangeTracker` machinery, not in the Npgsql provider, so a shared instance still races regardless of backing store. Use enough concurrent requests and a read/write mix to make the race reliable rather than flaky.
  - **Validate the test actually detects the regression** before finalizing: temporarily point this test's own `CreateFactory()` registration at `AddSingleton` (test file only, not `Program.cs`) and confirm it reliably fails, then restore `AddScoped` and confirm it passes. Do this during implementation as a sanity check; don't leave the reverted state committed.

### File: `CLAUDE.md`
- **Target Function/Module:** "Key gotchas" bullet, `CLAUDE.md:37`
- **Changes:** Update the bullet describing `AppDbContext`'s registration lifetime so it reflects `AddScoped` instead of the old `AddSingleton` "gotcha," so the doc doesn't contradict the fixed code.

### File: `docs/architecture.md`
- **Target Function/Module:** Data layer table row, `docs/architecture.md:37`
- **Changes:** Update the table row describing `AppDbContext`'s DI lifetime from Singleton to Scoped.

### File: `docs/cross-cutting-notes.md`
- **Target Function/Module:** Backend gotchas bullet, `docs/cross-cutting-notes.md:7`
- **Changes:** Update the bullet describing the `AppDbContext` lifetime gotcha to describe the corrected `Scoped` registration instead of the old Singleton issue.

## 4. Edge Cases & Risk Analysis
- **Potential Risks:**
  - The `.FindAsync(id).Result` blocking calls in `ConferenceService.GetByIdAsync`, `ConferenceService.UpdateAsync`, and `SessionService.GetByIdAsync` are a separate, pre-existing inconsistency (already documented in `docs/cross-cutting-notes.md:8`) — not the root cause of this crash. Explicitly out of scope: leave them as-is; mention in the PR description as a known, separately-tracked follow-up rather than silently ignoring them.
  - `SwaggerDocumentationTests.cs`'s own separate `AddSingleton<AppDbContext>` test-host override is unrelated (single-request test, unaffected by the bug either way) — leave it unmodified.
  - New regression test relies on concurrency timing to reproduce the race; too few concurrent requests or an all-read mix could make the test flaky or non-reproducing — mitigated by the fail/pass sanity check called out in Section 3.
- **Breaking Changes:** No. Changing `AppDbContext`'s DI lifetime from Singleton to Scoped does not change any public API, controller behavior, or response shape — it only changes how many `DbContext` instances exist per request, which is invisible to callers.
- **New Dependencies:** None.

## 5. Verification & Testing Plan
- **Unit Tests to Add/Update:**
  - Add `backend/BrownEvents.Tests/DbContextConcurrencyTests.cs` (new) — concurrent-request regression test as detailed in Section 3, including the fail/pass sanity check against a temporary `AddSingleton` test-host override.
  - No changes needed to existing `ConferenceServiceTests`, `SessionServiceTests`, or `SwaggerDocumentationTests` — confirm they still pass unmodified.
- **Verification Commands:**
  - `dotnet test BrownEvents.Tests` (from `backend/`) — run before any change, to confirm baseline passes.
  - `dotnet test BrownEvents.Tests` (from `backend/`) — run after the `Program.cs` fix and new test are added; confirm `ConferenceServiceTests`, `SessionServiceTests`, `SwaggerDocumentationTests`, and `DbContextConcurrencyTests` all pass.
  - `docker-compose up --build` (or the repo's `run` skill), then fire a concurrent burst (e.g. a handful of parallel `curl`/`hey` requests) at `http://localhost:5000/api/conferences` to confirm no 500s under real load against real Postgres, not just the InMemory test double.

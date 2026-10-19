# BrownEvents — Technical Debt Audit

**Scope:** Full read of every backend (`backend/BrownEvents.Api`, ASP.NET Core 6 / EF Core 6 / PostgreSQL) and frontend (`frontend/src`, React 18 / Vite 4) source file, plus build/deploy config (`docker-compose.yml`, `.gitlab-ci.yml`, `vite.config.js`, `nginx.conf`, `.gitignore`, package manifests).
**Method:** Manual line-by-line review; every citation below was re-verified against the current source on the audit date.
**Date:** 2026-09-08
**Purpose:** Feed the Phase 1 stabilization backlog with a concrete, actionable list of defects — not a style critique.

Issues found: **44** (29 backend, 15 frontend/config), grouped by category below. Some root causes appear in more than one category (e.g. the singleton `DbContext` is both a correctness and a performance defect) — those get one full write-up and a cross-reference elsewhere, rather than being duplicated.

## Summary table

| # | Severity | Category | Location | Issue |
|---|----------|----------|----------|-------|
| S1 | Critical | Security | `Program.cs:11` | `AppDbContext` registered as Singleton — not thread-safe |
| S2 | Critical | Security | `appsettings.json:10`, `docker-compose.yml:10,27` | Hardcoded DB password committed to git |
| S3 | Critical | Security | `Program.cs:31-38,51` + repo-wide | CORS `AllowAny*` combined with zero authentication anywhere |
| S4 | High | Security | `Program.cs:16` | `EnableSensitiveDataLogging()` unconditional — PII/SQL in prod logs |
| S5 | High | Security | `Program.cs:48-49` | Swagger UI exposed unconditionally, incl. "Production" |
| S6 | Medium | Security | `Program.cs` (whole file) | No HTTPS redirection/HSTS |
| S7 | Medium | Security | `api.js:4`; `ConferenceDetailPage.jsx:44,48,83,88,110,116`; `RegistrationPage.jsx:29` | PII logged to browser console in production |
| C1 | High | Correctness | `RegistrationService.cs:15-45` | No conference-exists check, no capacity check, duplicate registrations, TOCTOU race |
| C2 | High | Correctness | `ConferenceDetailPage.jsx:82-93,112-115` | Stale `selectedSession` silently attached to unrelated registration |
| C3 | Medium | Correctness | `ConferenceService.cs:23,36`; `SessionService.cs:18` | Sync-over-async `.Result` blocking calls |
| C4 | Medium | Correctness | `ConferenceService.cs:45` | `updated.Status.ToUpper()` throws NRE on null status |
| C5 | Medium | Correctness | `Models/*.cs`, `Controllers/*.cs` | ~~No model validation / `ModelState` checks anywhere~~ — resolved, see below |
| C6 | Medium | Correctness | `Conference.cs`, `Session.cs`, `ConferenceService.cs:27-52`, `AddSessionAsync:61-67` | No `StartDate<=EndDate` / `StartTime<=EndTime` validation |
| C7 | Medium | Correctness | `ConferencesController.cs:49-54,63-68` | Missing-conference `GetSessions`/`GetRegistrations` return `200 []` instead of `404` |
| C8 | Medium | Correctness | `Program.cs` (whole file) | No global exception-handling middleware |
| C9 | Medium | Correctness | `ConferenceDetailPage.jsx:34,307-332` | `dietaryRequirements` collected in state but never rendered as an input |
| C10 | Medium | Correctness | `ConferenceDetailPage.jsx:37-80`, `SessionDetailPage.jsx:11-22`, `ConferenceListPage.jsx:11-23` | No abort/staleness guard on fetch effects — race condition |
| C11 | Low | Correctness | `App.jsx:9-15`, `main.jsx:6-10` | No catch-all `404` route, no error boundary |
| P1 | Medium | Performance | `Program.cs:11` + `ConferenceService.cs:23,36`, `SessionService.cs:18` | Singleton DbContext + blocking calls → thread-pool starvation under load |
| P2 | Low | Performance | `ConferenceService.cs:16-19,54-59,69-74`; `SpeakerService.cs:16-19` | No pagination on any list endpoint |
| P3 | Low | Performance | `AppDbContext.cs:17-56`; `RegistrationService.cs:19` | No DB indexes, esp. `Attendee.Email` — full table scan on every registration |
| P4 | Low | Performance | `ConferenceListPage.jsx:58-60`, `SessionList.jsx:7-14` | No memoization on card lists; inline style objects allocated every render |
| P5 | Low | Performance | `ConferenceDetailPage.jsx:37-80` | 3 unbatched sequential fetches per page load |
| M1 | Medium | Maintainability | `ConferenceService.cs` vs `SessionService.cs` | Inconsistent async patterns within/between near-identical methods |
| M2 | Low | Maintainability | `ConferenceService.cs:34-52`, `SessionService.cs:22-37` | Manual field-by-field copy mapping, no shared mapper |
| M3 | Medium | Maintainability | `backend/BrownEvents.Tests/` | Only 2 of 4 services unit-tested; zero controller tests |
| M4 | Low | Maintainability | `RegistrationService.cs:17` | Stray empty comment / dead marker, no lint to catch it |
| M5 | Medium | Maintainability | `ConferenceCard.jsx:4-8`, `SessionCard.jsx:4-8`, `ConferenceDetailPage.jsx:136-141`, `SessionDetailPage.jsx:24-34` | `formatDate`/`formatTime` duplicated 5x |
| M6 | Low | Maintainability | `ConferenceCard.jsx:10-18`, `ConferenceDetailPage.jsx:125-134` | Status-badge-class switch duplicated verbatim |
| M7 | Medium | Maintainability | `RegistrationPage.jsx` vs `ConferenceDetailPage.jsx:292-343` | Two diverging implementations of the same registration flow |
| M8 | Medium | Maintainability | `ConferenceDetailPage.jsx` (346 lines) | God component: 15 `useState` hooks, 5 unrelated concerns |
| M9 | Low | Maintainability | `SessionList.jsx:3-13` | Prop drilling acknowledged in a comment, never fixed |
| M10 | Low | Maintainability | `package.json:17-18`, all `.jsx` files | `@types/react*` installed but unused — no TS, no PropTypes |
| M11 | Low | Maintainability | 32 occurrences across 6 files | Inconsistent inline-`style` vs CSS-class approach |
| M12 | Low | Maintainability | `SessionCard.jsx:11` | Clickable `div` with no `role`/`tabIndex`/keyboard handler |
| M13 | Medium | Maintainability | repo-wide | No ESLint/Prettier config, no lint script, anywhere |
| F1 | Low | Maintainability | `api.js:26-28`, `ConferenceDetailPage.jsx:42`, `SessionDetailPage.jsx:15` | Inconsistent, duplicated API-response unwrapping (`data.data \|\| data`) |
| CFG1 | Critical | Configuration | `docker-compose.yml:9-11,27` | Same weak DB password duplicated in 2 places (drift risk) — see S2 |
| CFG2 | High | Configuration | `docker-compose.yml:26-27` vs `Program.cs` | `ASPNETCORE_ENVIRONMENT=Production` doesn't actually gate Swagger/logging/CORS |
| CFG3 | High | Configuration | `.gitignore` (27 lines) | Doesn't exclude `appsettings*.json`; no `.env.example`; no startup config validation |
| CFG4 | High | Configuration | `frontend/src/api.js:1,5` | Hardcoded absolute `http://localhost:5000` API URL — breaks every non-local deployment |
| CFG5 | Low | Configuration | `vite.config.js:8-13`, `nginx.conf:13-17` | Dev + prod proxy configs are dead code because of CFG4 |
| CFG6 | Low | Configuration | repo root | Stray `sh.exe.stackdump` crash artifact left in working directory |
| CFG7 | Medium | Configuration | `BrownEvents.Api.csproj:4`; `package.json:20` | EOL dependencies: .NET 6 (EOL Nov 2024), Vite `^4.4.5` (superseded) |
| CFG8 | Low | Configuration | `BrownEvents.Tests.csproj:12`; `package.json:17-18` | Unused dependencies: `Moq` (never called), `@types/react*` (no TS/tsx) |
| CFG9 | Low | Configuration | `.gitlab-ci.yml:36-47` | No frontend test/lint stage in CI; no image vulnerability scanning |

---

## Security

### S1 — `AppDbContext` registered as Singleton
- **Location:** `backend/BrownEvents.Api/Program.cs:11`
- **Severity:** Critical
- **Description:** `builder.Services.AddSingleton<AppDbContext>(...)` shares one `DbContext` instance across every concurrent HTTP request. EF Core's `DbContext` is explicitly documented as not thread-safe. Under concurrent load this throws `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`, or silently corrupts/cross-contaminates data between unrelated requests' change-tracking state.
- **Suggested fix:** Replace with `builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString).UseLazyLoadingProxies())`, which registers it scoped (one instance per request) as EF Core expects. Move `EnableSensitiveDataLogging()` behind an `if (builder.Environment.IsDevelopment())` check (see S4).

### S2 — Hardcoded database credentials committed to git
- **Location:** `backend/BrownEvents.Api/appsettings.json:10`; duplicated in `docker-compose.yml:10,27`
- **Severity:** Critical
- **Description:** `"DefaultConnection": "Host=postgres;Port=5432;Database=brownevents;Username=brown;Password=brown123"` is tracked in git (confirmed via `git ls-files`). The password is weak, guessable, and reused identically in `docker-compose.yml`. Anyone with repo read access has the DB credential; the same secret is duplicated so it can silently drift.
- **Suggested fix:** Remove the password from `appsettings.json` (keep only non-secret defaults there); read the connection string from an environment variable (`ConnectionStrings__DefaultConnection`) or a secrets manager / `dotnet user-secrets` in development. Add `appsettings.*.json` secret-bearing sections to `.gitignore`/use `appsettings.json` only as a template with placeholder values (see CFG3).

### S3 — Permissive CORS combined with zero authentication
- **Location:** `backend/BrownEvents.Api/Program.cs:31-38,51`; absence confirmed across all files in `Controllers/`
- **Severity:** Critical
- **Description:** The `"AllowAll"` CORS policy (`AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`) is applied unconditionally via `app.UseCors("AllowAll")`, not gated by environment. No controller has an `[Authorize]` attribute, no auth scheme is registered in `Program.cs`, and no API keys exist anywhere. Every create/update/register endpoint is fully anonymous and callable from any website in any browser.
- **Suggested fix:** Restrict CORS to a known, explicit origin list read from configuration (dev: `http://localhost:5173`; prod: the real frontend origin). Add authentication (e.g. JWT bearer or cookie auth) and apply `[Authorize]` to write endpoints at minimum before any real users touch this system.

### S4 — Sensitive EF Core logging enabled unconditionally
- **Location:** `backend/BrownEvents.Api/Program.cs:16`; compounded by `appsettings.Development.json:4,6` (`Default: Debug`, EF command logging `Information`)
- **Severity:** High
- **Description:** `.EnableSensitiveDataLogging()` is set in the `DbContextOptionsBuilder` used for every environment, including the "Production" container defined in `docker-compose.yml:26`. This logs full SQL statements with parameter values — including attendee emails and other PII — to application logs in production.
- **Suggested fix:** Only call `.EnableSensitiveDataLogging()` when `builder.Environment.IsDevelopment()` is true.

### S5 — Swagger UI exposed in production
- **Location:** `backend/BrownEvents.Api/Program.cs:48-49`
- **Severity:** High
- **Description:** `app.UseSwagger(); app.UseSwaggerUI();` run unconditionally. `docker-compose.yml:26` sets `ASPNETCORE_ENVIRONMENT: Production` for the backend container, yet Swagger/OpenAPI stays reachable there, publishing the full API surface/schema to anyone who finds the endpoint — reconnaissance aid for an attacker.
- **Suggested fix:** Wrap both calls in `if (app.Environment.IsDevelopment()) { ... }`.

### S6 — No HTTPS redirection / HSTS
- **Location:** `backend/BrownEvents.Api/Program.cs` (entire pipeline, no `UseHttpsRedirection`/`UseHsts` call)
- **Severity:** Medium
- **Description:** The API accepts and serves plaintext HTTP with no enforced upgrade path, so credentials/tokens/PII in requests can travel unencrypted if the deployment doesn't terminate TLS upstream.
- **Suggested fix:** Add `app.UseHttpsRedirection();` and `app.UseHsts();` (guarded appropriately for non-dev environments), and ensure the reverse proxy/load balancer terminates TLS.

### S7 — PII logged to the browser console in production
- **Location:** `frontend/src/api.js:4`; `frontend/src/pages/ConferenceDetailPage.jsx:44,48,83,88,110,116`; `frontend/src/pages/RegistrationPage.jsx:29`
- **Severity:** Medium
- **Description:** Every API request (`api.js:4`, including request bodies) and every registration form submission (full name/email/phone/company) is logged via `console.log` with no environment guard, so this debug instrumentation ships to production and exposes user PII in anyone's browser devtools.
- **Suggested fix:** Remove these `console.log` calls, or gate them behind `if (import.meta.env.DEV)`.

## Correctness

### C1 — `RegistrationService` is missing every business-rule check it needs
- **Location:** `backend/BrownEvents.Api/Services/RegistrationService.cs:15-45`
- **Severity:** High
- **Description:** `RegisterAttendeeAsync` never verifies the `conferenceId` exists (an invalid id hits the DB and throws an unhandled FK-violation → raw 500, see C7/C8), never checks `Conference.MaxAttendees` (`Models/Conference.cs:13`) so a conference can be oversubscribed indefinitely, and never prevents the same attendee (matched by email at line 19) from registering repeatedly — no unique constraint on `(AttendeeId, ConferenceId)` exists in `AppDbContext.cs`, so duplicate `Registration` rows accumulate freely. The email-existence check (line 19) and the subsequent insert (lines 24-26) are also not atomic and `Attendee.Email` has no unique DB constraint, so concurrent requests with the same email can create duplicate `Attendee` rows (TOCTOU race).
- **Suggested fix:** Look up the conference first and return a clean 404/400 if missing; count existing confirmed registrations against `MaxAttendees` before inserting; add a unique index on `(AttendeeId, ConferenceId)` and on `Attendee.Email` in `AppDbContext.OnModelCreating`, and let the DB constraint (not a racy read-then-write) enforce uniqueness, catching the resulting exception to return a friendly "already registered" response.

### C2 — Stale session state leaks into an unrelated registration
- **Location:** `frontend/src/pages/ConferenceDetailPage.jsx:82-93` (`handleSessionClick`, `handleRegisterClick`) and `:112-115` (submit)
- **Severity:** High
- **Description:** `handleSessionClick` (line 82-85) sets `selectedSession`. `handleRegisterClick` (line 87-93), triggered by the page-level "Register Now" button (line 245), resets every field of `formData` but never resets `selectedSession`. If a user clicks any session card and then uses the unrelated general "Register Now" button, the submit handler (line 112-115) still attaches `sessionId: selectedSession.id` to the payload — the user has no indication a session got attached, but the backend receives one anyway.
- **Suggested fix:** In `handleRegisterClick`, also call `setSelectedSession(null)`, or better, don't share `selectedSession` state between the "session details" panel and the registration modal at all — pass the intended session explicitly into the modal open call.

### C3 — Sync-over-async blocking calls
- **Location:** `backend/BrownEvents.Api/Services/ConferenceService.cs:23` (`GetByIdAsync`) and `:36` (`UpdateAsync`); `backend/BrownEvents.Api/Services/SessionService.cs:18` (`GetByIdAsync`)
- **Severity:** Medium
- **Description:** `_context.Conferences.FindAsync(id).Result` blocks the calling thread on an async operation instead of awaiting it, then wraps the result in `Task.FromResult` — a pointless async facade that still occupies a thread-pool thread for the duration of the DB call. Combined with S1 (singleton context), this is also a scalability risk (see P1).
- **Suggested fix:** Make these methods properly `async` and `await _context.Conferences.FindAsync(id)` directly, matching the pattern already used correctly elsewhere in the same files (e.g. `SessionService.UpdateAsync:24`).

### C4 — Null status crashes with an unhandled `NullReferenceException`
- **Location:** `backend/BrownEvents.Api/Services/ConferenceService.cs:45`
- **Severity:** Medium
- **Description:** `existing.Status = updated.Status.ToUpper();` assumes `updated.Status` is non-null. `Conference.Status` (`Models/Conference.cs:12`) has a C# default of `"UPCOMING"`, but that default only applies when the object is constructed server-side — a client `PUT` with `"status": null` in the JSON body deserializes to a null `Status` (nullable annotations aren't enforced on JSON input binding), and `.ToUpper()` on null throws, surfacing as a raw 500 (see C8).
- **Suggested fix:** Guard with `existing.Status = (updated.Status ?? existing.Status).ToUpper();`, and add proper model validation (C5) so a missing/invalid status is rejected with a 400 before it reaches the service layer.

### C5 — No model validation anywhere (RESOLVED — EPMCDMPTST-1953)
- **Location:** All files under `backend/BrownEvents.Api/Models/`; all `Create`/`Update` actions in `backend/BrownEvents.Api/Controllers/*.cs`
- **Severity:** Medium
- **Description:** None of the entity classes carry `[Required]`/`[StringLength]`/`[EmailAddress]` data-annotation attributes, and no controller action checks `ModelState.IsValid`. `ConferencesController.Create` (`Controllers/ConferencesController.cs:34-39`) and `SpeakersController.Create` (`Controllers/SpeakersController.cs:24-29`) will happily persist a conference/speaker with an empty title, a negative `MaxAttendees`, or a malformed email.
- **Resolution:** `[Required]`/`[MaxLength]`/`[EmailAddress]` added to `Conference.Title`, `Session.Title`, `Speaker.FirstName/LastName/Email`, `Attendee.FirstName/LastName/Email`; `[ApiController]` added to `ConferencesController`, `SessionsController`, `SpeakersController` for automatic `ModelState` → `400` behavior. `Session.Conference` marked `[ValidateNever]` to avoid the framework's implicit-required check on that EF navigation property rejecting legitimate requests. Remaining scope not covered here: `MaxAttendees`/`Capacity` range checks, `StartDate<=EndDate` (see C6), and `Room`/`Registration` models (not currently accepted as request bodies).

### C6 — No date-range validation on conferences or sessions
- **Location:** `backend/BrownEvents.Api/Models/Conference.cs` (no validation), `Services/ConferenceService.cs:27-52`, `Services/ConferenceService.cs:61-67` (`AddSessionAsync`), `Services/SessionService.cs:22-37`
- **Severity:** Medium
- **Description:** Nothing checks that `Conference.StartDate <= Conference.EndDate` or `Session.StartTime <= Session.EndTime` on create/update. Clients can create a conference or session that ends before it starts, and the UI (`ConferenceDetailPage.jsx:199`, `SessionCard.jsx:14`) will render the resulting nonsensical date range without complaint.
- **Suggested fix:** Add a validation check in `CreateAsync`/`UpdateAsync`/`AddSessionAsync` (or via a data-annotation `IValidatableObject` implementation) that rejects `end < start` with a 400.

### C7 — Missing-conference sub-resource endpoints return `200 []` instead of `404`
- **Location:** `backend/BrownEvents.Api/Controllers/ConferencesController.cs:49-54` (`GetSessions`), `:63-68` (`GetRegistrations`)
- **Severity:** Medium
- **Description:** Both actions call the service with whatever `id` was supplied and return `Ok(...)` regardless of whether a conference with that id exists — a request for `/api/conferences/99999/sessions` returns `200` with an empty list rather than `404`, masking a client error as "conference exists but has no sessions."
- **Suggested fix:** Check `await _service.GetByIdAsync(id)` (or a lighter existence check) first and return `NotFound()` if the conference doesn't exist, before returning the sub-resource list.

### C8 — No global exception-handling middleware
- **Location:** `backend/BrownEvents.Api/Program.cs` (entire request pipeline, lines 45-62)
- **Severity:** Medium
- **Description:** There's no `app.UseExceptionHandler(...)`/`UseStatusCodePages`, so any unhandled exception — the FK violation from C1, the NRE from C4, or anything else — bubbles up as a raw ASP.NET Core developer/framework error response instead of a controlled, consistent error contract.
- **Suggested fix:** Add `app.UseExceptionHandler("/error")` (or a custom exception-handling middleware) that logs the exception and returns a sanitized `ProblemDetails` response, and only surface stack traces when `IsDevelopment()`.

### C9 — Dietary requirements field collected but never rendered
- **Location:** `frontend/src/pages/ConferenceDetailPage.jsx:34` (declared in `formData`), `:92` (reset), `:307-332` (form JSX — no matching input)
- **Severity:** Medium
- **Description:** `dietaryRequirements` is part of the `formData` state and is submitted with every registration (always as `''`), but the modal form never renders an `<input>`/`<textarea>` for it. A user has no way to actually enter this data despite the app clearly intending to collect it — dead/incomplete feature.
- **Suggested fix:** Either add the missing form field to the modal, or remove the dead state key if dietary requirements aren't actually needed yet.

### C10 — No guard against out-of-order fetch responses
- **Location:** `frontend/src/pages/ConferenceDetailPage.jsx:37-80` (3 separate effects), `frontend/src/pages/SessionDetailPage.jsx:11-22`, `frontend/src/pages/ConferenceListPage.jsx:11-23`
- **Severity:** Medium
- **Description:** None of these `useEffect`s use an `AbortController` or a "latest request wins" flag. If a user navigates from `/conferences/1` to `/conferences/2` before the first request resolves, a slow response for id `1` can resolve after the id `2` response and overwrite state with stale data for the wrong conference.
- **Suggested fix:** Use an `AbortController` per effect (abort on cleanup) or compare a captured `id`/request token against the current one before calling `setState` in the `.then()` callback.

### C11 — No 404 route, no error boundary
- **Location:** `frontend/src/App.jsx:9-15` (no `path="*"` route); `frontend/src/main.jsx:6-10` (no `ErrorBoundary`)
- **Severity:** Low
- **Description:** Any unmatched URL renders a blank page with no UI feedback. Any uncaught render error anywhere in the component tree white-screens the entire app with no fallback UI.
- **Suggested fix:** Add a catch-all `<Route path="*" element={<NotFoundPage />} />` in `App.jsx`, and wrap `<App />` in an error boundary component in `main.jsx`.

## Performance

### P1 — Singleton DbContext + blocking calls under concurrent load
- **Location:** `backend/BrownEvents.Api/Program.cs:11` (see S1), combined with `Services/ConferenceService.cs:23,36` and `Services/SessionService.cs:18` (see C3)
- **Severity:** Medium
- **Description:** Beyond the correctness risk already covered under S1/C3, this combination causes thread-pool starvation under concurrent load: every request contends for the same non-thread-safe context, and several code paths additionally block a thread-pool thread synchronously waiting on `.Result`.
- **Suggested fix:** Same fix as S1 and C3 — scope the DbContext correctly and remove the blocking calls; this single change resolves both the correctness and performance findings.

### P2 — No pagination on any list endpoint
- **Location:** `backend/BrownEvents.Api/Services/ConferenceService.cs:16-19` (`GetAllAsync`), `:54-59` (`GetSessionsAsync`), `:69-74` (`GetRegistrationsAsync`); `Services/SpeakerService.cs:16-19` (`GetAllAsync`)
- **Severity:** Low
- **Description:** All four methods do an unbounded `ToListAsync()`. Harmless at the current demo-data scale (`DataSeeder.cs` seeds 3 conferences, 7 sessions), but there is no defensive limit, so response size and query cost grow unbounded as real data accumulates.
- **Suggested fix:** Add `skip`/`take` query parameters (or cursor-based paging) to these endpoints before any production data volume is loaded.

### P3 — No database indexes, including on a hot-path lookup column
- **Location:** `backend/BrownEvents.Api/Data/AppDbContext.cs:17-56` (`OnModelCreating` — no `.HasIndex` calls anywhere); the hot path is `Services/RegistrationService.cs:19`
- **Severity:** Low
- **Description:** `Attendee.Email` is queried directly on every single registration attempt (`RegistrationService.cs:19`) but has no index — every registration currently does a full table scan of `attendees`. This compounds with C1 (no unique constraint) — the same column needs an index both for correctness (uniqueness) and performance.
- **Suggested fix:** Add `entity.HasIndex(a => a.Email).IsUnique();` to the `Attendee` configuration in `OnModelCreating`, which fixes both P3 and part of C1 in one change.

### P4 — No memoization on list rendering
- **Location:** `frontend/src/pages/ConferenceListPage.jsx:58-60`, `frontend/src/components/SessionList.jsx:7-14`
- **Severity:** Low
- **Description:** `ConferenceCard`/`SessionCard` are not wrapped in `React.memo`, so every parent re-render re-renders every card. Combined with the many inline `style={{...}}` object literals throughout these components (a new object allocated every render), this is a pattern that will degrade noticeably as list sizes grow, even though it's harmless at current data volumes.
- **Suggested fix:** Wrap `ConferenceCard`/`SessionCard` in `React.memo`, and hoist static inline style objects out of the render body (or move them into CSS classes, see M11).

### P5 — Unbatched sequential fetches on the conference detail page
- **Location:** `frontend/src/pages/ConferenceDetailPage.jsx:37-80`
- **Severity:** Low
- **Description:** Three independent `useEffect`s fire three separate network requests (conference, sessions, registrations) on every page visit, each maintaining its own loading/error state, rather than one coordinated fetch.
- **Suggested fix:** Combine into a single effect using `Promise.all([getConference(id), getConferenceSessions(id), getConferenceRegistrations(id)])`, or extract each into a small custom hook (`useConference`, `useSessions`, `useRegistrations`) as part of the broader decomposition in M8.

## Maintainability

### M1 — Inconsistent async patterns within the same files
- **Location:** `backend/BrownEvents.Api/Services/SessionService.cs` (`GetByIdAsync:18` blocks via `.Result`, `UpdateAsync:24` correctly awaits) and `Services/ConferenceService.cs` (`GetByIdAsync:23`/`UpdateAsync:36` block, `CreateAsync:27-32`/`GetSessionsAsync:54-59` correctly await)
- **Severity:** Medium
- **Description:** The same file mixes the broken blocking pattern (C3) with the correct async pattern with no consistent convention, making it easy for future contributors to copy the wrong example.
- **Suggested fix:** Fix all instances per C3, then add a Roslyn analyzer rule (or at minimum a PR checklist item) banning `.Result`/`.Wait()` on `Task` in this codebase.

### M2 — Manual field-by-field copy mapping with no shared helper
- **Location:** `backend/BrownEvents.Api/Services/ConferenceService.cs:39-48` (`UpdateAsync`), `Services/SessionService.cs:27-33` (`UpdateAsync`)
- **Severity:** Low
- **Description:** Every property is copied one-by-one by hand in both places, with no shared mapping abstraction (no AutoMapper, no manual mapper method). Adding a new field to `Conference`/`Session` requires remembering to update this code too — easy to introduce a silent field-sync bug.
- **Suggested fix:** Either add AutoMapper (or a hand-written `Apply(this Conference target, Conference source)` extension method) so the mapping lives in one place per entity.

### M3 — Thin backend test coverage
- **Location:** `backend/BrownEvents.Tests/` (only `ConferenceServiceTests.cs` and `SessionServiceTests.cs` exist)
- **Severity:** Medium
- **Description:** `SpeakerService` and, critically, `RegistrationService` — the service with the most business logic and the most bugs found in this audit (C1) — have zero unit tests. There are also no controller-level tests anywhere, so HTTP-layer behavior (status codes, routing, model binding) is entirely unverified.
- **Suggested fix:** Add `RegistrationServiceTests.cs` covering the capacity/duplicate/nonexistent-conference cases fixed under C1, `SpeakerServiceTests.cs`, and at least smoke-level controller tests using `WebApplicationFactory`.

### M4 — Dead comment marker left in source
- **Location:** `backend/BrownEvents.Api/Services/RegistrationService.cs:17`
- **Severity:** Low
- **Description:** A stray empty `//` comment sits at the top of `RegisterAttendeeAsync`, suggesting removed or never-finished validation logic. Harmless by itself, but a symptom of the missing linting (M13) that would normally flag this kind of clutter.
- **Suggested fix:** Remove the stray comment when fixing C1 in the same method.

### M5 — Date/time formatting duplicated five times
- **Location:** `frontend/src/components/ConferenceCard.jsx:4-8` (`formatDate`), `frontend/src/components/SessionCard.jsx:4-8` (`formatTime`), `frontend/src/pages/ConferenceDetailPage.jsx:136-141` (`formatDate`), `frontend/src/pages/SessionDetailPage.jsx:24-34` (`formatTime` + `formatDate`)
- **Severity:** Medium
- **Description:** Five near-identical functions, differing only in cosmetic locale options, are copy-pasted across four files instead of living in one shared module. Any bug fix (e.g. timezone handling) has to be applied in up to five places, and inevitably some will be missed.
- **Suggested fix:** Extract `formatDate`/`formatTime` into `frontend/src/utils/format.js` and import from there in all four components/pages.

### M6 — Status-badge-class logic duplicated
- **Location:** `frontend/src/components/ConferenceCard.jsx:10-18` (`statusClass`), `frontend/src/pages/ConferenceDetailPage.jsx:125-134` (`getStatusBadgeClass`)
- **Severity:** Low
- **Description:** Both are byte-for-byte identical `switch` statements under two different names.
- **Suggested fix:** Move alongside the M5 fix into the same shared utils module.

### M7 — Two diverging implementations of the registration flow
- **Location:** `frontend/src/pages/RegistrationPage.jsx` (whole file) vs. the modal in `frontend/src/pages/ConferenceDetailPage.jsx:28-35,87-123,292-343`
- **Severity:** Medium
- **Description:** Both implement "register for this conference" with the same field set (`firstName`, `lastName`, `email`, `phone`, `company`) and near-identical submit/loading/error/success handling, but they've already diverged — the modal additionally carries `dietaryRequirements` (C9) and can attach a stray `sessionId` (C2). Two sources of truth for the same business flow guarantees future drift.
- **Suggested fix:** Extract a shared `<RegistrationForm onSubmit={...} extraFields={...} />` component (or a `useRegistrationForm` hook) used by both `RegistrationPage` and the `ConferenceDetailPage` modal.

### M8 — 346-line god component
- **Location:** `frontend/src/pages/ConferenceDetailPage.jsx` (entire file)
- **Severity:** Medium
- **Description:** This single component owns conference fetch/loading/error state, sessions fetch/loading/error state, registrations fetch/loading/error state, session-selection UI state, and a full registration modal + form — five largely independent concerns spread across 15 `useState` calls (lines 10-28: `conference`, `sessions`, `loading`, `sessionsLoading`, `error`, `sessionsError`, `showModal`, `selectedSession`, `registrationLoading`, `registrationError`, `registrationSuccess`, `registrations`, `registrationsLoading`, `registrationsError`, `formData`).
- **Suggested fix:** Decompose into `useConference(id)`, `useSessions(id)`, `useRegistrations(id)` hooks plus an extracted `<RegistrationModal>` component (which can also absorb the M7 fix).

### M9 — Acknowledged, unfixed prop drilling
- **Location:** `frontend/src/components/SessionList.jsx:3-13`
- **Severity:** Low
- **Description:** The code has a comment at line 3 explicitly noting `conferenceId` and `onSessionClick` are threaded through `ConferenceDetailPage → SessionList → SessionCard` purely as pass-through props with no Context or state manager — the codebase flags its own tech debt here but never addresses it.
- **Suggested fix:** Either accept this as fine at current component depth (2 levels), or introduce a small Context if a third consumer appears — don't add a state library for a 2-hop prop pass.

### M10 — Unused type packages, no runtime prop validation
- **Location:** `frontend/package.json:17-18` (`@types/react`, `@types/react-dom`); every file under `frontend/src` is `.jsx`, none declare `PropTypes`
- **Severity:** Low
- **Description:** The `@types/*` packages provide no value without TypeScript (`.tsx`) files, and there's no `prop-types` usage either, so component props (`conference`, `session`, `room`, etc.) are effectively untyped/unvalidated everywhere.
- **Suggested fix:** Either remove the unused `@types/*` devDependencies, or (better, given the app's small size) migrate to TypeScript incrementally starting with `api.js` and shared utils.

### M11 — Inconsistent styling approach
- **Location:** 32 inline `style={{...}}` occurrences across `ConferenceDetailPage.jsx` (15), `SessionDetailPage.jsx` (6), `RegistrationPage.jsx` (5), `ConferenceCard.jsx` (3), `SessionCard.jsx` (2), `ConferenceListPage.jsx` (1) — concrete example at `SessionDetailPage.jsx:86` (`style={{ WebkitLineClamp: 'unset' }}`) fighting the CSS rule at `frontend/src/index.css:59-67`
- **Severity:** Low
- **Description:** `index.css` establishes a clean BEM-like class system, but layout tweaks are done ad hoc via inline styles throughout instead of additional modifier classes, making the two systems fight each other (e.g. the line-clamp override) and preventing CSS specificity/reuse.
- **Suggested fix:** Add modifier classes (e.g. `.card__description--no-clamp`) for the recurring inline-style cases instead of inline overrides.

### M12 — Non-semantic clickable element, no keyboard support
- **Location:** `frontend/src/components/SessionCard.jsx:11`
- **Severity:** Low
- **Description:** `<div className="card session-card" onClick={...} style={{ cursor: 'pointer' }}>` implements a clickable card using a bare `div` with only a mouse handler — no `role="button"`, no `tabIndex`, no `onKeyDown` for Enter/Space. Keyboard and screen-reader users cannot activate it.
- **Suggested fix:** Either use a `<button>`/`<Link>` as the clickable wrapper, or add `role="button" tabIndex={0}` plus an `onKeyDown` handler that triggers the same action on Enter/Space.

### M13 — No linting or formatting configuration anywhere
- **Location:** repo-wide — confirmed no `.eslintrc*`, `.prettierrc*`, or `.editorconfig` in `frontend/`; no `.editorconfig`/analyzer package references in `backend/`
- **Severity:** Medium
- **Description:** Zero static-analysis tooling exists for either half of the stack. Several issues in this audit (M4's dead comment, M1's inconsistent async pattern, M12's missing a11y attributes) are exactly the class of issue a linter would catch automatically going forward.
- **Suggested fix:** Add ESLint (+ `eslint-plugin-react`, `eslint-plugin-jsx-a11y`) and Prettier to the frontend with a `lint` script wired into `.gitlab-ci.yml`; add `.editorconfig` and consider a Roslyn analyzer package for the backend.

### F1 — Inconsistent, duplicated API-response unwrapping
- **Location:** `frontend/src/api.js:26-28` (`.then(res => res.data || res)`), `frontend/src/pages/ConferenceDetailPage.jsx:42` (`data.data || data`), `frontend/src/pages/SessionDetailPage.jsx:15` (`data.data || data`)
- **Severity:** Low
- **Description:** The backend itself is inconsistent — `ConferencesController.GetById` (`Controllers/ConferencesController.cs:26-32`) returns the conference unwrapped, while `GetSessions`/`AddSession` (`:49-54,56-61`) wrap results in `{ data: ... }`. The frontend compensates with the same defensive `res.data || res` guess repeated in three different places (API layer once, then again ad hoc in two page components) instead of normalizing the shape in one place.
- **Suggested fix:** Pick one response envelope convention on the backend (recommend: no wrapper — return the resource directly, matching `GetById`) and update `GetSessions`/`AddSession` to match; then remove the defensive unwrapping from the two page components once the API is consistent.

## Configuration

### CFG1 — Weak DB credential duplicated across files
- **Location:** `docker-compose.yml:9-11` and `:27`
- **Severity:** Critical
- See S2 for full detail — listed here because it's also a pure configuration-drift risk: the same literal string exists in two files and nothing keeps them in sync if one is rotated.
- **Suggested fix:** Same as S2 — source from a single environment variable / `.env` file referenced by both the app config and compose file, not copy-pasted.

### CFG2 — "Production" environment flag doesn't gate anything risky
- **Location:** `docker-compose.yml:26-27` (`ASPNETCORE_ENVIRONMENT: Production`) vs. `backend/BrownEvents.Api/Program.cs` (Swagger, sensitive logging, CORS all unconditional)
- **Severity:** High
- **Description:** The compose file sets the environment flag correctly, but `Program.cs` never checks `app.Environment.IsDevelopment()`/`IsProduction()` anywhere (see S4, S5) — the flag exists but gates nothing. Running "in production" and "in development" currently behave identically from a security-posture standpoint.
- **Suggested fix:** Implement the environment gates described in S4/S5 so the existing `ASPNETCORE_ENVIRONMENT` value actually changes behavior.

### CFG3 — `.gitignore` doesn't cover the actual secret-bearing file
- **Location:** `.gitignore` (27 lines, root)
- **Severity:** High
- **Description:** The file covers `.env`/`.env.local` (lines 16-17), but no `.env` files exist anywhere in the repo — the real secret (S2) lives in `appsettings.json`, which has no exclusion rule at all and is tracked in git. There's also no `.env.example` documenting required configuration, and `Program.cs` never validates at startup that `ConnectionStrings:DefaultConnection` is actually present (it would currently null-reference deep inside Npgsql rather than failing fast with a clear message).
- **Suggested fix:** After removing the committed secret per S2, add a documented `appsettings.Local.json.example` (gitignored real file) or rely on environment variables, and add a startup check that fails fast with a clear error if the connection string is missing.

### CFG4 — Hardcoded absolute API URL breaks every non-local deployment
- **Location:** `frontend/src/api.js:1,5`
- **Severity:** High
- **Description:** `const BASE_URL = 'http://localhost:5000'` is baked into the JS bundle at build time (no `import.meta.env` usage anywhere in the frontend). In any real deployment — the docker-compose stack, staging, production — the browser making the request is not the machine running the backend, so every API call from the built app fails outright. This single hardcoded value is the reason the dev proxy (CFG5) and the nginx reverse-proxy config are both unreachable dead code.
- **Suggested fix:** Read the base URL from `import.meta.env.VITE_API_URL` with an empty-string/relative-path default (so requests go to `/api/...` and get proxied by nginx in prod / Vite in dev), and set `VITE_API_URL` per environment via `.env`/build args.

### CFG5 — Dead proxy configuration
- **Location:** `frontend/vite.config.js:8-13` (dev proxy), `frontend/nginx.conf:13-17` (prod reverse proxy)
- **Severity:** Low
- **Description:** Both proxies correctly forward `/api` to the backend, but neither is ever exercised because `api.js` always issues an absolute URL (CFG4) instead of a relative `/api/...` path.
- **Suggested fix:** Once CFG4 is fixed to use relative paths (or an env-driven origin), both proxy configs become live and functional as originally intended — no changes needed to `vite.config.js`/`nginx.conf` themselves.

### CFG6 — Stray crash-dump artifact left in the working directory
- **Location:** `sh.exe.stackdump` (repo root)
- **Severity:** Low
- **Description:** A shell crash-dump file, unrelated to the application, sits at the repo root. It is **not** tracked by git (`git ls-files` shows nothing for it) and is correctly excluded by `.gitignore:26` — so there is no actual repo-hygiene defect here. The gitignore rule is written as an exact filename (`/sh.exe.stackdump`) rather than a wildcard, which only excludes this one file and would silently let a differently-named crash dump slip in as an untracked-but-visible artifact.
- **Suggested fix:** Delete the local artifact (`rm sh.exe.stackdump`) as routine housekeeping, and broaden the gitignore rule to a wildcard (`*.stackdump`) so any future crash dump is caught regardless of name.

### CFG7 — EOL runtime and build-tool dependencies
- **Location:** `backend/BrownEvents.Api/BrownEvents.Api.csproj:4` (`<TargetFramework>net6.0</TargetFramework>`); `frontend/package.json:20` (`"vite": "^4.4.5"`)
- **Severity:** Medium
- **Description:** .NET 6 has been out of support since November 12, 2024 — no further security patches will ever be released for the runtime this app runs on. Vite 4.x is likewise superseded (the ecosystem has moved to 5.x/6.x/7.x, visible even in this repo's own `package-lock.json` transitive peer ranges) and its dev server has had published file-disclosure CVEs in older versions.
- **Suggested fix:** Plan a runtime upgrade to .NET 8 (current LTS) and Vite 5+/6+ as part of the stabilization work — this is infrastructure debt, not a quick patch, so it belongs on the backlog explicitly rather than being silently deferred.

### CFG8 — Unused dependencies
- **Location:** `backend/BrownEvents.Tests/BrownEvents.Tests.csproj:12` (`Moq`, version 4.20.69); `frontend/package.json:17-18` (`@types/react`, `@types/react-dom`)
- **Severity:** Low
- **Description:** `Moq` is referenced in the test project but never actually used — both existing test files (`ConferenceServiceTests.cs`, `SessionServiceTests.cs`) use a real `AppDbContext` with the EF Core in-memory provider, not mocks. The `@types/react*` packages are dead weight without TypeScript (see M10).
- **Suggested fix:** Remove `Moq` if mocking isn't adopted as a pattern, or start using it for the controller tests recommended in M3. Remove `@types/react*` unless a TypeScript migration is planned.

### CFG9 — CI has no frontend test/lint stage and no vulnerability scanning
- **Location:** `.gitlab-ci.yml:36-47` (`frontend:build` only builds, never tests or lints); no scanning stage exists for either `docker:backend` or `docker:frontend` (lines 49-74)
- **Severity:** Low
- **Description:** `backend:test` (lines 26-34) runs `dotnet test`, but there is no equivalent `frontend:test`/`frontend:lint` stage — there's currently nothing to run even if tests/linting were added, since `package.json` has no `test`/`lint` script (see M13). The Docker build/push stages also never scan the resulting images (e.g. Trivy/Grype) before pushing.
- **Suggested fix:** Once M13 (lint) and frontend testing tooling are added, wire a `frontend:test` and `frontend:lint` stage into the `test` stage group; add an image-scanning step before `docker push` in both `docker:*` jobs.

---

## What's clean

Worth noting explicitly so this audit doesn't read as one-sided: no `dangerouslySetInnerHTML`, `eval`, raw `innerHTML`, hardcoded API keys/secrets in frontend code, or `localStorage`/`sessionStorage` usage was found anywhere in `frontend/src` — there is no classic XSS or client-side secret-leak vector in this app. Dependency versions that *are* current (React 18.2, EF Core/Npgsql 6.0.22-25, Swashbuckle 6.5.0) are pinned exactly rather than left floating, and the two existing backend test files that do exist are meaningful (real DB assertions via EF Core InMemory, not trivial smoke tests).

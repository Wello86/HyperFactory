# Technical Research

**Task**: e2e testing playwright session registration search filter
**Generated**: 2026-09-16T00:00:00Z
**Research path**: filesystem

---

## 1. Original Context

Implement Playwright E2E Flow 2 (session detail -> register attendee via SessionRegisterModal -> verify registration) and Flow 3 (conference search -> apply keyword filter -> verify results), each with a failure case (e.g. empty email for Flow 2, no-results search for Flow 3). Reuse the e2e-foundation slice's Playwright config (e2e/playwright.config.ts), readiness-wait helper (e2e/wait-for-stack.mjs) and test:e2e npm script -- do not duplicate them. This is slice 2 of 3 of a split EPMCDMPTST-2330 (End-to-End Test Suite) task; slice 1 (e2e-foundation) already exists on this branch with a working e2e/ Playwright project and Flow 1 spec.

---

## 2. Codebase Findings

### Existing Implementations

- `e2e/playwright.config.ts` — Playwright config: `testDir: './tests'`, `baseURL: 'http://localhost:5173'`, single `chromium` project, `trace: 'on-first-retry'`. Already exists; slice must not modify or duplicate it.
- `e2e/wait-for-stack.mjs` — standalone readiness-poll helper (`waitForStack(url, {timeoutMs, intervalMs})`), used from `package.json`'s `test:e2e` script and covered by `e2e/tests/wait-for-stack.unit.spec.ts`. Already exists; reuse as-is.
- `e2e/package.json` — `"test:e2e": "docker-compose -f ../docker-compose.yml up -d && node wait-for-stack.mjs http://localhost:5173/ && playwright test"`. Single script; new specs just need to live under `e2e/tests/` to be picked up (`testDir: './tests'`, no explicit glob override seen).
- `e2e/tests/flow1-browse-conference.spec.ts` — Flow 1 (browse → detail → sessions) plus its failure case (non-existent conference id → `.error-container` / "Failed to load conference"). This is the pattern precedent for Flow 2/3: `page.goto`, wait for `.loading-container` to hide with a 15000ms timeout, then assert on visible text/roles — no `data-testid` attributes used anywhere.
- `frontend/src/pages/SessionDetailPage.jsx` — hosts the register/waitlist button (`Register for Session` / `Join Waitlist` button text depends on capacity vs. `registeredCount`), opens `SessionRegisterModal` in `isModalOpen` state, wires `handleSubmit` to `useSessionRegistration`'s `register`/`joinWaitlist`. On success (`success === true`) it closes the modal; on failure it keeps the modal open and renders `actionError` inside the modal (`error` prop). After a successful registration, `session.currentAttendeeRegistrationId` becomes truthy and the page renders a `badge badge--active` with text "You're registered" plus a "Cancel Registration" button, replacing the register button — this is the state Flow 2's happy path should assert against.
- `frontend/src/components/SessionRegisterModal.jsx` — presentational modal form: `First Name *` / `Last Name *` / `Email Address *` inputs (`name="firstName"|"lastName"|"email"`), all HTML5 `required`, email input has `type="email"`. Submit button text is `Register` (or `submitLabel` prop, e.g. `Join Waitlist`), loading label `Registering...`. Error message (from `error` prop) renders inside a `.error-container` inside the modal only when set. Close button is `✕` (`.modal__close`), clicking the overlay outside `.modal` also closes it.
- `frontend/src/hooks/useSessionRegistration.js` — `register(formData)`/`joinWaitlist(formData)` call `registerForSession`/`joinWaitlist` (frontend/src/api.js), store the attendee email in localStorage via `setStoredAttendeeEmail`, then reload the session (`load()`). On API error it sets `actionError` from `err.message` (server `detail` text) and returns `false`, leaving the modal open.
- `frontend/src/api.js` — `registerForSession(id, data)` POSTs `/api/sessions/{id}/register`; `request()` throws on non-2xx with `Error(detail || text || 'HTTP {status}')`. No client-side validation beyond the browser's native `required`/`type="email"` constraint validation on the `<form>` — an empty email submit will be blocked by the browser's built-in validation UI (no `onSubmit` call reaches React) rather than by an app-rendered error message, since there's no `noValidate` on the `<form>` in `SessionRegisterModal.jsx`.
- `frontend/src/pages/ConferenceListPage.jsx` — conference list with a search input (`aria-label="Search conferences"`, placeholder `Search by title or description`), plus `From date`/`To date` filters. `handleSearchChange` updates `search` state and resets `page` to 1; the `useEffect` calls `getConferences(page, PAGE_SIZE, { search, from, to })` on any of `[page, search, fromDate, toDate]` changing — filtering is server-side via query params, no debounce. Empty-result state renders `No conferences match your filters.` (plain text, no dedicated `.error-container`, distinct from an actual fetch error which renders `.error-container` / "Failed to load conferences"). Conference cards render inside `.card-grid .card` (per Flow 1's spec/selectors) via `ConferenceCard`.
- `frontend/src/api.js` — `getConferences(page, pageSize, {search, from, to, status})` builds `URLSearchParams` and calls `GET /api/conferences?...`; only params that are truthy are set (so an empty search string produces the unfiltered request).

### Architecture and Layers Affected

- **E2E test layer only** (`e2e/tests/*.spec.ts`): new Playwright specs, no application code changes anticipated by the existing pattern (Flow 1 added no `data-testid`s and made no frontend/backend edits).
- **Frontend UI it exercises**: `SessionDetailPage.jsx` + `SessionRegisterModal.jsx` + `useSessionRegistration.js` (Flow 2), and `ConferenceListPage.jsx` + `ConferenceCard.jsx` (Flow 3) — read-only from the test's perspective, driven purely through the rendered DOM.
- **Backend endpoints indirectly exercised** (no backend code read further than call sites): `GET /api/conferences?search=...`, `GET /api/sessions/{id}`, `POST /api/sessions/{id}/register`, `POST /api/sessions/{id}/waitlist` — these run against whatever data the `docker-compose.yml` stack seeds; Flow 2/3 depend on stack seed data containing at least one session with open capacity and at least one conference matching (and one keyword definitely not matching, for the no-results case).

### Integration Points

- Both new flows depend on the already-running Docker Compose stack (`docker-compose.yml` at repo root, `frontend` service on `localhost:5173`, proxying `/api/` to `backend`) — same readiness contract Flow 1 already relies on via `wait-for-stack.mjs`.
- No new npm dependencies found in `e2e/package.json` (only `@playwright/test`) — nothing to add for these two flows.

### Patterns and Conventions

- Test file naming: `flow<N>-<slug>.spec.ts` (e.g. `flow1-browse-conference.spec.ts`) — Flow 2/3 should follow `flow2-...spec.ts` / `flow3-...spec.ts`.
- Selector strategy: no `data-testid`; use visible text, ARIA roles/labels, and existing CSS class hooks (`.loading-container`, `.error-container`, `.card-grid .card`, `.modal`, `.badge--active`) — matches the two grep-confirmed conventions above (`aria-label="Search conferences"`, `.modal__title`, `badge--active`).
- Loading-state wait pattern: `await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })` before asserting on loaded content — used both on the list and detail pages in Flow 1; the same pattern applies to session detail load (`SessionDetailPage`'s own `.loading-container`).
- Two-test-per-flow structure (happy path + failure case) inside one `test(...)`-pair spec file, matching the task's stated shape.

---

## 3. Documentation Findings

### Guides and Architecture Docs

No `.ai-run/guides/testing/testing-patterns.md`-specific E2E content beyond backend xUnit conventions (guide covers backend testing, not Playwright); no dedicated E2E guide exists yet. `docs/superpowers/tasks/2026-09-16-e2e-foundation/spec.md` is the authoritative precedent for this split task's slice 1 and defines the conventions slice 2 must follow (see Section 6 of that document — non-goals explicitly exclude Flow 2/3 from slice 1, confirming this task is exactly the deferred scope).

### Architectural Decisions

- Slice 1's spec records the decision to keep `e2e/` as an independent top-level project (own `package.json`/toolchain, not colocated in `frontend/`) and to use a hand-rolled polling script rather than a Docker healthcheck — both decisions this slice must honor by reusing rather than re-deriving.
- Slice 1's spec explicitly flags that selectors must be confirmed by reading component source at implementation time rather than assumed — the same discipline applies to `SessionRegisterModal`/`ConferenceListPage` selectors used here (now confirmed above by direct reads).

### Derived Conventions

Derived from Flow 1's spec/code (no dedicated E2E style guide): keep specs additive-only, avoid introducing `data-testid`s or app code changes, use the existing `test:e2e` script unmodified, and structure each flow as one `.spec.ts` file with a happy-path test and a failure-case test.

---

## 4. Testing Landscape

### Existing Coverage

- `e2e/tests/flow1-browse-conference.spec.ts` covers browse → conference detail → sessions list rendering, plus a non-existent-conference-id failure case. No existing Playwright coverage of session registration or conference search/filtering.
- `e2e/tests/wait-for-stack.unit.spec.ts` — unit-style Playwright test of the readiness helper itself, unrelated to app flows.

### Testing Framework and Patterns

`@playwright/test` ^1.47.0, single Chromium project, `fullyParallel: true`. Pattern per Flow 1: `test('description', async ({ page }) => { await page.goto(...); await expect(locator).toBeVisible/toBeHidden(...); ... })`, using role-based locators (`getByRole('link', { name: ... })`) alongside CSS-class locators.

### Coverage Gaps

- No existing E2E coverage of the registration form, the register/waitlist button state transitions, or the "You're registered" post-registration state.
- No existing E2E coverage of the search/filter input on `ConferenceListPage`, nor of the "No conferences match your filters." empty-result state.
- No existing coverage of native HTML5 form-validation behavior (empty required email) in this app — Flow 2's failure case will be the first place this is exercised, and browser-native validation (no `noValidate` on the form) means the failure signal is a browser constraint-violation UI state, not an app-rendered error, which is a behavior detail the test needs to account for.

---

## 5. Configuration and Environment

### Environment Variables

None found specific to `e2e/` beyond what `docker-compose.yml` already defines for the stack (not re-read in full for this task; Flow 1's stack dependency is unchanged).

### Configuration Files

- `e2e/playwright.config.ts` — hardcoded `baseURL: 'http://localhost:5173'`, no env-var-driven config; new specs use relative `page.goto('/...')` paths against this baseURL, matching Flow 1.
- `e2e/tsconfig.json`, `e2e/.gitignore` — present, unmodified by Flow 1's addition; no action needed for new spec files (they land inside the existing `testDir`).

### Feature Flags and Deployment Concerns

None found. No CI wiring for `e2e/` exists yet (explicitly deferred to a later slice per Flow 1's spec non-goals) — this slice (2/3) is test-content-only, not CI wiring, consistent with the task description.

---

## 6. Risk Indicators

- Speculative: Flow 2's happy path needs a session in the seeded stack data with open capacity (registeredCount < capacity) so the "Register for Session" button (not "Join Waitlist") is shown; if the seed data's first/available session already has `currentAttendeeRegistrationId` set from a prior test run (state carried in the same running stack/DB), the button branch could differ across runs — test data assumptions should be made explicit in the spec/plan.
- Speculative: Flow 2's failure case (empty email) will surface as native browser constraint-validation (the `<form>` lacks `noValidate`), not as an app-rendered `.error-container` message — the test approach (e.g., checking `page.locator('input[name="email"]').evaluate(el => !el.checkValidity())` versus attempting a different failure trigger such as an obviously invalid but non-empty value that the backend rejects) is a design decision, not something already present in the code.
- Speculative: Flow 3's no-results case needs a keyword guaranteed not to match any seeded conference title/description; because filtering is server-side and un-debounced, timing/race assumptions when typing into the search input may need explicit waits.
- `useSessionRegistration`'s `register`/`joinWaitlist` persist the submitted email to `localStorage` (`setStoredAttendeeEmail`) and `getSession` is called with that stored email on load — repeated Flow 2 runs against a persistent browser context/stack could see different "already registered" state depending on Playwright's storage-state isolation, which is a cross-run reliability risk worth calling out for the plan.
- No CI wiring exists for `e2e/` yet — running these new specs locally still requires the full `docker-compose up` + `wait-for-stack` flow from `test:e2e`; slower/higher-friction verification loop than typical unit tests.

---

## 7. Summary for Complexity Assessment

This task adds two new Playwright spec files (`e2e/tests/flow2-*.spec.ts`, `e2e/tests/flow3-*.spec.ts`) to an already-scaffolded `e2e/` project; no changes to `playwright.config.ts`, `wait-for-stack.mjs`, or `package.json` are needed since the task explicitly requires reuse. The primary layer touched is the E2E test layer, driving existing frontend UI (`SessionDetailPage`/`SessionRegisterModal`/`useSessionRegistration` for Flow 2, `ConferenceListPage`/`ConferenceCard` for Flow 3) with no application code changes anticipated — the file-change surface is small (two new spec files, following the established one-happy-path-plus-one-failure-case pattern from Flow 1).

Technical novelty is low relative to slice 1 (the tooling/pattern precedent already exists and was directly read), but there are two behavior nuances not previously encountered in this test suite: native HTML5 form validation on the registration modal (no `noValidate`, so an empty-required-field submit is intercepted by the browser rather than producing an app error state) and server-side, undebounced conference search filtering with a distinct "no results" UI state (plain text) versus a fetch-error UI state (`.error-container`). Both are directly observable from source and documented above rather than assumed.

Test coverage posture: zero existing E2E coverage of registration or search/filter flows, so this is pure net-new coverage with no regression risk to existing specs. Key risk factors are test-data/state assumptions against the shared Docker Compose stack (session capacity/availability, search keyword uniqueness, and cross-run `localStorage`-persisted attendee email affecting the "already registered" state) — these are environmental/data concerns for the plan to address, not code-structure risks, and CI wiring remains explicitly out of scope for this slice.

---

## 8. External References

None named by the task. The task instead points to in-repo artifacts already covered above as findings: `e2e/playwright.config.ts`, `e2e/wait-for-stack.mjs`, and the `test:e2e` npm script (`e2e/package.json`), all of which were read directly rather than treated as an external source.

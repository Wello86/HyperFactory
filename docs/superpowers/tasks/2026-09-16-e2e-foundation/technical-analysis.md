# Technical Research

**Task**: E2E test suite foundation — Playwright config, docker-compose readiness helper, Flow 1 (browse conferences → detail → sessions)
**Generated**: 2026-09-16T00:00:00Z
**Research path**: filesystem

---

## 1. Original Context

Add a Playwright config (frontend/ or e2e/), a docker-compose readiness-wait helper to work around the frontend service's missing healthcheck, and implement E2E Flow 1: browse conferences -> open conference detail -> view sessions, including one failure case. Must run locally against `docker-compose up`. This is slice 1 of 3 (EPMCDMPTST-2330 End-to-End Test Suite, split due to XL complexity). No existing E2E or Playwright precedent exists in this repo.

---

## 2. Codebase Findings

### Existing Implementations

No Playwright setup, `e2e/` directory, or `playwright.config.*` exists anywhere in the repo (confirmed via `find` for `*playwright*`/`*e2e*` — no source matches). No `.gitlab-ci.yml` job exists outside the current `lint`/`build`/`test`/`docker` stages; this slice's scope is explicitly local-only (`docker-compose up`), so CI wiring is out of scope here (it belongs to a later slice per the parent ticket's technical-analysis at `docs/superpowers/tasks/2026-09-16-e2e-test-suite/technical-analysis.md`).

Frontend routes relevant to Flow 1 (`frontend/src/App.jsx:18-34`):
- `/` → `ConferenceListPage.jsx` — browse/search list, "Add Conference" link, `ConferenceCard` grid, `Pagination`
- `/conferences/:id` → `ConferenceDetailPage.jsx` — conference detail; composes `Navbar`, `ConferenceHeader`, `ConferenceInfoTable`, `ConferenceSessionsSection`, `RegistrationsSection`, `SelectedSessionCard`, `RegistrationModal`

`ConferenceListPage.jsx` (read in full):
- Renders a `<nav className="navbar">` with `BrownEvents` brand link, `Conferences`/`Speakers` nav links
- Loading state: `.loading-container` + `.spinner` + "Loading conferences..." text
- Error state: `.error-container` with "Failed to load conferences" heading and the error message
- Empty state: literal text "No conferences match your filters." when `!loading && !error && conferences.length === 0`
- Each conference rendered via `<ConferenceCard key={conf.id} conference={conf} />` inside `.card-grid`
- Fetches via `getConferences(page, PAGE_SIZE, { search, from, to })` from `../api`, `PAGE_SIZE = 20`

`ConferenceDetailPage.jsx` (read in full):
- Uses `useParams()` for `:id`, then `useConference(id)`, `useConferenceSessions(id, sessionsPage, 20)`, `useConferenceRegistrations(id)`, `useRegistrationForm(id)`
- Loading state: same `.loading-container`/`.spinner`/"Loading conference..." pattern
- Error state: same `.error-container` pattern, "Failed to load conference" heading
- Breadcrumb: `<Link to="/">Conferences</Link> / {conference?.title}`
- Sessions rendered via `<ConferenceSessionsSection conferenceId sessions sessionsPage ... onSessionClick={registrationForm.setSelectedSession} />` — this is the "view sessions" part of Flow 1
- `SelectedSessionCard` and `RegistrationModal` are session/registration-flow concerns (Flow 2 in the parent ticket), not needed for this slice's Flow 1 scope beyond not breaking navigation

`ConferenceCard.jsx`, `ConferenceSessionsSection.jsx`, `SessionCard.jsx`, `SessionList.jsx` were located but not read in full; they are the direct rendering targets for "browse conferences" and "view sessions" assertions and warrant a read during spec/plan/implementation to confirm exact link hrefs and visible text/attributes for selectors.

### Architecture and Layers Affected

This task adds a **new top-level layer** (E2E test tooling) that sits outside the existing backend/frontend/CI layering described in `.ai-run/guides/architecture/architecture.md`:
- **New test-project layer**: Playwright config + spec files, location choice open (`frontend/` colocated with the Vite/Vitest project, or a new top-level `e2e/` directory) — no existing precedent favors either.
- **New local tooling layer**: a docker-compose readiness-wait script/helper (shell script, Node script, or Playwright global-setup hook) that polls the frontend/backend until actually serving before tests start.
- No production `backend/` or `frontend/src` code changes are required by this slice's stated scope — this is additive tooling only.

### Integration Points

- `docker-compose.yml` (root, read in full) — three services:
  - `postgres` (image `postgres:14`, host port 5433→5432, **has** healthcheck via `pg_isready -U brown -d brownevents`, 5s interval/timeout, 10 retries)
  - `backend` (build from `./backend/BrownEvents.Api/Dockerfile`, host port 5000→80, `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection` inline, `depends_on: postgres: condition: service_healthy` — **no healthcheck of its own**)
  - `frontend` (build from `./frontend`, host port 5173→80, `depends_on: backend` with **no condition** (defaults to `service_started`, not health/readiness) — **no healthcheck defined at all**
- This confirms the task's premise precisely: `frontend` has no healthcheck, and its `depends_on: backend` only waits for the backend *container* to start, not for the ASP.NET Core app inside it to finish booting and accept requests — so `docker-compose up` can report all services "Up" before the app is actually serving traffic.
- `frontend/nginx.conf` (read in full) — serves the SPA build (`try_files ... /index.html`), proxies `/api/` to `http://backend:80/api/` inside the container. Tests should navigate against the frontend's exposed port (`http://localhost:5173`) to exercise the real proxy path, not hit the backend port directly.
- `frontend/Dockerfile` (read in full) — two-stage build: `node:18-alpine` builds via `npm ci && npm run build`, then `nginx:alpine` serves `dist/` on port 80. No `HEALTHCHECK` instruction in the Dockerfile itself either.

### Patterns and Conventions

- No `data-testid` attributes observed in `ConferenceListPage.jsx` or `ConferenceDetailPage.jsx` — selectors will need to rely on `aria-label` (e.g. `aria-label="Search conferences"`), visible text/headings (e.g. "Failed to load conferences", "No conferences match your filters."), or ARIA roles/links.
- Frontend's own conventions (`hooks/` + `api.js` + `pages/`) are irrelevant to E2E — Playwright drives the browser/DOM, not app internals.
- No existing precedent in this repo for a "wait for service ready" script of any kind — `docker-compose.yml`'s only readiness mechanism today is Postgres's `pg_isready` healthcheck.

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/testing/testing-patterns.md` — backend xUnit conventions only ("no frontend test runner exists" — stale, since Vitest is in fact wired into CI); zero mention of E2E/Playwright.
- `.ai-run/guides/quality-gates.md` — 4 stages (lint/build/test/docker), no E2E gate. States Docker jobs (`docker:backend`/`docker:frontend`) "publish, don't verify" and only run `only: main` — not applicable as a pattern to copy since this slice is local-only, not a CI job.
- `.ai-run/guides/architecture/architecture.md` — confirms no auth layer, `EnsureCreated()` (no EF migrations, fresh schema each boot), hardcoded DB credentials in `docker-compose.yml`.
- `docs/superpowers/tasks/2026-09-16-e2e-test-suite/technical-analysis.md` — prior research for the parent ticket (EPMCDMPTST-2330) before it was split into 3 slices; covers all three flows, the docker-compose readiness gap, and CI integration considerations. Confirms: no Playwright toolchain installed anywhere in `frontend/package.json`; no `.env`/`.env.example` in the repo; CI job design (a later slice's concern) would follow the `{area}:{stage}` naming convention (`docker:backend`, `frontend:test`) and could reuse the `docker:28.5.2`+`dind` pattern for CI-side compose orchestration.

### Architectural Decisions

None found specific to E2E testing (no ADRs exist in this repo). The parent-ticket analysis notes the `Database.IsRelational()` guarded-transaction pattern (`RegistrationService.cs:23-25`) as a candidate for a future E2E failure-case test against real Npgsql — not relevant to this slice's Flow 1 (read-only browse/detail/sessions), which has no transaction path to exercise.

### Derived Conventions

- Branch naming: `EPMCDMPTST-<ticket#>-<Description>` per `.ai-run/guides/standards/git-workflow.md`.
- No repo convention yet for E2E test file naming/location — this slice is establishing that convention from scratch.

---

## 4. Testing Landscape

### Existing Coverage

- Backend: xUnit suite in `backend/BrownEvents.Tests/`, entirely against EF InMemory — irrelevant to this slice (no backend code changes).
- Frontend: Vitest tests exist for Speaker pages and utilities (`SpeakerCreatePage.test.jsx`, `SpeakerEditPage.test.jsx`, `SpeakerListPage.test.jsx`, `SpeakerProfilePage.test.jsx`, `api.test.js`); no test file exists for `ConferenceListPage.jsx` or `ConferenceDetailPage.jsx` — the exact pages this slice's Flow 1 touches.
- No E2E test of any kind exists anywhere in the repo today; this slice is the first.

### Testing Framework and Patterns

- Frontend unit tests: Vitest 0.34.6 + `@testing-library/react` + jsdom (component-level, not applicable to browser E2E).
- Playwright is not installed as a dependency anywhere in the repo (`frontend/package.json` devDependencies confirmed above) — a wholly new toolchain addition for this slice.

### Coverage Gaps

- Zero existing automated coverage (unit or E2E) for `ConferenceListPage.jsx` or `ConferenceDetailPage.jsx` — Flow 1's happy path and failure case will be the first automated verification of this browse/detail/sessions journey.
- No prior "wait for stack ready" tooling exists to model the readiness-wait helper on.

---

## 5. Configuration and Environment

### Environment Variables

No `.env`/`.env.example` files exist in the repo. `ASPNETCORE_ENVIRONMENT` and `ConnectionStrings__DefaultConnection` are set directly in `docker-compose.yml` for the `backend` service; no environment variables are defined for the `frontend` service.

### Configuration Files

- `docker-compose.yml` (root) — the stack this slice's Flow 1 must run against locally: `postgres:14` (healthchecked), `backend` (depends on postgres health, no own healthcheck), `frontend` (depends on backend container-start only, no healthcheck, nginx-served static build on port 5173).
- `frontend/nginx.conf` — SPA fallback + `/api/` reverse proxy to backend; base URL for Playwright navigation should be `http://localhost:5173`.
- `frontend/package.json` — existing `scripts` are `dev`/`build`/`preview`/`lint`/`lint:fix`/`format`/`format:check`/`test`/`test:coverage`; no `e2e`/`test:e2e` script exists yet.

### Feature Flags and Deployment Concerns

No feature flags anywhere in the codebase. The core deployment concern this slice must address locally: `frontend`'s `depends_on: backend` has no health-check condition and the service itself has no healthcheck, so `docker-compose up` can report the stack "Up" before the backend API (and thus the data the frontend needs to render conferences) is actually ready — this is the exact gap the readiness-wait helper is scoped to work around.

---

## 6. Risk Indicators

- No Playwright toolchain, config, or E2E precedent exists in this repo — this slice introduces the tooling from scratch with nothing in-repo to imitate.
- `docker-compose.yml`'s `frontend` service has no healthcheck and only `depends_on: backend` at container-start granularity (not HTTP readiness) — a readiness-wait helper that just checks "container running" rather than "backend/frontend actually serving" will not close this gap; it likely needs to poll an HTTP endpoint (e.g. the frontend root or `/api/...` proxy) until a 200 response, not just container status.
- No `data-testid` attributes exist on `ConferenceListPage.jsx` or `ConferenceDetailPage.jsx` — Playwright selectors must rely on `aria-label`/visible text/roles, which is more brittle to future copy changes.
- Zero existing test coverage (unit or E2E) on `ConferenceListPage` or `ConferenceDetailPage` — this Flow 1 suite is the first automated check of this journey, so any latent bugs may surface as new E2E failures rather than regressions.
- Speculative: the failure case for Flow 1 is unspecified by the task beyond "including one failure case" — candidates observed in the code are the conference-list error state (`.error-container`, "Failed to load conferences") and the conference-detail error state (`.error-container`, "Failed to load conference"), but which one (or how to trigger it against a live stack, e.g. a bad conference ID) is a design decision for spec/plan, not a discovered fact.
- Speculative: whether the Playwright config lives in `frontend/` (colocated, sharing `package.json`/`node_modules`) or a new top-level `e2e/` directory is an open choice the task explicitly defers ("frontend/ or e2e/") — this affects how the readiness-wait helper and `docker-compose up` are invoked (e.g. `npm` script vs. standalone script) and should be decided in planning, not assumed here.

---

## 7. Summary for Complexity Assessment

This slice introduces Playwright and the entire E2E testing layer to a repository with zero existing precedent — no config, no `e2e/` directory, no prior browser-automation dependency anywhere in `frontend/package.json`. The concrete deliverables are: a Playwright config (location undecided between `frontend/` and a new `e2e/`), a standalone docker-compose readiness-wait helper that must poll for actual HTTP readiness (not container status, since neither `backend` nor `frontend` has a healthcheck and `frontend`'s `depends_on: backend` is container-start-only), and one E2E spec file implementing Flow 1 (browse `ConferenceListPage` → open `ConferenceDetailPage` → view sessions via `ConferenceSessionsSection`) plus one failure case. No backend or frontend production code changes are implied — this is additive tooling that runs against the already-implemented, already-working browse/detail/sessions journey.

Technical novelty is high relative to this codebase's conventions: nothing in the repo demonstrates polling a service for readiness, and no test file anywhere touches a live docker-compose stack (existing xUnit/Vitest suites run against EF InMemory or jsdom, not real containers). Test coverage posture is a genuine risk multiplier — `ConferenceListPage.jsx` and `ConferenceDetailPage.jsx` have no existing automated coverage of any kind, and neither carries `data-testid` attributes, so selectors must be built from `aria-label`/visible text observed directly in the source (documented above) rather than a stable test-ID convention. Key risk factors for planning: the readiness-wait helper's poll target and mechanism (HTTP-based, not container-status-based) is the crux of the "workaround" the task describes; the failure-case scenario and the config's home directory are both open decisions the task defers to design rather than facts already present in the codebase.

---

## 8. External References

None named by the task.

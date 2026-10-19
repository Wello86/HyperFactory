# Technical Research

**Task**: frontend testing coverage ci
**Generated**: 2026-09-16
**Research path**: filesystem

---

## 1. Original Context

Ticket EPMCDMPTST-2331 / BEVN-206: "Frontend Has No Test Coverage Enforcement"

Goal: The frontend has a growing test suite, but there is no mechanism to prevent new code from
shipping without tests.

Context: Coverage has never been measured or tracked in CI, and pull requests can merge without
any test coverage for the code they introduce.

Acceptance Criteria:
- Code coverage is collected and reported in CI on every merge request
- Merge requests that introduce new uncovered code fail the CI pipeline
- A coverage report is available as a CI artifact and visible in the merge request view
- The baseline reflects the current state of the codebase — pre-existing uncovered code does not cause failures
- Coverage collection does not significantly increase CI pipeline duration

---

## 2. Codebase Findings

### Existing Implementations

There is **no frontend test runner, test file, or coverage tool present anywhere in the repo**.

- `frontend/package.json` — `scripts` contains only `dev`, `build`, `preview`, `lint`, `lint:fix`,
  `format`, `format:check`. No `test` script.
- `frontend/package.json` `devDependencies` — `@vitejs/plugin-react`, `eslint` (+ plugins),
  `prettier`, `vite@^4.4.5`. No `vitest`, `jest`, `@testing-library/*`, `c8`, `nyc`, `istanbul`, or
  any coverage/diff-coverage package.
- `find frontend -iname "*.test.*" -o -iname "*.spec.*"` (excluding `node_modules`) returned **no
  matches** — zero existing test files under `frontend/src`.
- `frontend/vite.config.js` — a bare Vite config (`@vitejs/plugin-react` plugin, dev server proxy
  to `http://localhost:5000/api`). No `test:` block (the block Vitest reads when co-located in
  `vite.config.js`).
- `frontend/src` structure: `components/` (13 files), `hooks/` (6 files), `pages/` (9 files),
  `utils/` (2 files), plus `App.jsx`, `main.jsx`, `api.js`. All `.jsx`/`.js`, no `.ts`/`.tsx`.

### Architecture and Layers Affected

- **Build/tooling layer only** — this task does not touch application code, it adds a new
  toolchain (test runner + coverage collector + diff-coverage gate) and CI configuration.
- CI pipeline layer: `.gitlab-ci.yml`, currently 4 stages — `lint → build → test → docker`. The
  `test` stage today contains only `backend:test` (dotnet test); there is no `frontend:test` job.
- `frontend:lint` (stage `lint`) and `frontend:build` (stage `build`) are the two existing frontend
  jobs, both `image: node:18-alpine`, both run `npm ci` then their command, both cache
  `frontend/node_modules/` under cache key `npm-packages`.

### Integration Points

- GitLab CI/MR integration: the acceptance criteria "coverage report … visible in the merge
  request view" and "artifact" map directly to GitLab's built-in MR coverage widgets, which read
  two distinct signals from a job:
  - a job-level `coverage:` regex (surfaces the percentage badge on the MR/pipeline) — requires the
    test command to print a coverage summary line to stdout.
  - `artifacts: reports: coverage_report: { coverage_format: cobertura, path: ... }` — surfaces an
    inline "Test coverage" annotation on changed lines in the MR Changes tab. This requires a
    Cobertura-format XML report as an artifact.
  Neither mechanism is configured anywhere in `.gitlab-ci.yml` today (repo-wide `grep -rn coverage
  .gitlab-ci.yml` found nothing outside `node_modules`).
- `lefthook.yml` pre-commit hook only runs `frontend-lint` (ESLint + `format:check`) on
  `frontend/**/*.{js,jsx}` — no test/coverage hook exists locally either.
- No `frontend/README.md`/testing doc references a test runner.

### Patterns and Conventions

- Backend precedent (`backend/BrownEvents.Tests`) uses xUnit with **no coverage collector
  configured either** (`.ai-run/guides/testing/testing-patterns.md` states this explicitly) — so
  there is no in-repo precedent for coverage tooling or CI coverage gating to mirror; this is a
  greenfield addition for the whole repo, not just the frontend.
  the pattern must be introduced fresh.
- Frontend code is plain JS/JSX with Vite 4 + React 18 + React Router 6 — a Vitest-based setup
  (Vite-native, jsdom environment, `@vitest/coverage-v8` or `@vitest/coverage-istanbul`) is the
  natural fit for this toolchain, but no such choice exists in the repo yet; it will need to be
  introduced as part of implementation, not discovered.

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/testing/testing-patterns.md` — covers **backend only**; its own header states
  "Project: BrownEvents (backend only — no frontend test runner exists)". Its "Coverage" section:
  "No coverage tool configured — no `coverlet`, no `--collect:\"XPlat Code Coverage\"`, no
  threshold. `.gitlab-ci.yml:48-54` runs tests with console logging only." This guide will need a
  frontend-testing counterpart section/file once a runner exists (a plan concern, not a research
  finding).
- `.ai-run/guides/quality-gates.md` — "Not Configured" table explicitly lists "Coverage threshold |
  None enforced — `dotnet test` runs without a coverage collector or minimum" and lists Frontend
  Lint/Build as the only two frontend gates; no frontend test gate row exists at all.
- `.ai-run/guides/architecture/architecture.md` — documents `frontend/src` layering
  (`pages/ → hooks/ → api.js`) and lists the error boundary and hooks as the only architectural
  components called out; no testing-related content.

### Architectural Decisions

None found. No ADR files, no inline `NOTE:`/`HACK:`/`DECISION:` markers referencing testing or
coverage in the frontend source (grep across `frontend/src` for these markers and for
`coverage`/`vitest`/`jest` returned nothing outside `node_modules`).

### Derived Conventions

- CI job naming convention observed: `{area}:{stage}` (e.g. `frontend:lint`, `frontend:build`,
  `backend:test`) — a new job would follow as `frontend:test`.
- Frontend CI jobs consistently: `image: node:18-alpine`, `cd frontend && npm ci && <command>`,
  cache `frontend/node_modules/` under key `npm-packages`. Any new frontend CI job should match
  this shape for consistency and cache reuse.
- Stage ordering is fastest-to-slowest per `quality-gates.md`; a coverage-collecting test job
  belongs in the existing `test` stage (currently only `backend:test` runs there).

---

## 4. Testing Landscape

### Existing Coverage

None. Zero frontend test files exist; there is nothing today measuring frontend coverage in any
form.

### Testing Framework and Patterns

No frontend testing framework is installed or configured. (Backend uses xUnit + EF InMemory, per
`.ai-run/guides/testing/testing-patterns.md`, but that is a separate stack/project and not directly
reusable for the frontend.)

### Coverage Gaps

Total — every component (`components/`, `hooks/`, `pages/`, `utils/`, `App.jsx`, `api.js`) has no
test coverage. This is the exact condition the ticket's "baseline reflects current state" criterion
is designed to handle: a diff-coverage gate scoped to changed lines only would not fail on this
pre-existing gap, but a whole-repo coverage threshold would fail immediately upon introduction.

---

## 5. Configuration and Environment

### Environment Variables

None found referencing testing/coverage in `frontend/` (no `.env`, `.env.example`, or `import.meta.env` usage tied to test/coverage config).

### Configuration Files

- `frontend/vite.config.js` — the file where a Vitest `test` block would be added if Vitest is
  adopted (no such block exists today).
- `frontend/package.json` — no `test` script or coverage-related devDependency exists today.
- `.gitlab-ci.yml` — single pipeline file, 4 stages, no frontend test job and no `coverage:`/
  `artifacts.reports.coverage_report` keys anywhere in the file.
- `.editorconfig`, `frontend/.eslintrc.cjs` — exist and govern lint/format only, unrelated to
  coverage.
- `lefthook.yml` — local pre-commit hooks, frontend hook covers lint/format only, no test hook.

### Feature Flags and Deployment Concerns

No feature flags in this domain. `docker:frontend` job (stage `docker`) only runs `only: [main]`,
building `frontend/Dockerfile`; it is unaffected by adding a `test`-stage job upstream of it in the
pipeline, but a new frontend:test job would need to not block or slow `docker:frontend` beyond
existing stage ordering.

---

## 6. Risk Indicators

- **No test runner exists at all** — this task's true prerequisite is standing up a frontend test
  runner (e.g. Vitest) from zero, not just wiring coverage into an existing suite. Speculative:
  the diff-coverage gate has nothing to enforce until at least a minimal set of tests exists, so
  the initial CI run may pass trivially with 0% new-code coverage unless the tooling choice
  includes writing example/seed tests.
- Speculative: achieving "coverage report visible in the MR view" per GitLab's built-in widget
  requires a Cobertura-format artifact (`artifacts.reports.coverage_report`) — the coverage tool
  chosen must be able to emit Cobertura, not just its native (e.g. V8/Istanbul JSON, lcov) format;
  this constrains tool selection but is not yet decided in this repo.
- Speculative: enforcing "new/changed lines only" coverage (not whole-codebase) is not a stock
  GitLab feature by itself — it typically needs a diff-coverage tool (e.g. `diff-cover`,
  Vitest+lcov piped through a diff-coverage script) run against a base ref, which is a new
  dependency/script not present anywhere in this repo today.
- No backend precedent for coverage tooling either — the team has never configured a coverage
  threshold gate in this CI pipeline (`quality-gates.md` explicitly calls this out for both
  backend and frontend), so there is no in-repo pattern to copy; conventions must be established
  from scratch.
- `.ai-run/guides/testing/testing-patterns.md` and `.ai-run/guides/quality-gates.md` will become
  stale/incomplete once frontend testing is introduced — both explicitly assert "no frontend test
  runner exists," and neither will reflect the new job/tooling without a follow-up guide update.
- CI duration risk: acceptance criteria demands coverage collection not "significantly increase"
  pipeline duration, but there is no existing frontend:test job or baseline to measure against —
  any added job is net-new pipeline time, not a regression against a prior number.
- Node 18 (`node:18-alpine`) is the current CI frontend image; whichever coverage/test tool is
  chosen must be compatible with Node 18.

---

## 7. Summary for Complexity Assessment

This task starts from true greenfield for frontend testing: there is no test runner, no test file,
no coverage tool, and no CI test job for the frontend anywhere in the repository today (confirmed
by an empty `find` for `*.test.*`/`*.spec.*` under `frontend/src`, an absent `test` script in
`frontend/package.json`, and the testing/quality-gates guides explicitly stating "no frontend test
runner exists"). The work touches two layers only: the frontend build/tooling layer (installing a
test runner, e.g. Vitest, and coverage collector, plus at minimum some example tests to exercise
the pipeline) and the CI configuration layer (`.gitlab-ci.yml`, adding a `frontend:test` job in the
existing `test` stage, wired to GitLab's `coverage:` regex and `artifacts.reports.coverage_report`
Cobertura mechanism for MR visibility).

The primary complexity driver is not any single file change but the number of independent new
concerns bundled together: choosing and installing a test framework, achieving a Cobertura-
compatible coverage report format for GitLab's native MR widget, and implementing a diff-coverage
gate scoped to new/changed lines (which is not a stock GitLab or Vitest feature and will likely
require an added tool/script such as `diff-cover` plus a baseline reference point) — all while
explicitly not failing on the 100% pre-existing uncovered codebase. There is zero existing test
coverage to build on and zero in-repo precedent for coverage tooling on either the frontend or
backend side, so every convention (job naming follows the existing `{area}:{stage}` pattern seen in
`frontend:lint`/`frontend:build`, and node:18-alpine/`npm ci` caching conventions apply) must be
established without an internal template beyond the existing two frontend CI jobs to copy the
shape of.

---

## 8. External References

None named by the task.

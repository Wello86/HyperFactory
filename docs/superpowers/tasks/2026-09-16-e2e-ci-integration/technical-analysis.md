# Technical Research

**Task**: gitlab-ci docker-compose playwright e2e
**Generated**: 2026-09-16T00:00:00Z
**Research path**: filesystem

---

## 1. Original Context

Wire the Playwright E2E suite (e2e/) into GitLab CI as a new blocking job: run docker-compose up, wait-for-stack readiness check, then playwright test; capture failure screenshots as CI artifacts. Slice 3 of 3 in split EPMCDMPTST-2330 (End-to-End Test Suite). Slice 1 (e2e-foundation: Playwright config at e2e/playwright.config.ts, readiness helper at e2e/wait-for-stack.mjs, Flow 1 spec) and slice 2 (e2e-remaining-flows: Flow 2 + Flow 3 specs) are already merged to this branch. e2e/package.json has a test:e2e script that runs docker-compose up -d, wait-for-stack, then playwright test.

---

## 2. Codebase Findings

### Existing Implementations

- `/Users/sergey_grigorash/dev/shazam/e2e/package.json` — `test:e2e` script: `docker-compose -f ../docker-compose.yml up -d && node wait-for-stack.mjs http://localhost:5173/ && playwright test`. Only devDependency is `@playwright/test@^1.47.0`.
- `/Users/sergey_grigorash/dev/shazam/e2e/playwright.config.ts` — `testDir: './tests'`, `fullyParallel: true`, `baseURL: 'http://localhost:5173'`, `trace: 'on-first-retry'`, single `chromium` project. No `screenshot:` option configured yet (Playwright's default is `screenshot: 'off'`; `trace: on-first-retry` is set but screenshots are not).
- `/Users/sergey_grigorash/dev/shazam/e2e/wait-for-stack.mjs` — exported `waitForStack(url, { timeoutMs, intervalMs })`, polls `fetch(url)` until a 2xx/3xx response or timeout; also runnable as a CLI (`node wait-for-stack.mjs <url> [timeoutMs] [intervalMs]`), used exactly this way in `test:e2e`.
- `/Users/sergey_grigorash/dev/shazam/e2e/tests/` — `flow1-browse-conference.spec.ts`, `flow2-session-registration.spec.ts`, `flow3-conference-search.spec.ts` (each one happy-path + one failure-case test), plus `wait-for-stack.unit.spec.ts` (a Playwright-test-runner-based unit test of the helper itself, not a browser E2E test — it also lives in `testDir`, so it will run as part of `playwright test` in CI).
- `/Users/sergey_grigorash/dev/shazam/e2e/.gitignore` — ignores `node_modules/`, `test-results/`, `playwright-report/`, confirming these are Playwright's default output directories (not currently produced/consumed anywhere else in the repo).
- `/Users/sergey_grigorash/dev/shazam/docker-compose.yml` (repo root) — three services: `postgres` (healthcheck via `pg_isready`, ports 5433:5432), `backend` (`depends_on: postgres: condition: service_healthy`, port 5000:80, built from `./backend/BrownEvents.Api/Dockerfile`), `frontend` (`depends_on: [backend]` with **no healthcheck condition** — only waits for container start, not backend readiness; port 5173:80, built from `./frontend`). This is the compose file `test:e2e` references via `-f ../docker-compose.yml`.
- `/Users/sergey_grigorash/dev/shazam/.gitlab-ci.yml` (repo root) — current stages: `lint → build → test → docker`. No `e2e` job or stage exists yet. `backend:test` and `frontend:test` run in the `test` stage; `frontend:test` is gated with `rules: changes: frontend/**/*`. `docker:backend`/`docker:frontend` (stage `docker`) build/push images using a `docker:28.5.2` image with `docker:28.5.2-dind` service and `DOCKER_HOST`/`DOCKER_TLS_CERTDIR`/`DOCKER_DRIVER` variables — this is the only existing pattern in the file for running actual Docker/`docker-compose` commands inside a CI job (currently only for `docker build`/`push`, not `docker-compose up`), and it is restricted `only: - main`.

### Architecture and Layers Affected

- CI/pipeline configuration layer: `.gitlab-ci.yml` — needs a new job (and possibly stage) that isn't present today.
- No application code layers (API/service/repository/frontend components) are touched by this slice per the task description — it is CI wiring only, consuming the already-merged `e2e/` suite as-is.

### Integration Points

- The new CI job must invoke `docker-compose -f docker-compose.yml up` (root-level compose file) inside the GitLab runner — the only existing CI precedent for Docker-in-CI is the `docker:backend`/`docker:frontend` jobs' `docker:28.5.2` image + `docker:28.5.2-dind` service pattern, which is currently used for `docker build`/`push`, not for running `docker-compose up` or `docker-compose down`; `docker-compose` binary availability inside that image is not verified in the repo.
- `wait-for-stack.mjs` is a plain Node script (`node wait-for-stack.mjs <url>`) — requires a Node runtime inside the job (separate from the `docker:28.5.2` image used for build/push jobs).
- `playwright test` requires Playwright's browser binaries (`@playwright/test`), which are not currently installed/cached anywhere in CI — no existing job installs Playwright or its browsers.
- Screenshot capture on failure requires either enabling `screenshot: 'only-on-failure'` (or similar) in `playwright.config.ts`, or relying on Playwright's default `test-results/` output directory, then wiring that directory into `.gitlab-ci.yml` `artifacts:` — no existing job in `.gitlab-ci.yml` publishes artifacts conditionally `on: failure` (the only `artifacts:` blocks today are `backend:build`, `frontend:build`, and `frontend:test`'s coverage report, all unconditional).

### Patterns and Conventions

- Every existing CI job runs from a single official language image (`mcr.microsoft.com/dotnet/sdk:10.0`, `node:18-alpine`) with `script:` steps; only the `docker` stage jobs use a `services:` block (dind) for actual Docker access.
- `cache:` blocks keyed by dependency manager (`dotnet-packages`, `npm-packages`) are used consistently for restore-heavy jobs — no `e2e`-specific cache exists yet.
- `rules: changes:` is used once (`frontend:test`) to scope a job to relevant file changes; `only: - main` is used for the `docker` stage jobs. No job in the file is currently unconditionally blocking on `main` and every branch equally.
- No `.gitlab-ci.yml` job currently declares `allow_failure: false` explicitly (default GitLab behavior already blocks on failure) or marks artifacts `when: on_failure`.

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/quality-gates.md` — documents current stages (`lint → build → test → docker`) and every existing gate's run/pass/fail behavior; explicitly lists an "Not Configured" table (type-check, secret scanning, dependency scanning, backend coverage threshold) but has **not yet been updated** to mention any `e2e` gate — this doc predates slice 3 and will need updating once the CI job lands (that update is out of scope for this research, noted only as an existing-state fact).
- `.ai-run/guides/standards/git-workflow.md` — branch/commit conventions (`EPMCDMPTST-<ticket#>-<Description>` branches, `EPMCDMPTST-<ticket#>: <Description>` commits); no CI-specific content.
- Sibling task directories for the same split ticket contain prior artifacts directly relevant to this slice's scope:
  - `/Users/sergey_grigorash/dev/shazam/docs/superpowers/tasks/2026-09-16-e2e-test-suite/complexity-assessment.json` — the original (pre-split) complexity assessment (score 27, XL, `split-required`) and its `split_recommendation.slices` array, which defines this exact slice (`e2e-ci-integration`) as: "Add an e2e job to the test stage in .gitlab-ci.yml that builds/starts the docker-compose stack, waits for readiness, runs the full Playwright suite, uploads failure screenshots as on-failure artifacts, and blocks merge on failure," with `depends_on: "Registration and search/filter flows"`.
  - `/Users/sergey_grigorash/dev/shazam/docs/superpowers/tasks/2026-09-16-e2e-foundation/spec.md` — slice 1 spec; documents the `frontend` service's missing healthcheck/readiness gap as the reason `wait-for-stack.mjs` exists, and explicitly scopes slice 1 as "No CI wiring (GitLab job, pipeline stage) — later slice" and "No Docker/Dockerfile HEALTHCHECK additions — the readiness gap is worked around at the test-tooling layer (polling), not fixed at the compose/image level."
  - `/Users/sergey_grigorash/dev/shazam/docs/superpowers/tasks/2026-09-16-e2e-remaining-flows/spec.md` — slice 2 spec; confirms Flow 2/Flow 3 specs, reiterates "No CI wiring — deferred to slice 3," and confirms no changes were made to `playwright.config.ts`, `wait-for-stack.mjs`, or `package.json` in slice 2 (so slice 1's originals are exactly what slice 3 inherits).

### Architectural Decisions

- Slice 1's spec records the decision to keep `e2e/` as an independent top-level directory/toolchain (not colocated in `frontend/`) specifically to avoid coupling E2E tooling to the app's Vite/Vitest build — relevant context for how the CI job should invoke it (as its own working directory, own `npm ci`, not folded into `frontend:*` jobs).
- Slice 1's spec explicitly deferred fixing the frontend healthcheck gap at the infrastructure level, instead handling it via `wait-for-stack.mjs` polling — meaning the CI job inherits this same polling-based readiness strategy rather than a compose-native healthcheck wait.

### Derived Conventions

- CI jobs in this repo are one-image-one-script; Docker-in-Docker access is only ever granted via the `docker:28.5.2` image + `dind` service pattern seen in the `docker` stage — any job needing to run `docker-compose` will likely need to adopt (or adapt) that same pattern, since no lighter-weight docker-compose-in-CI precedent exists.
- Artifact patterns in `.gitlab-ci.yml` (`backend:build`, `frontend:build`, `frontend:test`) are always unconditional `paths:` (and one `reports:` block); none use `when: on_failure`, so on-failure-only screenshot artifacts would be a new pattern in this file, not a reused one.

---

## 4. Testing Landscape

### Existing Coverage

- `e2e/tests/flow1-browse-conference.spec.ts`, `flow2-session-registration.spec.ts`, `flow3-conference-search.spec.ts` — one happy path + one failure case each, already merged and runnable locally via `npm run test:e2e` from `e2e/`.
- `e2e/tests/wait-for-stack.unit.spec.ts` — unit-style test of `waitForStack()` using Playwright's test runner directly (spins up a local `http` server rather than the real stack); this file lives inside `testDir: './tests'`, so a plain `playwright test` invocation in CI will run it alongside the three browser flows.
- `backend:test` (xUnit) and `frontend:test` (Vitest) are unrelated to `e2e/` and run in separate, pre-existing `test`-stage jobs.

### Testing Framework and Patterns

- E2E: `@playwright/test` 1.47, single `chromium` project, `fullyParallel: true`, `trace: 'on-first-retry'`. No screenshot-on-failure setting configured today.
- No fixtures/factories/mocks beyond the flows' own randomized-data conventions described in `e2e-remaining-flows/spec.md` (e.g. timestamp-suffixed emails to avoid collisions on the persistent stack).

### Coverage Gaps

- No existing CI job runs the Playwright suite at all — this is the entire gap this slice closes.
- No existing job installs Playwright browsers/dependencies, nor caches them, in CI.
- No existing GitLab CI pattern for `docker-compose up`/`down` lifecycle management (start, wait, run, teardown) — only single `docker build`/`push` commands exist today.
- No verification exists (in the repo) that the `docker:28.5.2` CI image or its `dind` service has `docker-compose` installed/available — this is an open unknown rather than a confirmed capability.

### TDD/BDD Conventions

- Flow specs follow a consistent one-happy-path/one-failure-case structure per file; no BDD framework (e.g. Cucumber) in use.

---

## 5. Configuration and Environment

### Environment Variables

- No `e2e`-specific environment variables exist today — `baseURL` (`http://localhost:5173`) and the compose service ports/credentials (`postgres`/`brown`/`brown123`, `ConnectionStrings__DefaultConnection`) are all hardcoded in `playwright.config.ts` and `docker-compose.yml` respectively; no `.env`/`.env.example` file was found under `e2e/` or repo root for these values.
- `.gitlab-ci.yml` root `variables:` block defines `DOCKER_IMAGE`, `DOCKER_HOST`, `DOCKER_TLS_CERTDIR`, `DOCKER_DRIVER` — used only by the existing `docker` stage jobs.

### Configuration Files

- `/Users/sergey_grigorash/dev/shazam/e2e/playwright.config.ts` — governs test discovery, base URL, tracing, browser project; no `outputDir`/`screenshot` override present (Playwright defaults apply: `test-results/` output dir, `screenshot: 'off'`).
- `/Users/sergey_grigorash/dev/shazam/e2e/package.json` — governs the `test:e2e` script and the sole `@playwright/test` devDependency; no `npm ci`/browser-install script (`npx playwright install`) is defined anywhere in `e2e/package.json` or `.gitlab-ci.yml`.
- `/Users/sergey_grigorash/dev/shazam/docker-compose.yml` — governs the three-service stack the CI job must bring up; note the `frontend` service's `depends_on` has no `condition: service_healthy` (no healthcheck defined on `frontend` itself), consistent with the readiness gap `wait-for-stack.mjs` was built to cover.

### Feature Flags and Deployment Concerns

- None found — no feature-flag system exists in this repo for CI gating.
- The `docker` stage jobs (`docker:backend`, `docker:frontend`) are restricted to `only: - main` and use registry credentials (`$CI_REGISTRY_USER`, `$CI_REGISTRY_PASSWORD`, `$CI_REGISTRY_IMAGE`) via `docker login` — these are the only secrets-handling pattern present in `.gitlab-ci.yml`; the new e2e job does not need registry access since it builds compose services from local Dockerfiles (per `docker-compose.yml`'s `build:` blocks) rather than pulling from the registry.

---

## 6. Risk Indicators

- No confirmed `docker-compose` binary availability inside the `docker:28.5.2` CI image used for the repo's only existing Docker-in-CI pattern — Speculative: the new job may need `apk add docker-compose` or an alternate image/plugin (`docker compose` v2 CLI) to run reliably in GitLab CI.
- No existing pattern in `.gitlab-ci.yml` for `when: on_failure` artifacts — the failure-screenshot requirement is a new pattern, not a reused one.
- `playwright.config.ts` does not currently set `screenshot: 'only-on-failure'` (or equivalent) — Speculative: capturing failure screenshots as CI artifacts likely requires a config change to `e2e/playwright.config.ts` in addition to the `.gitlab-ci.yml` job itself, which sits right at the edge of "CI wiring only" vs. touching the already-merged Playwright config.
- `e2e/tests/wait-for-stack.unit.spec.ts` is discovered by the same `testDir` as the three browser flows — a plain `playwright test` in CI runs all four files together; if this unit-style test needs a different runner/browser context than the flow specs, that's an existing ambiguity, not something introduced by this slice.
- No Playwright browser install step exists anywhere (`npx playwright install --with-deps`) — the new CI job needs one, and CI runner image compatibility (browser system dependencies) with whatever base image is chosen is unverified.
- The frontend service's compose `depends_on` (no `service_healthy` condition) is unchanged by design (slice 1 explicitly deferred this) — the new CI job's reliability depends entirely on `wait-for-stack.mjs`'s polling working correctly against a fresh multi-container `docker-compose up -d` in a CI runner, which may be slower/less predictable than local dev.
- `frontend:test`'s existing `rules: changes: frontend/**/*` shows a path-scoping convention exists in this file; whether the new e2e job should run unconditionally (blocking every MR per the task's "blocking" requirement) or be scoped similarly is a design decision for spec/plan, not resolved by anything found in code.

---

## 7. Summary for Complexity Assessment

This slice is CI-configuration-only: it adds a new job (and likely artifact/cache config) to `.gitlab-ci.yml`, with no changes anticipated to `e2e/playwright.config.ts`, `wait-for-stack.mjs`, or the three flow specs, all of which are already merged and runnable locally via `e2e/package.json`'s `test:e2e` script. The primary layer touched is the GitLab CI/pipeline layer; the file-change surface is narrow (`.gitlab-ci.yml`, and possibly one line in `playwright.config.ts` if failure screenshots require a config flag) — the smallest of the three slices in this split by file count.

Technical novelty and risk concentrate in two places: (1) the repo's only existing Docker-in-CI pattern (`docker:28.5.2` image + `dind` service, currently used solely for `docker build`/`push` restricted to `main`) has never been used to run `docker-compose up`/orchestrate a multi-service stack, so `docker-compose` binary availability and multi-container networking inside a GitLab runner are unverified; and (2) no job in this repo has ever installed Playwright browsers, cached them, or captured `when: on_failure` artifacts — both are new patterns for this `.gitlab-ci.yml`. Test coverage of the CI job itself is inherently untestable outside a real GitLab pipeline run; the underlying Playwright suite it invokes is already covered by three flow specs plus a helper unit test from the prior two slices.

Requirements are relatively clear (task and the original split's slice definition agree closely: new blocking `test`-stage job, compose up, readiness wait, playwright test, on-failure screenshot artifacts), but two open questions remain for spec/plan to resolve rather than being facts discovered in code: whether `playwright.config.ts` needs a `screenshot:` setting change to actually produce failure screenshots, and whether/how `docker-compose` teardown is handled in CI (slice 1 explicitly left teardown manual for local dev, but CI runners are ephemeral per-job so this may be moot, or may need explicit `docker-compose down`/cleanup in an `after_script`).

---

## 8. External References

None named by the task. The task's own description ("e2e/package.json has a test:e2e script that runs docker-compose up -d, wait-for-stack, then playwright test") points at files inside this repository, all of which were read directly and are cited above (`e2e/package.json`, `e2e/playwright.config.ts`, `e2e/wait-for-stack.mjs`, `docker-compose.yml`, `.gitlab-ci.yml`). Sibling planning artifacts for this same split ticket (`docs/superpowers/tasks/2026-09-16-e2e-test-suite/complexity-assessment.json`, `docs/superpowers/tasks/2026-09-16-e2e-foundation/spec.md`, `docs/superpowers/tasks/2026-09-16-e2e-remaining-flows/spec.md`) were also read as directly relevant prior-slice context and are cited in Section 3.

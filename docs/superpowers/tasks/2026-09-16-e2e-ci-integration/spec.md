# Spec: Wire Playwright E2E Suite into GitLab CI

**Ticket**: EPMCDMPTST-2330 (slice 3 of 3)

## Context

Slices 1 and 2 already merged the Playwright E2E toolchain (`e2e/playwright.config.ts`,
`e2e/wait-for-stack.mjs`, `e2e/package.json`'s `test:e2e` script) and all three flow specs
under `e2e/tests/`. Everything runs locally today via `npm run test:e2e` from `e2e/`. No
CI job exercises this suite. This slice closes that gap by adding a blocking job to
`.gitlab-ci.yml`.

## Approach

Add a new `e2e:test` job to the existing `test` stage in `.gitlab-ci.yml`, following the
repo's one-image-one-script convention. The job:

1. Runs unconditionally on every branch/MR (no `rules: changes:` scoping, unlike
   `frontend:test`) — an E2E failure should gate merges regardless of which layer changed.
2. Brings up the root `docker-compose.yml` stack (`docker-compose -f docker-compose.yml up
   -d --build`), reusing the same lifecycle the local `test:e2e` script already exercises.
3. Installs `e2e/`'s dependencies and Playwright browsers (`npm ci` +
   `npx playwright install --with-deps chromium`) — no existing job does this today, so this
   is new caching/setup, not a reused pattern.
4. Waits for readiness via the already-merged `node wait-for-stack.mjs http://localhost:5173/`
   before invoking Playwright, exactly as the local script does.
5. Runs `npx playwright test` from `e2e/`.
6. Tears down the stack in `after_script` (`docker-compose down -v`) for hygiene, even
   though CI runners are ephemeral per job.

Set `screenshot: 'only-on-failure'` in `e2e/playwright.config.ts` (currently unset, so
Playwright's default `'off'` applies). This is a one-line change to an already-merged file,
justified because "capture failure screenshots as CI artifacts" is an explicit requirement
of this slice — without it, there is nothing to capture.

Declare `artifacts: when: on_failure` on the job, pointing at Playwright's default
`e2e/test-results/` output directory (where screenshots and traces land). This is a new
artifact pattern for this file (existing jobs use unconditional artifacts); scope it to
`when: on_failure` specifically so passing runs don't upload anything.

Use the existing `docker:28.5.2` image + `docker:28.5.2-dind` service pairing (the repo's
only precedent for Docker access inside CI) as the job's base, rather than introducing a
second Docker-in-CI pattern. Node and npm are not present in that image, so the job installs
Node via the package manager available in the `docker:28.5.2` image (Alpine-based, `apk add
nodejs npm`) before running the npm-based steps. `docker-compose` binary availability inside
this image is unverified (see Risks) — implementation must confirm it works and fall back to
the `docker compose` v2 plugin syntax or an alternate base image if not.

## Acceptance Criteria

- `.gitlab-ci.yml` has a new job in the `test` stage that runs `docker-compose up`, waits
  for readiness via `wait-for-stack.mjs`, and runs `playwright test` against the `e2e/`
  suite.
- The job runs on every branch/MR unconditionally (no path-based `rules:`/`only:` scoping)
  and its failure blocks the pipeline (default GitLab behavior — no `allow_failure: true`).
- `e2e/playwright.config.ts` sets `screenshot: 'only-on-failure'` (or equivalent).
- On test failure, Playwright's failure screenshots/output are published as pipeline
  artifacts scoped to `when: on_failure`.
- The job cleans up its docker-compose stack after running (`after_script` teardown).
- No changes to `e2e/wait-for-stack.mjs`, the three flow spec files, or any application code.

## Non-Goals

- No changes to `e2e/wait-for-stack.mjs`, `e2e/tests/*.spec.ts`, or `docker-compose.yml`'s
  service definitions (including not adding a `frontend` healthcheck).
- No caching of Playwright browsers or npm dependencies across pipeline runs — first
  implementation may install fresh each run; caching is an optional follow-up, not required
  here.
- No changes to the `docker:backend`/`docker:frontend` jobs or the `docker` stage's
  `only: - main` restriction.
- No updates to `.ai-run/guides/quality-gates.md` or other documentation — noted as a known
  follow-up, not part of this slice's acceptance criteria.
- No retry/flake-mitigation logic beyond what `playwright.config.ts` already configures
  (`trace: 'on-first-retry'`); this slice does not tune retries or timeouts.

## Risks / Open Implementation Verification

- `docker-compose` (or `docker compose` v2) binary availability inside the `docker:28.5.2`
  image is unverified in this repo; implementation must confirm and adjust (`apk add
  docker-compose`, or use `docker compose` subcommand syntax) if the plain binary is absent.
- Playwright's system dependencies (`--with-deps`) must succeed inside an Alpine-based
  `docker:28.5.2` image; if browser install fails on Alpine, implementation may need a
  different base/companion image for the browser-install + test-run steps specifically.
- `e2e/tests/wait-for-stack.unit.spec.ts` will run alongside the three flow specs under a
  plain `playwright test` invocation; this is expected/acceptable, not a defect to fix here.

# E2E CI Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a blocking `e2e:test` job to `.gitlab-ci.yml` that brings up the docker-compose
stack, waits for readiness, runs the already-merged Playwright suite, and publishes
failure screenshots as `on_failure` artifacts.

**Architecture:** CI-configuration-only change. One new job in the existing `test` stage of
`.gitlab-ci.yml`, using the repo's only Docker-in-CI precedent (`docker:28.5.2` image +
`docker:28.5.2-dind` service). One supporting one-line change to `e2e/playwright.config.ts`
to enable failure screenshots. No application code, no changes to `wait-for-stack.mjs` or
the flow specs.

**Tech Stack:** GitLab CI, Docker-in-Docker, docker-compose (or `docker compose` v2 plugin),
Node/npm, `@playwright/test` 1.47.

**Spec:** `/Users/sergey_grigorash/dev/shazam/docs/superpowers/tasks/2026-09-16-e2e-ci-integration/spec.md`

## Global Constraints

- New job runs unconditionally on every branch/MR — no `rules: changes:` scoping.
- Job failure blocks the pipeline by default (no `allow_failure: true`).
- No changes to `e2e/wait-for-stack.mjs`, `e2e/tests/*.spec.ts`, or `docker-compose.yml` service
  definitions (including no frontend healthcheck).
- No caching of npm deps / Playwright browsers across runs (optional follow-up, out of scope).
- No changes to `docker:backend`/`docker:frontend` jobs or the `docker` stage's `only: - main`.
- No retry/timeout tuning beyond existing `trace: 'on-first-retry'`.
- Commit per task using the repository's existing convention (`EPMCDMPTST-2330: <description>`).

---

### Task 1: Enable failure screenshots in Playwright config

**Files:**
- Modify: `e2e/playwright.config.ts:6-9`
- Test: `e2e/tests/wait-for-stack.unit.spec.ts` (existing, run as verification — no new test
  file needed; screenshot behavior is only observable end-to-end)

**Interfaces:**
- Produces: `use.screenshot: 'only-on-failure'` in the exported Playwright config, consumed
  implicitly by `npx playwright test` in Task 2's CI job (screenshots land in
  `e2e/test-results/` on failing tests).

Test-first: no — this is a one-line declarative config value; there is no unit that can assert
a Playwright runtime screenshot behavior without actually running a real failing browser test
(covered end-to-end once Task 2's CI job runs against a real failure, not practical to write
a narrower failing test here).

- [ ] **Step 1: Add `screenshot: 'only-on-failure'` to the `use` block**

In `e2e/playwright.config.ts:6-9`, add one line to the existing `use` block:

```ts
use: {
  baseURL: 'http://localhost:5173',
  trace: 'on-first-retry',
  screenshot: 'only-on-failure',
},
```

- [ ] **Step 2: Sanity-check config loads**

Run (from `e2e/`): `npx playwright test --list`
Expected: lists the existing 4 spec files with no config errors.

---

### Task 2: Add `e2e:test` job to `.gitlab-ci.yml`

**Files:**
- Modify: `.gitlab-ci.yml:58-81` (insert new job after `frontend:test`, before `frontend:build`)

**Interfaces:**
- Consumes: `e2e/playwright.config.ts`'s `screenshot: 'only-on-failure'` (Task 1),
  root `docker-compose.yml` (unmodified), `e2e/wait-for-stack.mjs` CLI
  (`node wait-for-stack.mjs <url>`), `e2e/package.json`'s existing `test:e2e` lifecycle steps.
- Produces: a `test`-stage job named `e2e:test`, unconditional on every branch, with
  `artifacts: when: on_failure` pointing at `e2e/test-results/`.

Test-first: yes — the "failing test" for a pure CI-config change is running the pipeline job
itself and observing it fail before the fix (e.g. `docker-compose` binary missing, or
`playwright test` erroring) then re-running after each fix until green. Concretely: push the
job as scaffolded first with just the `docker-compose up` + readiness wait steps and confirm
in a GitLab CI run that it currently fails (job doesn't exist yet / errors on missing
`docker-compose` binary), before completing the rest of the script.

- [ ] **Step 1: Verify docker-compose availability in `docker:28.5.2` image locally**

Run (from repo root, to decide `docker-compose` vs `docker compose` v2 syntax):
`docker run --rm docker:28.5.2 sh -c "which docker-compose || docker compose version"`
Expected: one of the two succeeds — use whichever binary/subcommand is confirmed present as
the exact invocation in Step 2's script. If `docker-compose` is absent, use `docker compose`
(v2 plugin, included in `docker:28.5.2`) and install nothing extra.

- [ ] **Step 2: Add the `e2e:test` job**

Insert after `frontend:test` (`.gitlab-ci.yml:81`), before `frontend:build` (`.gitlab-ci.yml:83`):

```yaml
e2e:test:
  stage: test
  image: docker:28.5.2
  services:
    - name: docker:28.5.2-dind
      command: ["--tls=false"]
  script:
    - apk add --no-cache nodejs npm
    - docker compose -f docker-compose.yml up -d --build
    - cd e2e && npm ci
    - node wait-for-stack.mjs http://localhost:5173/
    - npx playwright install --with-deps chromium
    - npx playwright test
  after_script:
    - docker compose -f docker-compose.yml down -v
  artifacts:
    when: on_failure
    paths:
      - e2e/test-results/
```

Use whichever of `docker compose` / `docker-compose` Step 1 confirmed, consistently in both
the `script` and `after_script` lines.

- [ ] **Step 3: Run the job in a real pipeline and confirm it fails first, then passes**

Push the branch and open/update the MR. Expected first run (before any fixes needed):
the job either passes outright, or fails at a specific step (compose binary missing, npm
install error, browser install error, or a flaky readiness wait) — capture which step failed.

- [ ] **Step 4: Fix any failing step and re-run until the job passes**

Common fallbacks if Step 3 fails:
- `docker compose` unavailable → confirm `docker-compose` binary name instead (swap the two
  script lines in Task 2 Step 2).
- `playwright install --with-deps` fails on Alpine → drop `--with-deps` and add explicit
  `apk add` packages Playwright's error output names as missing.
Re-run the pipeline after each fix. Expected: `e2e:test` job passes, `test-results/` artifact
is absent (job passed, so `on_failure` artifacts don't upload).

- [ ] **Step 5: Confirm failure-artifact path by temporarily breaking a flow spec (local check only, not committed)**

Locally (from `e2e/`, stack already running via `npm run test:e2e` once through Task 1's
change): temporarily edit any assertion in `flow1-browse-conference.spec.ts` to a value that
will fail, run `npx playwright test`, confirm `test-results/` now contains a
`*-failed-*/test-failed-*.png` screenshot, then revert the temporary edit (do not commit it).
Expected: screenshot file exists after the induced failure, directory is git-ignored per
`e2e/.gitignore:1-3` so it never gets committed either way.

---

## Negative-Constraints Pass

- "No changes to `e2e/wait-for-stack.mjs`" → honored: no task modifies it, only invokes it.
- "No changes to `e2e/tests/*.spec.ts`" → honored: Task 2 Step 5 explicitly reverts its
  temporary local edit and never commits it.
- "No changes to `docker-compose.yml` service definitions (including no frontend healthcheck)"
  → honored: no task modifies `docker-compose.yml`; readiness is handled entirely via
  `wait-for-stack.mjs` polling, as the spec requires.
- "No `rules:`/`only:` path scoping on the new job" → honored: Task 2's `e2e:test` job has no
  `rules:` or `only:` block, unlike `frontend:test`.
- "No `allow_failure: true`" → honored: not set anywhere in Task 2.
- "No caching of npm/Playwright browsers across runs" → honored: no `cache:` block added to
  `e2e:test`.
- "No changes to `docker:backend`/`docker:frontend` or `only: - main`" → honored: no task
  touches those jobs.
- "No retry/timeout tuning" → honored: Task 1 only adds `screenshot`, doesn't touch `trace`
  or retries config.

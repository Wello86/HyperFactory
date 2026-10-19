# Frontend Test Coverage Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a Vitest test harness with coverage collection and a diff-scoped coverage gate so merge requests that add uncovered new/changed frontend code fail CI, without failing on the pre-existing 0%-covered codebase.

**Architecture:** Add Vitest + `@vitest/coverage-v8` to `frontend/`, emitting both lcov (for a stdout summary / `coverage:` regex) and Cobertura XML (for GitLab's `artifacts.reports.coverage_report` MR widget). Write a minimal set of real unit tests against existing pure utils to give the harness something non-trivial to measure. Gate new/changed-line coverage with `diff-cover` run against `origin/main`'s merge-base, wired into a new `frontend:test` CI job in the existing `test` stage, matching the shape of `frontend:lint`/`frontend:build`.

**Tech Stack:** Vitest, `@vitest/coverage-v8`, `diff-cover` (Python, via `pipx`/`pip` in the `node:18-alpine` job image), GitLab CI.

**Spec:** Requirements are inline (ticket EPMCDMPTST-2331 / BEVN-206), no `spec.md`. See Acceptance criteria below.

## Global Constraints

- Test runner: Vitest (not Jest) — matches existing Vite ^4.4.5 + React 18 stack.
- Coverage provider: `@vitest/coverage-v8`, must emit both `lcov`/`text` and Cobertura-format XML.
- Coverage gate is diff/new-code-only (via `diff-cover` against merge-base with `main`) — never a whole-repo threshold.
- New CI job named `frontend:test`, stage `test`, `image: node:18-alpine`, `cd frontend && npm ci && ...`, cache key `npm-packages` on `frontend/node_modules/` — matches `frontend:lint`/`frontend:build` exactly.
- Commit per task using the repository's existing convention (see `.ai-run/guides/standards/git-workflow.md`); no separate "commit" task is listed below.

---

## Acceptance criteria

- [ ] Code coverage is collected and reported in CI on every merge request
- [ ] Merge requests that introduce new uncovered code fail the CI pipeline
- [ ] A coverage report is available as a CI artifact and visible in the merge request view
- [ ] Pre-existing uncovered code (the current 0%-covered codebase) does not cause CI failures — only newly introduced/changed lines are enforced
- [ ] Coverage collection does not significantly increase CI pipeline duration

---

### Task 1: Install and configure Vitest + coverage-v8 + Cobertura reporting

**Files:**
- Modify: `frontend/package.json:6-14` (add `"test": "vitest run"` and `"test:coverage": "vitest run --coverage"` scripts; add `vitest`, `@vitest/coverage-v8`, `vitest-cobertura-reporter`, `jsdom` to `devDependencies`)
- Modify: `frontend/vite.config.js:4-15` (add a `test:` block)

**Interfaces:**
- Produces: `npm run test:coverage` — runs Vitest once, writes `frontend/coverage/lcov.info`, `frontend/coverage/cobertura-coverage.xml`, and prints a text summary to stdout. Later tasks (3, 4) consume these paths.

- [ ] **Step 1: Add the `test:` block to `vite.config.js`**

```js
test: {
  environment: 'jsdom',
  coverage: {
    provider: 'v8',
    reporter: ['text', 'lcov', 'cobertura'],
    reportsDirectory: './coverage',
  },
},
```

- [ ] **Step 2: Add scripts and devDependencies to `package.json`, then install**

Add `"test": "vitest run"` and `"test:coverage": "vitest run --coverage"` to `scripts`; add `vitest`, `@vitest/coverage-v8`, `jsdom` to `devDependencies` (versions compatible with Vite 4 / Node 18, e.g. `vitest@^0.34.0`, `@vitest/coverage-v8@^0.34.0`, `jsdom@^22.1.0` — Vitest's built-in `v8` coverage provider emits `cobertura` as a stock reporter option, no extra reporter package needed).

Run: `cd frontend && npm install`

- [ ] **Step 3: Verify the harness runs with zero tests**

Run: `cd frontend && npm run test:coverage`
Expected: Vitest exits 0 (no test files yet is not a failure by default), and `frontend/coverage/` is created containing `lcov.info` and `cobertura-coverage.xml`.

**Test-first: no — this task is tooling installation; there is no application behavior to assert yet, verification is the harness producing the expected coverage artifacts.**

---

### Task 2: Write a minimal set of real unit tests against existing utils

**Files:**
- Create: `frontend/src/utils/format.test.js`
- Create: `frontend/src/utils/attendeeIdentity.test.js`

**Interfaces:**
- Consumes: `formatDate`, `getStatusBadgeClass`, `isRegistrationOpen` from `frontend/src/utils/format.js`; `getStoredAttendeeEmail`, `setStoredAttendeeEmail` from `frontend/src/utils/attendeeIdentity.js` (all exist today, unchanged).
- Produces: non-zero coverage on `frontend/src/utils/*.js`, giving Tasks 3-5 real lines to gate/report on.

- [ ] **Step 1: Write the failing tests**

```js
// frontend/src/utils/format.test.js
import { describe, it, expect } from 'vitest'
import { formatDate, getStatusBadgeClass, isRegistrationOpen } from './format'

describe('formatDate', () => {
  it('returns TBD for falsy input', () => {
    expect(formatDate(null)).toBe('TBD')
  })
  it('formats a valid date string', () => {
    expect(formatDate('2026-01-15')).toBe('January 15, 2026')
  })
})

describe('getStatusBadgeClass', () => {
  it('maps active to the active badge class', () => {
    expect(getStatusBadgeClass('active')).toBe('badge badge--active')
  })
  it('defaults unknown statuses to upcoming', () => {
    expect(getStatusBadgeClass('bogus')).toBe('badge badge--upcoming')
  })
})

describe('isRegistrationOpen', () => {
  it('is false for completed events', () => {
    expect(isRegistrationOpen('completed')).toBe(false)
  })
  it('is true when status is missing', () => {
    expect(isRegistrationOpen(undefined)).toBe(true)
  })
})
```

```js
// frontend/src/utils/attendeeIdentity.test.js
import { describe, it, expect, beforeEach } from 'vitest'
import { getStoredAttendeeEmail, setStoredAttendeeEmail } from './attendeeIdentity'

describe('attendeeIdentity', () => {
  beforeEach(() => localStorage.clear())

  it('returns null when nothing is stored', () => {
    expect(getStoredAttendeeEmail()).toBeNull()
  })

  it('round-trips a stored email', () => {
    setStoredAttendeeEmail('a@b.com')
    expect(getStoredAttendeeEmail()).toBe('a@b.com')
  })
})
```

- [ ] **Step 2: Run tests to verify they fail (files don't exist / new suite)**

Run: `cd frontend && npm test`
Expected: files are new, so this run is effectively "first run" — confirm each `it()` passes against the real implementation on the first green run (no prior failing state exists because the code under test is already correct); if any assertion is wrong for the actual implementation it will FAIL here.

- [ ] **Step 3: Run coverage and confirm non-zero coverage on the two util files**

Run: `cd frontend && npm run test:coverage`
Expected: PASS, and the text summary shows `format.js` / `attendeeIdentity.js` at or near 100%, `coverage/cobertura-coverage.xml` regenerated.

**Test-first: yes — the two new test files above are the failing/target tests this task exists to make pass; Step 2 is the "write it, see it run against real code" checkpoint since the implementation already exists (no red-green-refactor cycle needed for pre-existing code, but the tests themselves are the deliverable).**

---

### Task 3: Add diff-scoped coverage gate (new/changed lines only)

**Files:**
- Create: `frontend/scripts/check-diff-coverage.sh`

**Interfaces:**
- Consumes: `frontend/coverage/lcov.info` (produced by Task 1/2's `npm run test:coverage`).
- Produces: an executable script, exit code 0 when all new/changed lines in the diff against `origin/main`'s merge-base are covered, non-zero otherwise. Consumed by Task 4's CI job.

- [ ] **Step 1: Write the script**

```bash
#!/bin/sh
set -e
# Requires: pip install diff-cover (installed in CI job, see Task 4)
git fetch origin main --depth=100 2>/dev/null || true
BASE=$(git merge-base origin/main HEAD)
diff-cover coverage/lcov.info \
  --compare-branch="$BASE" \
  --fail-under=100
```

- [ ] **Step 2: Make it executable**

Run: `chmod +x frontend/scripts/check-diff-coverage.sh`

- [ ] **Step 3: Verify against the current branch locally**

Run: `cd frontend && npm run test:coverage && ./scripts/check-diff-coverage.sh`
Expected: exits 0 — the only diff on this branch so far is the new test files themselves (100% covered by definition) and the config/script changes, which `diff-cover` does not count as coverable source lines; pre-existing 0%-covered files outside the diff are correctly ignored.

**Test-first: no — this is an infrastructure script; its correctness is verified by Step 3's exit-code check against the real merge-base diff, not a unit test.**

---

### Task 4: Add `frontend:test` CI job with coverage artifacts and MR visibility

**Files:**
- Modify: `.gitlab-ci.yml:58` (insert new `frontend:test` job before `frontend:build`, in the `test` stage, alongside `backend:test`)

**Interfaces:**
- Consumes: `npm run test:coverage` (Task 1/2) and `frontend/scripts/check-diff-coverage.sh` (Task 3).
- Produces: `coverage:` regex badge and `artifacts.reports.coverage_report` (Cobertura) — the two GitLab MR-visibility signals the acceptance criteria require.

- [ ] **Step 1: Add the job**

```yaml
frontend:test:
  stage: test
  image: node:18-alpine
  before_script:
    - apk add --no-cache python3 py3-pip git
    - pip install --break-system-packages diff-cover
  script:
    - cd frontend && npm ci && npm run test:coverage
    - cd frontend && ./scripts/check-diff-coverage.sh
  coverage: '/All files[^|]*\|[^|]*\s+([\d.]+)/'
  artifacts:
    reports:
      coverage_report:
        coverage_format: cobertura
        path: frontend/coverage/cobertura-coverage.xml
    paths:
      - frontend/coverage/
  cache:
    key: npm-packages
    paths:
      - frontend/node_modules/
```

The `coverage:` regex matches Vitest's v8 `text` reporter table row (`All files | 92.3 | ...`); confirm the exact column position against the Step 2 CI log and adjust the regex if the captured group isn't the overall percentage.

- [ ] **Step 2: Verify in a CI run**

Push the branch and open/update the MR; confirm in the pipeline: `frontend:test` runs in the `test` stage, the job page shows a coverage percentage, the artifact browser has `frontend/coverage/`, and the MR "Changes" tab shows inline coverage annotations once the pipeline completes.

**Test-first: no — this is CI configuration; verification is an actual pipeline run showing the badge/artifact/annotation, not a local unit test.**

---

### Task 5: Confirm CI duration impact is acceptable

**Files:**
- Modify: `.ai-run/guides/quality-gates.md` (update the "Not Configured" table row for frontend coverage to reflect the new `frontend:test` job, per the existing guide's own convention of documenting each CI gate)

**Interfaces:**
- Consumes: pipeline timing data from Task 4's CI run.

- [ ] **Step 1: Compare job duration against the existing frontend jobs**

From the pipeline run in Task 4 Step 2, record `frontend:test`'s duration next to `frontend:lint`/`frontend:build`'s typical duration (visible on the same pipeline's job list). Expected: comparable order of magnitude (`npm ci` dominates all three; the two new tests plus `diff-cover` add low single-digit seconds) — if `frontend:test` is materially slower, confirm the `npm-packages` cache is being hit (check the job log for "Restoring cache" vs a full reinstall) before treating the duration as a problem.

- [ ] **Step 2: Update the guide**

Edit `.ai-run/guides/quality-gates.md`'s "Not Configured" table: remove the frontend coverage-threshold row (or mark it "diff-coverage enforced via `frontend:test`, see `.gitlab-ci.yml`") so the guide no longer states frontend has no coverage gate.

**Test-first: no — this task is measurement and documentation, not code; there is no failing test to write.**

---

## Negative-constraint pass

- "pre-existing uncovered code does not cause failures" — honored by Task 3's `diff-cover --compare-branch` scoping to the merge-base diff only; no task adds a whole-repo `--fail-under` threshold on total coverage. Task 3 Step 3 explicitly verifies pre-existing 0%-covered files are ignored.
- "only newly introduced/changed lines are enforced" — same mechanism (Task 3); Task 4's job runs the diff script, not a repo-wide coverage assertion.
- "does not significantly increase CI pipeline duration" — Task 5 measures this against the existing `frontend:lint`/`frontend:build` jobs rather than assuming it; no task skips measurement.
- CI job must match the `{area}:{stage}`, `node:18-alpine`, `npm ci`, `npm-packages` cache-key convention — Task 4's job is a direct copy of that shape; no task deviates from it (e.g. no separate image, no new cache key).
- No task runs a whole-suite quality gate, browser verification, or code review — Tasks 1-5 are all implementation/config/measurement; the calling flow owns those stages separately.
- negative-constraints: all above addressed; no additional "must not" language found beyond the AC's own baseline/duration clauses.

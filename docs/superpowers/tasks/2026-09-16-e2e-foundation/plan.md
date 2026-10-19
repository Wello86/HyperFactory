# E2E Test Suite Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a standalone Playwright E2E toolchain (`e2e/`), a docker-compose HTTP readiness-wait helper, and Flow 1 (browse conferences → detail → sessions, plus one failure case), runnable locally via `docker-compose up`.

**Architecture:** New top-level `e2e/` directory, independent `package.json`/`playwright.config.ts`, Chromium only, base URL `http://localhost:5173`. No backend/frontend production code changes.

**Tech Stack:** Playwright (`@playwright/test`), Node's built-in `http`/`fetch` for the readiness helper — no new runtime dependencies beyond Playwright itself.

**Spec:** `docs/superpowers/tasks/2026-09-16-e2e-foundation/spec.md`

## Global Constraints

- Chromium only — no Firefox/WebKit.
- No `.env`/environment variables — base URL hardcoded in `playwright.config.ts`.
- No `data-testid` additions, no backend/frontend production code changes.
- No Dockerfile/`docker-compose.yml` `HEALTHCHECK` additions.
- No automated `docker-compose down`/cleanup.
- No CI wiring — local-only for this slice.
- Commit per task using the repository's existing convention (see `.ai-run/guides/standards/git-workflow.md`); no commit format is invented here.

---

### Task 1: E2E project scaffolding

**Files:**
- Create: `e2e/package.json`
- Create: `e2e/playwright.config.ts`
- Create: `e2e/tsconfig.json`
- Create: `e2e/.gitignore` (ignore `node_modules/`, `test-results/`, `playwright-report/`)

**Interfaces:**
- Produces: `playwright.config.ts` exporting a config with `testDir: './tests'`, `use.baseURL: 'http://localhost:5173'`, `projects: [{ name: 'chromium', use: devices['Desktop Chrome'] }]`.
- Produces: `e2e/package.json` scripts `"test:e2e"` (defined fully in Task 4) and devDependency `@playwright/test`.

Test-first: no — this task is toolchain scaffolding with no behavior to assert; verified by running `npx playwright test --list` inside `e2e/` and confirming it reports zero tests found (config loads without error) rather than an error.

- [ ] Step 1: Create `e2e/package.json` with `"name": "e2e"`, `"private": true`, `"type": "module"`, `"devDependencies": { "@playwright/test": "^1.47.0" }`, and an empty `"scripts"` object (populated in Task 4).
- [ ] Step 2: Create `e2e/playwright.config.ts`:
```ts
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
```
- [ ] Step 3: Create `e2e/tsconfig.json` with `"compilerOptions": { "target": "ES2020", "module": "ESNext", "moduleResolution": "Bundler", "types": ["@playwright/test"] }`.
- [ ] Step 4: Create `e2e/.gitignore` with `node_modules/`, `test-results/`, `playwright-report/`.
- [ ] Step 5: Run `cd e2e && npm install && npx playwright install --with-deps chromium && npx playwright test --list`. Expected: exits 0, reports "0 tests" (no `tests/` files yet), no config errors.
- [ ] Step 6: Commit.

---

### Task 2: Readiness-wait helper

**Files:**
- Create: `e2e/wait-for-stack.mjs`
- Test: `e2e/tests/wait-for-stack.unit.spec.ts`

**Interfaces:**
- Consumes: nothing from Task 1 besides the `e2e/` directory and `@playwright/test`'s test runner (used here purely as a Node test harness for the helper's unit test — not a browser test).
- Produces: `wait-for-stack.mjs` exporting `async function waitForStack(url, { timeoutMs, intervalMs } = {})` returning a Promise that resolves once `fetch(url)` returns any `2xx`/`3xx` response, and rejects with an `Error` on timeout. Also runnable as a CLI: `node wait-for-stack.mjs <url> [timeoutMs] [intervalMs]`, exiting 0 on success, 1 with the error message on timeout.

Test-first: yes — write `wait-for-stack.unit.spec.ts` asserting `waitForStack` resolves once a local Node `http` server (started in the test, listening on an ephemeral port) starts responding after a delay, and rejects when nothing ever listens on the target port within a short timeout; run it before `wait-for-stack.mjs` exists and confirm it fails with "Cannot find module './wait-for-stack.mjs'".

- [ ] Step 1: Write `e2e/tests/wait-for-stack.unit.spec.ts`:
```ts
import { test, expect } from '@playwright/test'
import http from 'node:http'
import { waitForStack } from '../wait-for-stack.mjs'

test('resolves once the target starts responding', async () => {
  const server = http.createServer((_req, res) => res.end('ok'))
  await new Promise((resolve) => server.listen(0, resolve))
  const port = server.address().port
  setTimeout(() => {}, 0) // server already listening; simulates "ready"
  await expect(waitForStack(`http://localhost:${port}/`, { timeoutMs: 2000, intervalMs: 50 })).resolves.toBeUndefined()
  server.close()
})

test('rejects on timeout when nothing responds', async () => {
  await expect(
    waitForStack('http://localhost:1/', { timeoutMs: 300, intervalMs: 50 }),
  ).rejects.toThrow(/timed out/i)
})
```
- [ ] Step 2: Run `cd e2e && npx playwright test wait-for-stack.unit.spec.ts`. Expected: FAIL — "Cannot find module '../wait-for-stack.mjs'".
- [ ] Step 3: Write `e2e/wait-for-stack.mjs`:
```js
export async function waitForStack(url, { timeoutMs = 60000, intervalMs = 1000 } = {}) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url)
      if (res.status >= 200 && res.status < 400) return
    } catch {
      // not ready yet — keep polling
    }
    await new Promise((r) => setTimeout(r, intervalMs))
  }
  throw new Error(`waitForStack: timed out waiting for ${url} after ${timeoutMs}ms`)
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [url, timeoutMs, intervalMs] = process.argv.slice(2)
  waitForStack(url, {
    timeoutMs: timeoutMs ? Number(timeoutMs) : undefined,
    intervalMs: intervalMs ? Number(intervalMs) : undefined,
  })
    .then(() => process.exit(0))
    .catch((err) => {
      console.error(err.message)
      process.exit(1)
    })
}
```
- [ ] Step 4: Run `cd e2e && npx playwright test wait-for-stack.unit.spec.ts`. Expected: PASS (2 tests).
- [ ] Step 5: Commit.

---

### Task 3: Flow 1 — happy path (browse → detail → sessions)

**Files:**
- Create: `e2e/tests/flow1-browse-conference.spec.ts`

**Interfaces:**
- Consumes: `playwright.config.ts`'s `baseURL` (Task 1). No production code changes — reads existing DOM from `frontend/src/pages/ConferenceListPage.jsx`, `frontend/src/components/ConferenceCard.jsx` ("View Details" link to `/conferences/:id`), `frontend/src/pages/ConferenceDetailPage.jsx`, `frontend/src/components/ConferenceSessionsSection.jsx` (`h2.detail-section__title` "Sessions").
- Produces: nothing consumed by later tasks — this is a leaf test file.

Test-first: yes — this spec is itself the failing test motivating the task: before it exists, `npx playwright test flow1-browse-conference.spec.ts` fails with "no tests found"; the deliverable is writing it and confirming it passes against a running stack.

**Precondition for Steps 2/4 in this task and Task 4: a stack must already be running** — this task assumes a developer runs `docker-compose up -d` from the repo root and waits (or uses `node e2e/wait-for-stack.mjs http://localhost:5173/` from Task 2) before running Playwright manually; the single-command wiring (`npm run test:e2e`) is delivered in Task 4.

- [ ] Step 1: Write `e2e/tests/flow1-browse-conference.spec.ts`:
```ts
import { test, expect } from '@playwright/test'

test('browse conferences -> detail -> sessions', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
  const firstCard = page.locator('.card-grid .card').first()
  await expect(firstCard).toBeVisible()

  await firstCard.getByRole('link', { name: 'View Details' }).click()
  await expect(page).toHaveURL(/\/conferences\/[^/]+$/)
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
  await expect(page.getByRole('link', { name: 'Conferences' })).toBeVisible()

  const sessionsHeading = page.locator('h2.detail-section__title', { hasText: 'Sessions' })
  await expect(sessionsHeading).toBeVisible()
})
```
- [ ] Step 2: With the stack running locally (`docker-compose up -d` then `node e2e/wait-for-stack.mjs http://localhost:5173/`), run `cd e2e && npx playwright test flow1-browse-conference.spec.ts`. Expected: PASS. If it fails, inspect the failure against the live DOM (selectors in `ConferenceCard.jsx`/`ConferenceSessionsSection.jsx` are read verbatim from source, but live data may render zero conferences — seed at least one conference via the app UI or existing API first if the list is empty).
- [ ] Step 3: Commit.

---

### Task 4: Flow 1 — failure case + single-command invocation

**Files:**
- Modify: `e2e/tests/flow1-browse-conference.spec.ts` (append failure-case test)
- Modify: `e2e/package.json:scripts` (add `test:e2e`)

**Interfaces:**
- Consumes: `waitForStack` CLI entry point from Task 2 (`node wait-for-stack.mjs <url>`).
- Produces: `npm run test:e2e` (from `e2e/`) as the single command that brings up the stack, waits for readiness, and runs the full Playwright suite — the deliverable the spec's "Invocation" section requires.

Test-first: yes — append the failure-case test to the existing spec file and run it before any other change; it should FAIL if the detail page currently crashes or blanks on an unknown id rather than rendering the documented error state (confirms the test asserts real behavior, not a tautology).

- [ ] Step 1: Append to `e2e/tests/flow1-browse-conference.spec.ts`:
```ts
test('detail page shows error state for a non-existent conference id', async ({ page }) => {
  await page.goto('/conferences/00000000-0000-0000-0000-000000000000')
  const errorContainer = page.locator('.error-container')
  await expect(errorContainer).toBeVisible({ timeout: 15000 })
  await expect(errorContainer.getByRole('heading', { name: 'Failed to load conference' })).toBeVisible()
})
```
- [ ] Step 2: With the stack running, run `cd e2e && npx playwright test flow1-browse-conference.spec.ts -g "non-existent"`. Expected: PASS, confirming the error state (not a crash/blank page) renders for an unknown id.
- [ ] Step 3: Update `e2e/package.json:scripts` to:
```json
"scripts": {
  "test:e2e": "docker-compose -f ../docker-compose.yml up -d && node wait-for-stack.mjs http://localhost:5173/ && playwright test"
}
```
- [ ] Step 4: From the repo root, run `cd e2e && npm run test:e2e`. Expected: brings the stack up (or reuses an already-running one), waits for readiness, then runs all 3 specs (happy path, sessions, failure case) and reports PASS.
- [ ] Step 5: Commit.

---

## Negative-constraints check

- No CI wiring: no `.gitlab-ci.yml` change in any task — confirmed.
- No Flow 2/3 coverage: only `flow1-browse-conference.spec.ts` exists; no registration/session-create/speaker flow test added.
- No backend/frontend production code changes: all 4 tasks touch only `e2e/`.
- No `data-testid` additions: all selectors in Tasks 3–4 use existing text/roles/classes (`.card-grid .card`, "View Details", `h2.detail-section__title`, `.error-container`), never a new attribute.
- No Dockerfile/compose `HEALTHCHECK` additions: readiness is solved entirely in `e2e/wait-for-stack.mjs` (Task 2), an HTTP poll external to Docker — no `docker-compose.yml` or `Dockerfile` edit appears in any task.
- No automated `docker-compose down`: Task 4's `test:e2e` script starts the stack but never stops it — matches spec's explicit non-goal.
- No cross-browser matrix: `playwright.config.ts` (Task 1) defines only the `chromium` project.
- No `.env`/environment variables: base URL is hardcoded in `playwright.config.ts`; `wait-for-stack.mjs` takes its URL as a CLI argument in the npm script, not an env var.

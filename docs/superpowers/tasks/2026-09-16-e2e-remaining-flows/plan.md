# E2E Flow 2 (Session Registration) & Flow 3 (Conference Search) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `e2e/tests/flow2-session-registration.spec.ts` and `e2e/tests/flow3-conference-search.spec.ts`, each with one happy-path test and one failure-case test, reusing the existing Playwright scaffold from slice 1.

**Architecture:** Pure test-content addition. Both specs use `page.goto`, the `.loading-container` hidden-wait pattern from `flow1-browse-conference.spec.ts`, and text/role/CSS-class locators only. No application code, config, or tooling changes.

**Tech Stack:** `@playwright/test` ^1.47.0, existing `e2e/playwright.config.ts` (baseURL `http://localhost:5173`), existing `test:e2e` npm script.

**Spec:** `docs/superpowers/tasks/2026-09-16-e2e-remaining-flows/spec.md`

## Global Constraints

- No changes to `e2e/playwright.config.ts`, `e2e/wait-for-stack.mjs`, or `e2e/package.json`/`test:e2e` script.
- No `data-testid` additions or any other frontend/backend application code changes.
- No new npm dependencies.
- No debounce or other testability changes to `ConferenceListPage`'s search behavior.
- File naming: `flow<N>-<slug>.spec.ts`, one happy-path test + one failure-case test per file.
- Selectors: visible text, ARIA role/label, or existing CSS class hooks only — matches `flow1-browse-conference.spec.ts`.
- Wait pattern: `await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })` before asserting on loaded content.
- Registration uses per-test randomized email addresses (e.g. `Test.<Date.now()>@example.com`) so re-runs against the persistent stack don't collide with prior "already registered" state.
- Do not assert `.error-container` for either flow's failure case — Flow 2's failure is native browser constraint validation, Flow 3's is the plain-text "no results" state, both distinct from a fetch error.

---

### Task 1: Flow 2 — session registration spec

**Files:**
- Create: `e2e/tests/flow2-session-registration.spec.ts`

**Interfaces:**
- Consumes: rendered DOM only — `.card-grid .card` (conference cards, from `ConferenceCard.jsx`), `.session-card` + `Details →` link (from `SessionCard.jsx`, navigates to `/conferences/:id/sessions/:sessionId`), `SessionDetailPage.jsx`'s register/waitlist button (`Register for Session` vs `Join Waitlist`, text button), `SessionRegisterModal.jsx`'s form (`input[name="firstName"|"lastName"|"email"]`, submit button text `Register`/`Registering...`), `.badge--active` "You're registered" text and "Cancel Registration" button.
- Produces: nothing consumed by other tasks (Task 2 is an independent file).

- [ ] **Step 1: Write both tests in the new spec file**

Because both the happy path and the failure case need to locate a session that currently shows "Register for Session" (capacity not yet full), and no session id can be hardcoded (state persists on the shared stack), both tests share the same navigation-and-pick helper defined inline in the file.

```typescript
import { test, expect, type Page } from '@playwright/test'

async function openConferenceWithOpenSession(page: Page) {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

  const cardCount = await page.locator('.card-grid .card').count()
  for (let i = 0; i < cardCount; i++) {
    await page.goto('/')
    await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
    await page.locator('.card-grid .card').nth(i).getByRole('link', { name: 'View Details' }).click()
    await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

    const sessionCount = await page.locator('.session-card').count()
    for (let j = 0; j < sessionCount; j++) {
      await page.locator('.session-card').nth(j).getByRole('link', { name: 'Details →' }).click()
      await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

      const registerButton = page.getByRole('button', { name: 'Register for Session' })
      if (await registerButton.isVisible()) {
        return
      }

      await page.goBack()
      await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
    }
  }

  throw new Error('No session with an open "Register for Session" button was found in the seeded stack')
}

test('register for a session shows the registered badge', async ({ page }) => {
  await openConferenceWithOpenSession(page)

  await page.getByRole('button', { name: 'Register for Session' }).click()
  await expect(page.locator('.modal')).toBeVisible()

  const email = `Test.${Date.now()}@example.com`
  await page.locator('input[name="firstName"]').fill('Test')
  await page.locator('input[name="lastName"]').fill('Attendee')
  await page.locator('input[name="email"]').fill(email)
  await page.getByRole('button', { name: 'Register', exact: true }).click()

  await expect(page.locator('.modal')).toBeHidden({ timeout: 15000 })
  await expect(page.getByText("You're registered")).toBeVisible()
  await expect(page.getByRole('button', { name: 'Cancel Registration' })).toBeVisible()
})

test('submitting the registration form with an empty email is blocked by browser validation', async ({
  page,
}) => {
  await openConferenceWithOpenSession(page)

  await page.getByRole('button', { name: 'Register for Session' }).click()
  await expect(page.locator('.modal')).toBeVisible()

  await page.locator('input[name="firstName"]').fill('Test')
  await page.locator('input[name="lastName"]').fill('Attendee')
  await page.getByRole('button', { name: 'Register', exact: true }).click()

  const emailInput = page.locator('input[name="email"]')
  const isValid = await emailInput.evaluate((el: HTMLInputElement) => el.checkValidity())
  expect(isValid).toBe(false)

  await expect(page.locator('.modal')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Register', exact: true })).toBeVisible()
  await expect(page.locator('.badge--active')).toHaveCount(0)
})
```

Test-first: no — this is a new E2E spec, not a unit under TDD; correctness is verified by running it against the live stack in the next step rather than by a preceding red/green cycle on application code.

- [ ] **Step 2: Run the new spec against the local stack**

Run: `cd e2e && npm run test:e2e -- flow2-session-registration.spec.ts` (or `npx playwright test flow2-session-registration.spec.ts` if the stack is already running from a prior `npm run test:e2e`)
Expected: both tests PASS. If the "no open session found" error throws, the seeded stack lacks an available session — note this as a stack/data issue, not a spec bug, and re-run once seed data is confirmed to include one.

- [ ] **Step 3: Commit**

Commit per task using the repository's existing convention.

---

### Task 2: Flow 3 — conference search spec

**Files:**
- Create: `e2e/tests/flow3-conference-search.spec.ts`

**Interfaces:**
- Consumes: rendered DOM only — `aria-label="Search conferences"` input, `.card-grid .card`, the plain-text "No conferences match your filters." message (from `ConferenceListPage.jsx`).
- Produces: nothing consumed by other tasks.

- [ ] **Step 1: Write both tests in the new spec file**

The happy-path keyword must match an existing seeded conference's title/description; reuse the title of whatever conference Flow 1's first `.card-grid .card` renders, read at runtime rather than hardcoded, so the test stays correct if seed data changes.

```typescript
import { test, expect } from '@playwright/test'

test('searching by a known keyword filters to the matching conference', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

  const firstCardTitle = await page.locator('.card-grid .card .card__title').first().innerText()
  const keyword = firstCardTitle.trim().split(/\s+/)[0]

  const searchInput = page.getByLabel('Search conferences')
  await searchInput.fill(keyword)

  await expect(async () => {
    const count = await page.locator('.card-grid .card').count()
    expect(count).toBeGreaterThan(0)
  }).toPass({ timeout: 15000 })

  await expect(
    page.locator('.card-grid .card .card__title', { hasText: keyword }).first()
  ).toBeVisible()
})

test('searching by a non-matching keyword shows the no-results message', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

  const searchInput = page.getByLabel('Search conferences')
  await searchInput.fill('zzz-no-such-conference-zzz')

  await expect(page.getByText('No conferences match your filters.')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('.card-grid .card')).toHaveCount(0)
  await expect(page.locator('.error-container')).toHaveCount(0)
})
```

Test-first: no — new E2E spec verified by running it against the live stack rather than a preceding red/green cycle on application code.

- [ ] **Step 2: Run the new spec against the local stack**

Run: `cd e2e && npm run test:e2e -- flow3-conference-search.spec.ts` (or `npx playwright test flow3-conference-search.spec.ts` if the stack is already running)
Expected: both tests PASS.

- [ ] **Step 3: Commit**

Commit per task using the repository's existing convention.

---

## Negative-constraints pass

- "No changes to `playwright.config.ts`, `wait-for-stack.mjs`, or the `test:e2e` script" — Task 1 and Task 2 only create new files under `e2e/tests/`; neither step touches those three files.
- "No `data-testid` additions or any other frontend/backend application code changes" — both tasks are additive test files only; no `frontend/` or backend path appears in either task's Files list.
- "No new npm dependencies" — both specs import only `@playwright/test`, already a dependency.
- "No debounce or other testability changes to `ConferenceListPage`'s search behavior" — Task 2 waits for the result via `toPass`/`toBeVisible` polling on the rendered DOM instead of modifying the app to add a settled-state signal.
- "Do not assert `.error-container`" for Flow 2's empty-email case — Task 1's failure test asserts `checkValidity()` and modal visibility only, no `.error-container` locator.
- "Distinguish from the `.error-container` fetch-error state" for Flow 3's no-results case — Task 2's failure test explicitly asserts `page.locator('.error-container')).toHaveCount(0)` alongside the no-results text.
- "No handling of true network/server errors" — neither task includes a test for a fetch-error/`.error-container` scenario.

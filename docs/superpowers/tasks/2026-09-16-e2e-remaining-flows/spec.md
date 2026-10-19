# Spec: E2E Flow 2 (Session Registration) and Flow 3 (Conference Search)

**Ticket**: EPMCDMPTST-2330, slice 2 of 3. **Slice 1** (already merged into this branch)
delivered the `e2e/` Playwright scaffold, `playwright.config.ts`, `wait-for-stack.mjs`,
the `test:e2e` script, and `flow1-browse-conference.spec.ts` as the pattern precedent.
**Slice 3** (CI wiring) follows later and is out of scope here.

## Goal

Add two new Playwright spec files under `e2e/tests/`, each covering one user flow's happy
path and one failure case, following Flow 1's established structure and selector style
(text/role/CSS-class locators, no `data-testid`).

## Flow 2 — Session registration (`e2e/tests/flow2-session-registration.spec.ts`)

**Happy path**: From the conference list, navigate into a conference, then into a session
that currently shows the **"Register for Session"** button (not "Join Waitlist" — skip/pick
a session where capacity allows it; do not hardcode a specific session id). Open the
registration modal, fill First Name / Last Name / Email with fresh, randomly-suffixed test
data (e.g. `Test.<timestamp>@example.com`) so repeated runs against the persistent stack
don't collide with a prior run's "already registered" state. Submit, wait for the modal to
close, and assert the session page now shows the `badge--active` "You're registered" badge
and a "Cancel Registration" button in place of the register button.

**Failure case**: Open the modal, fill First Name/Last Name, leave Email empty, and submit.
Because the `<form>` has no `noValidate`, this is intercepted by native HTML5 constraint
validation rather than reaching the app. Assert on that native behavior directly: the email
input's `checkValidity()` is `false` and the modal remains open (submit button still reads
"Register", no `badge--active` appears). Do not assert an `.error-container` message for
this case — that only renders for server-side failures.

## Flow 3 — Conference search (`e2e/tests/flow3-conference-search.spec.ts`)

**Happy path**: Go to the conference list, type a keyword known to match an existing seeded
conference's title (reuse a title fragment already visible in Flow 1's browse assertions,
or the seeded conference under test) into the `aria-label="Search conferences"` input, wait
for the async server-side filter to settle by asserting on the resulting `.card-grid .card`
count/content (not a fixed sleep), and confirm the matching card is present.

**Failure case**: Type an obviously non-matching keyword (e.g. a random string like
`zzz-no-such-conference-zzz`). Wait for the request to settle, then assert the plain-text
message **"No conferences match your filters."** is visible and `.card-grid .card` has zero
matches. Distinguish this from the `.error-container` fetch-error state, which does not apply
here.

## Conventions carried over from Flow 1

- File naming `flow<N>-<slug>.spec.ts`, one happy-path test + one failure-case test per file.
- `await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })` before
  asserting on loaded content.
- Locate by visible text, ARIA role/label, or existing CSS class hooks — no new
  `data-testid` attributes.
- No changes to `e2e/playwright.config.ts`, `e2e/wait-for-stack.mjs`, or `e2e/package.json`;
  new files simply land in the existing `testDir: './tests'`.

## Assumptions (test-data / environment)

- The seeded Docker Compose stack has at least one session with open capacity at test time.
- The seeded stack has at least one conference whose title/description matches a chosen
  keyword, and no conference matches the chosen no-results keyword.
- Registration uses per-test randomized email addresses so re-runs against the same
  persistent stack don't collide with `localStorage`-persisted "already registered" state
  from a previous run; no new isolation tooling is introduced beyond this.

## Non-goals

- No CI wiring — deferred to slice 3.
- No changes to `playwright.config.ts`, `wait-for-stack.mjs`, or the `test:e2e` script.
- No `data-testid` additions or any other frontend/backend application code changes.
- No new npm dependencies.
- No debounce or other testability changes to `ConferenceListPage`'s search behavior.
- No handling of true network/server errors (`.error-container`) for either flow — only the
  two specified failure cases (empty-email constraint validation, no-results search).

## Acceptance Criteria

- `e2e/tests/flow2-session-registration.spec.ts` exists with one happy-path test asserting
  the post-registration "You're registered" state, and one failure-case test asserting
  native email-field invalidity with the modal still open.
- `e2e/tests/flow3-conference-search.spec.ts` exists with one happy-path test asserting a
  matching conference card appears for a known keyword, and one failure-case test asserting
  the "No conferences match your filters." text for a non-matching keyword.
- Both spec files run via the existing `npm run test:e2e` script without any modification to
  `playwright.config.ts`, `wait-for-stack.mjs`, or `package.json`.
- No application code (`frontend/`, backend) is modified.

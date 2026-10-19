# Spec: E2E Test Suite Foundation (Slice 1/3 — EPMCDMPTST-2330)

## Context

BrownEvents has zero E2E precedent: no Playwright, no `e2e/` dir, no readiness
tooling. `docker-compose.yml`'s `frontend` service has no healthcheck and its
`depends_on: backend` only waits for container start, not for the ASP.NET Core
app to actually accept requests — so `docker-compose up` can report "Up" while
the stack isn't yet serving. This slice lays the foundation two later slices
(remaining flows, CI wiring) depend on: tooling + one full flow, proving the
approach end-to-end.

## Approach

**Location**: a new top-level `e2e/` directory with its own `package.json` and
`playwright.config.ts`, independent of `frontend/`'s Vite/Vitest toolchain.
Chosen over colocating in `frontend/` because Playwright's own test runner,
lint rules, and browser binaries are a different concern from the app's build
tooling; keeping them separate avoids coupling `frontend/package.json`'s
scripts/lint config to E2E and keeps this slice's diff additive-only.

**Readiness-wait helper**: a standalone Node script (`e2e/wait-for-stack.mjs`,
no new dependency — uses Node's built-in `http`/`fetch`) that polls
`GET http://localhost:5173/` on an interval until it receives a 200, up to a
timeout, then exits 0 (or non-zero on timeout with a clear error). This closes
the actual gap — container/service state already reports "Up" today, but that
doesn't mean the app inside is serving. Polling the frontend's own root is
sufficient signal for Flow 1's scope: the frontend nginx container being
reachable implies its own boot is done, and Flow 1's assertions further
confirm the `/api/` proxy path is live indirectly (the list page fetch must
succeed for the happy-path assertions to pass). A dedicated backend-only probe
is left for a later slice if a flow needs to distinguish backend-down from
frontend-down failure modes.

**Test flow (`e2e/tests/flow1-browse-conference.spec.ts`)**:
1. Happy path: navigate to `/`, wait for the conference list to render past
   the loading state, click into a conference card, assert the detail page's
   breadcrumb/title render, assert the sessions section renders (using
   whatever selectors `ConferenceCard`/`ConferenceSessionsSection` expose —
   implementer confirms exact text/roles from source, per the technical
   analysis).
2. Failure case: navigate directly to `/conferences/00000000-0000-0000-0000-000000000000`
   (a syntactically-valid but non-existent id) and assert the `.error-container`
   / "Failed to load conference" state renders instead of a crash or blank page.

**Invocation**: an `e2e/package.json` npm script (e.g. `test:e2e`) that runs
`docker-compose up -d` (relative to repo root), then `wait-for-stack.mjs`,
then `playwright test`, in that order, so a single command reproduces the flow
locally. Teardown (`docker-compose down`) is left to the developer/README, not
automated in this slice, to avoid tearing down a stack the developer may want
to keep running for follow-up debugging.

## Acceptance Criteria (see block below)

## Non-goals

- No CI wiring (GitLab job, pipeline stage) — later slice.
- No Flow 2 (registration) or Flow 3 coverage — later slices; this slice
  touches only browse → detail → sessions.
- No backend or frontend production code changes; no `data-testid` attributes
  added — selectors rely on existing text/ARIA roles per the technical
  analysis.
- No Docker/Dockerfile `HEALTHCHECK` additions — the readiness gap is worked
  around at the test-tooling layer (polling), not fixed at the compose/image
  level.
- No automated `docker-compose down` / cleanup — manual or left to a future
  slice.
- No cross-browser matrix (Firefox/WebKit) — Chromium only for this slice.
- No `.env`/environment-variable introduction — base URL and other config are
  hardcoded/inlined in `playwright.config.ts` for now.

## Open Risks

- Selectors depend on reading `ConferenceCard.jsx`/`ConferenceSessionsSection.jsx`/
  `SessionCard.jsx`/`SessionList.jsx` in full during implementation (not done
  at spec time) — implementer must confirm exact link hrefs/visible text before
  writing assertions.
- Polling only the frontend root as the readiness signal could pass before the
  backend is fully warmed up in rare slow-boot scenarios; if Flow 1 proves
  flaky in practice, a follow-up could add a backend-specific probe.

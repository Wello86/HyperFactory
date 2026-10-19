# Technical Research

**Task**: frontend speaker CRUD UI
**Generated**: 2026-09-16
**Research path**: filesystem

---

## 1. Original Context

EPMCDMPTST-2334: No Way to Add or Edit Speakers from the UI. The BrownEvents frontend currently has no way for a user to add or edit speaker records through the UI. Need to add UI (and possibly API support if missing) so users can create and edit Speaker entities from the frontend.

---

## 2. Codebase Findings

### Existing Implementations

Backend (`backend/BrownEvents.Api/`):
- `Models/Speaker.cs` — entity: `Id`, `FirstName` (required, max 100), `LastName` (required, max 100), `Bio` (nullable), `Email` (nullable, `[EmailAddress]`), `Sessions` collection.
- `Dtos/SpeakerDto.cs` — flat response DTO (Id, FirstName, LastName, Bio, Email).
- `Dtos/SpeakerProfileDto.cs`, `Dtos/SpeakerSessionDto.cs` — used by the single-speaker profile endpoint (includes sessions).
- `Controllers/SpeakersController.cs` (`api/speakers`) — only three actions exist:
  - `GET /api/speakers` → `GetAll` (list, no paging)
  - `GET /api/speakers/{id}` → `GetById` (profile with sessions), 404 via `ProblemDetails`
  - `POST /api/speakers` → `Create`, takes raw `[FromBody] Speaker`, returns 201 with `Location` header `api/speakers/{id}`
  - **No `PUT`/`PATCH` and no `DELETE` action exists on this controller.**
- `Services/ISpeakerService.cs` / `Services/SpeakerService.cs` — only `GetAllAsync`, `GetByIdWithSessionsAsync`, `CreateAsync`. **No `UpdateAsync` method exists** (contrast with `ConferencesController`/its service, which has a full `UpdateAsync(id, conference, ct)` → `HttpPut("{id}")` pair — see below).
- `SwaggerExamples/SpeakerExample.cs` — Swagger request example wired to the `Create` action via `[SwaggerRequestExample(typeof(Speaker), typeof(SpeakerExample))]`.
- `backend/BrownEvents.Tests/SpeakerServiceTests.cs` — exists but only exercises current methods (`Update`/`Delete` are not present anywhere in this file, confirmed via grep).

Reference pattern already implemented for another entity (Conference), directly reusable as a template:
- `Controllers/ConferencesController.cs:99-107` — `[HttpPut("{id}")] Update(int id, [FromBody] Conference conference, ct)` → `_service.UpdateAsync(id, conference, ct)` → 200 with DTO or 404.
- Frontend equivalents: `frontend/src/pages/ConferenceCreatePage.jsx` (create form) and `frontend/src/pages/ConferenceEditPage.jsx` (load-then-edit form), both routed in `App.jsx`.

Frontend (`frontend/src/`):
- `api.js` — has `getSpeakers()` (line 69) and `getSpeaker(id)` (line 71) only. **No `createSpeaker`/`updateSpeaker` exported**, even though the backend `POST /api/speakers` already exists and is unused by the frontend. Compare with the Conference equivalents already present: `createConference(data)` (line 37) and `updateConference(id, data)` (line 40), both simple `request()` wrappers.
- `pages/SpeakerListPage.jsx` — lists speakers via `getSpeakers()`, renders `SpeakerCard`s in a `.card-grid`, has its own inline `<nav className="navbar">` block (duplicated markup, not using the shared `Navbar` component). No "Add speaker" affordance.
- `pages/SpeakerProfilePage.jsx` — speaker detail/profile view (uses `useSpeaker` hook, not read in full but present). No edit affordance.
- `components/SpeakerCard.jsx` — presentational card, renders name/bio/email.
- `hooks/useSpeaker.js` — hook wrapping `getSpeaker(id)` fetch, used by `SpeakerProfilePage`.
- `pages/ConferenceCreatePage.jsx` and `pages/ConferenceEditPage.jsx` — the directly analogous CRUD forms for another entity; both are plain controlled-input React forms (`useState` for `formData`, `handleChange`, `validate()`, `handleSubmit` calling the api.js function then `navigate(...)`), no form library, no shared `<Form>` component.
- `App.jsx` — router: `/speakers` → `SpeakerListPage`, `/speakers/:id` → `SpeakerProfilePage`. **No `/speakers/new` or `/speakers/:id/edit` routes** (contrast with `/conferences/new` and `/conferences/:id/edit`, which exist).
- `components/Navbar.jsx` — a shared Navbar component *does* exist with a Speakers link, but most pages (including `SpeakerListPage`, `ConferenceCreatePage`, `ConferenceEditPage`) still inline-duplicate the navbar markup rather than using it — a known pre-existing inconsistency (also called out in `docs/plans/EPMCDMPTST-1962-speakers-directory-page.md`, which explicitly deferred "refactor the duplicated navbar" as out of scope).

### Architecture and Layers Affected

- **Backend API layer**: `SpeakersController` needs a new `Update` action (and, only if edit-only scope expands to deletion, a `Delete` action) if the task requires full parity with Conference edit. `Create` already exists and is unused by the frontend.
- **Backend service layer**: `ISpeakerService`/`SpeakerService` needs an `UpdateAsync` method mirroring `ConferenceService.UpdateAsync` if update support is added.
- **Frontend API client** (`frontend/src/api.js`): needs `createSpeaker`/`updateSpeaker` wrapper functions.
- **Frontend pages**: needs new `SpeakerCreatePage`/`SpeakerEditPage` (or similarly named) components, modeled on `ConferenceCreatePage.jsx`/`ConferenceEditPage.jsx`.
- **Frontend router** (`App.jsx`): needs new routes for the above pages.
- **Frontend navigation**: `SpeakerListPage.jsx` (and possibly `SpeakerProfilePage.jsx`) need an "Add speaker"/"Edit" entry point (link/button) analogous to how Conference list/detail pages link to their create/edit pages (not directly inspected here, but `ConferenceCreatePage`/`EditPage` imply corresponding links exist somewhere in `ConferenceListPage.jsx`/`ConferenceDetailPage.jsx`).

### Integration Points

- Frontend `api.js`'s `request()` helper is the single fetch wrapper (JSON in/out, error parsing from `ProblemDetails.detail`) — any new speaker API functions should use it exactly as `createConference`/`updateConference` do.
- Backend `SpeakerService` depends on `AppDbContext` (EF Core) directly, no repository abstraction.
- No auth layer exists anywhere in the app (per `.ai-run/guides/security/security-practices.md` heading, not read in full but referenced in AGENTS.md) — Create/Update speaker endpoints, like Conference's, have no authorization checks.

### Patterns and Conventions

- **Request body shape**: per `.ai-run/guides/api/api-patterns.md`, POST/PUT bodies bind directly to the raw domain `Model` (e.g. `[FromBody] Speaker speaker`), not a request DTO — the existing `Create` action already follows this, and a new `Update` action should too, matching `ConferencesController.Update`.
- **Validation**: purely via `[ApiController]` automatic model-state validation against data annotations already on `Speaker.cs` (`[Required]`, `[MaxLength]`, `[EmailAddress]`) — no manual `ModelState.IsValid` checks anywhere in the codebase.
- **Controller action shape**: bind → call one service method → map `.ToDto()` → return; 404 via a private `{Entity}NotFound(id)` helper returning `ProblemDetails` (already present in `SpeakersController` for `GetById`, reusable for `Update`).
- **Frontend form pattern**: plain controlled-component form, `useState` object for all fields, single `handleChange(e)` using `e.target.name`/`value`, a `validate()` function returning an error string or null, `handleSubmit` wrapping the API call in try/catch with `loading`/`submitting` state — no form library (no Formik/react-hook-form in `package.json`).
- **No shared Navbar usage** in the pages most relevant here — new pages will likely continue the existing copy-paste inline navbar pattern unless the task also removes that duplication (out of scope per the prior speakers-list ticket).

---

## 3. Documentation Findings

### Guides and Architecture Docs

- `.ai-run/guides/api/api-patterns.md` — route conventions, controller pattern (bind → service → DTO), request-body-is-raw-model convention, validation-via-annotations convention. Directly governs any new `PUT /api/speakers/{id}` endpoint.
- `.ai-run/guides/testing/testing-patterns.md` — states plainly the project is "backend only — no frontend test runner exists" as of when it was written, and documents the xUnit + EF InMemory backend test conventions (naming `MethodName_Scenario_ExpectedResult`, flat `BrownEvents.Tests/` directory, no mocks). **This guide is now stale for the frontend**: `frontend/package.json` has a working `vitest` test runner (`npm run test`, `npm run test:coverage`) and test files already exist (`frontend/src/utils/attendeeIdentity.test.js`, `frontend/src/utils/format.test.js`), added by the recent `EPMCDMPTST-2331` frontend-test-coverage work (see `git log`: "Frontend Has No Test Coverage Enforcement"). No component/page-level frontend tests exist yet, so there's no established pattern in-repo for testing a React form page with vitest — only unit tests of plain utility functions.
- `.ai-run/guides/quality-gates.md` — documents a **frontend diff-coverage gate**: `npm run test:coverage && ./scripts/check-diff-coverage.sh` requires `diff-cover --fail-under=100` on every new/changed line in the MR diff. This directly constrains this task: any new frontend page/component code added for speaker create/edit must ship with tests covering every changed line, or the `frontend:test` CI job fails.
- `docs/plans/EPMCDMPTST-1962-speakers-directory-page.md` — prior plan for the speaker list page (already implemented). Confirms the list page was built when there was "no frontend test infrastructure" (now outdated) and documents the inline-navbar-duplication convention as deliberate/accepted debt.

### Architectural Decisions

- No formal ADRs found under `docs/`. The `api-patterns.md` guide explicitly calls out the DTO-asymmetry pattern (responses via DTO, requests via raw model, with one documented exception for attendee registration) as an intentional, if not fully consistent, convention — new speaker endpoints should follow the dominant pattern (raw model in requests), not the exception.

### Derived Conventions

- New backend action naming/shape should exactly mirror `ConferencesController.Update`/`ConferenceService.UpdateAsync`.
- New frontend pages should exactly mirror `ConferenceCreatePage.jsx`/`ConferenceEditPage.jsx` structurally (state shape, validate/handleSubmit split, loading/submitting/error state variables, breadcrumb + nav block markup).

---

## 4. Testing Landscape

### Existing Coverage

- Backend: `backend/BrownEvents.Tests/SpeakerServiceTests.cs` covers only `GetAllAsync`/`GetByIdWithSessionsAsync`/`CreateAsync` (whatever subset exists today — no `Update`/`Delete` tests since those methods don't exist).
- Frontend: only two test files exist project-wide, both for plain utility functions (`attendeeIdentity.test.js`, `format.test.js`). **No test exists for any page or component** — not `SpeakerListPage`, not `ConferenceCreatePage`/`ConferenceEditPage`, not `SpeakerCard`. There is no in-repo example of a component-level or page-level vitest test to copy from.

### Testing Framework and Patterns

- Backend: xUnit + EF Core InMemory, naming `MethodName_Scenario_ExpectedResult`, flat test directory, no mocks (Moq present but unused).
- Frontend: Vitest 0.34.6 + `@vitest/coverage-v8`, jsdom environment (`jsdom` devDependency), `npm run test` / `npm run test:coverage`. No React Testing Library dependency found in `package.json` — confirm whether one exists before assuming component-render tests are straightforward; the two existing test files test plain JS functions only, not rendered components.

### Coverage Gaps

- No frontend component/page test exists anywhere, so any new `SpeakerCreatePage`/`SpeakerEditPage` will need tests written from scratch with no established in-repo page-test pattern to copy — and the 100%-diff-coverage gate (`quality-gates.md`) makes this a hard requirement, not optional.
- No backend test exists for speaker update/delete since those methods don't exist yet.

---

## 5. Configuration and Environment

### Environment Variables

- None found specific to the speaker feature area. `frontend/src/api.js` uses a relative `BASE_URL = ''` (same-origin fetch), no env-driven API base URL.

### Configuration Files

- `frontend/package.json` — scripts for lint/format/test/build (see Step 1 excerpt above).
- `.gitlab-ci.yml` — defines the lint → build → test → docker pipeline stages referenced by `.ai-run/guides/quality-gates.md`, including the frontend diff-coverage job.
- No dedicated config file for the speaker domain (no feature flags file, no `appsettings.*` entries specific to speakers found).

### Feature Flags and Deployment Concerns

- None found. No feature-flag mechanism exists in this codebase for gating new UI.

---

## 6. Risk Indicators

- Speculative: The backend has no `Update`/`Delete` speaker endpoint or service method today — implementing "edit" from the UI will very likely require adding a `PUT /api/speakers/{id}` action and `SpeakerService.UpdateAsync`, which is a backend change beyond pure frontend work, contrary to a "frontend-only" framing of the ticket.
- The frontend has no `createSpeaker`/`updateSpeaker` functions in `api.js` yet, even though `POST /api/speakers` already exists unused — this gap must be closed regardless of whether Update is added on the backend.
- No frontend page/component test exists anywhere in the repo, and the CI `frontend:test` job enforces 100% diff-coverage on new/changed lines (`scripts/check-diff-coverage.sh` via `diff-cover --fail-under=100`) — new create/edit pages carry real risk of failing this gate if tests aren't written alongside the implementation, and there is no existing page-level test to model from (only utility-function tests exist).
- `.ai-run/guides/testing/testing-patterns.md` is stale (says "no frontend test runner exists"), which could mislead planning if read literally instead of cross-checked against `quality-gates.md` and the actual `frontend/package.json`.
- No React Testing Library (or equivalent) dependency was confirmed present in `frontend/package.json` — if page-level rendering tests are needed to satisfy the coverage gate, an additional dev dependency may be required, which is itself a small scope/complexity addition.
- The inline-navbar-duplication pattern means a new Speaker create/edit page will likely need to copy-paste the navbar block (and possibly add "Speakers" links elsewhere) rather than use the existing shared `Navbar.jsx` component — consistent with existing debt but worth flagging as avoidable duplication.
- Backend validation for `Speaker` already covers required/max-length/email constraints via data annotations — no new validation library or pattern needed if reusing the existing model.

---

## 7. Summary for Complexity Assessment

This task touches both frontend and backend layers. The backend `POST /api/speakers` (create) endpoint and service method already exist and just need a frontend wrapper (`createSpeaker` in `api.js`) and a create page/route — a small, well-trodden addition modeled directly on `ConferenceCreatePage.jsx`. Edit, however, has no backend support at all: `SpeakersController`/`SpeakerService` lack any `Update`/`PUT` action, unlike the parallel `ConferencesController`/`ConferenceService` pair, which is a ready-made template to copy. So "edit" requires a new backend controller action, service method, and (if desired) backend unit tests, in addition to a new frontend edit page, route, and API wrapper — roughly doubling the surface area versus create alone.

Technical novelty is low: every needed pattern (raw-model request body, DTO response mapping, `ProblemDetails` 404 helper, controlled-component form with `useState`/`validate`/`handleSubmit`) already has a working, nearly-identical implementation in the Conference feature that can be closely mirrored. The main non-trivial risk is process rather than code: the frontend enforces a 100%-diff-coverage CI gate (`quality-gates.md`, `check-diff-coverage.sh`) and there is currently no example anywhere in the repo of a page-level or component-level vitest test (only plain-function tests exist), so new page code will need tests written essentially from scratch, possibly requiring a new testing-library dependency. Backend coverage posture is more standard (existing `SpeakerServiceTests.cs` to extend following the established xUnit/EF-InMemory conventions).

Overall this reads as a moderate-complexity, two-layer (frontend + backend) task: low risk in pattern novelty, moderate risk in scope (create is a copy-paste-scale change, edit requires new backend surface), and a real but bounded risk around meeting the frontend coverage gate given the lack of any precedent for testing React pages in this repo.

---

## 8. External References

None named by the task.

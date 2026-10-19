# Spec: Add/Edit Speakers from the UI (EPMCDMPTST-2334)

## Problem

The BrownEvents frontend has no way to create or edit Speaker records. `POST /api/speakers`
already exists on the backend but is unused by the frontend, and there is no `PUT` endpoint at
all — so "edit" has no backend support today.

## Approach

Mirror the existing Conference create/edit implementation end-to-end, since it is a structurally
identical, already-working precedent in this codebase.

### Backend

- `SpeakersController`: add `[HttpPut("{id}")] Update(int id, [FromBody] Speaker speaker, ct)`,
  matching `ConferencesController.Update` (see `ConferencesController.cs:99-107`) — bind raw
  `Speaker` model, call `_service.UpdateAsync(id, speaker, ct)`, map result `.ToDto()`, return 200,
  or reuse the existing `SpeakerNotFound(id)` `ProblemDetails` helper for 404.
- `ISpeakerService` / `SpeakerService`: add `UpdateAsync(int id, Speaker speaker, CancellationToken ct)`
  mirroring `ConferenceService.UpdateAsync` — load existing entity, copy updatable fields
  (`FirstName`, `LastName`, `Bio`, `Email`), save, return updated entity or null if not found.
- Validation stays purely data-annotation-driven (`[Required]`, `[MaxLength(100)]`,
  `[EmailAddress]` already on `Speaker.cs`) — no new manual checks.
- Backend tests: extend `SpeakerServiceTests.cs` with `UpdateAsync_*` cases (found/updates fields,
  not-found/returns null) following the existing `MethodName_Scenario_ExpectedResult` naming and
  EF InMemory, no-mocks convention.

### Frontend — API client

- `api.js`: add `createSpeaker(data)` and `updateSpeaker(id, data)`, each a thin `request()`
  wrapper identical in shape to `createConference`/`updateConference`.

### Frontend — pages

- New `SpeakerCreatePage.jsx` and `SpeakerEditPage.jsx`, structurally mirroring
  `ConferenceCreatePage.jsx`/`ConferenceEditPage.jsx`: controlled `useState` form (`firstName`,
  `lastName`, `bio`, `email`), `handleChange(e)` via `e.target.name`/`value`, a `validate()`
  returning an error string or `null` (required first/last name, max length 100, basic email
  shape check to match backend annotations), `handleSubmit` calling `createSpeaker`/`updateSpeaker`
  inside try/catch with `loading`/`submitting`/`error` state, then `navigate(...)` on success.
  `SpeakerEditPage` loads the existing speaker via `getSpeaker(id)` before rendering the form
  (mirroring `ConferenceEditPage`'s load-then-edit shape).
- Both pages reuse the existing inline `<nav className="navbar">` block exactly as
  `SpeakerListPage.jsx` does today — no shared `Navbar` component introduced.

### Frontend — routing and entry points

- `App.jsx`: add routes `/speakers/new` → `SpeakerCreatePage`, `/speakers/:id/edit` → `SpeakerEditPage`.
- `SpeakerListPage.jsx`: add an "Add speaker" link/button to `/speakers/new`.
- `SpeakerProfilePage.jsx`: add an "Edit" link/button to `/speakers/:id/edit`.

### Frontend — testing

- Add `@testing-library/react` and `@testing-library/jest-dom` as new devDependencies (per
  explicit decision — this is the first page/component-level test in the repo, establishing the
  pattern for future pages).
- Write render/interaction tests for `SpeakerCreatePage` and `SpeakerEditPage`: render, fill
  fields, submit, assert the mocked API call and navigation; cover the validation-error and
  API-error branches. Target 100% coverage of all new/changed lines to satisfy the
  `check-diff-coverage.sh` CI gate.

## Acceptance Criteria

- `PUT /api/speakers/{id}` exists, updates an existing speaker, returns 200 with the updated DTO,
  and returns 404 (via `ProblemDetails`) for an unknown id.
- `SpeakerService.UpdateAsync` has passing xUnit tests for both the found and not-found cases.
- `api.js` exports working `createSpeaker` and `updateSpeaker` functions.
- A user can navigate to `/speakers/new`, fill in and submit a valid form, and land on the new
  speaker's page with the record persisted.
- A user can navigate from a speaker's profile to `/speakers/:id/edit`, change fields, submit, and
  see the changes persisted and reflected after navigating back.
- Client-side validation blocks submission and shows an error for missing required fields or an
  invalid email, without a network call.
- `npm run test:coverage` plus `check-diff-coverage.sh` pass with 100% coverage of all lines
  changed by this task.
- All existing backend and frontend tests continue to pass.

## Non-Goals

- Deleting/removing speakers — not requested by the ticket, no `DELETE` endpoint is added.
- Fixing the inline-navbar-duplication pattern — pre-existing accepted debt, unrelated to this task.
- Adding authentication or authorization to any endpoint — no auth layer exists anywhere in this
  app; Create/Update speaker endpoints stay open like every other endpoint.
- Changing the request-body convention — `PUT`/`POST` continue to bind the raw `Speaker` model
  directly, not a request DTO, for consistency with the existing `Create` action.
- Adding a form library (Formik, react-hook-form, etc.) — the new pages use the same plain
  controlled-component pattern as every existing form page.
- Any refactor of `SpeakerListPage`'s or `SpeakerProfilePage`'s existing structure beyond adding
  the new entry-point link/button.

## Open Risks

- No existing frontend page-level test pattern to copy from; the new tests establish the first
  one, so review overhead for the test approach may be higher than for a typical change.
- Adding `@testing-library/react`/`jest-dom` is a new toolchain dependency; if jsdom/vitest version
  compatibility issues surface, resolving them is in scope for this task but not estimated here.

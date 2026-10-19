# EPMCDMPTST-1963 — Create/Edit Conference forms

## Context
Administrators currently have no UI to create or edit conferences — only the raw API supports
these operations. This adds two routed pages (`/conferences/new` and `/conferences/:id/edit`)
following existing frontend patterns, with entry points from the conference list and detail pages.

## TDD
No — there's no test framework in `frontend/` (no Vitest/Jest/RTL, no test files) and adding one
is out of scope for this ticket. Verification will be manual (dev server + browser) per
CLAUDE.md's UI-testing guidance, plus `npm run lint`/`npm run format:check`. Backend is untouched
(existing `POST`/`PUT /api/conferences` endpoints already support this).

## Files to add
- `frontend/src/pages/ConferenceCreatePage.jsx`
- `frontend/src/pages/ConferenceEditPage.jsx`

## Files to change
- `frontend/src/App.jsx` — add routes:
  - `<Route path="/conferences/new" element={<ConferenceCreatePage />} />`
  - `<Route path="/conferences/:id/edit" element={<ConferenceEditPage />} />`
- `frontend/src/pages/ConferenceListPage.jsx` — add an "Add Conference" `btn btn--primary` link
  (`<Link to="/conferences/new">`) in the existing `.page-header` div next to the `<h1>`.
- `frontend/src/pages/ConferenceDetailPage.jsx` — add an "Edit" `btn btn--secondary` link
  (`<Link to={`/conferences/${id}/edit`}>`) in the `.detail-header` area.

No changes needed to `frontend/src/api.js` — `createConference(data)` and `updateConference(id, data)`
already exist and match the backend contract.

## Implementation approach

Both pages follow the existing hand-rolled form pattern from `RegistrationPage.jsx`: local
`useState` object for form fields, single `handleChange(e)` keyed by `e.target.name`, `.form-group`
markup, `.card` wrapper, breadcrumb, Cancel (`<Link>`) + Submit button pair, `btn`/`loading-container`/
`error-container` CSS classes already defined globally — no new UI components introduced.

**ConferenceCreatePage.jsx**
- Form fields mapped to the `Conference` model: `title` (required), `description`, `location`,
  `venue`, `startDate` (required, `<input type="date">`), `endDate` (required, `<input type="date">`),
  `status` (select: `UPCOMING`/`ACTIVE`/`CANCELLED`/`COMPLETED`, default `UPCOMING`), `maxAttendees`
  (number, optional), `organizerName`, `website`.
- Client-side required-field validation before submit: check `title`, `startDate`, `endDate` are
  non-empty (in addition to native `required` on the inputs, since acceptance criteria explicitly
  call for validation) and that `endDate >= startDate`; set an `error` state message and skip the
  API call if invalid.
- `handleSubmit`: `setLoading(true)`, call `createConference(payload)`, on success
  `navigate(`/conferences/${created.id}`)` (immediate redirect, since AC says "after successful
  create/edit user is redirected" — no intermediate success screen needed here, unlike
  `RegistrationPage`), on failure set `error` from caught exception message, always `setLoading(false)`
  in `finally`.
- Render: breadcrumb (`Conferences > New`), loading state only applies to submit button (`disabled`
  + "Creating..." text) since there's no initial fetch; error state renders inline
  `.error-container`-style message above the form (not a full-page replacement, since the form
  should stay usable to retry).

**ConferenceEditPage.jsx**
- `useParams()` for `:id`. On mount, `useEffect` calls `getConference(id)`, sets form state from
  the response (`data.data || data` fallback pattern per `ConferenceDetailPage`), handles
  `loading`/`error` for this initial fetch same as `ConferenceDetailPage.jsx` (`.loading-container`
  full-page spinner, `.error-container` full-page message with no form shown if fetch fails).
  Dates from the API (`DateTime`) need slicing to `YYYY-MM-DD` for the `<input type="date">` value.
- Same fields/validation as create.
- `handleSubmit`: same pattern, calls `updateConference(id, payload)`, on success
  `navigate(`/conferences/${id}`)`, on failure sets a separate `submitError` state (distinct from
  the initial-load `error`) shown inline above the form so a submit failure doesn't blow away the
  already-loaded form.

## Verification
- `npm run lint` and `npm run format:check` in `frontend/`.
- Manual verification via the `run` skill (Podman-based launch) or `npm run dev`: create a
  conference from the list page, confirm redirect to its detail page and that it now appears in
  the list; edit an existing conference, confirm fields prefill correctly, submit, confirm redirect
  and updated data on the detail page; test validation by submitting with empty title/dates; test
  error state by stopping the backend and submitting.
- No backend changes, so no `dotnet test` run needed, but will double check nothing under
  `backend/` was touched.

## Out of scope
- No new shared UI components (Navbar extraction, Button/Input components) — not requested and
  would expand scope beyond the ticket.
- No test framework setup for frontend.
- No changes to `Status` from a free string to an enum — kept as-is per existing model.

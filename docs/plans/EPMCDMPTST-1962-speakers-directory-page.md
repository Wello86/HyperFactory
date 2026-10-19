# EPMCDMPTST-1962 — Speakers Directory Page Is Missing

## Context
The app has no way to browse speakers. The backend already exposes `GET /api/speakers`
(`SpeakersController.GetAll`, returns a flat unpaginated array of `Speaker`), and the frontend
API client already has `getSpeakers()` in `frontend/src/api.js:40`. Nothing in the frontend
calls it — there's no page, no route, and no nav link. This ticket wires that up.

## Acceptance criteria
- Speakers directory page lists all speakers
- Each entry shows at minimum the speaker's name
- Reachable via a nav link in the main menu
- Route registered in the router
- Loading and error states handled

## TDD decision
No — there is no frontend test infrastructure in this repo (no test files, no test runner script
in `package.json`). Verification will be manual: run the dev server and exercise the page.

## Implementation steps

1. **API client** — already done (`getSpeakers()` in `frontend/src/api.js`). No change needed.

2. **`frontend/src/components/SpeakerCard.jsx`** (new) — presentational card modeled on
   `ConferenceCard.jsx`. Renders speaker name (`${firstName} ${lastName}`) and, if present,
   `bio`/`email`. Uses the existing `.card` / `.card__title` / `.card__description` classes from
   `index.css` — no new CSS needed.

3. **`frontend/src/pages/SpeakerListPage.jsx`** (new) — modeled on `ConferenceListPage.jsx` but
   simplified since `/api/speakers` returns a plain array, not `{ data, totalCount }`:
   - `useState` for `speakers`, `loading` (init `true`), `error`.
   - `useEffect` (no deps / run once) calling `getSpeakers()`, setting `speakers`/`error` and
     `loading(false)` in `.then`/`.catch`, same pattern as `ConferenceListPage`.
   - Same nav block, `page-header`, loading/error/empty blocks as `ConferenceListPage`.
   - Render `.card-grid` of `SpeakerCard`s keyed by `speaker.id`. No `Pagination` component.

4. **Router** — `frontend/src/App.jsx`: import `SpeakerListPage`, add
   `<Route path="/speakers" element={<SpeakerListPage />} />`.

5. **Nav link** — add `<Link to="/speakers" className="navbar__link">Speakers</Link>` next to the
   existing "Conferences" link in every page's inline navbar block (there's no shared Navbar
   component, so this follows the existing copy-paste pattern):
   - `ConferenceListPage.jsx` (1 spot)
   - `ConferenceDetailPage.jsx` (3 spots — loading/error/success nav blocks)
   - `SessionDetailPage.jsx` (1 spot)
   - `RegistrationPage.jsx` (1 spot)
   - `SpeakerListPage.jsx` itself (1 spot, in its own nav block)

## Out of scope
- Backend changes (endpoint already exists and is sufficient for "list all speakers, show name").
- Pagination for speakers (backend doesn't support it; AC doesn't require it).
- Speaker detail page / linking speakers to their sessions (not in AC).
- Refactoring the duplicated navbar into a shared component (pre-existing pattern, out of scope).

## Verification
- `npm run lint` and `npm run format:check` in `frontend/`.
- Run the app (`docker-compose up --build` or the `run` skill via Podman), navigate to `/speakers`
  directly and via the new nav link from each page, confirm speaker names render.
- Manually verify loading state (visible briefly) and error state (e.g. temporarily point API at
  a bad URL or stop the backend) show the expected UI blocks.

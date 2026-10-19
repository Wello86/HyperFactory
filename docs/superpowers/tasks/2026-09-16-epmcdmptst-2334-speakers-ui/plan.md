# Speakers Create/Edit UI (EPMCDMPTST-2334) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users create and edit Speaker records from the BrownEvents UI, adding the missing backend `PUT` support along the way.

**Architecture:** Mirror the existing Conference create/edit feature exactly: add `PUT /api/speakers/{id}` + `SpeakerService.UpdateAsync` on the backend, `createSpeaker`/`updateSpeaker` wrappers in `api.js`, new `SpeakerCreatePage`/`SpeakerEditPage` React pages, new routes, and entry-point links.

**Tech Stack:** ASP.NET Core (backend), React + react-router-dom + Vitest (frontend). Commit per task using the repository's existing convention — no separate commit steps are spelled out below.

**Spec:** `docs/superpowers/tasks/2026-09-16-epmcdmptst-2334-speakers-ui/spec.md`

## Global Constraints

- Request/response bodies keep binding the raw `Speaker` model (no request DTO) — matches `Create`.
- No `DELETE` endpoint, no navbar-dedup refactor, no auth added, no form library added.
- New pages must reuse the inline `<nav className="navbar">` block, not the shared `Navbar` component.
- All new/changed frontend lines must reach 100% coverage (`check-diff-coverage.sh` CI gate).

---

### Task 1: Backend — `SpeakerService.UpdateAsync`

**Files:**
- Modify: `backend/BrownEvents.Api/Services/ISpeakerService.cs:9` (add method signature)
- Modify: `backend/BrownEvents.Api/Services/SpeakerService.cs:35` (add method body)
- Test: `backend/BrownEvents.Tests/SpeakerServiceTests.cs`

**Interfaces:**
- Produces: `Task<Speaker?> UpdateAsync(int id, Speaker speaker, CancellationToken cancellationToken = default)` on `ISpeakerService`/`SpeakerService`.

Test-first: yes — write `UpdateAsync_ExistingSpeaker_UpdatesFieldsAndPersists` and `UpdateAsync_UnknownId_ReturnsNull` before the implementation exists (compile error / assertion failure).

- [ ] **Step 1: Write the failing tests** in `SpeakerServiceTests.cs`, following the existing `CreateContext()` helper and `MethodName_Scenario_ExpectedResult` naming:

```csharp
[Fact]
public async Task UpdateAsync_ExistingSpeaker_UpdatesFieldsAndPersists()
{
    using var context = CreateContext();
    var speaker = new Speaker { FirstName = "Ada", LastName = "Lovelace", Bio = "Old", Email = "old@example.com" };
    context.Speakers.Add(speaker);
    await context.SaveChangesAsync();

    var service = new SpeakerService(context);
    var result = await service.UpdateAsync(speaker.Id, new Speaker
    {
        FirstName = "Grace", LastName = "Hopper", Bio = "New", Email = "new@example.com"
    });

    Assert.NotNull(result);
    Assert.Equal("Grace", result!.FirstName);
    Assert.Equal("Hopper", result.LastName);
    Assert.Equal("New", result.Bio);
    Assert.Equal("new@example.com", result.Email);
}

[Fact]
public async Task UpdateAsync_UnknownId_ReturnsNull()
{
    using var context = CreateContext();
    var service = new SpeakerService(context);
    var result = await service.UpdateAsync(999, new Speaker { FirstName = "X", LastName = "Y" });
    Assert.Null(result);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/BrownEvents.Tests --filter SpeakerServiceTests` — expected FAIL (`UpdateAsync` does not exist).

- [ ] **Step 3: Implement `UpdateAsync`**

In `ISpeakerService.cs`, add `Task<Speaker?> UpdateAsync(int id, Speaker speaker, CancellationToken cancellationToken = default);`.
In `SpeakerService.cs`, add (mirroring `ConferenceService.UpdateAsync`, `backend/BrownEvents.Api/Services/ConferenceService.cs:86-102`):

```csharp
public async Task<Speaker?> UpdateAsync(int id, Speaker updated, CancellationToken cancellationToken = default)
{
    var existing = await _context.Speakers.FindAsync(new object?[] { id }, cancellationToken);
    if (existing is null) return null;

    existing.FirstName = updated.FirstName;
    existing.LastName = updated.LastName;
    existing.Bio = updated.Bio;
    existing.Email = updated.Email;

    await _context.SaveChangesAsync(cancellationToken);
    return existing;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/BrownEvents.Tests --filter SpeakerServiceTests` — expected PASS, all cases green.

- [ ] **Step 5: Commit**

---

### Task 2: Backend — `PUT /api/speakers/{id}` endpoint

**Files:**
- Modify: `backend/BrownEvents.Api/Controllers/SpeakersController.cs:71` (add `Update` action, after `Create`)

**Interfaces:**
- Consumes: `ISpeakerService.UpdateAsync(int, Speaker, CancellationToken)` from Task 1.
- Produces: `PUT /api/speakers/{id}` — 200 with `SpeakerDto` on success, 404 `ProblemDetails` via existing `SpeakerNotFound(id)` helper otherwise.

Test-first: no — this is a thin controller action with no branching logic beyond what Task 1 already tests; covered by the acceptance criteria's manual/API check, not a new unit test.

- [ ] **Step 1: Add the `Update` action**

```csharp
/// <summary>
/// Updates an existing speaker.
/// </summary>
/// <param name="id">The id of the speaker to update.</param>
/// <param name="speaker">The updated speaker data.</param>
/// <param name="cancellationToken">Cancellation token from the request.</param>
/// <response code="200">The updated speaker.</response>
/// <response code="404">No speaker exists with the given id.</response>
[HttpPut("{id}")]
[ProducesResponseType(typeof(SpeakerDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[SwaggerRequestExample(typeof(Speaker), typeof(SpeakerExample))]
public async Task<IActionResult> Update(int id, [FromBody] Speaker speaker, CancellationToken cancellationToken)
{
    var updated = await _service.UpdateAsync(id, speaker, cancellationToken);
    if (updated is null) return NotFound(SpeakerNotFound(id));
    return Ok(updated.ToDto());
}
```

- [ ] **Step 2: Build and smoke-check**

Run: `dotnet build backend/BrownEvents.Api` — expected succeed, no new warnings.

- [ ] **Step 3: Commit**

---

### Task 3: Frontend — `createSpeaker`/`updateSpeaker` API wrappers

**Files:**
- Modify: `frontend/src/api.js:72` (append after `getSpeaker`)

**Interfaces:**
- Produces: `createSpeaker(data)` → `POST /api/speakers`; `updateSpeaker(id, data)` → `PUT /api/speakers/{id}`. Both used by Tasks 4–5.

Test-first: no — these are one-line pass-throughs of the already-tested `request()` helper, identical in shape to `createConference`/`updateConference`; exercised indirectly by the page tests in Tasks 4–5.

- [ ] **Step 1: Add the wrappers**

```javascript
export const createSpeaker = (data) =>
  request('/api/speakers', { method: 'POST', body: JSON.stringify(data) })

export const updateSpeaker = (id, data) =>
  request(`/api/speakers/${id}`, { method: 'PUT', body: JSON.stringify(data) })
```

- [ ] **Step 2: Commit**

---

### Task 4: Frontend — test tooling (`@testing-library/react`, `@testing-library/jest-dom`)

**Files:**
- Modify: `frontend/package.json:22-35` (devDependencies)
- Create: `frontend/src/test-setup.js` (jest-dom matchers)
- Modify: `frontend/vite.config.js` (or `vitest.config.js`, whichever holds the `test` block) — point `setupFiles` at `src/test-setup.js`

**Interfaces:**
- Produces: `expect(...).toBeInTheDocument()` and other jest-dom matchers available to every test file via the new `test-setup.js`, consumed by Tasks 5–6.

Test-first: no — this is dependency/config plumbing with no behavior of its own; it is proven by the first real render test in Task 5 passing.

- [ ] **Step 1: Add devDependencies**

In `package.json` devDependencies, add `"@testing-library/react": "^14.1.2"` and `"@testing-library/jest-dom": "^6.1.5"` (versions compatible with React 18 / Vitest 0.34 / jsdom 22 already pinned in this repo). Run `npm install` in `frontend/`.

- [ ] **Step 2: Create the setup file**

```javascript
import '@testing-library/jest-dom'
```

- [ ] **Step 3: Wire the setup file into Vitest config**

Locate the `test:` block in `frontend/vite.config.js` (read it first — do not guess its current shape) and add `setupFiles: ['./src/test-setup.js']` alongside the existing `environment: 'jsdom'` entry.

- [ ] **Step 4: Verify the toolchain resolves**

Run: `npm run test` in `frontend/` — expected: existing suite (`attendeeIdentity.test.js`, `format.test.js`) still passes, no new failures.

- [ ] **Step 5: Commit**

---

### Task 5: Frontend — `SpeakerCreatePage`

**Files:**
- Create: `frontend/src/pages/SpeakerCreatePage.jsx`
- Create: `frontend/src/pages/SpeakerCreatePage.test.jsx`
- Modify: `frontend/src/App.jsx:9` (import) and `:23` (route)

**Interfaces:**
- Consumes: `createSpeaker(data)` from Task 3.
- Produces: default export `SpeakerCreatePage`, mounted at route `/speakers/new`.

Test-first: yes — write `SpeakerCreatePage.test.jsx` against the not-yet-created component first (import fails / render throws) before writing the component.

- [ ] **Step 1: Write the failing tests**

```javascript
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import SpeakerCreatePage from './SpeakerCreatePage'
import * as api from '../api'

vi.mock('../api')
const navigateMock = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

function renderPage() {
  render(
    <MemoryRouter>
      <SpeakerCreatePage />
    </MemoryRouter>
  )
}

test('submits valid form and navigates to the new speaker', async () => {
  api.createSpeaker.mockResolvedValue({ id: 7, firstName: 'Ada', lastName: 'Lovelace' })
  renderPage()
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Ada' } })
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Lovelace' } })
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  await waitFor(() => expect(api.createSpeaker).toHaveBeenCalledWith(
    expect.objectContaining({ firstName: 'Ada', lastName: 'Lovelace' })
  ))
  expect(navigateMock).toHaveBeenCalledWith('/speakers/7')
})

test('blocks submission and shows an error when first name is missing', () => {
  renderPage()
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  expect(screen.getByText(/first name is required/i)).toBeInTheDocument()
  expect(api.createSpeaker).not.toHaveBeenCalled()
})

test('blocks submission and shows an error for an invalid email', () => {
  renderPage()
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Ada' } })
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Lovelace' } })
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'not-an-email' } })
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  expect(screen.getByText(/valid email/i)).toBeInTheDocument()
  expect(api.createSpeaker).not.toHaveBeenCalled()
})

test('shows the API error message when creation fails', async () => {
  api.createSpeaker.mockRejectedValue(new Error('Boom'))
  renderPage()
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Ada' } })
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Lovelace' } })
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  await waitFor(() => expect(screen.getByText('Boom')).toBeInTheDocument())
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm run test -- SpeakerCreatePage` — expected FAIL (module not found).

- [ ] **Step 3: Implement `SpeakerCreatePage.jsx`**

Structurally mirror `frontend/src/pages/ConferenceCreatePage.jsx:1-203`: `useState` form `{ firstName: '', lastName: '', bio: '', email: '' }`, `handleChange`, `validate()` (required `firstName`/`lastName`, max length 100 each, basic email regex when non-empty), `handleSubmit` calling `createSpeaker` then `navigate(`/speakers/${created.id}`)`, `loading`/`error` state, the same inline `<nav className="navbar">` block as `SpeakerListPage.jsx:27-37`, labelled inputs (`<label htmlFor="firstName">First Name *</label>` etc. so `getByLabelText` resolves), and a submit button reading "Create Speaker".

- [ ] **Step 4: Wire the route**

In `App.jsx`, add `import SpeakerCreatePage from './pages/SpeakerCreatePage'` and `<Route path="/speakers/new" element={<SpeakerCreatePage />} />` before the existing `/speakers/:id` route (so it isn't shadowed).

- [ ] **Step 5: Run tests to verify they pass**

Run: `npm run test -- SpeakerCreatePage` — expected PASS, all four cases green.

- [ ] **Step 6: Commit**

---

### Task 6: Frontend — `SpeakerEditPage`

**Files:**
- Create: `frontend/src/pages/SpeakerEditPage.jsx`
- Create: `frontend/src/pages/SpeakerEditPage.test.jsx`
- Modify: `frontend/src/App.jsx` (import + route, added in Task 5's edited block)

**Interfaces:**
- Consumes: `getSpeaker(id)` (existing), `updateSpeaker(id, data)` from Task 3.
- Produces: default export `SpeakerEditPage`, mounted at route `/speakers/:id/edit`.

Test-first: yes — write `SpeakerEditPage.test.jsx` against the not-yet-created component first.

- [ ] **Step 1: Write the failing tests**

```javascript
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import SpeakerEditPage from './SpeakerEditPage'
import * as api from '../api'

vi.mock('../api')
const navigateMock = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/speakers/7/edit']}>
      <Routes>
        <Route path="/speakers/:id/edit" element={<SpeakerEditPage />} />
      </Routes>
    </MemoryRouter>
  )
}

test('loads the speaker then submits an update and navigates back', async () => {
  api.getSpeaker.mockResolvedValue({ id: 7, firstName: 'Ada', lastName: 'Lovelace', bio: '', email: '' })
  api.updateSpeaker.mockResolvedValue({ id: 7 })
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Hopper' } })
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  await waitFor(() => expect(api.updateSpeaker).toHaveBeenCalledWith(
    '7', expect.objectContaining({ lastName: 'Hopper' })
  ))
  expect(navigateMock).toHaveBeenCalledWith('/speakers/7')
})

test('shows a load error when the speaker fetch fails', async () => {
  api.getSpeaker.mockRejectedValue(new Error('Not found'))
  renderPage()
  await waitFor(() => expect(screen.getByText('Not found')).toBeInTheDocument())
})

test('blocks submission and shows an error when last name is cleared', async () => {
  api.getSpeaker.mockResolvedValue({ id: 7, firstName: 'Ada', lastName: 'Lovelace', bio: '', email: '' })
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: '' } })
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  expect(screen.getByText(/last name is required/i)).toBeInTheDocument()
  expect(api.updateSpeaker).not.toHaveBeenCalled()
})

test('shows the API error message when the update fails', async () => {
  api.getSpeaker.mockResolvedValue({ id: 7, firstName: 'Ada', lastName: 'Lovelace', bio: '', email: '' })
  api.updateSpeaker.mockRejectedValue(new Error('Boom'))
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  await waitFor(() => expect(screen.getByText('Boom')).toBeInTheDocument())
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm run test -- SpeakerEditPage` — expected FAIL (module not found).

- [ ] **Step 3: Implement `SpeakerEditPage.jsx`**

Structurally mirror `frontend/src/pages/ConferenceEditPage.jsx:1-257`: load via `getSpeaker(id)` in a `useEffect`, `formData`/`loading`/`error`/`submitting`/`submitError` state, same `validate()` rules as `SpeakerCreatePage`, `handleSubmit` calling `updateSpeaker(id, formData)` then `navigate(`/speakers/${id}`)`, the same inline navbar block, labelled inputs matching the test's `getByLabelText` queries, submit button reading "Save Changes".

- [ ] **Step 4: Wire the route**

In `App.jsx`, add `import SpeakerEditPage from './pages/SpeakerEditPage'` and `<Route path="/speakers/:id/edit" element={<SpeakerEditPage />} />` after the `/speakers/:id` route.

- [ ] **Step 5: Run tests to verify they pass**

Run: `npm run test -- SpeakerEditPage` — expected PASS, all four cases green.

- [ ] **Step 6: Commit**

---

### Task 7: Frontend — entry-point links

**Files:**
- Modify: `frontend/src/pages/SpeakerListPage.jsx:40-42` (page header)
- Modify: `frontend/src/pages/SpeakerProfilePage.jsx:95-98` (detail header)
- Modify: `frontend/src/pages/SpeakerListPage.test.jsx` / `frontend/src/pages/SpeakerProfilePage.test.jsx` (create if absent — see below)

**Interfaces:**
- Consumes: routes `/speakers/new` and `/speakers/:id/edit` from Tasks 5–6.

Test-first: yes — add/extend a render test asserting the new link's `href` before adding the link.

- [ ] **Step 1: Write the failing assertions**

If no test file exists yet for these two pages, create minimal ones; otherwise add to existing coverage:

```javascript
// SpeakerListPage.test.jsx
test('renders an Add speaker link to /speakers/new', async () => {
  api.getSpeakers.mockResolvedValue([])
  render(<MemoryRouter><SpeakerListPage /></MemoryRouter>)
  await waitFor(() => expect(screen.getByRole('link', { name: /add speaker/i })).toHaveAttribute('href', '/speakers/new'))
})
```

```javascript
// SpeakerProfilePage.test.jsx (mocking useSpeaker as the hook already does)
test('renders an Edit link to /speakers/:id/edit', () => {
  render(<MemoryRouter><SpeakerProfilePage /></MemoryRouter>)
  expect(screen.getByRole('link', { name: /edit/i })).toHaveAttribute('href', '/speakers/7/edit')
})
```

(Mock `../hooks/useSpeaker` to return a loaded speaker with `id: 7` for the profile test.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm run test -- SpeakerListPage SpeakerProfilePage` — expected FAIL (link not found).

- [ ] **Step 3: Add the links**

In `SpeakerListPage.jsx`, inside the `page-header` div (`:40-42`), add `<Link to="/speakers/new" className="btn btn--primary">Add speaker</Link>`.
In `SpeakerProfilePage.jsx`, inside `detail-header` (`:95-98`), add `<Link to={`/speakers/${id}/edit`} className="btn btn--ghost">Edit</Link>`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm run test -- SpeakerListPage SpeakerProfilePage` — expected PASS.

- [ ] **Step 5: Commit**

---

## Self-Review

**Spec coverage:** PUT endpoint (Task 2) + service (Task 1) + backend tests (Task 1) + `createSpeaker`/`updateSpeaker` (Task 3) + create page (Task 5) + edit page (Task 6) + routes (Tasks 5–6) + entry-point links (Task 7) + testing-library setup and page tests (Tasks 4–7) all covered. No gaps against the spec's Acceptance Criteria.

**Placeholder scan:** none — every step shows concrete code or an exact file/line target.

**Type/name consistency:** `createSpeaker`/`updateSpeaker` names match across Tasks 3, 5, 6. `SpeakerService.UpdateAsync` signature matches between interface, implementation, and controller call in Tasks 1–2.

**Negative-constraints pass:**
- No DELETE endpoint added — confirmed no task adds one (Tasks 1–2 only add `Update`).
- No navbar refactor — Tasks 5–6 explicitly copy the inline navbar block; Task 7 only inserts a link inside existing header markup, doesn't touch `Navbar.jsx` or `SpeakerProfilePage`'s existing `<Navbar />` usage structure.
- No auth added — no task touches authorization/middleware.
- No request-DTO conversion — Task 2's `Update` action binds raw `Speaker`, matching `Create`.
- No form library added — Tasks 5–6 use plain `useState`/controlled inputs, no new dependency beyond testing-library (Task 4, which is testing infra, not a form library).
- No other refactor of `SpeakerListPage`/`SpeakerProfilePage` beyond one link each — Task 7 is scoped to exactly that.

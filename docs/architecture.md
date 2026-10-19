# Architecture

Map of the `backend/BrownEvents.Api` (ASP.NET Core) and `frontend/src` (React) codebases for someone who has never opened the code.

## Backend

## 1. Project structure

```
backend/BrownEvents.Api/
├── Program.cs                  # app bootstrap: DI, middleware, EnsureCreated + seed
├── Controllers/
│   ├── ConferencesController.cs  # /api/conferences  (+ nested sessions/registrations)
│   ├── SessionsController.cs     # /api/sessions
│   ├── SpeakersController.cs     # /api/speakers
│   └── AttendeesController.cs    # /api/attendees  (registrations across conferences, by email)
├── Services/
│   ├── IConferenceService.cs / ConferenceService.cs
│   ├── ISessionService.cs / SessionService.cs
│   ├── ISpeakerService.cs / SpeakerService.cs
│   ├── IRegistrationService.cs / RegistrationService.cs
│   └── ISessionRegistrationService.cs / SessionRegistrationService.cs
├── Models/
│   ├── Conference.cs, Session.cs, Speaker.cs, Room.cs, Attendee.cs, Registration.cs, SessionRegistration.cs
└── Data/
    ├── AppDbContext.cs         # EF Core DbContext + table/relationship config
    └── DataSeeder.cs           # idempotent demo-data seeding
```

There is no `Migrations/` folder — the schema is created at startup via `context.Database.EnsureCreated()` (see `Program.cs:81`), not EF migrations.

## 2. Layer responsibilities

| Layer | Location | Responsibility |
|---|---|---|
| **Controller** | `Controllers/*.cs` | Thin HTTP adapters: parse route/body, call one service method, return `Ok()`/`CreatedAtAction()`/`Created()` on success or a `ProblemDetails` body (404/400/500 — 500s caught by the global exception handler in `Program.cs`) on failure. No business logic. One per top-level resource; nested resources (sessions/registrations) are extra routes on `ConferencesController`. All three controllers are `[ApiController]`. |
| **Service** | `Services/I*.cs` + `Services/*.cs` | All business logic (CRUD, dedup, status rules), talking to `AppDbContext` directly via LINQ. Interfaces let controllers depend on abstractions; registered `Scoped`. |
| **Models** | `Models/*.cs` | Plain EF Core entities: scalar properties + `virtual` navigation properties (required for lazy loading). No behavior/validation logic. |
| **Data** | `Data/AppDbContext.cs` | `DbSet<T>` declarations + `OnModelCreating` (table names, FK relationships). Registered `Scoped`, same lifetime as the services that consume it. |
| **Data (seed)** | `Data/DataSeeder.cs` | One-time demo data, runs on every startup; no-op if `Conferences` already has rows; its `SaveChangesAsync()` calls run inside one DB transaction so a crash partway through can't leave partial/duplicated seed data. |

## 3. Entity-relationship diagram

See [erd.md](erd.md).

## 4. Request flow, end to end

### 4.1 `GET /api/conferences/{id}/sessions` — list a conference's sessions (paged)

1. **Controller**: `ConferencesController.GetSessions(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)` receives the route param and paging query params.
2. **Service**: `ConferenceService.GetSessionsAsync(id, page, pageSize)` clamps `page`/`pageSize`, runs a separate `CountAsync()` for the total, then `_context.Sessions.Where(s => s.ConferenceId == conferenceId).OrderBy(s => s.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync()`.
3. **Data**: EF Core translates the LINQ query (including `Skip`/`Take`) to SQL against the `sessions` table via Npgsql — only one page of rows is fetched, not the full table.
4. **Back up**: controller returns `Ok(result)` where `result` is a `PagedResult<Session>` (`{ data, page, pageSize, totalCount }`), replacing the old bare `{ data = sessions }` envelope. Requesting a page past the last page returns `200 OK` with an empty `data` array, not an error.

### 4.2 `POST /api/conferences/{id}/register` — register an attendee

1. **Controller**: `ConferencesController.Register(int id, [FromBody] Attendee attendee)` model-binds the JSON body to an `Attendee`.
2. **Service**: `RegistrationService.RegisterAttendeeAsync(id, attendee)` looks up an existing `Attendee` by email (creating one if none exists), then creates a `Registration { ConferenceId, AttendeeId, RegisteredAt = UtcNow, Status = "CONFIRMED" }`.
3. **Data**: two round trips to Postgres — an optional `INSERT` into `attendees`, then an `INSERT` into `registrations`.
4. **Back up**: controller returns `Created(...)` (`201`) with the registration and a `Location` header, or a `ProblemDetails` `404` if the conference doesn't exist.

### 4.3 `GET /api/conferences` — list conferences (paged)

1. **Controller**: `ConferencesController.GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)`.
2. **Service**: `ConferenceService.GetAllAsync(page, pageSize)` clamps `page`/`pageSize` (pageSize capped at 100), runs a separate `CountAsync()` for the total, then `_context.Conferences.OrderBy(c => c.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync()`.
3. **Data**: EF Core issues a `SELECT` with `OFFSET`/`LIMIT` against `conferences` — only one page of rows is fetched.
4. **Back up**: controller returns `Ok(result)` where `result` is a `PagedResult<Conference>` (`{ data, page, pageSize, totalCount }`), replacing the old bare array response. Requesting a page past the last page returns `200 OK` with an empty `data` array, not an error.

### 4.4 `GET /api/conferences/{id}` — get one conference

1. **Controller**: `ConferencesController.GetById(int id)`.
2. **Service**: `ConferenceService.GetByIdAsync(id)` awaits `_context.Conferences.FindAsync(id)`.
3. **Data**: EF Core issues a primary-key lookup against `conferences`.
4. **Back up**: controller returns a `ProblemDetails` `404` if the lookup is null, else `Ok(conference)`.

### 4.5 `POST /api/conferences` — create a conference

1. **Controller**: `ConferencesController.Create([FromBody] Conference conference)` model-binds the JSON body.
2. **Service**: `ConferenceService.CreateAsync(conference)` adds the entity and saves — no dedup/lookup, unlike `RegisterAttendeeAsync` (§4.2).
3. **Data**: single `INSERT` into `conferences`.
4. **Back up**: controller returns `CreatedAtAction(...)` (`201`, with `Location` header) instead of `200 OK`.

### 4.6 `PUT /api/conferences/{id}` — update a conference

1. **Controller**: `ConferencesController.Update(int id, [FromBody] Conference conference)`.
2. **Service**: `ConferenceService.UpdateAsync(id, updated)` loads the existing row via `await FindAsync(id)`, copies every field (`Title`, `Description`, `Location`, `Venue`, `StartDate`, `EndDate`, `Status.ToUpper()`, `MaxAttendees`, `OrganizerName`, `Website`) onto it, then saves — a read-modify-write, distinct from the insert-only flows above.
3. **Data**: a `SELECT` (via `FindAsync`) followed by an `UPDATE` against `conferences`.
4. **Back up**: controller returns a `ProblemDetails` `404` if no existing row, else `Ok(updated)`.

### 4.7 `POST /api/conferences/{id}/sessions` — add a session to a conference

1. **Controller**: `ConferencesController.AddSession(int id, [FromBody] Session session)`.
2. **Service**: `ConferenceService.AddSessionAsync(id, session)` sets `session.ConferenceId`, adds it, saves.
3. **Data**: single `INSERT` into `sessions`.
4. **Back up**: controller returns `CreatedAtAction(...)` (`201`) with the created `Session` directly — the old one-off `{ data = created }` envelope was removed so this matches every other create endpoint.

### 4.8 `GET /api/conferences/{id}/registrations` — list a conference's registrations

1. **Controller**: `ConferencesController.GetRegistrations(int id)`.
2. **Service**: `ConferenceService.GetRegistrationsAsync(id)` runs `_context.Registrations.Where(r => r.ConferenceId == conferenceId).ToListAsync()`.
3. **Data**: `SELECT` against `registrations` filtered by `conference_id`.
4. **Back up**: controller returns `Ok(registrations)` — bare array, not wrapped in `{ data }`.

### 4.9 `GET /api/sessions/{id}` — get one session

1. **Controller**: `SessionsController.GetById(int id)`.
2. **Service**: `SessionService.GetByIdAsync(id)` awaits `_context.Sessions.FindAsync(id)`.
3. **Data**: primary-key lookup against `sessions`.
4. **Back up**: `ProblemDetails` `404` if null, else `Ok(session)`.

### 4.10 `PUT /api/sessions/{id}` — update a session

1. **Controller**: `SessionsController.Update(int id, [FromBody] Session session)`.
2. **Service**: `SessionService.UpdateAsync(id, updated)` awaits `_context.Sessions.FindAsync(id)`, copies `Title`, `Description`, `StartTime`, `EndTime`, `Capacity`, `SpeakerId`, `RoomId`, then saves.
3. **Data**: `SELECT` then `UPDATE` against `sessions`.
4. **Back up**: `ProblemDetails` `404` if no existing row, else `Ok(updated)`.

### 4.10a `GET /api/sessions/{id}?email=` — get one session with registration state

1. **Controller**: `SessionsController.GetById(int id, [FromQuery] string? email)`. `email` is optional — the frontend supplies it from `localStorage` (there's no auth system) to ask "is this attendee registered for this session?".
2. **Service**: `SessionService.GetByIdAsync(id)` for the session itself, then `SessionRegistrationService.GetSummaryAsync(id, email)`: `CountAsync` of `CONFIRMED` `SessionRegistration` rows for `RegisteredCount`, a separate `CountAsync` of `WAITLISTED` rows for `WaitlistCount`, plus (only if `email` was supplied) a lookup by trimmed/case-insensitive `Attendee.Email` match for `CurrentAttendeeRegistrationId`.
3. **Data**: primary-key lookup against `sessions`, plus two counts and (conditionally) a filtered lookup against `session_registrations` joined to `attendees`.
4. **Back up**: `ProblemDetails` `404` if the session doesn't exist, else `Ok(session)` with `RegisteredCount`/`WaitlistCount`/`CurrentAttendeeRegistrationId` populated on the DTO.

### 4.10b `POST /api/sessions/{id}/register` — register an attendee for a session

1. **Controller**: `SessionsController.Register(int id, [FromBody] AttendeeRegistrationRequestDto request)`. Binds to a dedicated request DTO (firstName/lastName/email only) rather than the `Attendee` entity directly, since `Attendee` has navigation collections (e.g. `SessionRegistrations`) that a client-controlled body could otherwise use to smuggle rows past the checks in step 2.
2. **Service**: `SessionRegistrationService.RegisterAttendeeAsync(id, session.Capacity, request)` looks up an existing `Attendee` by trimmed/case-insensitive email (creating one if none exists), returns `AlreadyRegistered` if a `CONFIRMED` registration already exists for that attendee/session pair, returns `SessionFull` if the `CONFIRMED` count has reached `Capacity`, otherwise inserts a `CONFIRMED` `SessionRegistration`. Runs inside a transaction (guarded by `Database.IsRelational()`, same pattern as `RegistrationService.RegisterAttendeeAsync`) since it's a multi-step write.
3. **Data**: an optional `INSERT` into `attendees`, plus a count/exists check and an `INSERT` against `session_registrations`.
4. **Back up**: controller returns `Created(...)` (`201`) on success, `Conflict(ProblemDetails)` (`409`) for `AlreadyRegistered`/`SessionFull`, or `ProblemDetails` `404` if the session doesn't exist.

### 4.10c `DELETE /api/sessions/{id}/registrations/{registrationId}` — cancel a session registration

1. **Controller**: `SessionsController.CancelRegistration(int id, int registrationId)`.
2. **Service**: `SessionRegistrationService.CancelRegistrationAsync(id, registrationId)` finds a matching `CONFIRMED` `SessionRegistration` and soft-deletes it (`Status = "CANCELLED"`) rather than removing the row; returns `NotFound` if no such `CONFIRMED` row exists (this covers both "never existed" and "already cancelled" — there's no distinct `AlreadyCancelled` state). After the cancellation is saved, it looks up the earliest `WAITLISTED` row for the session (ordered by `RegisteredAt`, tiebreak `Id`) and, if one exists, promotes it to `CONFIRMED` and logs an `Information`-level message via the injected `ILogger<SessionRegistrationService>` — the first per-service logging call in the backend (see [[development-practices]]).
3. **Data**: `SELECT` then `UPDATE` against `session_registrations`; same transaction guard as §4.10b; the waitlist-promotion lookup/update runs inside the same transaction.
4. **Back up**: `NoContent()` (`204`) on success, `ProblemDetails` `404` if the session or the active registration doesn't exist.

### 4.10d `POST /api/sessions/{id}/waitlist` — join a session's waitlist

1. **Controller**: `SessionsController.JoinWaitlist(int id, [FromBody] AttendeeRegistrationRequestDto request)`. Reuses the same request DTO as §4.10b — no separate waitlist-specific request shape.
2. **Service**: `SessionRegistrationService.JoinWaitlistAsync(id, session.Capacity, request)` looks up an existing `Attendee` by trimmed/case-insensitive email (creating one if none exists), returns `AlreadyRegistered` if a `CONFIRMED` registration already exists for that attendee/session pair, returns `AlreadyWaitlisted` if a `WAITLISTED` one already exists, returns `SessionNotFull` if the `CONFIRMED` count hasn't reached `Capacity` (waitlisting is only allowed once the session is full), otherwise inserts a `WAITLISTED` `SessionRegistration`. Same transaction guard as §4.10b.
3. **Data**: an optional `INSERT` into `attendees`, plus count/exists checks and an `INSERT` against `session_registrations`.
4. **Back up**: controller returns `Created(...)` (`201`) on success, `Conflict(ProblemDetails)` (`409`) for `AlreadyRegistered`/`AlreadyWaitlisted`/`SessionNotFull`, or `ProblemDetails` `404` if the session doesn't exist.

### 4.11 `GET /api/speakers` — list all speakers

1. **Controller**: `SpeakersController.GetAll()`.
2. **Service**: `SpeakerService.GetAllAsync()` runs `_context.Speakers.ToListAsync()`.
3. **Data**: `SELECT` against `speakers`.
4. **Back up**: `Ok(speakers)`.

### 4.12 `POST /api/speakers` — create a speaker

1. **Controller**: `SpeakersController.Create([FromBody] Speaker speaker)`.
2. **Service**: `SpeakerService.CreateAsync(speaker)` adds the entity and saves.
3. **Data**: single `INSERT` into `speakers`.
4. **Back up**: `Created(...)` (`201`) with the created speaker, instead of `200 OK`.

### 4.13 `GET /api/attendees/{email}/registrations` — list an attendee's registrations across conferences

1. **Controller**: `AttendeesController.GetRegistrations(string email)`.
2. **Service**: `RegistrationService.AttendeeExistsAsync(email)` first (case-insensitive match) to distinguish "no such attendee" (404) from "attendee exists, zero registrations" (200 + `[]`); then `RegistrationService.GetByAttendeeEmailAsync(email)` runs `_context.Registrations.Include(r => r.Conference).Where(r => r.Attendee.Email.ToLower() == normalizedEmail).OrderByDescending(r => r.RegisteredAt).ToListAsync()`.
3. **Data**: an `EXISTS`-style query against `attendees`, then a `SELECT` against `registrations` joined to `conferences` (eager-loaded to populate the DTO's conference title/dates without N+1).
4. **Back up**: `ProblemDetails` `404` if no attendee matches the email, else `Ok(registrations)` — bare array, possibly empty.

### 4.14 `DELETE /api/conferences/{id}/registrations/{registrationId}` — cancel a conference registration

1. **Controller**: `ConferencesController.CancelRegistration(int id, int registrationId)`.
2. **Service**: `RegistrationService.CancelRegistrationAsync(id, registrationId)` finds a matching `CONFIRMED` `Registration` scoped to that conference and flips it to `Status = "CANCELLED"`; returns `NotFound` if no such `CONFIRMED` row exists (covers both "never existed" and "already cancelled"). No waitlist promotion — conference-level `Registration` has no `WAITLISTED` status, unlike `SessionRegistration` (§4.10c).
3. **Data**: `SELECT` then `UPDATE` against `registrations`, wrapped in the same `Database.IsRelational()` transaction guard as `RegisterAttendeeAsync` (§4.2).
4. **Back up**: `NoContent()` (`204`) on success, `ProblemDetails` `404` if the conference or the active registration doesn't exist.

## Frontend

### 5. Project structure

```
frontend/src/
├── main.jsx                     # React root, mounts <App/> in <React.StrictMode>
├── App.jsx                      # BrowserRouter + the full route table (8 routes)
├── api.js                       # fetch wrapper + one exported function per backend endpoint
├── index.css                    # global styles: cards, badges, modal, forms, navbar
├── pages/
│   ├── ConferenceListPage.jsx    # "/" — fetch + render one page of conferences, with Prev/Next paging
│   ├── ConferenceCreatePage.jsx  # "/conferences/new" — standalone create-conference form
│   ├── ConferenceDetailPage.jsx  # "/conferences/:id" — orchestrator: composes hooks/ + components/ below
│   ├── ConferenceEditPage.jsx    # "/conferences/:id/edit" — fetches the conference, standalone edit form
│   ├── SessionDetailPage.jsx     # "/conferences/:id/sessions/:sessionId" — one session + its speaker/room
│   ├── RegistrationPage.jsx      # "/conferences/:id/register" — standalone registration form
│   ├── SpeakerListPage.jsx       # "/speakers" — fetch + render all speakers, no pagination (backend doesn't paginate this endpoint)
│   └── DashboardPage.jsx         # "/dashboard" — attendee looks up their registrations by email, can cancel a CONFIRMED one
├── hooks/
│   ├── useConference.js               # fetch one conference by id -> { conference, loading, error }
│   ├── useConferenceSessions.js       # paginated sessions -> { sessions, totalCount, loading, error }
│   ├── useConferenceRegistrations.js  # registrations list -> { registrations, loading, error }
│   ├── useRegistrationForm.js         # registration modal open/close + form state + submit
│   └── useAttendeeDashboard.js        # load(email)/cancel(...) -> { registrations, loading, error }, backs DashboardPage
├── utils/
│   └── format.js                 # formatDate, getStatusBadgeClass, isRegistrationOpen — shared by ConferenceDetailPage's components
└── components/
    ├── ConferenceCard.jsx        # summary card, used in ConferenceListPage's grid
    ├── SessionCard.jsx           # summary card, used by SessionList
    ├── SessionList.jsx           # thin map: sessions[] -> <SessionCard>
    ├── SpeakerCard.jsx           # summary card (name/bio/email), used in SpeakerListPage's grid
    ├── Navbar.jsx                 # shared top nav bar
    ├── ConferenceHeader.jsx       # title/status/meta/description block + Edit link
    ├── ConferenceInfoTable.jsx    # conference details table
    ├── ConferenceSessionsSection.jsx  # sessions loading/error/empty + SessionList + Pagination
    ├── RegistrationsSection.jsx   # registrations list + "Register Now" button (hidden when registration is closed)
    ├── SelectedSessionCard.jsx    # session picked from the list, with view/dismiss actions
    └── RegistrationModal.jsx      # registration overlay/modal + form
```

`hooks/` and `utils/` exist only for `ConferenceDetailPage`'s decomposition so far — other pages still
own their state inline via `useState`/`useEffect`. There's no `context/` or state-management folder;
nothing is shared between pages except through URL params and re-fetching.

### 6. Routing and API client

| Path (`App.jsx`) | Page |
|---|---|
| `/` | `ConferenceListPage` |
| `/conferences/new` | `ConferenceCreatePage` |
| `/conferences/:id` | `ConferenceDetailPage` |
| `/conferences/:id/edit` | `ConferenceEditPage` |
| `/conferences/:id/sessions/:sessionId` | `SessionDetailPage` |
| `/conferences/:id/register` | `RegistrationPage` |
| `/speakers` | `SpeakerListPage` |
| `/dashboard` | `DashboardPage` |

`api.js` is the only place `fetch` is called:
- `BASE_URL` is hardcoded to `http://localhost:5000`, bypassing the Vite dev-server proxy.
- `request()` is a shared wrapper: sets `Content-Type: application/json` and throws an `Error` on non-`ok` responses.
- `getConferences` and `getConferenceSessions` both accept `(page, pageSize)` and return the paged envelope as-is (`{ data, page, pageSize, totalCount }`); callers read `.data`/`.totalCount` themselves rather than the client unwrapping it. A shared `Pagination` component (`components/Pagination.jsx`) renders Prev/Next controls from that envelope.
- No generated client, no response typing/validation. Mostly one function per backend endpoint: `getAttendeeRegistrations` → `GET /api/attendees/{email}/registrations` (§4.13), and `deleteRegistration` → `DELETE /api/conferences/{id}/registrations/{registrationId}` (§4.14, cancels rather than deletes the row).

### 7. Data flow, end to end

#### 7.1 `/conferences/:id` — `ConferenceDetailPage`

The page itself is a thin orchestrator that calls three data hooks — `useConference`,
`useConferenceSessions`, `useConferenceRegistrations` — each wrapping its own `useEffect` with
independent `loading`/`error` state, so partial failures (e.g. sessions fail to load while the
rest succeeds) are possible. A fourth hook, `useRegistrationForm`, owns the modal/form state.

Registration happens two ways from here: an **inline modal** (threading `sessionId` into the payload if a session card was clicked first), or the **full-page** `RegistrationPage`. Both call `registerAttendee(id, data)` → `POST /api/conferences/{id}/register` (backend §4.2), via independently maintained duplicate forms.

#### 7.2 `/` — `ConferenceListPage`

`getConferences()` → `GET /api/conferences` → array of conferences rendered as a `ConferenceCard` grid, each linking to its detail and registration pages.

#### 7.3 `/conferences/:id/sessions/:sessionId` — `SessionDetailPage`

Owns its data/action state via `useSessionRegistration(sessionId)` (`hooks/useSessionRegistration.js`), which fetches the session (`getSession`, §4.10a) on mount and exposes `register`/`joinWaitlist`/`cancel` actions plus `actionLoading`/`actionError`. All three actions re-fetch the session after a successful call so `registeredCount`/`waitlistCount`/`currentAttendeeRegistrationId` stay current. The page shows one of three states based on the fetched session: **registered** (cancel button), **full** (`registeredCount >= capacity`, "Join Waitlist" button → `joinWaitlist` → `POST /api/sessions/{id}/waitlist`, §4.10d), or **open** ("Register" button → `register` → §4.10b) — both buttons open the same `SessionRegisterModal` component with mode-specific title/labels.

Other pages not covered above follow the same fetch-on-mount → render shape.

## Cross-cutting notes

See [cross-cutting-notes.md](cross-cutting-notes.md) for gotchas and deviations spanning both backend and frontend.

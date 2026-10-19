# EPMCDMPTST-1960 — Hide Register Now button for inactive conferences

## Context

`ConferenceDetailPage.jsx` always renders the "Register Now" button regardless of
conference status. Users on completed/cancelled conferences can click it and either
get a backend error or register for a conference that's no longer accepting
registrations. The backend `Conference.Status` field (`"UPCOMING" | "ACTIVE" |
"COMPLETED" | "CANCELLED"`, default `"UPCOMING"`) is already authoritative and already
shipped in the API payload (`GET /api/conferences/{id}`) — no backend change needed.

## TDD

No — there is currently zero frontend test infrastructure in this repo (no vitest/jest/
RTL in `package.json`, no test script, no existing `.test.jsx` files). Standing up a test
framework from scratch is out of scope for a one-condition UI gating fix. Will verify
manually via the running app (backend already seeds one conference of each status via
`DataSeeder.cs`, so completed/cancelled/upcoming/active cases are all reachable without
extra setup) and via `npm run lint`/`npm run build`.

## Plan

1. In `frontend/src/pages/ConferenceDetailPage.jsx`, add a small helper (near
   `getStatusBadgeClass`) e.g. `isRegistrationOpen(status)` that returns `false` for
   `'completed'` and `'cancelled'` (case-insensitive), `true` otherwise (covers
   `'upcoming'`, `'active'`, and any unrecognized/missing status — matches existing
   `getStatusBadgeClass` default-to-upcoming behavior).
2. Wrap the existing `<button className="btn btn--primary" onClick={handleRegisterClick}>
   Register Now</button>` (around line 347) in
   `{isRegistrationOpen(conference?.status) && ( ... )}` so it's simply not rendered for
   completed/cancelled conferences. No changes to `handleRegisterClick`, the registration
   modal, or `registerAttendee` — the registration flow itself is untouched, so active/
   upcoming conferences keep working exactly as before.

## Verification

- `npm run lint` and `npm run build` in `frontend/` to catch syntax/lint issues.
- Manually run the app (`podman`/`docker-compose up --build` per `run` skill), open the
  seeded conferences of each status (`DataSeeder.cs` seeds 3 conferences — check their
  statuses, or manually flip a status via the API/DB if seed data doesn't cover
  cancelled/completed) and confirm:
  - Completed conference → no Register Now button.
  - Cancelled conference → no Register Now button.
  - Upcoming/Active conference → button visible, click still opens the registration
    modal and submits successfully (no regression).

## Out of scope

- Adding a frontend test framework.
- Backend changes (status is already correct/shipped as-is).
- Deriving status from dates (ticket is about gating on existing `status` field, not
  fixing potential status/date drift).

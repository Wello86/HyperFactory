# EPMCDMPTST-1958 — Fix Read Performance: Remove Unnecessary Change Tracking

## Context

All service-layer read queries currently track returned entities (EF Core's default
`QueryTrackingBehavior.TrackAll`), even though these entities are never mutated in the same
request. This wastes memory (snapshot copies for change detection) and slows down reads. No global
tracking override exists in `AppDbContext`/`Program.cs`, and `.AsNoTracking()` is used nowhere in
the codebase today. Fix: add `.AsNoTracking()` to every read-only query, leave write/mutate paths
untouched.

## TDD decision

TDD: no. `.AsNoTracking()` changes tracking behavior, not shape of returned data (no assertable
before/after difference at the unit-test level — `AsNoTracking` only affects EF's internal
identity map/change tracker, which existing tests won't observe unless a test specifically asserts
`ChangeTracker.Entries().Count()`). Will rely on the existing test suite (`BrownEvents.Tests`) to
confirm no behavior regression, plus a manual smoke check that responses are unchanged.

## Scope: methods to change

Add `.AsNoTracking()` to these read-only queries only:

1. `Services/ConferenceService.cs`
   - `GetAllAsync` (line ~27, the `_context.Conferences...ToListAsync()` chain)
   - `GetByIdAsync` (line ~50, the `_context.Conferences...FirstOrDefaultAsync()` chain)
   - `GetSessionsAsync` (line ~88/95, the `query` built from `_context.Sessions.Where(...)`)
   - `GetRegistrationsAsync` (line ~122, `_context.Registrations.Where(...).ToListAsync()`)
2. `Services/SessionService.cs`
   - `GetByIdAsync` (line ~21, `_context.Sessions.Include...FirstOrDefaultAsync()`)
3. `Services/SpeakerService.cs`
   - `GetAllAsync` (line ~18, `_context.Speakers.ToListAsync()`)

`.AsNoTracking()` goes right after the root `DbSet` access (e.g. `_context.Conferences.AsNoTracking()...`),
before `.Where`/`.Include`/`.OrderBy` — this is idiomatic EF Core and avoids ordering issues since
`AsNoTracking()` just flips a query-option flag, order relative to other LINQ operators doesn't
matter functionally, but placing it first keeps it readable/consistent across all 6 call sites.

## Explicitly excluded (writes / mutation-dependent — must stay tracked)

- `ConferenceService.CreateAsync`, `UpdateAsync`, `AddSessionAsync`
- `SessionService.UpdateAsync` (its `FindAsync` must stay tracked since the entity is mutated and
  saved; note `UpdateAsync`'s trailing `Entry(existing).Reference(...).LoadAsync()` calls are
  reloading nav props on that same tracked entity post-save, not separate no-track candidates)
- `SpeakerService.CreateAsync`
- `RegistrationService.RegisterAttendeeAsync` (contains a read used to decide whether to attach an
  existing `Attendee`, must stay tracked as it's part of the same write unit-of-work)

## Verification

- Run `dotnet test BrownEvents.Tests` (via podman, per repo convention) — full suite should pass
  unchanged; this confirms acceptance criterion "no data missing from responses" and "write
  operations unaffected."
- Manual smoke: after change, spin up the app (or run targeted tests) and hit
  `GET /api/conferences`, `GET /api/conferences/{id}`, `GET /api/conferences/{id}/sessions`,
  `GET /api/conferences/{id}/registrations`, `GET /api/sessions/{id}`, `GET /api/speakers` to
  confirm response shape/content unchanged.
- `dotnet format BrownEvents.sln --verify-no-changes` is not runnable locally (no dotnet installed
  per CLAUDE.md); rely on CI `backend:lint` job, and hand-check `using` ordering isn't affected
  since only `.AsNoTracking()` calls are added, no new usings needed (already using
  `Microsoft.EntityFrameworkCore` in each file).

## Out of scope

- `RegistrationService` and its write-heavy `RegisterAttendeeAsync` — no read-only method to fix.
- Any repository-pattern refactor — not requested, keep the existing raw-LINQ-on-DbSet style.
- Global `QueryTrackingBehavior.NoTrackingWithIdentityResolution` default on `AppDbContext` —
  ticket asks to fix read-only queries specifically, not to change the global default (which would
  silently affect any future write-via-read logic).

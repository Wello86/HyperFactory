# Fix slow conference page (N+1 query bug on sessions)

## Context

Frontend conference detail page calls `GET /api/conferences/{id}` and `GET /api/conferences/{id}/sessions`
(`frontend/src/api.js`). `AppDbContext` is registered `UseLazyLoadingProxies()` (`Program.cs`), and
`Session`/`Conference` navigation properties (`Speaker`, `Room`, `Conference`, `Sessions`) are all
`virtual`. None of the service methods use `.Include()` anywhere in the codebase. So every time a
list of `Session`s is JSON-serialized, each session's `Speaker` and `Room` nav properties trigger a
separate lazy-load SQL query — an N+1: 1 query for the sessions, then 2 more per session. That's the
root cause of "conference page slow when many sessions."

Primary offender: `ConferenceService.GetSessionsAsync` (backs `GET /api/conferences/{id}/sessions`,
the endpoint named in the acceptance criteria). `ConferenceService.GetByIdAsync` and
`SessionService.GetByIdAsync`/`UpdateAsync` have the same latent issue (Sessions collection / single
session's Speaker/Room), so the fix is applied consistently to all of them.

## TDD: yes

EF Core's lazy-loading proxies still intercept navigation getters under the InMemory provider (proxy
creation is provider-independent), but InMemory doesn't execute real SQL commands, so we can't count
SQL round-trips with a `DbCommandInterceptor` there. Instead the test asserts eager-loading state
directly via `context.Entry(x).Reference(...).IsLoaded` / `.Collection(...).IsLoaded` right after the
service call returns — true only if `.Include()` was used, false if it'll lazy-load on first access.
This fails today (red) and passes once `.Include()` is added (green), and is a faithful proxy for
"queries are bounded" without needing a relational test provider.

## Steps

1. In `backend/BrownEvents.Tests/ConferenceServiceTests.cs`: extend
   `GetSessionsAsync_ReturnsOnlyConferenceSessions` (or add a new test) that seeds a `Speaker` and
   `Room` on each session, creates the context with `.UseLazyLoadingProxies()` added to the
   `DbContextOptionsBuilder`, calls `GetSessionsAsync`, and asserts
   `context.Entry(session).Reference(s => s.Speaker).IsLoaded` and `...Reference(s => s.Room).IsLoaded`
   are `true` for every returned session. Add an analogous assertion for `GetByIdAsync` checking
   `context.Entry(conference).Collection(c => c.Sessions).IsLoaded`.
2. In `backend/BrownEvents.Tests/SessionServiceTests.cs`: add the same kind of `IsLoaded` assertion
   for `GetByIdAsync` and `UpdateAsync` (Speaker/Room loaded).
3. Run `dotnet test --filter Sessions` / the new tests, confirm they fail for the expected reason
   (nav props not loaded).
4. Fix `backend/BrownEvents.Api/Services/ConferenceService.cs`:
   - `GetSessionsAsync`: add `.Include(s => s.Speaker).Include(s => s.Room)`, with a comment at this
     line documenting the N+1 root cause (lazy-loading proxies + virtual nav props → 1 query per
     session when serialized).
   - `GetByIdAsync`: add `.Include(c => c.Sessions).ThenInclude(s => s.Speaker)` and
     `.Include(c => c.Sessions).ThenInclude(s => s.Room)` (switch from `FindAsync` to a query since
     `FindAsync` can't take `Include`).
5. Fix `backend/BrownEvents.Api/Services/SessionService.cs`:
   - `GetByIdAsync`: switch from `FindAsync` to `_context.Sessions.Include(s => s.Speaker).Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == id)`.
   - `UpdateAsync`: same Include when loading `existing` before mutating/returning it.
6. Re-run the new/updated tests — confirm green — then run the full `dotnet test BrownEvents.Tests`
   suite to confirm no regressions (existing assertions on returned data/shape are unchanged, since
   `.Include()` only changes what's eagerly loaded, not what's returned).
7. Manually sanity-check response shape is unchanged (same JSON fields) — Include doesn't add/remove
   properties, only pre-loads them, so `ReferenceHandler.IgnoreCycles` output stays identical.

## Verification

- New/updated unit tests in `ConferenceServiceTests.cs` / `SessionServiceTests.cs` pass.
- Full suite: `dotnet test backend/BrownEvents.Tests` (or via podman if docker unavailable).
- Confirm no `.Include()` was needed elsewhere (`AddSessionAsync`, `CreateAsync`, `GetAllAsync`,
  `GetRegistrationsAsync` don't serialize per-item nav props in a loop the same way, so out of scope).

## Out of scope

- Changing the AppDbContext Singleton registration or other unrelated gotchas from CLAUDE.md.
- Changing JSON response shape/fields.
- Frontend changes.

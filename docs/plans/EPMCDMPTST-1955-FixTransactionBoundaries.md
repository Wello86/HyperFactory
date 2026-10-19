# Fix transaction boundaries (EPMCDMPTST-1955)

## Context

`RegistrationService.RegisterAttendeeAsync` (`backend/BrownEvents.Api/Services/RegistrationService.cs:15-45`)
saves a new `Attendee` and then a `Registration` in two separate `SaveChangesAsync()` calls. If
the process crashes, the request is cancelled, or the second save throws (e.g. a future FK/
validation failure) after the first one succeeds, the attendee row is committed with no matching
registration — an orphaned row the app has no way to detect or clean up. The ticket asks to find
*all* such multi-step write operations in the backend and make them atomic, with a comment on each
explaining the corruption a transaction prevents.

Repo-wide search (via Explore agent + manual read of `DataSeeder.cs`) found exactly **two**
qualifying methods — every other service method (`ConferenceService.CreateAsync/UpdateAsync/
AddSessionAsync`, `SessionService.UpdateAsync`, `SpeakerService.CreateAsync`) calls
`SaveChangesAsync()` only once, which EF Core already wraps in a single implicit transaction:

1. **`RegistrationService.RegisterAttendeeAsync`** — the one named in the ticket.
2. **`DataSeeder.SeedAsync`** (`backend/BrownEvents.Api/Data/DataSeeder.cs:7-161`) — seeds
   Speakers → Rooms → Conferences → Sessions across **four** separate `SaveChangesAsync()` calls,
   guarded only by `if (context.Conferences.Any()) return;`. If the process dies after speakers/
   rooms are saved but before conferences are, the guard doesn't trip on restart (`Conferences` is
   still empty), so `SeedAsync` re-runs and duplicates the speakers/rooms it already inserted. Same
   class of bug, same fix.

No existing transaction pattern exists in the repo (`BeginTransactionAsync`/`IDbContextTransaction`/
`TransactionScope` — zero matches), so this introduces the pattern for the first time. `AppDbContext`
is a plain EF Core `DbContext` (Npgsql provider in production, EF Core 10), so
`Database.BeginTransactionAsync()` is available directly with no extra plumbing needed.

## TDD decision

**No.** A true red test would need to force a failure between the two saves and observe the
first save get rolled back — but unit tests run against the EF Core **InMemory** provider
(`BrownEvents.Tests` uses `UseInMemoryDatabase`, confirmed in `ConferenceServiceTests.CreateContext()`),
and InMemory's transaction support is a documented no-op (it logs a `TransactionIgnoredWarning` but
doesn't actually defer or roll back writes — each `SaveChangesAsync()` commits to the in-memory
store immediately regardless of any open `IDbContextTransaction`). Writing a "rollback" test against
InMemory would either pass vacuously or require mocking `DbContext` internals, which isn't a seam
this codebase has and isn't worth introducing for this fix. Real rollback behavior only manifests
against the real Npgsql/Postgres provider used in production.
Instead: add regression tests for the happy paths (new attendee, existing attendee reuse) to prove
the transaction wrapper doesn't change observable behavior, and rely on the code comment + manual
reasoning for the correctness argument about the failure path, per the acceptance criteria.

## Implementation steps

1. **`RegistrationService.RegisterAttendeeAsync`**: wrap the existing body (both `SaveChangesAsync`
   calls) in `using var transaction = await _context.Database.BeginTransactionAsync();` /
   `await transaction.CommitAsync();`. Remove the stray `//` comment line already there and replace
   it with a real comment explaining that without the transaction, a failure after the attendee
   insert but before the registration insert leaves an attendee row with no corresponding
   registration (an orphaned attendee that looks registered nowhere). No functional change to the
   lookup-or-create logic itself.

2. **`DataSeeder.SeedAsync`**: wrap the whole body (after the `if (context.Conferences.Any()) return;`
   guard) in the same `BeginTransactionAsync`/`CommitAsync` pattern, so all four `SaveChangesAsync()`
   calls (speakers, rooms, conferences, sessions) commit together or not at all. Comment explains
   that without the transaction, a crash partway leaves partial demo data, and — because the
   idempotency guard only checks `Conferences.Any()` — a restart after such a crash would re-seed
   speakers/rooms a second time (duplicate rows) since they don't gate re-entry the same way
   conferences do.

3. **Tests** — add `backend/BrownEvents.Tests/RegistrationServiceTests.cs` following the
   `CreateContext()` pattern from `ConferenceServiceTests.cs`:
   - `RegisterAttendeeAsync_CreatesNewAttendeeAndRegistration_WhenEmailNotFound`
   - `RegisterAttendeeAsync_ReusesExistingAttendee_WhenEmailFound` (only one attendee row ends up
     in the DB; a new registration still gets created)
   These guard against the transaction wrapper accidentally breaking the create-or-reuse logic or
   the return value.
   No new test file for `DataSeeder` — it has no existing test coverage and none is required by the
   acceptance criteria; existing tests that indirectly exercise it (`DbContextConcurrencyTests`,
   which boots the app via `WebApplicationFactory<Program>` against InMemory) already assert on the
   outcome (`200 OK` on all concurrent calls) and will catch a regression if the wrap breaks startup.

4. Run the full backend suite (`dotnet test` inside the repo's docker/podman toolchain, since
   `dotnet` isn't installed locally per `CLAUDE.md`) to confirm `ConferenceServiceTests`,
   `SessionServiceTests`, `DbContextConcurrencyTests`, `SwaggerDocumentationTests`, and the new
   `RegistrationServiceTests` all pass.

5. Hand-check `using` directive ordering in any touched file against `.editorconfig`'s rule
   (`System.*` first alphabetically, then everything else alphabetically, no blank line between
   groups) since `dotnet format` can't run locally.

## Out of scope

- No changes to `ConferenceService`, `SessionService`, or `SpeakerService` — each has exactly one
  `SaveChangesAsync()` call per method, already atomic via EF Core's implicit per-`SaveChanges`
  transaction.
- Not introducing a generic "unit of work" abstraction or interface around `AppDbContext` —
  only two call sites need this, and `AppDbContext` is already directly injected everywhere; adding
  a wrapper would be scope creep for this fix.

## Verification

- `dotnet test backend/BrownEvents.Tests` (via docker/podman) — all existing tests green, plus the
  two new `RegistrationServiceTests`.
- Manual read-through confirming both transactional methods have the required explanatory comment
  per the acceptance criteria.

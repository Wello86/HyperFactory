# Database Patterns

**Project**: BrownEvents
**ORM**: EF Core 10.0.12 (`Microsoft.EntityFrameworkCore`, `.Proxies`, `.Relational`) + Npgsql 10.0.3
**Database**: PostgreSQL 14
**Schema management**: `Database.EnsureCreated()` — **no `Migrations/` folder exists**

---

## Connection Setup

```csharp
// backend/BrownEvents.Api/Program.cs:22-32
builder.Services.AddScoped<AppDbContext>(sp =>
{
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString)
        .UseLazyLoadingProxies();
    if (builder.Environment.IsDevelopment())
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }
    return new AppDbContext(optionsBuilder.Options);
});
```

Connection string source: `builder.Configuration.GetConnectionString("DefaultConnection")` (`Program.cs:15`) → `appsettings.json` `ConnectionStrings:DefaultConnection` (hardcoded plaintext credentials — see [[security-practices]]).

**Registration lifetime is `AddScoped`, not `AddSingleton`.** A comment at `Program.cs:16-21` documents that `AddSingleton` previously caused concurrency crashes under concurrent requests — do not change this without re-reading that comment.

## Data Access Pattern

Services query `AppDbContext` directly — there is no repository interface layer:

```csharp
// backend/BrownEvents.Api/Services/ConferenceService.cs:9
private readonly AppDbContext _context;
```

## Entity/Model Definition

Seven entities in `backend/BrownEvents.Api/Models/`: `Conference`, `Session`, `Speaker`, `Room`, `Attendee`, `Registration`, `SessionRegistration`. Validation via data annotations, e.g.:

```csharp
// backend/BrownEvents.Api/Models/Conference.cs:9-10
[Required]
[MaxLength(200)]
public string Title { get; set; }
```

All navigation properties are `virtual` (required for `UseLazyLoadingProxies()`).

## Relationships

```csharp
// backend/BrownEvents.Api/Models/Session.cs:18-26
public int ConferenceId { get; set; }         // required FK
public int? SpeakerId { get; set; }           // optional FK
public int? RoomId { get; set; }              // optional FK
[ValidateNever]
public virtual Conference Conference { get; set; }
public virtual Speaker? Speaker { get; set; }
public virtual Room? Room { get; set; }
public virtual ICollection<SessionRegistration> SessionRegistrations { get; set; }
```

Configured in `OnModelCreating`: Conference→Sessions/Registrations (`Data/AppDbContext.cs:24-29`), Session→Speaker/Room as optional (`:36-43`), Registration→Attendee (`:53-55`), SessionRegistration→Session/Attendee (`:61-66`). `docs/erd.md` (Mermaid ERD) matches these seven entities and their FK nullability exactly — no drift found.

**Many-to-many via join entities**: `Session` ↔ `Attendee` goes through `SessionRegistration` (`Models/SessionRegistration.cs:9-13`); `Conference` ↔ `Attendee` goes through `Registration` (`Models/Registration.cs:9-13`). Neither uses EF's implicit skip-navigation many-to-many — both are explicit join entities with their own FKs.

## Query Patterns

Eager-load with `.Include()`/`.ThenInclude()` to avoid N+1 against the lazy-loading proxies — comments explain this explicitly:

```csharp
// backend/BrownEvents.Api/Services/ConferenceService.cs:48-55
var data = await query
    .AsNoTracking()
    .OrderBy(c => c.Id)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Include(c => c.Sessions).ThenInclude(s => s.Speaker)
    .Include(c => c.Sessions).ThenInclude(s => s.Room)
    .ToListAsync(cancellationToken);
```

Read-only list queries add `.AsNoTracking()` (`ConferenceService.cs:49,73`, `SpeakerService.cs:18`). Async suffix is universal: `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync` (e.g. `SessionRegistrationService.cs:28,47,55`).

**No raw SQL anywhere** — no `FromSqlRaw`/`ExecuteSqlRaw` in the codebase; all access is parameterized LINQ.

## Transactions

Two methods need atomicity across multiple `SaveChangesAsync()` calls and guard the transaction on `Database.IsRelational()`, because the EF InMemory provider used by tests no-ops `BeginTransactionAsync` (logs `TransactionIgnoredWarning` instead of failing):

```csharp
// backend/BrownEvents.Api/Services/RegistrationService.cs:23-25
using var transaction = _context.Database.IsRelational()
    ? await _context.Database.BeginTransactionAsync()
    : null;
// ... two SaveChangesAsync calls in between ...
// RegistrationService.cs:51-54
if (transaction is not null)
{
    await transaction.CommitAsync();
}
```

The same pattern is used in `SessionRegistrationService.cs:22-24,83-85,151-153` and `Data/DataSeeder.cs:18-20,173-176`. **Follow this exact guarded pattern for any new multi-step write** — an unguarded `BeginTransactionAsync()` will pass against Npgsql but throw or misbehave in InMemory-backed tests.

## Write Ordering

Construct and persist a **new** entity only after every domain-validation check has run, so a rejected write never leaves an orphaned row behind. `JoinWaitlistAsync` (`backend/BrownEvents.Api/Services/SessionRegistrationService.cs:81-147`) demonstrates this: it loads `existingAttendee` read-only first (`:88-89`), runs the `AlreadyRegistered`/`AlreadyWaitlisted` checks against it (`:91-108`) and the capacity check (`:110-115`), and only creates + saves a new `Attendee` after every check passes (`:117-128`) — a reviewer fix, since the original version saved the `Attendee` before those checks and could leave one orphaned on rejection under EF InMemory (no real transaction rollback there — see Transactions above).

| Avoid | Prefer |
|---|---|
| Creating/saving a new entity before validation checks run | Run every check first; create and save the new entity only once all of them pass |

**Known asymmetry**: `RegisterAttendeeAsync` (`SessionRegistrationService.cs:20-79`) still creates and saves a new `Attendee` (`:33-40`) *before* its own `AlreadyRegistered`/`SessionFull` checks (`:47-60`) — the same orphan-row risk `JoinWaitlistAsync` was fixed to avoid. This is a pre-existing inconsistency between the two methods, not a pattern to copy; follow `JoinWaitlistAsync`'s ordering for any new write.

## Eager Loading / N+1

`UseLazyLoadingProxies()` (`Program.cs:26`) means any unloaded `virtual` nav property triggers a lazy DB round-trip the first time it's accessed — this is why every list-returning service method explicitly `.Include()`s the nav properties its DTO mapping will read (`ConferenceService.cs:32-34,53-55`, `SessionService.cs:23-25`). When adding a new endpoint that returns a nav property, add the matching `.Include()` rather than relying on lazy loading, to avoid N+1 across a result set.

## Migrations

**None.** `Database.EnsureCreated()` (`Program.cs:102`) builds the schema from the current model on startup; there is no `Migrations/` folder and no `dotnet ef migrations add` workflow. Any model change takes effect on the next `EnsureCreated()` run against an empty database — it will **not** alter an existing schema. Coordinate schema changes with a fresh database (`docker-compose down -v` on the `postgres` volume) rather than expecting an in-place migration.

## Seeding / Fixtures

```csharp
// backend/BrownEvents.Api/Data/DataSeeder.cs:10
if (context.Conferences.Any()) return;
```

Idempotent no-op guard, then seeds `Speakers` → `Rooms` → `Conferences` → `Sessions` in order (each batch via `AddRange` + `SaveChangesAsync`), inside the same `IsRelational()`-guarded transaction described above (`DataSeeder.cs:18-176`). Called from `Program.cs:103` right after `EnsureCreated()`. Demo data: 3 conferences, 7 sessions, 3 speakers, 4 rooms.

## Raw Queries

Not used. See Query Patterns above.

## Error Handling

No database-specific exception handling exists in `Services/*` — DB exceptions propagate to the global handler at `Program.cs:74-90`, which returns a generic 500 `ProblemDetails`. There is no retry/resilience policy around Npgsql calls.

## Conventions Summary

- One `AppDbContext` (`AddScoped`), one PostgreSQL database, no repository layer.
- Every service holds `AppDbContext` directly; queries are LINQ, never raw SQL.
- `.Include()`/`.ThenInclude()` for any nav property the response DTO needs — don't rely on lazy loading in a loop.
- Guard any new cross-`SaveChangesAsync()` write with the `Database.IsRelational()` transaction pattern.
- No migrations — schema changes require `EnsureCreated()` against a fresh database.

## Quick Reference

| Need | Location |
|---|---|
| DbContext + relationship config | `backend/BrownEvents.Api/Data/AppDbContext.cs` |
| Connection string / DI wiring | `backend/BrownEvents.Api/Program.cs:15,22-30` |
| Entity models | `backend/BrownEvents.Api/Models/` |
| Demo data seeding | `backend/BrownEvents.Api/Data/DataSeeder.cs` |
| ERD reference | `docs/erd.md` |
| Transaction-guard pattern | `RegistrationService.cs:23-25`, `SessionRegistrationService.cs:22-24,83-85,151-153` |
| Write-ordering pattern | `SessionRegistrationService.cs:88-128` (validate-before-persist) |

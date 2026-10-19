# Testing Patterns

**Project**: BrownEvents (backend only — no frontend test runner exists)
**Framework**: xUnit 2.9.3 (+ `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk 18.9.0`)
**Mocking**: Moq 4.20.72 — declared but **unused** (no `Mock<...>` anywhere in the suite)
**Test DB**: EF Core InMemory provider (`Microsoft.EntityFrameworkCore.InMemory 10.0.12`)
**Test Location**: `backend/BrownEvents.Tests/` (flat, no subfolders)

---

## Test Organization

```
backend/BrownEvents.Tests/
├── ConferenceServiceTests.cs
├── SessionServiceTests.cs
├── RegistrationServiceTests.cs
├── SessionRegistrationServiceTests.cs
├── DbContextConcurrencyTests.cs
├── ApiResponseShapeTests.cs
├── AttendeePiiExposureTests.cs
├── SwaggerDocumentationTests.cs
├── ValidationTests.cs
└── BrownEvents.Tests.csproj        (ProjectReference → BrownEvents.Api.csproj)
```

No `unit/`/`integration/`/`fixtures/` subfolders — every test class lives at the project root. Behavior-themed files (`ApiResponseShapeTests.cs`, `AttendeePiiExposureTests.cs`, `ValidationTests.cs`, `SwaggerDocumentationTests.cs`) exercise cross-cutting concerns via `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory`; the rest are per-service unit tests.

### Naming Conventions

| Element | Pattern | Example |
|---|---|---|
| Test files | `{ServiceName}Tests.cs` or `{Concern}Tests.cs` | `RegistrationServiceTests.cs`, `ValidationTests.cs` |
| Test methods | `MethodName_Scenario_ExpectedResult` | `RegisterAttendeeAsync_ReusesExistingAttendee_WhenEmailFound` (`RegistrationServiceTests.cs:51`) |

---

## Running Tests

| Action | Command |
|---|---|
| All tests | `dotnet test BrownEvents.Tests` (from `backend/`) |
| Single class | `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~SessionServiceTests"` |
| Single test | `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~SessionServiceTests.UpdateAsync_ChangesSessionTitle"` |
| CI invocation | `dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj -c Release --logger "console;verbosity=normal"` (`.gitlab-ci.yml:48-54`) |

No watch mode, no coverage flag configured — CI logs console output only, no `--collect`/trx/JUnit artifact.

---

## Unit Test Pattern

```csharp
// Source: backend/BrownEvents.Tests/RegistrationServiceTests.cs:33-48
[Fact]
public async Task RegisterAttendeeAsync_CreatesNewAttendeeAndRegistration_WhenEmailNotFound()
{
    using var context = CreateContext();
    // Arrange: seed a conference, construct the service
    // Act
    var registration = await service.RegisterAttendeeAsync(conferenceId, attendee);
    // Assert
    Assert.Equal(expected, registration.Status);
}
```

### Structure

Arrange (seed `AppDbContext` rows directly) → Act (call the service method under test) → Assert (`Assert.Equal`/`Assert.NotNull`, etc.). No `[Theory]`/`[InlineData]` used anywhere in the suite — every test is a `[Fact]`.

---

## Fixtures / Test Data

No `fixtures/`/factory classes. Each test file defines its own `CreateContext()` helper and seeds rows inline in the test body rather than sharing fixture files:

```csharp
// Source: backend/BrownEvents.Tests/RegistrationServiceTests.cs:11-17
private static AppDbContext CreateContext() =>
    new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options);
```

---

## Mocking

**Not used.** `Moq` is referenced in `BrownEvents.Tests.csproj:14` but no test constructs a `Mock<T>` — tests build a real `AppDbContext` against InMemory and seed rows directly instead of mocking the data layer. There is no service/interface to mock either, since services depend on `AppDbContext` concretely (see [[architecture]]).

| Layer | Mock In Unit Tests | Actual Practice Here |
|---|---|---|
| Database | Usually mocked | **Not mocked** — real `AppDbContext` + InMemory provider |
| External APIs | Usually mocked | N/A — project has none |

---

## Database Testing

**Test DB**: EF Core InMemory provider, most files use a unique GUID database name per test for isolation:

```csharp
// Source: backend/BrownEvents.Tests/ConferenceServiceTests.cs:14
.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
```

Some files intentionally share a fixed database name to test concurrent-access/shared-state scenarios: `DbContextConcurrencyTests.cs:34` (`"DbContextConcurrencyTestsDb"`), `SwaggerDocumentationTests.cs:45` (`"SwaggerDocumentationTestsDb"`). `WebApplicationFactory`-based tests pass an explicit `InMemoryDatabaseRoot` for isolation control (`ApiResponseShapeTests.cs:36`, `AttendeePiiExposureTests.cs:40`).

### Isolation Pattern

No transaction-rollback isolation — isolation comes entirely from the unique database name per test, not from rolling back a shared transaction.

### Transactions in tests

Tests never open a transaction themselves. Production code's `Database.IsRelational()` guard (see [[database-patterns]]) means `RegistrationService`/`SessionRegistrationService`/`DataSeeder` **skip** the transaction entirely against InMemory — so existing tests can only assert happy-path behavior, not actual rollback-on-failure, which only manifests against real Npgsql (`docs/testing-ci.md:7`).

---

## API / Endpoint Testing

`ApiResponseShapeTests.cs`, `AttendeePiiExposureTests.cs`, `SwaggerDocumentationTests.cs`, `ValidationTests.cs` use `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` to boot the full app in-process and issue real HTTP requests against it, with the InMemory provider substituted for Npgsql.

---

## Async Testing

Every test method is `async Task`, using plain `await` — no callback or `.Result`/`.Wait()` blocking found.

---

## Parameterized Tests

None — no `[Theory]`/`[InlineData]` in the suite. New parameterized cases should still use `[Theory]`/`[InlineData]` (standard xUnit) rather than looping inside a `[Fact]`, to keep with the framework's reporting granularity, even though no precedent exists yet.

---

## Coverage

No coverage tool configured — no `coverlet`, no `--collect:"XPlat Code Coverage"`, no threshold. `.gitlab-ci.yml:48-54` runs tests with console logging only.

---

## Test Environment

No `.env.test` or test-specific `appsettings`. `WebApplicationFactory`-based tests override only the DbContext registration in-process; everything else runs with the app's normal `Program.cs` startup (development environment settings apply, e.g. `EnableSensitiveDataLogging()`).

---

## Writing New Tests

1. Create `backend/BrownEvents.Tests/{Thing}Tests.cs`.
2. Add a `private static AppDbContext CreateContext()` helper using `UseInMemoryDatabase(Guid.NewGuid().ToString())` unless the test needs a shared DB name.
3. Seed rows directly on the context — do not mock `AppDbContext` or introduce a repository interface to make mocking possible.
4. Name the test `MethodName_Scenario_ExpectedResult`.
5. Run it: `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~{Thing}Tests.{TestName}"`.

---

## Common Patterns

| Pattern | When to Use | Example |
|---|---|---|
| `CreateContext()` per test | Fresh isolated InMemory DB | `RegistrationServiceTests.cs:11-17` |
| Fixed shared DB name | Testing concurrent/shared state | `DbContextConcurrencyTests.cs:34` |
| `WebApplicationFactory<Program>` | Full HTTP round-trip / response-shape tests | `ApiResponseShapeTests.cs:36` |

---

## Troubleshooting

| Issue | Solution |
|---|---|
| `TransactionIgnoredWarning` in test output | Expected — InMemory provider no-ops `BeginTransactionAsync`; guarded by `Database.IsRelational()` in production code, not a bug |
| Tests interfering with each other | Check the test uses a unique `Guid.NewGuid()` database name, not a shared literal name |
| Lazy-loading proxy errors in tests | InMemory context still needs `UseLazyLoadingProxies()` matched to how the entity was configured in `AppDbContext` |

---

## Quick Reference

| Need | Location |
|---|---|
| Test project | `backend/BrownEvents.Tests/BrownEvents.Tests.csproj` |
| CI test command | `.gitlab-ci.yml:48-54` |
| Test conventions doc | `docs/testing-ci.md` |
| Transaction-guard rationale | [[database-patterns]] |

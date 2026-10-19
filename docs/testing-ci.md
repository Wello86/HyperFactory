# Testing & CI

## Testing

Backend tests (`BrownEvents.Tests`) use xUnit against EF Core's `InMemoryDatabase` (a fresh `Guid`-named DB per test) rather than mocking `AppDbContext` — construct a real `AppDbContext` via `CreateContext()`-style helpers and seed rows directly instead of mocking. `Moq` is referenced but the existing tests don't use it. There is no frontend test setup.

Note: the InMemory provider silently ignores `Database.BeginTransactionAsync()`/`CommitAsync()` (logs `TransactionIgnoredWarning`, commits each `SaveChangesAsync()` immediately) rather than deferring writes — so multi-`SaveChangesAsync()` methods that need atomicity (`RegistrationService.RegisterAttendeeAsync`, `DataSeeder.SeedAsync`) only open a transaction when `Database.IsRelational()` is true, and existing tests can only assert the happy-path behavior is unchanged, not the actual rollback-on-failure path (which only manifests against the real Npgsql provider).

## Linting & formatting

- Frontend (`frontend/`): ESLint (`eslint-plugin-react`, `eslint-plugin-react-hooks`) + Prettier. Run `npm run lint` / `npm run lint:fix`, `npm run format` / `npm run format:check`. Config lives in `.eslintrc.cjs`/`.prettierrc`.
- Backend (`backend/`): style is governed by the root `.editorconfig` and checked with `dotnet format backend/BrownEvents.sln --verify-no-changes` (run without `--verify-no-changes` to auto-fix). Both `.csproj` files also enable Roslyn analyzers (`EnableNETAnalyzers`, `AnalysisLevel=latest`), which surface as build warnings.

## CI (`.gitlab-ci.yml`)

Four stages: `lint` -> `build` -> `test` -> `docker`. `frontend:lint` (`node:18-alpine`) runs `npm run lint && npm run format:check`; `backend:lint` (`mcr.microsoft.com/dotnet/sdk:6.0`) runs `dotnet format backend/BrownEvents.sln --verify-no-changes` — both block the pipeline on style violations. Both backend build/test jobs use the `mcr.microsoft.com/dotnet/sdk:6.0` image: `backend:build` builds `backend/BrownEvents.sln`, while `backend:test` runs `dotnet test` directly against `backend/BrownEvents.Tests/BrownEvents.Tests.csproj`. Frontend build uses `node:18-alpine`. The `docker:*` jobs (build + push images) only run on `main`.

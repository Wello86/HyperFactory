# EPMCDMPTST-1951 — Upgrade BrownEvents backend from .NET 6 to .NET 10

## Context

The backend (`backend/BrownEvents.Api`, `backend/BrownEvents.Tests`) targets .NET 6, which
reached End of Life in November 2024. The ticket requires moving the whole backend to the latest
available .NET version (.NET 10), bumping all related NuGet packages to compatible current
versions, and updating the Docker/CI build images to match — while keeping the app runnable
(`docker-compose up`) and the existing unit test suite green.

Current state (confirmed by direct inspection):
- Both `.csproj` files target `net6.0`. No `global.json`, no `Directory.Build.props` /
  `Directory.Packages.props` — versions are pinned inline per-project.
- `backend/BrownEvents.Api/Dockerfile` uses `mcr.microsoft.com/dotnet/sdk:6.0` (build stage) and
  `mcr.microsoft.com/dotnet/aspnet:6.0` (runtime stage).
- `.gitlab-ci.yml` has two jobs (`backend:build`, `backend:test`) pinned to
  `mcr.microsoft.com/dotnet/sdk:6.0`, and `backend:build`'s artifact path is
  `backend/BrownEvents.Api/bin/Release/net6.0/`. Frontend/docker jobs are unrelated and untouched.
- `docker-compose.yml` has no direct .NET version pin — it inherits from the Dockerfile, so no
  changes needed there beyond re-verifying the stack comes up.
- Notable runtime patterns to protect during the bump: `AppDbContext` is registered as
  `AddSingleton<AppDbContext>` (not scoped) with `.UseLazyLoadingProxies()` and
  `.EnableSensitiveDataLogging()`; schema is created via `Database.EnsureCreated()` (no EF
  migrations); `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` is set at
  startup; JSON serialization uses `ReferenceHandler.IgnoreCycles`. None of these need to change as
  part of this ticket, but each is a plausible break point across a 4-major-version EF Core/Npgsql
  jump and must be spot-checked at runtime (not just via the InMemory-backed unit tests).
- This machine has SDKs 3.1.426, 7.0.410, 8.0.102, 9.0.205, 9.0.316, 10.0.302 installed (no .NET 6
  SDK) — `dotnet` currently resolves to 10.0.302 by default with no `global.json` pin.

**TDD: not applicable.** This is a dependency/TFM upgrade with a fixed, already-passing test
suite as the safety net — no new behavior is being added, so no new tests are written. Success is
measured by the existing suite staying green plus manual runtime verification of the risk points
above.

## Target package versions (verified live against nuget.org, current as of today)

**`backend/BrownEvents.Api/BrownEvents.Api.csproj`**
- `TargetFramework`: `net10.0`
- `Microsoft.EntityFrameworkCore`: `10.0.12`
- `Microsoft.EntityFrameworkCore.Design`: `10.0.12`
- `Microsoft.EntityFrameworkCore.Proxies`: `10.0.12`
- `Npgsql.EntityFrameworkCore.PostgreSQL`: `10.0.3` (latest stable; requires
  `Microsoft.EntityFrameworkCore >= 10.0.4 <11.0.0`, satisfied by `10.0.12`)
- `Swashbuckle.AspNetCore`: `10.2.3`
- `Swashbuckle.AspNetCore.Filters`: `10.0.1` (depends on `Swashbuckle.AspNetCore.SwaggerGen >=
  10.0.0`, satisfied)

**`backend/BrownEvents.Tests/BrownEvents.Tests.csproj`**
- `TargetFramework`: `net10.0`
- `Microsoft.NET.Test.Sdk`: `18.9.0`
- `Moq`: `4.20.72`
- `xunit`: `2.9.3`
- `xunit.runner.visualstudio`: `4.0.0` (`2.9.3` does not exist on nuget.org for this package —
  pinning it fails restore with `NU1603`; `4.0.0` is the correct current version and runs xunit v2
  tests fine)
- `Microsoft.EntityFrameworkCore.InMemory`: `10.0.12` (must match the Api project's EF Core
  version exactly — mismatches produce confusing `MissingMethodException`s, not clear restore
  errors)
- `Microsoft.AspNetCore.Mvc.Testing`: `10.0.12`

**Deliberately staying on xunit v2** (not migrating to xunit v3): v3 changes the test host model
and is materially bigger than a version bump; out of scope for "bump to latest compatible, keep
tests green."

If any of these exact patch numbers have moved by the time of implementation, re-check on
nuget.org rather than assuming — but the hard rule stands regardless: `EntityFrameworkCore` /
`.Design` / `.Proxies` / `.InMemory` must all carry the identical version string.

## Steps

1. **`backend/BrownEvents.Api/BrownEvents.Api.csproj`** — bump `TargetFramework` to `net10.0` and
   all `PackageReference` versions per the table above. Leave `Nullable`, `ImplicitUsings`,
   `RootNamespace`, `GenerateDocumentationFile`, `NoWarn` untouched. Also add an explicit
   `Microsoft.EntityFrameworkCore.Relational` `10.0.12` reference: `Npgsql.EntityFrameworkCore.PostgreSQL
   10.0.3` only requires `Relational >= 10.0.4`, so without pinning it explicitly, restore can
   resolve a `Relational` version below the `10.0.12` used elsewhere and violate the
   identical-version-string rule.
   - **`backend/BrownEvents.Api/Program.cs`** — change `using Microsoft.OpenApi.Models;` to
     `using Microsoft.OpenApi;`. Swashbuckle.AspNetCore 10.x depends on `Microsoft.OpenApi` 2.x,
     which moved `OpenApiInfo` (and other core types) out of the `Microsoft.OpenApi.Models`
     namespace into `Microsoft.OpenApi`; without this change the project does not compile.
2. **`backend/BrownEvents.Tests/BrownEvents.Tests.csproj`** — same TFM bump, package versions per
   the table above. Leave `IsPackable`, the `ProjectReference` untouched.
3. **`backend/BrownEvents.Api/Dockerfile`** — change `FROM mcr.microsoft.com/dotnet/sdk:6.0 AS
   build` → `sdk:10.0`, and `FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime` → `aspnet:10.0`.
   Two follow-on fixes are also required in the runtime stage, both caused by the base image
   change rather than the TFM bump itself:
   - Add `ENV ASPNETCORE_HTTP_PORTS=80` before `EXPOSE 80`. .NET 8+ `aspnet` images default to
     listening on `8080`, not `80`; without this the container no longer matches the `5000:80`
     mapping in `docker-compose.yml`.
   - Add `RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 &&
     rm -rf /var/lib/apt/lists/*` before `WORKDIR /app`. The `aspnet:6.0` image (Debian 11) shipped
     `libgssapi_krb5.so.2`; the `aspnet:10.0` image (Ubuntu 24.04) does not, so Npgsql's GSSAPI
     probe logs `Error: libgssapi_krb5.so.2: cannot open shared object file` on every startup
     without it. (`libgssapi-krb5-2` is the Ubuntu package that ships that specific `.so` — not
     `libkrb5-3`, which provides a different library.)
4. **`.gitlab-ci.yml`** — in `backend:build` and `backend:test`, change `image:
   mcr.microsoft.com/dotnet/sdk:6.0` → `sdk:10.0`; in `backend:build`'s `artifacts.paths`, change
   `backend/BrownEvents.Api/bin/Release/net6.0/` → `net10.0/`. Grep the file afterward to confirm
   no `net6.0` / `sdk:6.0` string survives anywhere else (e.g. a cache key or comment).
   Frontend/docker jobs are not touched.
5. **No `global.json` added.** Keeping the diff scoped to exactly what the ticket asks for; the
   repo has never pinned an SDK version and CI/Docker images are already effectively single-SDK
   environments, so the main risk (multiple local SDKs) is a local-dev-only concern. If review
   pushback wants a pin, `backend/global.json` with `"rollForward": "latestMinor"` is the
   documented fallback, added alongside a `COPY global.json ./` line in the Dockerfile's build
   stage.
6. **Restore & build**: `dotnet restore backend/BrownEvents.sln` then `dotnet build
   backend/BrownEvents.sln -c Release`. Watch for `NU1605` (package downgrade — an EF Core-family
   package wasn't bumped to match) or `NU1608` (range conflict — most likely from
   `Swashbuckle.AspNetCore.Filters`).
7. **Unit tests**: `dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj`. All existing
   tests must pass. Note this run does *not* exercise real Postgres, so it cannot validate the
   Npgsql timestamp behavior below.
8. **Local runtime verification** against a real Postgres (e.g. `docker-compose up postgres -d`,
   then `dotnet run --project backend/BrownEvents.Api` pointed at it):
   - App starts cleanly; `EnsureCreated()` + `DataSeeder.SeedAsync` complete without exceptions on
     a fresh DB.
   - Exercise an endpoint touching `DateTime` fields (session/conference start-end times) to
     confirm the `Npgsql.EnableLegacyTimestampBehavior` switch (or its absence) doesn't produce a
     timestamp `InvalidCastException`/`PostgresException` or silently wrong values.
   - Hit `/swagger` and `/swagger/v1/swagger.json` — confirm Swagger UI renders and the JSON
     document returns 200 (Swashbuckle/ASP.NET Core mismatches fail at first request, not compile
     time).
   - Hit an endpoint that returns entities with navigation properties (e.g. a session with
     speaker/room) to confirm lazy-loading proxies still generate and `IgnoreCycles` serialization
     still works.
9. **Full stack**: `docker-compose up --build` — confirm postgres, backend (`:5000`), and frontend
   (`:5173`) all come up healthy and the frontend can call the backend successfully end to end. (Per
   the `run` skill, this machine uses Podman since Docker Desktop needs manual Gatekeeper
   approval — use `podman`/`podman-compose` if `docker-compose` isn't usable directly.)
10. **Self-check** against the ticket's acceptance criteria before declaring done.

## Risk flags to watch for (ranked)

1. **Npgsql legacy timestamp switch** — most likely silent break. `AppContext.SetSwitch` never
   throws even if the switch name is no longer read, so this fails silently unless explicitly
   checked live in step 8. If it breaks, the fix is aligning model `DateTime.Kind` with the mapped
   column type — flag to the team rather than scope-creeping a fix in silently.
2. **`Swashbuckle.AspNetCore.Filters` lagging `Swashbuckle.AspNetCore`** — smaller, separately
   maintained package; if restore fails on this pair specifically, check actual usage of
   Filters-specific APIs in the codebase before deciding whether to drop it as a contingency
   (flag as a scope decision, don't drop unilaterally without noting it).
3. **Pre-existing singleton `AppDbContext` + lazy-loading proxies** — not something this ticket
   fixes, but later EF Core versions have tightened concurrency-detection exceptions. If it
   surfaces during verification, note it as pre-existing tech debt discovered, not something
   introduced by the upgrade.
4. **Version-matching hard rule** — `EntityFrameworkCore`/`.Design`/`.Proxies`/`.InMemory` must be
   the identical version string; `Mvc.Testing` must match the `net10.0` shared framework. Mismatches
   produce `MissingMethodException` at runtime, not a clear restore-time error.

## Verification summary

Restore → build → unit tests → local run against real Postgres (swagger, timestamps, nav
properties) → `docker-compose up --build` full-stack check → line-by-line diff review of
`.gitlab-ci.yml` to confirm no stray `net6.0`/`sdk:6.0` remains.

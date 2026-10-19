# EPMCDMPTST-1959 — Fix Security: Sensitive Data Logging and CORS

## Context

A security review flagged two issues in `backend/BrownEvents.Api/Program.cs`:

1. `EnableSensitiveDataLogging()` is called unconditionally when building `AppDbContext`, so SQL
   parameter values (potentially including PII) are logged even when `ASPNETCORE_ENVIRONMENT=Production`
   (which is what `docker-compose.yml` sets for the backend container today).
2. A CORS policy named `AllowAll` permits any origin, any method, any header, and is applied
   unconditionally via `app.UseCors("AllowAll")` — any website can make requests against this API.

Goal: gate sensitive-data logging behind a non-production check, and replace the wildcard CORS
policy with an explicit, configuration-driven allow-list of origins — without breaking
`docker-compose up` + the frontend on `localhost:5173`.

## TDD: No

This is infrastructure/config wiring in `Program.cs` (startup composition), not a unit of testable
business logic — there's no existing test harness that spins up the ASP.NET pipeline to assert on
CORS headers or EF logging behavior, and building one is out of scope for this fix. Verification
will be manual: run via `docker-compose`/`podman-compose`, hit an endpoint from the frontend origin
and from a disallowed origin, and confirm SQL params don't appear in Production-mode logs.

## Steps

1. **Gate `EnableSensitiveDataLogging()`** in `backend/BrownEvents.Api/Program.cs`:
   - Only call `.EnableSensitiveDataLogging()` when `builder.Environment.IsDevelopment()` is true.
   - This is the first use of `builder.Environment` in the file — no prior convention exists, so use
     the standard ASP.NET Core `WebApplicationBuilder.Environment` property directly.

2. **Add configurable allowed-origins list**:
   - Add a `"Cors": { "AllowedOrigins": [...] }` section to `appsettings.json` with
     `http://localhost:5173` as the default entry (keeps `docker-compose up` + Vite dev server working).
   - No `appsettings.Production.json` exists; don't add one — the base `appsettings.json` value is
     the effective Production config unless overridden via env var (`Cors__AllowedOrigins__0`, etc.)
     in `docker-compose.yml` or real deployment config. This matches how `ConnectionStrings` is
     already handled (base file + docker-compose env override).

3. **Replace the `AllowAll` CORS policy** in `Program.cs`:
   - Read the origins list from configuration: `builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()`.
   - Rename the policy (e.g. `"Default"`) and use `.WithOrigins(allowedOrigins).WithMethods(...).WithHeaders(...)`
     instead of `AllowAnyOrigin/AllowAnyMethod/AllowAnyHeader`. Use explicit method/header lists
     appropriate for this API (standard REST verbs: GET, POST, PUT, DELETE; headers: Content-Type,
     Authorization) rather than another wildcard, since the ticket calls out "any method, any
     header" as part of the problem.
   - Update `app.UseCors("AllowAll")` → `app.UseCors("Default")` (or whatever the new policy name is).

4. **No docker-compose.yml changes required** — the default `appsettings.json` origin list already
   covers `localhost:5173`, so `docker-compose up --build` keeps working as-is. If desired later, an
   `ASPNETCORE_ENVIRONMENT`/origin override could be added via env vars, but that's not required by
   the acceptance criteria and is left as a possible follow-up, not part of this change.

## Verification

- `dotnet build` (or via container) to confirm the changed `Program.cs` compiles.
- Run `docker-compose up --build` (or `podman-compose` per repo tooling notes) and confirm:
  - Frontend on `localhost:5173` can still call the API successfully (e.g. load the conference list).
  - A request with `Origin: http://evil.example.com` gets no `Access-Control-Allow-Origin` header
    (rejected by CORS) — check via `curl -H "Origin: http://evil.example.com" -I http://localhost:5000/api/...`.
- Confirm in Production mode (`ASPNETCORE_ENVIRONMENT=Production`, as set by docker-compose) that
  EF Core command logging does not include parameter values — e.g. trigger a query and check
  container logs for absence of literal parameter values in SQL log lines.
- In Development mode (`ASPNETCORE_ENVIRONMENT=Development`, e.g. `dotnet run`), confirm sensitive
  data logging is still enabled (unchanged dev experience).

## Out of scope

- Broader logging redaction beyond `EnableSensitiveDataLogging` (e.g. scrubbing app-level `ILogger`
  calls) — not mentioned in the ticket.
- Adding `appsettings.Production.json` or other environment-specific override files — not required
  since the base config already satisfies the acceptance criteria.
- Fixing the hardcoded `frontend/src/api.js` `BASE_URL` or the checked-in DB credentials in
  `appsettings.json` — pre-existing issues, unrelated to this ticket's two acceptance criteria.

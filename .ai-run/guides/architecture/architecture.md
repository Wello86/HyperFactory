# Architecture

**Project**: BrownEvents (.NET version of the BrownEvents brownfield reference project)
**Type**: Conference-management web app — ASP.NET Core 10 API + React 18/Vite SPA
**Data store**: PostgreSQL 14 via EF Core 10 / Npgsql

---

## Architecture Overview

Two independently deployed apps sharing one PostgreSQL database, wired together only by `docker-compose.yml` and the frontend's HTTP calls to the API:

```
React 18 SPA (frontend/src)  --fetch-->  ASP.NET Core 10 API (backend/BrownEvents.Api)  --EF Core-->  PostgreSQL
```

The backend is a classic thin-controller / fat-service layering with no repository abstraction: `Services/*` call `AppDbContext` directly. There is no authentication layer anywhere in the stack (`backend/BrownEvents.Api/Program.cs` has no `AddAuthentication`/`AddAuthorization`).

## Component Structure

```
backend/BrownEvents.Api/
├── Controllers/       ConferencesController.cs, SessionsController.cs, SpeakersController.cs,
│                       RoomsController.cs, AttendeesController.cs
├── Services/          I{X}Service.cs + {X}Service.cs pairs: Conference, Session, Speaker, Room,
│                       Registration, SessionRegistration
├── Data/              AppDbContext.cs, DataSeeder.cs
├── Models/            Conference, Session, Speaker, Room, Attendee, Registration,
│                       SessionRegistration, PagedResult
├── Dtos/              per-entity *Dto.cs + EntityDtoExtensions.cs (mapping)
├── SwaggerExamples/    Swashbuckle.AspNetCore.Filters request-body examples
└── Program.cs          composition root

frontend/src/
├── components/         ErrorBoundary.jsx, ConferenceCard.jsx, RegistrationModal.jsx, ...
├── hooks/              useConference.js, useConferenceSessions.js, useSessionRegistration.js, ...
├── pages/              RegistrationPage.jsx, SpeakerListPage.jsx, DashboardPage.jsx, ...
├── utils/
├── App.jsx             react-router-dom route table
├── main.jsx            entry point, wraps <App/> in <ErrorBoundary>
└── api.js              fetch-based API client, hardcoded BASE_URL
```

## Design Patterns Detected

| Pattern | Where | Notes |
|---|---|---|
| Service layer, no repository | `backend/BrownEvents.Api/Services/ConferenceService.cs:9` | Services call `AppDbContext` directly; no `IRepository<T>` abstraction |
| DTO mapping via extension methods | `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs:9-111` | Static `.ToDto()` per entity, called from controllers |
| Global exception-handling middleware | `backend/BrownEvents.Api/Program.cs:74-90` | Single catch-all → RFC7807 `ProblemDetails`, no per-domain exception types |
| Paged-result envelope | `backend/BrownEvents.Api/Models/PagedResult.cs:3-9` | Used by `Conferences.GetAll` and `Conferences/{id}/sessions` only |
| Guarded transaction | `backend/BrownEvents.Api/Services/RegistrationService.cs:23-25` | `Database.IsRelational() ? BeginTransactionAsync() : null` — see [[database-patterns]] |
| Error boundary (frontend) | `frontend/src/components/ErrorBoundary.jsx:3-27` | Wraps whole app in `main.jsx:9-11` |
| Custom data-fetching hooks (frontend) | `frontend/src/hooks/*.js` | One hook per page/concern, e.g. `useConference.js`, `useSessionRegistration.js` |

## Layer/Module Responsibilities

| Layer | Responsibility | Depends on |
|---|---|---|
| `Controllers/` | Routing, model binding, status-code mapping, calling one service, `.ToDto()` on the result | `Services/*` interfaces, `Dtos/` |
| `Services/` | Business logic, EF Core queries (`.Include`/`.ThenInclude`, transactions) | `AppDbContext` directly (`Data/AppDbContext.cs`) |
| `Data/` | `DbContext` + relationship config (`OnModelCreating`) + startup seeding | `Models/*` |
| `Models/` | EF entities with data-annotation validation | nothing (leaf layer) |
| `Dtos/` | Response shape + `Model → Dto` mapping | `Models/*` |
| frontend `pages/` | Page composition | `hooks/*` |
| frontend `hooks/` | Data fetching/local state per concern | `api.js` |
| frontend `api.js` | Thin `fetch` wrapper | nothing |

## Dependency Rules

- Controllers take service **interfaces** via constructor injection (`backend/BrownEvents.Api/Controllers/ConferencesController.cs:19-20`); concrete types are wired in `Program.cs:34-39` (all `AddScoped`).
- Services depend on `AppDbContext` directly — there is no interface between a service and EF Core, so unit tests exercise a real in-memory `AppDbContext` rather than mocking a repository (see [[testing-patterns]]).
- `Models/*` have zero outbound dependencies; `Session.Conference` nav is marked `[ValidateNever]` (`backend/BrownEvents.Api/Models/Session.cs:23`) to keep model-binding validation from requiring the parent object.
- Frontend `pages/` never call `fetch` directly — they go through a `hooks/` function, which goes through `api.js`.

## Data Flow

```
HTTP request
  → Controller action (route + model binding)
  → Service method (EF Core query against AppDbContext, eager-loaded via .Include/.ThenInclude
     to avoid N+1 against lazy-loading proxies — backend/BrownEvents.Api/Services/ConferenceService.cs:53-54)
  → domain Model returned to controller
  → controller calls .ToDto() (backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs)
  → JSON response
```

Write path (`ConferencesController.cs:81-88`, POST): controller binds the raw `Conference` model from the body (no input DTO except registration, see [[api-patterns]]), passes it to the service, service `SaveChangesAsync`s, controller returns `CreatedAtAction(...)`.

## Key Abstractions

| Abstraction | Location | Purpose |
|---|---|---|
| `AppDbContext` | `backend/BrownEvents.Api/Data/AppDbContext.cs:6` | `AddScoped` (not `AddSingleton` — comment at `Program.cs:16-21` explains a prior concurrency bug), `UseLazyLoadingProxies()`, `EnableSensitiveDataLogging()` in dev only (`Program.cs:22-30`) |
| `PagedResult<T>` | `backend/BrownEvents.Api/Models/PagedResult.cs:3-9` | `Data`/`Page`/`PageSize`/`TotalCount` envelope for the two paginated endpoints |
| `EntityDtoExtensions` | `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs:9-111` | Centralizes every `Model → Dto` mapping as extension methods |
| Guarded transaction | `RegistrationService.cs:23-25`, `SessionRegistrationService.cs:22-24,83-85,151-153`, `DataSeeder.cs:18-20` | `Database.IsRelational()` guard so EF InMemory tests don't hit unsupported transactions |

## Adding New Features

To add a new resource (mirrors the existing Conference/Session/Speaker pattern):

1. Add the entity to `Models/`, with data-annotation validation (`[Required]`, `[MaxLength]`).
2. Add a `DbSet<T>` to `AppDbContext.cs` and configure relationships in `OnModelCreating`.
3. Add `I{X}Service`/`{X}Service` in `Services/`, querying `AppDbContext` directly; use `.Include()` for any lazy-loaded nav property you read in the same request.
4. Register the service in `Program.cs` (`AddScoped<I{X}Service, {X}Service>()`).
5. Add a controller under `Controllers/` with `[ApiController]`/`[Route("api/{plural})")]`; accept the domain model directly unless the shape genuinely differs from persistence (only registration does today — add a `Dtos/*RequestDto.cs` in that case).
6. Add a `ToDto()` mapping in `Dtos/EntityDtoExtensions.cs` and a response `Dto` class.
7. Optionally add a `SwaggerExamples/{X}Example.cs` for Swagger UI.

## Configuration & Environment

| Setting | Source | Notes |
|---|---|---|
| DB connection string | `appsettings.json` → `ConnectionStrings:DefaultConnection` | Hardcoded plaintext credentials in source — see [[security-practices]] |
| CORS allowed origins | `appsettings.json` → `Cors:AllowedOrigins` (`Program.cs:42`) | Policy named `"Default"`, not permissive — see [[security-practices]] |
| Lazy-loading + sensitive logging | `Program.cs:22-30` | `EnableSensitiveDataLogging()` only when `IsDevelopment()` |
| Schema creation | `Program.cs:102` | `Database.EnsureCreated()` — no `Migrations/` folder exists |
| Frontend API base URL | `frontend/src/api.js:1` | `BASE_URL = ''`; relies on Vite dev proxy / same-origin deploy |

## Boundaries Summary

- No repository/unit-of-work abstraction: swapping persistence would require touching every `Services/*.cs` file.
- No authentication/authorization boundary anywhere — every endpoint is open.
- DTO boundary is asymmetric: responses always go through `Dtos/EntityDtoExtensions.cs`, but request bodies are raw domain `Models/*` except for attendee registration.
- No API versioning boundary — all routes are unversioned `api/{resource}`.

## Quick Reference

| Need | Location |
|---|---|
| Add/modify an entity | `backend/BrownEvents.Api/Models/` + `Data/AppDbContext.cs` `OnModelCreating` |
| Add/modify business logic | `backend/BrownEvents.Api/Services/` |
| Add/modify an endpoint | `backend/BrownEvents.Api/Controllers/` |
| Add/modify response shape | `backend/BrownEvents.Api/Dtos/EntityDtoExtensions.cs` |
| DI registrations | `backend/BrownEvents.Api/Program.cs:34-39` |
| Frontend routes | `frontend/src/App.jsx` |
| Frontend API calls | `frontend/src/api.js` |

# Security Practices

**Project**: BrownEvents
**Auth Method**: None — no authentication or authorization system exists anywhere in the backend
**Auth Library**: N/A

> This app has no login, no user identity, and no per-request authorization. Every endpoint is publicly callable. Treat that as the current baseline, not an oversight to silently "fix" — if a task requires auth, raise it as a design decision rather than bolting one framework on ad hoc.

---

## Authentication Flow

**None implemented.** `backend/BrownEvents.Api/Program.cs` has no `AddAuthentication`, `AddAuthorization`, `UseAuthentication`, or `UseAuthorization` call, and no controller has an `[Authorize]` attribute. Every action in `ConferencesController.cs`, `SessionsController.cs`, `SpeakersController.cs` is reachable without credentials.

### Protect a Route

There is no existing pattern to copy — adding auth to one endpoint without a project-wide decision would create an inconsistent security surface. Flag this to the user before implementing.

---

## Authorization

**None.** No role/permission model exists — do not assume RBAC scaffolding is present anywhere.

---

## Input Validation

**Library**: ASP.NET Core data annotations (`[Required]`, `[MaxLength]`, `[EmailAddress]`), enforced automatically by `[ApiController]`'s model-binding validation.

```csharp
// Source: backend/BrownEvents.Api/Dtos/AttendeeRegistrationRequestDto.cs:7-17
[Required]
[MaxLength(100)]
public string FirstName { get; set; } = string.Empty;
[Required]
[MaxLength(100)]
public string LastName { get; set; } = string.Empty;
[Required]
[EmailAddress]
public string Email { get; set; } = string.Empty;
```

Same pattern on domain models directly, e.g. `Models/Conference.cs:9-10`, `Models/Attendee.cs:9-19`, `Models/Speaker.cs:9-20`. `[ApiController]` (`ConferencesController.cs:14`) auto-returns 400 on annotation failure — **there is no manual `ModelState.IsValid` check anywhere**, and don't add one; it's redundant with `[ApiController]`.

### Rules

- ✅ Validate via data annotations on the `Model`/`Dto` class itself, not in the controller body.
- ❌ Unannotated fields have **no** validation — e.g. `Session.StartDate`/`EndDate` ordering, `Registration.Status` values are not range/enum-checked anywhere. Don't assume a field is validated just because it's on a model class.

---

## Secrets Management

### Loading Pattern

```csharp
// Source: backend/BrownEvents.Api/Program.cs:15
builder.Configuration.GetConnectionString("DefaultConnection")
```

### Secret Variables

| Variable | Purpose | Current state |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection | **Hardcoded plaintext credentials in `appsettings.json:10-12`** (`Host=postgres;...;Username=brown;Password=brown123`) — not an env var, dev-only compose credentials |

### Rules

- ❌ Do not add new secrets to `appsettings.json` in plaintext — this is an existing, known dev-only gotcha (see [[architecture]] Configuration table), not a pattern to extend.
- ✅ New secrets should come from environment variables / `appsettings.{Environment}.json` overrides, not the committed `appsettings.json`.

---

## Security Middleware

| Protection | Implementation | Source |
|---|---|---|
| CORS | Named policy `"Default"` (not permissive) | `Program.cs:42-53,95` |
| Rate Limiting | None | — |
| CSRF | None | — |
| Security headers (HSTS, etc.) | None | — |

### CORS Configuration

```csharp
// Source: backend/BrownEvents.Api/Program.cs:42-53
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Default", policy =>
    policy.WithOrigins(allowedOrigins)
          .WithMethods("GET", "POST", "PUT", "DELETE")
          .WithHeaders("Content-Type", "Authorization")));
// applied at Program.cs:95 — app.UseCors("Default");
```

Origins configured in `appsettings.json:11-13` → currently `["http://localhost:5173"]`.

> **Correction of stale documentation**: `docs/cross-cutting-notes.md` and the old `CLAUDE.md` "Key Gotchas" describe a policy named `"AllowAll"` that "permits any origin/method/header." That is **no longer true** — the policy is named `"Default"`, whitelists a single configured origin, and restricts to 4 named methods and 2 named headers. This guide reflects current code; do not carry the old claim forward.

---

## SQL Injection Prevention

**Protection**: EF Core LINQ (fully parameterized). No `FromSqlRaw`/`ExecuteSqlRaw`/string-concatenated SQL exists anywhere in `backend/`.

```csharp
// ✅ Safe — Source: backend/BrownEvents.Api/Services/ConferenceService.cs:53-55
await _context.Conferences.Include(c => c.Sessions).FirstOrDefaultAsync(c => c.Id == id)
```

There is no anti-pattern to contrast against — keep it that way; never introduce `FromSqlRaw` with interpolated/concatenated input.

---

## XSS Prevention

**Protection**: React's default JSX escaping on the frontend (no `dangerouslySetInnerHTML` usage found); ASP.NET Core JSON serialization on the API (no server-rendered HTML). No explicit Content Security Policy is configured.

---

## File Upload Security

**Not applicable** — no `IFormFile` usage anywhere in the backend; the app has no file upload feature.

---

## Audit Logging

**None security-relevant.** The global exception handler logs unhandled exceptions:

```csharp
// Source: backend/BrownEvents.Api/Program.cs:77-78
logger.LogError(exceptionFeature?.Error, "Unhandled exception processing request");
```

`SessionRegistrationService` additionally logs one `LogInformation` call when a cancellation promotes the next waitlisted attendee (`SessionRegistrationService.cs`, per-service `ILogger<T>` injection — see [[development-practices]]) — an operational trace, not a security/audit event. No login/permission/data-access events are logged (there is no auth to log). If audit logging is added later, route it through `ILogger<T>` injected per-service rather than a new ad hoc mechanism, following the pattern `SessionRegistrationService` already established.

---

## Dependency Security

No `npm audit`/`dotnet list package --vulnerable`/Dependabot/Snyk configuration found in `.gitlab-ci.yml` or `package.json`. No automated dependency scanning currently runs in CI.

---

## Security Anti-Patterns

| ❌ NEVER | ✅ INSTEAD | Risk |
|---|---|---|
| Add a new secret to committed `appsettings.json` | Use environment-specific config or env vars | Credential leak via git history |
| Add `FromSqlRaw`/`ExecuteSqlRaw` with string-built input | Use LINQ (parameterized) as every existing service does | SQL injection |
| Add `[Authorize]` to one endpoint without a project-wide auth decision | Raise the auth-strategy question first | Inconsistent, false sense of security |
| Log full exception details / request bodies to a client-visible response | Keep the generic `ProblemDetails` shape from `Program.cs:83-88` | Information leakage |

---

## Quick Reference

| Security Need | Location |
|---|---|
| CORS policy | `backend/BrownEvents.Api/Program.cs:42-53,95` |
| Validation annotations | `backend/BrownEvents.Api/Models/*.cs`, `Dtos/AttendeeRegistrationRequestDto.cs` |
| Connection string / secrets | `backend/BrownEvents.Api/appsettings.json` |
| Global error handling | `backend/BrownEvents.Api/Program.cs:74-90` |
| Auth | Not configured — none exists |

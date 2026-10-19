# Development Practices

**Project**: BrownEvents
**Backend**: C# / ASP.NET Core 10, formatted by `dotnet format` against the root `.editorconfig`
**Frontend**: JavaScript/JSX, formatted by Prettier, linted by ESLint

---

## Formatting

### Backend (`.editorconfig`, all of `backend/`)

```ini
# Source: .editorconfig:1-22
root = true
[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 2

[*.{cs}]
indent_size = 4
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_prefer_braces = true
```

**`using` directive order** — `System.*` first (alphabetical within that group), then everything else alphabetically, with **no blank line** between the two groups (`dotnet_separate_import_directive_groups = false`):

```csharp
// Source: backend/BrownEvents.Api/Program.cs:1-9
using System.Reflection;
using BrownEvents.Api.Data;
using BrownEvents.Api.Services;
using BrownEvents.Api.SwaggerExamples;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;
```

Check: `dotnet format BrownEvents.sln --verify-no-changes` (run from `backend/`). Auto-fix: drop `--verify-no-changes`. Applied automatically by the `dotnet-format` pre-commit hook on any staged `backend/**/*.cs` change (`lefthook.yml:3-5`) and independently by the `backend:lint` CI job — see [[quality-gates]].

### Frontend (Prettier + ESLint)

```bash
npm run lint           # ESLint (frontend/)
npm run format:check   # Prettier check
npm run format         # Prettier auto-fix
```

Enforced by the `frontend-lint` pre-commit hook on staged `frontend/**/*.{js,jsx}` (`lefthook.yml:6-8`) and the `frontend:lint` CI job — see [[quality-gates]].

---

## Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes / Controllers / Services | PascalCase, `{Name}Controller` / `I{Name}Service` + `{Name}Service` pair | `ConferencesController`, `IConferenceService`/`ConferenceService` |
| Async methods | PascalCase + `Async` suffix | `GetAllAsync`, `RegisterAttendeeAsync` (`Services/ConferenceService.cs`) |
| Private fields | `_camelCase` | `_service`, `_registrationService` (`Controllers/ConferencesController.cs:19-20`) |
| DTOs | `{Entity}Dto` for responses, `{Entity}{Purpose}Dto` for request shapes | `ConferenceDto`, `AttendeeRegistrationRequestDto` |
| React components | PascalCase file + export | `ConferenceCard.jsx`, `RegistrationModal.jsx` |
| React hooks | `use{Concern}.js` | `useConference.js`, `useSessionRegistration.js` |

---

## Error Handling

**Backend**: a single global exception-handling middleware — there are no per-domain exception types and no per-controller `try/catch`:

```csharp
// Source: backend/BrownEvents.Api/Program.cs:74-90
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
    context.RequestServices.GetRequiredService<ILogger<Program>>()
        .LogError(exceptionFeature?.Error, "Unhandled exception processing request");

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    var problem = new ProblemDetails { Title = "An unexpected error occurred.", ... };
    await context.Response.WriteAsJsonAsync(problem);
}));
```

Controllers handle only the "not found" case explicitly, via a local `private static ProblemDetails {X}NotFound(int id)` helper repeated per controller (`ConferencesController.cs:184-189`), returning `NotFound(...)`. Everything else propagates up to the handler above. **Do not add a per-controller `try/catch`** — follow the existing pattern of a local not-found helper plus letting unexpected exceptions fall through to the global handler.

**Frontend**: a single React error boundary wraps the whole app (`frontend/src/components/ErrorBoundary.jsx:3-27`, mounted at `main.jsx:9-11`) — there is no per-page or per-request error UI beyond that boundary and whatever a hook chooses to surface.

---

## Logging

**Minimal by design** — besides the global exception handler (`Program.cs:77-78`, `LogError`), the only service that logs is `SessionRegistrationService`, which injects `ILogger<SessionRegistrationService>` (standard ASP.NET Core constructor injection, no custom abstraction) and logs one `LogInformation` call when cancelling a confirmed registration promotes the next waitlisted attendee. No other service or controller logs individually. Follow this same pattern (inject `ILogger<T>`, log only for outcomes worth tracing) if you add logging elsewhere — it remains the exception rather than the norm.

---

## Async Conventions

Every I/O-bound method is `async Task<T>`/`async Task`, awaited directly — no `.Result`/`.Wait()` blocking calls, no fire-and-forget. Controller actions accept a `CancellationToken cancellationToken` parameter (defaulted on GET-list endpoints, required on the rest) and pass it straight through to the service call (`ConferencesController.cs:49,66,84`).

---

## Pre-Commit Hooks

```yaml
# Source: lefthook.yml
pre-commit:
  commands:
    dotnet-format:
      glob: "backend/**/*.cs"
      run: dotnet format backend/BrownEvents.sln --verify-no-changes
    frontend-lint:
      glob: "frontend/**/*.{js,jsx}"
      run: npm --prefix frontend run lint && npm --prefix frontend run format:check
```

One-time setup per clone: install `lefthook` (`brew install lefthook` / `go install github.com/evilmartians/lefthook@latest` / `npm i -g lefthook`), then `lefthook install`. **Neither `dotnet` nor `lefthook` is installed in this AI environment**, so this hook never fires here and `dotnet format` cannot be run to self-check locally — the `backend:lint` CI job is the only backstop; apply the `using`-order rule by hand (see above) rather than copying a neighboring file's import order.

---

## Common Anti-Patterns

| ❌ NEVER | ✅ INSTEAD | Why |
|---|---|---|
| Add a per-controller `try/catch` for generic errors | Let it propagate to the global handler (`Program.cs:74-90`) | Duplicates the existing single point of error shaping |
| Block on async code (`.Result`, `.Wait()`) | `await` | Deadlock risk under ASP.NET Core's request context |
| Skip the `Async` suffix on a new async method | Name it `{Verb}Async` | Matches every existing service/controller method |
| Reorder `using` directives to match a neighboring file without checking `.editorconfig` | Apply `System.*`-first, no-blank-line rule by hand | `dotnet format` isn't runnable locally in this environment — CI is the only catch |

---

## Quick Reference

| Need | Location |
|---|---|
| Formatting rules | `.editorconfig` |
| Pre-commit hooks | `lefthook.yml` |
| Global error handling | `backend/BrownEvents.Api/Program.cs:74-90` |
| Frontend error boundary | `frontend/src/components/ErrorBoundary.jsx` |
| Lint/format commands | [[quality-gates]] |

# EPMCDMPTST-1950: Standardize API Responses and Error Handling

## Context

The frontend team hits two problems today:
1. Response shapes are inconsistent per endpoint — every action returns the raw resource via `Ok(...)`/`NotFound()` **except** `ConferencesController.AddSession`, which wraps its result in an ad-hoc `new { data = created }` envelope found nowhere else.
2. There is zero exception handling anywhere in the app (`Program.cs`, `Controllers/`, `Services/`). An unhandled exception bubbles to the default ASP.NET Core handler, which can render an HTML page with a stack trace (or a bare empty 500), and no controller validates input, so "bad input" never reliably yields 400.

Goal: make every endpoint predictable — same success shape convention, structured JSON errors, correct status codes for create/not-found/bad-input/unexpected-error — without introducing a heavyweight wrapper type the frontend doesn't need.

## Approach

- **Success responses**: keep returning the resource/collection directly (drop the one-off `{ data = created }` envelope in `AddSession` so it matches every other endpoint). "Same structure" = every endpoint follows the same convention (resource on success, `ProblemDetails` on error) — not a new universal wrapper object, which would be unnecessary churn for both API and frontend.
- **Error responses**: use the built-in `Microsoft.AspNetCore.Mvc.ProblemDetails` JSON shape everywhere (`{ type, title, status, detail }`), via:
  - `ControllerBase.Problem(...)`/`NotFound(...)` for handled cases (404).
  - `[ApiController]` attribute added to all controllers — gives automatic `400 ValidationProblemDetails` for invalid `[FromBody]` model state, for free, no per-action code.
  - A global exception-handling middleware in `Program.cs` (`app.UseExceptionHandler`) that catches any unhandled exception and writes a `ProblemDetails` JSON 500 response instead of an HTML page/stack trace.
- **201 Created for POSTs**: switch every POST that creates a resource from `Ok(...)` to `Created(...)`/`CreatedAtAction(...)`, with a `Location` header pointing at the new resource where a matching GET-by-id action exists.

TDD: **yes** for the new behavior — it's clear, checkable HTTP behavior (status code + body shape). Will extend `DbContextConcurrencyTests.cs` (which currently asserts `200 OK` on the register POST — this will need updating to `201` regardless) and add a new integration test file for the ProblemDetails/400/404/500 shapes, using the existing `WebApplicationFactory<Program>` + in-memory `AppDbContext` pattern already established there.

## Files to change

**`backend/BrownEvents.Api/Program.cs`**
- Add `app.UseExceptionHandler(...)` as the first middleware, writing a `ProblemDetails` JSON body (`status 500`, generic title, no exception details leaked) for any unhandled exception.

**Controllers** (`ConferencesController.cs`, `SessionsController.cs`, `SpeakersController.cs`) — apply the same pattern to each:
- Add `[ApiController]` to the class (enables automatic 400 on invalid model state).
- Change every `Ok(created)` on a POST that creates a resource to `CreatedAtAction(nameof(GetById), new { id = created.Id }, created)` where a `GetById` action exists in that controller (`ConferencesController.Create`, `SessionsController` via `ConferencesController.AddSession` — creates a `Session`, so route to `SessionsController`'s `GetById`), otherwise `Created($"api/.../{created.Id}", created)` (`SpeakersController.Create` — no speaker-by-id endpoint exists; `ConferencesController.Register` — no registration-by-id endpoint exists, so `Created($"api/conferences/{id}/registrations/{registration.Id}", registration)`).
- Remove the `new { data = created }` envelope in `AddSession`; return the `Session` directly via `CreatedAtAction`.
- Replace bare `NotFound()` with `NotFound(new ProblemDetails { Title = "Not Found", Status = 404, Detail = "<specific message>" })` (or the `Problem(statusCode: 404, detail: ...)` helper) so 404s carry a message body, not an empty one.
- Update XML doc `<response>` comments/`[ProducesResponseType]` attributes to reflect `201`/`ProblemDetails`.

**`backend/BrownEvents.Tests/DbContextConcurrencyTests.cs`**
- The register POST assertion currently expects all responses (GET + GET + POST register) to be `200 OK`. Split the assertion so GETs still expect `200` and the register POST expects `201`.

**New: `backend/BrownEvents.Tests/ApiResponseShapeTests.cs`**
- Using the same `WebApplicationFactory<Program>` + in-memory `AppDbContext` setup as `DbContextConcurrencyTests`:
  - POST to create a conference → assert `201 Created`, `Location` header present, body deserializes to the resource.
  - GET a non-existent conference id → assert `404`, body deserializes as `ProblemDetails` with a non-empty `Detail`/`Title`.
  - POST with invalid body (e.g. missing required field, or malformed JSON triggering model-state failure) → assert `400`, body is a `ValidationProblemDetails`-shaped JSON (has `errors`).
  - Trigger an unhandled exception path (e.g. POST register for a non-existent conference is already a handled 404 — for the 500 case, verify indirectly that the global handler is wired by checking `Content-Type: application/json` and absence of HTML on any error response) — if a clean way to force a genuine 500 isn't available without new production code paths, scope this assertion to confirming the JSON error shape on the 404/400 cases and note the 500 handler is covered by manual verification (`curl` against a forced exception) rather than an automated test, to avoid adding fake failure injection code.

## Out of scope

- No new universal success-response wrapper type — see rationale above.
- No changes to frontend (`frontend/src/api.js` etc.) — ticket is backend-only; frontend already just deserializes whatever body comes back, and shapes for success responses are unchanged (still the raw resource) except `AddSession` losing its `data` wrapper, which nothing in `frontend/` currently depends on (to confirm during implementation).
- No new custom exception types in `Services/` — the existing "service returns null → controller returns 404" convention is kept; the change is only in how that 404 (and validation, and the unhandled-exception path) is serialized.

## Verification

- `dotnet test BrownEvents.Tests` — full suite green, including updated `DbContextConcurrencyTests` and new `ApiResponseShapeTests`.
- `dotnet format BrownEvents.sln --verify-no-changes`.
- Manual: `docker-compose up --build` (or the `run` skill via Podman), then `curl -i` a few endpoints:
  - `POST /api/conferences` → `201` + `Location` header.
  - `GET /api/conferences/999999` → `404` JSON body with a message, not HTML.
  - `POST /api/conferences` with malformed JSON → `400` JSON body.

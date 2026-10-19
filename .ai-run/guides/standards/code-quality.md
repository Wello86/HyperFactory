# Code Quality Standards

**Project**: BrownEvents
**Backend Linter/Formatter**: `dotnet format` against root `.editorconfig` (C#, no separate linter tool — no StyleCop/Roslynator configured)
**Frontend Linter**: ESLint 8.57 (`eslint:recommended` + `plugin:react/recommended` + `plugin:react-hooks/recommended` + `prettier`)
**Frontend Formatter**: Prettier 3.2.5
**Type Checker**: None — backend relies on C#'s static typing; frontend is plain JS/JSX, no TypeScript/PropTypes/Flow

---

## Quality Commands

| Action | Command | Description |
|---|---|---|
| Backend lint check | `dotnet format backend/BrownEvents.sln --verify-no-changes` | Verifies formatting, fails on violations |
| Backend lint fix | `dotnet format backend/BrownEvents.sln` | Auto-fixes formatting |
| Frontend lint | `npm run lint` (in `frontend/`) | ESLint check |
| Frontend format check | `npm run format:check` | Prettier check |
| Frontend format fix | `npm run format` | Prettier auto-fix |

**Before committing, run**: the pre-commit hooks in `lefthook.yml` do this automatically per staged glob — see [[development-practices]].

---

## Enforced Rules

### From Backend (`.editorconfig`)

| Rule | Setting | Rationale |
|---|---|---|
| `dotnet_sort_system_directives_first` | `true` | `System.*` usings sorted first |
| `dotnet_separate_import_directive_groups` | `false` | No blank line between the two using groups |
| `csharp_prefer_braces` | `true` | Every `if`/`for`/etc. must use `{ }`, even single-line |
| `csharp_new_line_before_open_brace` | `all` | Allman-style braces (own line) |
| `indent_size` (C#) | `4` | vs. `2` for everything else |

### From Frontend (`.eslintrc.cjs`)

| Rule | Setting | Rationale |
|---|---|---|
| `eslint:recommended` | on | Baseline JS correctness rules |
| `react/recommended` | on | React-specific correctness |
| `react-hooks/recommended` | on | Hooks rules-of-hooks / exhaustive-deps |
| `react/react-in-jsx-scope` | `off` | Not needed — React 18 JSX transform |
| `react/prop-types` | `off` | No PropTypes used anywhere in the app |

### From Formatter (`.prettierrc`)

| Setting | Value |
|---|---|
| Semicolons | `false` (no semicolons) |
| Quotes | Single |
| Trailing comma | `es5` |
| Print width | `100` |

---

## Naming Standards

| Element | Rule | ✅ Correct | ❌ Wrong |
|---|---|---|---|
| C# classes/interfaces | PascalCase, `I{Name}` for interfaces | `ConferenceService`, `IConferenceService` | `conferenceService` |
| C# async methods | PascalCase + `Async` suffix | `GetAllAsync` | `getAll` |
| C# private fields | `_camelCase` | `_context`, `_service` | `context` |
| React components | PascalCase file + default/named export | `ConferenceCard.jsx` | `conferenceCard.jsx` |
| React hooks | `use{Concern}.js`, camelCase | `useConference.js` | `ConferenceHook.js` |
| JS variables/functions | camelCase | `registrationCount` | `registration_count` |

---

## Type Safety

**Backend**: C#'s compiler enforces static types on every parameter/return — this is inherent to the language, not a separate lint rule. Data-annotation validation (`[Required]`, `[MaxLength]`) supplements this at the model level — see [[security-practices]].

**Frontend**: **No type checking at all.** No TypeScript, no `PropTypes` (explicitly disabled via `react/prop-types: 'off'`), no runtime shape validation on props. Do not introduce PropTypes on some components but not others — the project has made a consistent choice to have none; if type safety is wanted, that's a project-wide TypeScript migration decision, not a per-component addition.

---

## Code Complexity Limits

No cyclomatic-complexity, max-function-length, max-file-length, or max-nesting rule is configured in either `.editorconfig` or `.eslintrc.cjs`. There is no enforced limit — keep methods/components focused by following the existing service-per-entity / hook-per-concern structure (see [[architecture]]) rather than a numeric threshold.

---

## Import Organization

### Backend (C#)

```csharp
// Source: backend/BrownEvents.Api/Program.cs:1-9
using System.Reflection;
using BrownEvents.Api.Data;
using BrownEvents.Api.Services;
using BrownEvents.Api.SwaggerExamples;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
```

**Rules**: `System.*` first (alphabetical within that group), then all others alphabetically, **no blank line** between groups.

### Frontend (JS)

No import-order ESLint rule is configured — imports are not alphabetized or grouped by convention in this codebase; follow the existing order in the file you're editing rather than reordering unrelated imports.

---

## Documentation Standards

| Element | Required | Format |
|---|---|---|
| Public controller actions | ✅ | XML doc comments (`///`) — every action in `ConferencesController.cs` has `<summary>`/`<param>`/`<response>` tags, consumed by Swagger via `IncludeXmlComments` (`Program.cs:65-66`) |
| Services/private methods | Optional | No doc-comment convention observed |
| React components/hooks | Optional | No JSDoc convention observed |

```csharp
// Source: backend/BrownEvents.Api/Controllers/ConferencesController.cs:55-63
/// <summary>
/// Gets a single conference by its id.
/// </summary>
/// <param name="id">The conference id.</param>
/// <param name="cancellationToken">Cancellation token from the request.</param>
/// <response code="200">The requested conference.</response>
/// <response code="404">No conference exists with the given id.</response>
/// <response code="500">An unexpected error occurred.</response>
[HttpGet("{id}")]
```

---

## Common Violations & Fixes

| Violation | Fix |
|---|---|
| `dotnet format --verify-no-changes` fails on `using` order | Run `dotnet format backend/BrownEvents.sln` (no flag) to auto-fix, or apply the System-first/no-blank-line rule by hand — `dotnet` is not installed in this AI environment |
| ESLint `react-hooks/exhaustive-deps` warning | Add the missing dependency to the hook's dependency array, or explain why it's intentionally omitted |
| `npm run format:check` fails | Run `npm run format` to auto-fix with Prettier |

---

## Quick Reference

| Need | Location |
|---|---|
| Backend style rules | `.editorconfig` |
| Frontend lint rules | `frontend/.eslintrc.cjs` |
| Frontend format rules | `frontend/.prettierrc` |
| Exact CI/pre-commit commands | [[quality-gates]], [[development-practices]] |

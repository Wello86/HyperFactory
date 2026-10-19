# Add and configure linters for BrownEvents

## Context

The repo currently has zero lint/format tooling on either side (confirmed via exploration — no `.eslintrc*`/`eslint.config.*`/`.prettierrc*` in `frontend/`, no `.editorconfig` anywhere, no analyzer settings in either `.csproj`). Style drift is currently unconstrained and CI has no lint stage. This task adds baseline linting/formatting for both frontend and backend and wires a blocking lint stage into the existing GitLab CI pipeline.

## TDD: N/A

This is config/tooling-only work (no application behavior changes), so TDD doesn't apply. Verification is: linters run clean (or with only expected/fixed findings) against the current codebase, and CI's new stage passes locally-simulated commands.

## Scope / stack facts from exploration

- Frontend: React 18.2.0, Vite 4.4.5, `@vitejs/plugin-react`, plain JS/JSX (no TypeScript), no `lint`/`format` npm scripts yet.
- Backend: .NET 6 (`net6.0`), two projects — `BrownEvents.Api` and `BrownEvents.Tests`, no `BrownEvents.sln`-level Directory.Build.props.
- CI: single `.gitlab-ci.yml` at repo root, stages `build → test → docker`. Need to insert a `lint` stage (before `build`, so it fails fast) with `frontend:lint` and `backend:lint` jobs.

## Steps

### 1. Frontend — ESLint + Prettier

- Add devDependencies to `frontend/package.json`: `eslint`, `eslint-plugin-react`, `eslint-plugin-react-hooks`, `prettier`, `eslint-config-prettier`.
- Use classic `.eslintrc.cjs` format for simplest compatibility with React 18 + eslint-plugin-react-hooks.
- Add `.prettierrc`, `.prettierignore`, `.eslintignore`.
- Add npm scripts: `lint`, `lint:fix`, `format`, `format:check`.
- Run `npm install` then `npm run lint` locally; fix any violations found in existing source.

### 2. Backend — .editorconfig, dotnet format, Roslyn analyzers

- Add root-level `/.editorconfig` with standard .NET conventions.
- Enable `EnableNETAnalyzers`/`AnalysisLevel=latest` in both csproj files.
- Run `dotnet format backend/BrownEvents.sln --verify-no-changes`; apply fixes if needed.
- Run `dotnet build` to confirm no build breakage.

### 3. CI — add blocking lint stage

- Add `lint` stage before `build` in `.gitlab-ci.yml`.
- Add `frontend:lint` (node:18-alpine, `npm ci && npm run lint`).
- Add `backend:lint` (dotnet sdk:6.0, `dotnet format backend/BrownEvents.sln --verify-no-changes`).

### 4. Documentation

- Update `docs/testing-ci.md` and `CLAUDE.md` for the new lint stage/commands.

## Verification

- `npm run lint`, `npm run format:check` pass in `frontend/`.
- `dotnet format backend/BrownEvents.sln --verify-no-changes` passes.
- `dotnet build backend/BrownEvents.sln` builds clean.
- `.gitlab-ci.yml` YAML structure reviewed.

## Out of scope

- No TypeScript migration.
- No changes to existing app behavior/tests.
- Not configuring flat-config (`eslint.config.js`) — using classic `.eslintrc.cjs`.

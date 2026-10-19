# Quality Gates

**Project**: BrownEvents
**Source**: `.gitlab-ci.yml` (4 stages: lint → build → test → docker), cross-checked against [[standards/code-quality]] and [[development/development-practices]]

> Ordered fastest-to-slowest. Run earlier gates first — no point waiting on a slow build/test cycle if formatting already fails.

---

### Backend Lint (`dotnet format`)

**Run**: `dotnet format backend/BrownEvents.sln --verify-no-changes` (CI additionally runs `dotnet restore` first and adds `--no-restore`, `.gitlab-ci.yml:17-18`)
**Pass**: exits 0, no output
**Fail**: lists each file/rule violation (e.g. using-directive order, brace style) per `.editorconfig`; nonzero exit
**Auto-fix**: drop `--verify-no-changes` — `dotnet format backend/BrownEvents.sln`
**Skip if**: no `.cs` files changed. **Note**: neither `dotnet` nor `lefthook` is installed in this AI environment, so this cannot be run or self-checked locally here — apply the using-order rule by hand (see [[development/development-practices]]) and rely on the `backend:lint` CI job as the actual gate.

### Frontend Lint (ESLint + Prettier)

**Run**: `npm run lint && npm run format:check` (in `frontend/`; CI also runs `npm ci` first, `.gitlab-ci.yml:28`)
**Pass**: both exit 0
**Fail**: `npm run lint` lists ESLint rule violations by file:line; `npm run format:check` lists files that would be reformatted
**Auto-fix**: `npm run lint -- --fix` for ESLint-fixable rules; `npm run format` for Prettier
**Skip if**: no `frontend/**/*.{js,jsx}` files changed

### Backend Build

**Run**: `dotnet build backend/BrownEvents.sln -c Release` (CI: `dotnet restore` then `dotnet build backend/BrownEvents.sln --no-restore -c Release`, `.gitlab-ci.yml:38-39`)
**Pass**: exits 0, produces `backend/BrownEvents.Api/bin/Release/net10.0/`
**Fail**: compiler errors printed with file:line; nonzero exit
**Skip if**: never — build is not skippable once lint passes

### Frontend Build

**Run**: `npm run build` (in `frontend/`; CI also runs `npm ci` first, `.gitlab-ci.yml:62`)
**Pass**: exits 0, produces `frontend/dist/`
**Fail**: Vite/Rollup error with file reference; nonzero exit
**Skip if**: never — build is not skippable once lint passes

### Backend Test

**Run**: `dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj -c Release --logger "console;verbosity=normal"` (`.gitlab-ci.yml:52`)
**Pass**: all xUnit tests pass, summary line shows `Failed: 0`
**Fail**: failing test names + assertion diffs printed; nonzero exit
**Skip if**: never — this is the last gate before merge; see [[testing/testing-patterns]] for what a new test should look like

### Frontend Test + Coverage Gate (Vitest + diff-cover)

**Run**: `npm run test:coverage && ./scripts/check-diff-coverage.sh` (in `frontend/`; CI also runs `npm ci` first and installs `diff-cover` via pip, `.gitlab-ci.yml` `frontend:test`)
**Pass**: all Vitest tests pass, and every new/changed line in the diff against `origin/main`'s merge-base is covered (`diff-cover --fail-under=100`)
**Fail**: failing Vitest assertions, or `diff-cover` reports uncovered new/changed lines with file:line
**Skip if**: no `frontend/**` files changed. **Note**: this is a diff-scoped gate — it enforces coverage only on newly introduced/changed lines and does not fail on the pre-existing 0%-covered codebase. Coverage is reported to the MR via `artifacts.reports.coverage_report` (Cobertura) and the `coverage:` regex badge.

---

## Not Configured

| Gate | Status |
|---|---|
| Type-check (frontend) | None — no TypeScript in this project, see [[standards/code-quality]] |
| Secret scanning | None configured in `.gitlab-ci.yml` |
| Dependency/vulnerability scanning | None configured — see [[security/security-practices]] Dependency Security |
| Coverage threshold (backend) | None enforced — `dotnet test` runs without a coverage collector or minimum |

Docker build/push (`docker:backend`, `docker:frontend`) run only on `main` (`.gitlab-ci.yml:81-82,94-95`) and are not a pre-merge gate — they publish, they don't verify.

---

## Quick Reference

| Need | Command |
|---|---|
| Run everything locally before pushing | `dotnet format backend/BrownEvents.sln && dotnet build backend/BrownEvents.sln -c Release && dotnet test backend/BrownEvents.Tests/BrownEvents.Tests.csproj -c Release && npm run lint && npm run format:check && npm run build && npm run test:coverage && ./scripts/check-diff-coverage.sh` (run frontend commands from `frontend/`) |
| Full CI definition | `.gitlab-ci.yml` |
| Pre-commit equivalents (subset, staged files only) | `lefthook.yml` — see [[development/development-practices]] |

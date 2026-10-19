# EPMCDMPTST-1964 — Frontend Has No Linting or Style Enforcement

## Context

The ticket asks for automated frontend code style enforcement: ESLint + Prettier configured and
integrated, `npm run lint`/`lint:fix` scripts, a pre-commit hook blocking bad commits, and CI
failing on lint/format violations.

## Findings from exploration

Most of this already exists in `frontend/`:
- `.eslintrc.cjs` — `eslint:recommended` + `plugin:react/recommended` + `plugin:react-hooks/recommended` + `prettier` (disables conflicting stylistic rules). Reasonable ruleset for a React 18 app.
- `.eslintignore` — ignores `dist`, `node_modules`.
- `.prettierrc` / `.prettierignore` — sensible defaults (`semi: false`, `singleQuote: true`, etc.), ignores `dist`, `node_modules`, `package-lock.json`.
- `package.json` scripts: `lint`, `lint:fix`, `format`, `format:check` — all present and working (`npm run lint` and `npm run format:check` both pass clean on current code).
- `.gitlab-ci.yml` has a `frontend:lint` job that runs `npm run lint` in the `lint` stage.

**Gaps against the acceptance criteria:**
1. CI's `frontend:lint` job only runs `npm run lint` — it does **not** run `npm run format:check`, so the pipeline does not fail on formatting violations (one AC item unmet).
2. `lefthook.yml` only has a pre-commit command for `backend/**/*.cs` (`dotnet format`). There is no equivalent hook for frontend files — nothing prevents committing JS/JSX that fails lint or formatting.

## TDD

TDD: no — this is config/tooling only (CI YAML, lefthook YAML). There's no application behavior to drive with a red/green test; verification is by running the lint/format/CI commands directly and observing pass/fail.

## Plan

1. **CI**: update `frontend:lint` job in `.gitlab-ci.yml` to also run `npm run format:check` after `npm run lint`, so both lint and formatting violations fail the pipeline.
2. **Pre-commit hook**: add a `frontend-lint` command to `lefthook.yml`'s `pre-commit` section, globbed to `frontend/**/*.{js,jsx}`, running `npm --prefix frontend run lint` (mirrors the existing `dotnet-format` command's pattern). This blocks commits with lint violations. Since Prettier is already wired into ESLint via `eslint-config-prettier` and `npm run lint` catches formatting issues that would otherwise conflict, but format-only violations (whitespace, quotes) aren't caught by ESLint alone — so also run `npm --prefix frontend run format:check` in the same command (`&&`), matching what CI now enforces.
3. Verify: run `npm run lint`, `npm run lint:fix`, `npm run format:check` in `frontend/` to confirm scripts still work; introduce a throwaway lint/format violation locally to confirm the new lefthook command blocks a commit, then revert it.

## Out of scope

- No changes to the existing ESLint ruleset or Prettier config — both are already "appropriate for the project" per the AC, and current code passes clean.
- Not touching `backend` lint/format tooling (separate, already covered by `dotnet format`).

## Verification

- `cd frontend && npm run lint` — passes (no violations currently; AC requires it to *report* violations, i.e., be capable of failing — confirmed by temporarily introducing a violation).
- `cd frontend && npm run lint:fix` — auto-fixes a temporarily introduced fixable violation.
- `cd frontend && npm run format:check` — passes; fails when a formatting violation is introduced.
- Stage a file with an intentional lint violation, attempt `git commit` — lefthook pre-commit hook blocks it; revert the violation and confirm commit proceeds normally (dry-run only, not an actual commit unless requested).
- Inspect updated `.gitlab-ci.yml` `frontend:lint` job to confirm it runs both `npm run lint` and `npm run format:check`.

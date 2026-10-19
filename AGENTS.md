# AGENTS.md

Primary AI entrypoint for `BrownEvents`. Load the relevant `.ai-run/guides/` file before changing code.

## Guide Imports

<!-- ai-run-init:guide-imports start -->
| Category | Guide Path | Purpose |
|---|---|---|
| Architecture | .ai-run/guides/architecture/architecture.md | Backend layering, request flows, entity relationships, frontend routing |
| Data | .ai-run/guides/data/database-patterns.md | EF Core patterns, `EnsureCreated`, transaction guard pattern |
| API | .ai-run/guides/api/api-patterns.md | Route conventions, DTO mapping, pagination, error response shapes |
| Security | .ai-run/guides/security/security-practices.md | Current security posture (no auth), input validation, CORS, secrets |
| Testing | .ai-run/guides/testing/testing-patterns.md | xUnit + EF InMemory test conventions |
| Development | .ai-run/guides/development/development-practices.md | Formatting, naming, error handling, logging, async conventions |
| Code Quality | .ai-run/guides/standards/code-quality.md | Lint/format rules, naming standards, type safety |
| Git Workflow | .ai-run/guides/standards/git-workflow.md | Branching, commit format, GitLab MR/merge strategy |
| Quality Gates | .ai-run/guides/quality-gates.md | Exact lint/build/test commands, ordered fastest-to-slowest |
| Project Context | .ai-run/guides/project.md | Ticket adapter, MR adapter, tracker key |
<!-- ai-run-init:guide-imports end -->

## Task Classifier

<!-- ai-run-init:task-classifier start -->
| Category | User Intent | Example Requests | P0 Guide | P1 Guide |
|---|---|---|---|---|
| Architecture | Understand system structure | "How does session registration flow work?" | .ai-run/guides/architecture/architecture.md | .ai-run/guides/data/database-patterns.md |
| API | Add or modify an endpoint | "Add a DELETE endpoint for speakers" | .ai-run/guides/api/api-patterns.md | .ai-run/guides/security/security-practices.md |
| Data | Change models or EF queries | "Add a field to Session" | .ai-run/guides/data/database-patterns.md | .ai-run/guides/architecture/architecture.md |
| Security | Auth, validation, or secrets questions | "Add login" / "Is this endpoint safe?" | .ai-run/guides/security/security-practices.md | - |
| Testing | Write or run tests | "Add a test for the registration service" | .ai-run/guides/testing/testing-patterns.md | .ai-run/guides/quality-gates.md |
| Code Style | Formatting, naming, or lint questions | "Fix the lint errors" | .ai-run/guides/standards/code-quality.md | .ai-run/guides/development/development-practices.md |
| Git / MR | Commit, push, open an MR | "Commit and open an MR" | .ai-run/guides/standards/git-workflow.md | .ai-run/guides/project.md |
| Build / Verify | Run lint/build/test gates | "Run the tests" | .ai-run/guides/quality-gates.md | - |
<!-- ai-run-init:task-classifier end -->

## Critical Rules

<!-- ai-run-init:critical-rules start -->
| Rule | Trigger | Action |
|---|---|---|
| Check Guides First | ANY task | Match request to category and load the P0 guide before searching broadly |
| Testing | "write tests" / "run tests" | Only write or run tests when requested or when needed for verification |
| Git Operations | "commit" / "push" / "PR" / "MR" | Only then; load `.ai-run/guides/standards/git-workflow.md` |
| Shell | ANY shell command | Use bash/Linux-compatible syntax |
| Ticket Lookup | A ticket key is mentioned (e.g. an `EPMCDMPTST-####` reference) | Load `.ai-run/guides/project.md` `## Ticket Adapter` before acting |
<!-- ai-run-init:critical-rules end -->

## Commands

<!-- ai-run-init:commands start -->
| Need | Source Guide | Source Evidence | Notes |
|---|---|---|---|
| Lint / format | .ai-run/guides/quality-gates.md | `.gitlab-ci.yml`, `.editorconfig`, `frontend/.eslintrc.cjs` | Load guide before running |
| Build | .ai-run/guides/quality-gates.md | `.gitlab-ci.yml` | Load guide before running |
| Tests | .ai-run/guides/quality-gates.md | `.gitlab-ci.yml` | Load guide before running |
| Git / review workflow | .ai-run/guides/standards/git-workflow.md | `git log`, `git remote -v` | Load guide before git operations |
<!-- ai-run-init:commands end -->

# CLAUDE.md

**Purpose**: Claude Code entrypoint for `BrownEvents`.

<!-- ai-run-init:guide-imports start -->
@AGENTS.md
<!-- ai-run-init:guide-imports end -->

## Workflow

For any implementation task (bug fix, feature, refactor) — including tasks pasted as plain
text/tickets with no explicit command — invoke the `task-runner` skill first, before exploring
the code or making edits. Do not skip it because the request doesn't contain a specific trigger
phrase; any request to build/fix something in this repo qualifies.

## Skills

- `task-runner` — use first for any implement/fix/build request; enforces creating plan using plan-creator skill, **Do not use your native plan** → confirm → implement → test → review workflow.
- `code-review` (aka `mr-review`) — reviews the current branch against `main` and produces GitLab-ready MR comments in `mr-comments.md`.

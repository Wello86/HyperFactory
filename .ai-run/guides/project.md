# Project Context

## Project Identity

| Field | Value | Source |
|---|---|---|
| Project name | BrownEvents | README.md, CLAUDE.md |
| Repository/package | `shazam` (repo), `backend/BrownEvents.sln` + `brownevents-dotnet-frontend` (`frontend/package.json:2`) | `git remote -v`, `backend/BrownEvents.sln`, `frontend/package.json:2` |
| Project code/key | EPMCDMPTST | `git log --oneline` commit/branch prefixes |

## Work Item Tracker

| Field | Value |
|---|---|
| Provider | Jira |
| Key/prefix | EPMCDMPTST |

> Adapter configuration belongs exclusively in `## Ticket Adapter`. Do not duplicate adapter status or instructions in the Work Item Tracker table.

## Ticket Adapter

**Status**: configured
**Adapter**: Launch the `jira-assistant-ai-runway` agent via the Agent tool
**Lookup**: Fetch full ticket details (summary, description, acceptance criteria, links) for a given `EPMCDMPTST-<number>` key — read-only
**Create**: not configured
**Output**: Ticket fields returned by the agent in its response

## Source Control And Review

| Field | Value |
|---|---|
| Provider | GitLab |
| Repository remote | `git@gitbud.epam.com:epm-cdmp/ai-runway/runs/September-2026/shazam.git` |
| Default target branch | main |
| Review artifact type | MR |

## MR Adapter

**Status**: configured
**Adapter**: `glab` CLI (GitLab's `gh` equivalent) — see `.claude/skills/task-runner/references/docs-and-mr.md`
**Instructions**: Check `glab auth status` before use; never ask the user to paste a personal access token into chat — direct them to run `glab auth login` themselves via a `!`-prefixed command. Check for an existing MR with `glab mr view --output json` before creating a new one; `glab mr update <iid> --description-file <path>` to update, `glab mr create --title "<TICKET-ID>: <title>" --description-file <path> --target-branch main` to create. Never pass `--yes` or auto-merge flags.
**Body Template**: `docs/plans/<slug>-mr.md`, written by `.claude/skills/task-runner` Step 10 before publishing

## Complexity Scoring

**Status**: not configured
**Field**: not configured
**Format**: not configured

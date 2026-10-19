# Git Workflow

## Quick Summary

Git workflow for BrownEvents: branching, commits, merge requests, and code review — detected from `git log` history against the `EPMCDMPTST` Jira project key (see [[project]]).

**Category**: Standards
**Complexity**: Simple
**Prerequisites**: Git basics, a GitLab MR

> These conventions were detected from `git log` history, not read from a written team policy doc — no CONTRIBUTING.md exists. Confidence is high (20+ consistent commits sampled) but not user-confirmed; correct this file if it doesn't match team intent.

---

## Branching Strategy

| Branch Type | Pattern | Purpose |
|---|---|---|
| Main | `main` | Production / merge target |
| Ticket work | `EPMCDMPTST-<ticket#>-<PascalCase-or-Hyphenated-Description>` | All feature/fix/refactor work |

**Project Pattern** (observed, e.g. `git log --all`):
```
EPMCDMPTST-1966-PublicAPIEndpoints
EPMCDMPTST-1965-AttendeesCannotRegister
EPMCDMPTST-1957-Frontend-Cleanup-After-Code-Review
EPMCDMPTST-1959-Fix-Security
```
No dedicated `feature/`/`bugfix/`/`hotfix/` prefixes are used — every branch is named directly after its ticket number, with either PascalCase or hyphenated words for the description (both forms observed; either is acceptable).

---

## Workflow

```bash
# 1. Create branch from the ticket
git checkout main
git pull
git checkout -b EPMCDMPTST-<ticket#>-<Description>

# 2. Work & commit
git add [files]
git commit -m "EPMCDMPTST-<ticket#>: <Description>"

# 3. Push
git push origin EPMCDMPTST-<ticket#>-<Description>

# 4. Open a GitLab merge request targeting main
```

---

## Commit Message Format

```
EPMCDMPTST-<ticket#>: <Description>
```

**Examples** (from `git log --oneline`):
- `EPMCDMPTST-1965: Attendees Cannot Register for Individual Sessions`
- `EPMCDMPTST-1955: Wrap multi-step writes in transactions`
- `EPMCDMPTST-1954: Introduce DTOs`

One commit per ticket is typical, but not enforced — some tickets have a single commit before merge, others (e.g. `EPMCDMPTST-1956`, which also has a `Merge remote-tracking branch 'origin/main' into ...` commit) accumulate a sync commit. Prefix every commit subject with the ticket key even when there will be only one.

---

## Commit Rules

| ✅ DO | ❌ DON'T |
|---|---|
| Prefix with `EPMCDMPTST-<ticket#>:` | Commit without a ticket reference |
| Use the ticket's own description as the subject | Write a generic message like "fix stuff" |
| Keep description in title case matching the ticket title | Use past tense inconsistently with the ticket phrasing |

---

## Merge Request

BrownEvents uses **GitLab merge requests** (not GitHub PRs) — see [[project]] Source Control And Review.

### MR Title

Match the ticket: `EPMCDMPTST-<ticket#>: <Description>` (same as the commit subject).

### Merge Strategy

**Project Standard**: **merge commit** (GitLab "Merge" button, not squash/rebase) — every ticket branch lands via a non-fast-forward merge commit:

```
Merge branch 'EPMCDMPTST-1966-PublicAPIEndpoints' into 'main'

See merge request epm-cdmp/ai-runway/runs/...
```

```bash
# After MR approved (GitLab UI performs this) — do not squash or rebase locally before merge
git branch -d EPMCDMPTST-<ticket#>-<Description>   # cleanup after merge
```

Do not rebase a ticket branch onto `main` before opening the MR unless asked — the observed history keeps each ticket's original commits intact under the merge commit.

---

## Code Review Checklist

- [ ] Code follows [[code-quality]] standards
- [ ] `dotnet format` / `npm run lint` + `npm run format:check` pass (see [[quality-gates]])
- [ ] Tests pass (`dotnet test`)
- [ ] Commit subject is prefixed with the correct `EPMCDMPTST-<ticket#>:`
- [ ] No merge conflicts with `main`

---

## Common Commands

```bash
git status
git add [files]
git commit -m "EPMCDMPTST-<ticket#>: <Description>"
git push origin EPMCDMPTST-<ticket#>-<Description>
git pull origin main
git reset --soft HEAD~1   # undo last local commit, keep changes
```

---

## Anti-Patterns

| ❌ NEVER | ✅ INSTEAD | Risk |
|---|---|---|
| Commit without the `EPMCDMPTST-<ticket#>:` prefix | Always prefix with the ticket key | Breaks traceability to Jira, inconsistent with 100% of sampled history |
| Squash or rebase a ticket branch before merging | Merge via GitLab's merge-commit button | Diverges from the observed non-fast-forward merge pattern |
| Push directly to `main` | Open a merge request | No branch protection evidence found, but every sampled ticket went through an MR |

---

## Troubleshooting

| Issue | Solution |
|---|---|
| Branch name doesn't match ticket description | Rename with `git branch -m` before pushing, or let GitLab auto-generate from the MR title |
| Accidentally committed without ticket prefix | `git commit --amend` before pushing (if unpushed); otherwise a follow-up commit with the correct prefix |

---

## References

- **Ticket adapter**: [[project]] — `## Ticket Adapter` section
- **Quality gates run before merge**: [[quality-gates]]

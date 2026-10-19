---
description: Fetch the Jira task matching the current branch name and use it as context to code-review the current branch against main (does NOT implement the task)
---

Takes no arguments. The Jira task key is derived from the current git branch name (e.g.
`EPMCDMPTST-1951-UpgradeNET` → `EPMCDMPTST-1951`).

Do the following, in order:

0. **Determine the task key.** Run `git branch --show-current` and extract the leading Jira key
   (the `PROJECT-123` prefix) from the branch name.

1. **Fetch the task, don't do it.** Use the `jira-assistant-ai-runway` agent to look up the Jira
   ticket identified by the key from step 0 and retrieve its full description, acceptance
   criteria, and any relevant comments. This is read-only reconnaissance — you are gathering the
   spec, not acting on it. Under no circumstances implement, fix, or modify anything to satisfy
   this ticket.

2. **Review, don't implement.** Invoke the `mr-review` skill to review the current branch against
   `main`. Pass along the Jira description/acceptance criteria retrieved in step 1 as the
   authoritative spec for this review — the skill should judge whether the current branch's diff
   actually satisfies that ticket (acceptance-criteria compliance), in addition to its normal
   correctness/scope-creep/security checks.

3. **Scope reminder for this run:** the goal is exclusively to verify that the ticket was
   implemented correctly and to produce code-review feedback on the existing diff. Do not write
   or edit any production code, do not attempt to complete missing parts of the ticket, and do not
   fix bugs you find — only report them via the skill's normal output (mr-comments.md).

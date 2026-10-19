---
name: mr-review
description: Review the current branch against main and produce GitLab-ready MR comments in mr-comments.md. Use when asked to review a branch or MR, to code-review changes before opening an MR, or to prepare review comments for GitLab. Covers correctness, scope creep, acceptance-criteria compliance and security.
---

# MR review

Produce a review of the current branch against `main` as a file the reviewer can paste into GitLab, comment by comment.

The deliverable is `mr-comments.md` in the repo root. Use `templates/mr-comments.md` next to this skill as the skeleton. Do **not** commit it — it is a review artifact of the branch under review and would pollute the diff.

## Procedure

1. **Get the task text.** If the user pasted a task description with acceptance criteria, treat it as the primary spec. If not, ask for it or find it (branch name often carries a ticket ID). Without it you cannot judge scope or completeness — say so rather than guessing.

2. **Get the diff.**
   ```bash
   git diff main...HEAD --stat
   git diff main...HEAD --name-status
   ```
   Read every changed file in full. For new files, `git log --oneline main -- <path>` confirms it did not exist before — needed before claiming "this changes previous behaviour".

3. **Walk the acceptance criteria literally, sentence by sentence.** Treat each one as a checkbox and record met / not met with evidence. This is the step that finds the misses a skim does not: a document can look complete and still cover 2 of the 3 required items. Do not accept near-misses — if the criterion says "3 endpoints end-to-end", material that stops short of the database does not count.

4. **Scope check.** For every changed file ask: does the task ask for this? Two distinct findings live here:
   - files outside the task's scope (a config or code change in a docs-only MR is the classic — it gets waved through because reviewers skim docs MRs);
   - work inside the scope but beyond the ask, which matters mainly when a required item was skipped in favour of it.

5. **Verify claims, in both directions.**
   - If the branch adds documentation, check its assertions against the code: line references, counts, entity fields, endpoint lists. A confidently wrong doc is worse than a missing one — it sends the next reader down a false trail.
   - Check the inverse too: what does the code do that the doc claims it does not, or omits entirely?

6. **Security pass.** Go through the list explicitly rather than by intuition, and state which items you checked:
   - authentication / authorization — grep for `Authorize`, `UseAuthentication`, `UseAuthorization`; anonymous write endpoints are a finding
   - input validation and DTOs — request bodies bound straight to ORM entities allow mass assignment / overposting; check for nested navigation collections that a client could populate
   - framework validation actually enabled (in ASP.NET Core: is `[ApiController]` present? without it there is no automatic ModelState check and no automatic 400)
   - logging of PII / secrets — trace the whole chain: does the sensitive-logging flag depend on environment, and does the configured log level let the category through in production?
   - secrets in committed files, CORS, rate limiting
   - Distinguish **regression introduced by this branch** from **pre-existing weakness the branch failed to document**. Say which it is. In a brownfield repo most of it is pre-existing and not the author's doing.

7. **Browser check (frontend-touching MRs only).** If the diff touches `frontend/`, launch the app per `run` skill and open the affected pages in the browser. Compare console errors/warnings against `main` (checkout main, run, note baseline; then the branch) — only new ones are findings. If no environment is available to run it, say so explicitly in the output rather than skipping silently.

8. **Run the verification pass below.** Not optional.

9. **Write `mr-comments.md`** from the template.

## Verification pass — mandatory

An AI review's failure mode is not missing findings, it is **overclaiming**: asserting a consequence without checking its premise. One refutable claim costs the credibility of every correct claim beside it. Before writing each comment:

- **Reachability.** Claiming code misbehaves at runtime requires that it runs. `grep -rn "<symbol>" <src>` for imports and call sites first. Dead code is still a finding — but phrase it as "will fail if wired up", not "fails".
- **Constraints and configuration.** Claiming an operation succeeds or fails requires checking what the schema and model config enforce: FK constraints, required relationships, unique indexes, nullability, generated keys. A required relationship means the ORM emits a real FK, so the bad insert fails loudly instead of quietly writing an orphan — a different defect with a different fix.
- **Line references.** Re-read every line you cite, in the committed version. Never carry over a number from memory.
- **Counts and inventories.** Verify with a command (`grep -c`, a file listing), not by recall.
- **Executed vs derived.** State which. Anything not actually run is a derivation from configuration — label it and supply the command that would settle it, so the author can check in a minute instead of arguing.

When a claim does not survive this pass, weaken it to what is true rather than dropping the finding: the underlying observation is usually still worth reporting, just not with the consequence you first attached to it.

## Severity scale

| Label | Meaning |
|---|---|
| `**Blocking.**` | Do not merge as-is: a defect, an unmet acceptance criterion, or a behavioural regression |
| `**Non-blocking.**` | Worth fixing, does not gate the merge; can become a follow-up ticket |
| `**Nit.**` | Cosmetic — typo, numbering, naming, small omission. Author's call |

Singular `Nit.` on an individual comment; `Nits` only as a group heading.

## Output contract

One numbered entry per comment, grouped by severity, in this exact shape:

```
N. file `path/to/file` line NN comment "**Blocking.** …"
```

Rules:
- `line NN` must be a line that exists in the committed file and is part of the diff — GitLab can only anchor a comment there.
- Put the severity label inside the quoted comment text so it survives the copy-paste into GitLab.
- One finding per comment. Do not bundle.
- Each comment states what is wrong, why it matters, and what to do. Cite `file:line` for evidence so the author can verify without hunting.
- Every claim about behaviour must be phrased so it is still true after the author checks it.
- Keep each comment to 1–3 sentences: what is wrong, why it matters, what to do. No preamble, no restating the diff, no hedging paragraph before the point.
- End the file with a "What was verified as correct" section, marked *not for posting*. It gives the reviewer the praise material and pre-empts "did you even look at the rest".

Close with the minimum set required for merge, so the MR does not stall on the full list.

## Tone

- Lead with what is verifiably correct, naming specifics. Generic praise reads as ritual; "every `Program.cs` line reference checks out and the ERD fields match the models one-to-one" reads as evidence the review was careful.
- Frame pre-existing problems as "the document should warn about this", not "you did this". Check the history before assigning authorship.
- Prefer asks that close several findings at once (a missing inventory table often resolves a completeness gap, a factual error and a scope gap together). It reads as help, not as a longer list of demands.
- Recommend, do not survey. If two fixes are possible, name the one you would take.

## Do not

- Do not commit `mr-comments.md`, and do not stage unrelated fixes while reviewing — reviewing and fixing are separate MRs.
- Do not report a finding you have not located in the code. No speculative "there may be".
- Do not pad with counts of files read or a description of your own process.
- Do not soften a Blocking finding into a Nit to keep the tone pleasant; the labels are the mechanism that makes the tone safe.
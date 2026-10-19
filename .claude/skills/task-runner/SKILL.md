---
name: task-runner
description: >-
  Use this skill FIRST, before any other tool (Explore, Agent, Edit, Write, Bash), whenever the
  request is to implement, fix, or build something in code — regardless of phrasing. This
  includes generic requests like "выполни задание"/"do this task", a pasted ticket/task
  description with no explicit command at all, bug reports, and feature requests. Do not
  pattern-match on specific trigger words before invoking it — the absence of a special phrase
  is not a signal to skip it. This applies even if the session is already in plan mode when the
  request arrives (e.g. the user toggled plan mode before typing, or phrased the ask casually as
  "plan then implement it") — native plan mode is not a substitute for this skill; invoke it
  before using EnterPlanMode/ExitPlanMode, Agent, or any research tool, and let it drive the
  plan-mode workflow rather than freehanding one. The only coding requests that don't need it are
  trivial one-line fixes with no ambiguity and no verification story. It enforces a disciplined
  workflow: plan in
  Plan mode first, decide whether TDD applies, get the plan confirmed by the author, persist the
  plan to docs/plans/, execute in auto mode, self-review against the original task, run tests and
  fix failures iteratively, and finish with an independent code-review subagent using a clean
  context.
allowed-tools: [Read, Grep, Glob, Bash, Edit, Write, EnterPlanMode, ExitPlanMode, AskUserQuestion, Agent, TaskCreate, TaskUpdate, TaskList]
---

# Task Runner — Disciplined Task Execution

Run a coding task end-to-end through a fixed sequence: understand → decide on TDD → plan →
confirm → persist plan → implement → self-check → test → review. Do not skip or reorder steps.
The point of this skill is to make execution mechanical, and to add a real quality gate (self-check + tests + an
independent code-review agent) before declaring the task done.

---

## Step 1: Study the code in Plan mode

Enter plan mode (`EnterPlanMode`) before writing any code. If the session is already in plan
mode when this request arrives, that does not mean this step — or this skill — has already been
satisfied: invoking `task-runner` is itself a required action, not merely a way to reach plan
mode. Continue through Steps 2–3 under this skill's discipline rather than treating the
pre-existing plan mode as license to research and `ExitPlanMode` ad hoc. While in plan mode:

- Read the task description/ticket carefully; extract explicit and implicit acceptance criteria.
- Explore the relevant parts of the codebase (use `Explore`/`Grep`/`Read` as needed) to understand
  existing patterns, the files that will need to change, and any conventions already in place.
- Note existing test setup: test runner, test file locations/naming, how to run a single test vs
  the whole suite.

Do not exit plan mode until Steps 2–3 below are done.

---

## Step 2: Decide whether TDD applies

Ask: can this task be driven by a test that fails first and then is made to pass?

TDD fits when:
- The change has a clear, checkable behavior (bug fix with a reproducible failure, new function/
  endpoint/component with defined inputs/outputs, a refactor with existing behavior to pin down).
- A test can be written before the implementation exists and meaningfully fails for the right reason.

TDD does not fit well (skip it, note why in the plan) when:
- Pure exploration/spike work, one-off scripts, config/infra-only changes, docs, or changes where
  correctness is primarily visual/subjective (e.g. some UI styling) and not easily unit-tested.
- No test infrastructure exists for the area and adding one is out of scope for this task.

Record the decision explicitly in the plan (Step 3): "TDD: yes — will write failing test(s) for X
before implementing" or "TDD: no — because Y; will rely on manual verification / existing tests".

If TDD applies, the implementation step (Step 5) must follow red → green → refactor: write the
failing test(s) first, confirm they fail for the expected reason, then implement until they pass.

---

## Step 3: Draft the plan and get it confirmed

Still in plan mode, invoke the `plan-creator` skill to write the deterministic implementation plan to `docs/plans/`. Do not compose the plan freehand as the `plan` argument to `ExitPlanMode` and skip `plan-creator` — even under time pressure or a casually-phrased request ("plan then implement it"), the plan must come from `plan-creator`'s template so it lands in `docs/plans/` rather than only in Claude Code's own ephemeral plan-mode storage.

Ensure the created plan covers:
1. Summary of the task and acceptance criteria.
2. TDD decision and initial test cases.
3. Ordered concrete steps (files to touch, function changes, migrations).
4. Verification plan (automated tests, manual checks, CLI commands).
5. Explicit out-of-scope items, questions, and assumptions.

Call `ExitPlanMode` to present this plan to the user (the task author) for confirmation. Do not
proceed past this point without their approval — if they request changes, revise the plan and
exit plan mode again.

---

## Step 4: Persist the confirmed plan

Once approved, save the plan as Markdown in the project at:

```
docs/plans/<full-branch-name>.md
```

- The filename must match the full branch name (e.g. `EPMCDMPTST-1947-Fix-Read-Performance.md`).
  This ensures the plan is traceable back to the specific feature branch.
- Create the `docs/plans/` directory if it doesn't exist.
- The file content is the confirmed plan from Step 3 (summary, TDD decision, steps, verification
  plan, open questions), so it can be referenced later or by other agents/reviewers.

---

## Step 5: Execute the plan (auto mode)

Work through the plan's steps in order without pausing for confirmation on routine sub-decisions —
use auto-mode judgment per the session's standing instructions, and only stop if genuinely blocked.

- If TDD applies: for each unit of behavior, write the test first, run it and confirm it fails for
  the expected reason, then write the minimum implementation to make it pass, then refactor if
  needed while keeping tests green.
- If TDD does not apply: implement the steps directly, per the plan.
- Use `TaskCreate`/`TaskUpdate` to track the plan's steps as todos if the plan has 3+ discrete steps,
  marking each complete as you finish it.
- Keep changes scoped to what the plan and task require — no speculative extras.

---

## Step 6: Self-check against the task

Before running tests, re-read the original task description and the plan from Step 4, then verify:

- Every acceptance criterion is actually addressed by the changes made.
- No plan step was silently skipped or partially done.
- The diff doesn't contain unrelated changes, leftover debug code, or TODOs that should be resolved.

If anything is missing or wrong, fix it now and re-run this self-check before moving on.

---

## Step 7: Run tests and fix failures

This project uses **Podman**, not Docker, for containerized builds/tests — `docker`/
`docker-compose` may not be usable in this environment (daemon not running, socket absent). If
`docker-compose` fails or the Docker daemon is unreachable, use `podman`/`podman-compose` instead
(e.g. `podman machine start` if the machine isn't running, then `podman build`/
`podman run ... dotnet test ...`, setting `DOCKER_HOST` to the podman socket if a tool insists on
the Docker API). Don't conclude tests can't be run just because `docker` is unavailable.

---

## Step 8: Independent code review

Once tests pass and the self-check is clean, launch a code-review pass with a **clean context**,
using the project's `mr-review` skill. Full procedure, including why the bundled `/code-review`
skill and `code-reviewer` agent type can't be used here: see
`references/code-review.md`.

If `mr-comments.md` has blocking findings, fix them and re-run Steps 6–8. Non-blocking/nit
findings don't gate completion.

---

## Step 9: Check whether documentation needs updating

## Step 10: Write the MR description

Full procedure for both steps: see `references/docs-and-mr.md`.

---

## Step 11: Publish the description to the real GitLab MR

If the branch has been pushed and the user wants a real MR created/updated (not just the local
description file from Step 10), push the description via `glab`. Full procedure: see
`references/docs-and-mr.md`.

---

## Notes

- Never skip Step 3's confirmation — even under auto-mode, plan approval is the one gate that must
  come from the human author before implementation starts.
- Steps 6 and 7 form a loop, not a sequence — don't report the task done while either could still fail.
- Step 8 must run in a context that did not participate in planning/implementation, so it can catch
  what the implementer would rationalize away.
- Step 10 only runs after Step 8 is clean of blocking findings, and after Step 9's documentation
  check — don't write the MR description for a change that's still expected to churn.
- Step 11 is push-affecting (creates/updates a real MR visible to others) — per the session's
  standing risk guidance, confirm with the user before running it unless they already asked for
  the MR to be created/updated in this turn.



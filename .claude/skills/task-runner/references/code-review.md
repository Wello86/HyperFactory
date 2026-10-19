# Step 8: Independent code review

Once tests pass and the self-check is clean, launch a code-review pass with a **clean context**
(it must not inherit this conversation's assumptions), using the project's `mr-review` skill.

**Do not use the bundled `/code-review` skill or a `code-reviewer` agent type for this** — in this
project `code-review` is registered with `disable-model-invocation` (only callable by the user
typing `/code-review`; `Skill({skill: "code-review"})` from an agent errors out), and a
`code-reviewer` agent type is not a registered agent type here (`Agent` with
`subagent_type: "code-reviewer"` fails with "agent type not found"). `mr-review` is this project's
purpose-built review skill and does not have `disable-model-invocation`, so it is callable
programmatically.

Procedure:

- Use the `Agent` tool with a fresh `general-purpose` subagent (clean context — it must not see
  this conversation) and instruct it to call `Skill({skill: "mr-review"})` and follow that skill's
  procedure in full against the current branch vs `main`.
- Give the agent what `mr-review` needs to do its job, since it starts with no memory of this
  conversation: the task description / acceptance criteria (or the path to the persisted plan at
  `docs/plans/<task-id>.md`, which contains them), and confirmation that the base branch is `main`.
  Do not paste your own analysis or pre-judge the findings.
- Pass `run_in_background: false` explicitly on this `Agent` call. Its findings gate completion, so
  the call must block and return the result directly as this step's tool output.
- `mr-review` writes its findings to `mr-comments.md` in the repo root (per its own contract, not
  committed). Have the subagent report the contents of that file back as its final output, since
  you won't otherwise see a file it wrote from inside the subagent's own working context.

After the review agent reports:
- If `mr-comments.md` contains any `**Blocking.**` findings, fix them, then re-run Steps 6–7
  (self-check + tests), and re-run this review step once more on the updated diff.
- `**Non-blocking.**` and `**Nit.**` findings don't gate completion — surface them to the user but
  don't block on them unless they ask you to address them.
- If it finds nothing blocking, summarize the review outcome to the user and consider the task done.

Step 8 must run in a context that did not participate in planning/implementation, so it can catch
what the implementer would rationalize away.

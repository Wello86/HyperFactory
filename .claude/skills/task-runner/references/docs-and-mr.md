# Step 9: Check whether documentation needs updating

Before writing the MR description, check whether the change makes any existing documentation
stale — `CLAUDE.md`, `README.md`, and files under `docs/` (e.g. `docs/architecture.md`,
`docs/cross-cutting-notes.md`, `docs/testing-ci.md`, `erd.md`) are the ones known to exist in this
repo, but check for any others that describe what changed.

- Compare what changed against what these files claim: new/removed commands, changed gotchas or
  architectural decisions (e.g. DbContext lifetime, migrations strategy, CORS policy), new
  entities/relationships, new test conventions, changed ports/URLs.
- If a file's claim is now wrong or incomplete because of this change, update it as part of this
  task — documentation drift is a real regression, not a nice-to-have.
- If nothing changed, say so explicitly rather than skipping the check silently.

---

# Step 10: Write the MR description

Once Step 8 has no remaining blocking findings, write an English-language description of the
change suitable for pasting directly into a GitLab merge request, and save it as:

```
docs/plans/<same-task-id-or-slug-as-step-4>-mr.md
```

- Base it on the actual diff and the confirmed plan from Step 4, not on intent — describe what was
  done, not what was planned, if the two diverged.
- Cover: a short summary of the change and why, what was added/changed/removed, any documentation
  updated per Step 9, how it was verified (tests run, manual checks), and any follow-ups or known
  limitations called out as non-blocking in Step 8.
- Write it in English regardless of the language used elsewhere in the conversation, since it is
  meant to be copied into GitLab as-is.
- Do not commit this file — like `mr-comments.md`, it is a review/process artifact, not part of
  the change itself. It should still be left on disk in the repo (under `docs/plans/`, ignored by
  git if needed) for the author to copy from — do not delete it automatically after writing or
  presenting it.

Step 10 only runs after Step 8 is clean of blocking findings, and after Step 9's documentation
check — don't write the MR description for a change that's still expected to churn.

---

# Step 11: Publish the description to the real GitLab MR

Only run this step if the branch is pushed and the user wants the description to actually land in
GitLab (not just sit in the local `*-mr.md` file from Step 10). This is push-affecting — confirm
with the user first unless they've already asked for it this turn (e.g. "push it and open the MR",
"update the MR description").

Uses the `glab` CLI (GitLab's equivalent of `gh`).

1. **Check `glab` is installed and authenticated**:
   ```bash
   glab --version || brew install glab
   glab auth status || glab auth login --hostname <gitlab-host>
   ```
   If auth isn't set up, **never ask the user to paste a personal access token into the chat** —
   the token then lives in plaintext in the conversation/session logs, effectively exposed. Ask
   them to run the login themselves, out of band, via `!` in the session (e.g.
   `! glab auth login --hostname <gitlab-host>`), which lets them enter the token into `glab`'s own
   interactive prompt without it ever appearing in the transcript. Don't try to source a token from
   files either.
   - If a token was already pasted into the chat in this or a prior turn (by the user or by you
     echoing a command that embedded it), tell the user explicitly to revoke/rotate it in GitLab
     (Settings → Access Tokens) once you're done — treat it as compromised regardless of scope.
   - If `--stdin`-based non-interactive login misbehaves (e.g. the literal string `--stdin` ends up
     stored as the token instead of the piped value — a known glab parsing quirk in some
     environments), do not fall back to passing the token as a `--token=<value>` command-line flag
     if it can be avoided — flag values can land in shell history and process listings. Prefer
     retrying the interactive `!`-prompted login first; only use `--token=<value>` as a last resort
     and tell the user to rotate that token afterward too.

2. **Check whether an MR already exists for this branch**:
   ```bash
   glab mr view --output json 2>/dev/null
   ```
   - If it returns an MR (exit 0, JSON with an `iid`), one already exists — update it (next step).
   - If it errors ("no open merge request"), none exists yet — create it (step after).

3. **Update an existing MR's description**:
   ```bash
   glab mr update <iid> --description-file docs/plans/<slug>-mr.md
   ```

4. **Create a new MR from the current branch**, using the Step 10 file as the description and the
   first line/heading of that file (or the branch's ticket ID + short title) as the title:
   ```bash
   glab mr create --fill-commit-body=false \
     --title "<TICKET-ID>: <short title>" \
     --description-file docs/plans/<slug>-mr.md \
     --target-branch main
   ```
   Add `--draft` only if the user asked for a draft MR. Do not set `--yes`/auto-merge flags.

5. Report back the MR URL `glab` prints — don't guess or construct it yourself.

If `glab` isn't available and the user doesn't want to install it, fall back to what was already
happening before this step existed: hand them the local `*-mr.md` file's path and content to paste
into GitLab manually.

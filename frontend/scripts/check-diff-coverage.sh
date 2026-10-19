#!/bin/sh
set -e
# Requires: pip install diff-cover (installed in CI job, see .gitlab-ci.yml frontend:test)
# Use --unshallow when the local clone is shallow so the fetch reliably reaches
# a common ancestor with origin/main, instead of guessing a fixed depth.
if [ -f "$(git rev-parse --git-dir)/shallow" ]; then
  git fetch origin main --unshallow
else
  git fetch origin main
fi
BASE=$(git merge-base origin/main HEAD)
diff-cover coverage/lcov.info \
  --compare-branch="$BASE" \
  --fail-under=100

#!/usr/bin/env bash
# Evaluate the version gate for one pull request and exit non-zero when it fails, so that
# the calling job's check run is the gate result itself. No commit status is published:
# a single check run keeps exactly one gate entry on the pull request, and the tag driven
# refresh workflow re-runs this job in place rather than writing a second signal.
#
# The gate is evaluated on refs/pull/<n>/merge, so a pull request that does not touch
# inventory/group_vars/all.yml automatically inherits the base branch version instead of
# being blocked for being out of date.
#
# This script only reads pull request content as data. It never checks out or executes
# code from the pull request, which is what makes it safe to run from pull_request_target.
#
# Usage: evaluate_version_gate.sh <pr-number> [base-branch]
# Requires: git and python3, run from a checkout of the base branch.
#
# NOTE: this script's behavior is documented in
# documentation/developer-docs/github/version-gate-workflow.md - please keep that doc in
# sync when changing it.

set -euo pipefail

pr_number="$1"
base_branch="${2:-develop}"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

# GitHub computes the merge ref asynchronously and omits it entirely for conflicting
# pull requests, so retry a few times before giving up.
merge_ref_available=false
for attempt in 1 2 3; do
    if git fetch --quiet --no-tags --depth=1 origin "+refs/pull/${pr_number}/merge:refs/fwo/pr-merge"; then
        merge_ref_available=true
        break
    fi
    echo "Merge ref for PR #${pr_number} not available yet (attempt ${attempt})."
    sleep $((attempt * 10))
done

if [[ "$merge_ref_available" != "true" ]]; then
    echo "Cannot determine the merged product version. Resolve the merge conflicts first." >&2
    exit 1
fi

git fetch --quiet --no-tags --depth=1 origin "+refs/heads/${base_branch}:refs/fwo/base"

git show "refs/fwo/pr-merge:inventory/group_vars/all.yml" >"${work_dir}/merged-all.yml"
git show "refs/fwo/pr-merge:documentation/revision-history.md" >"${work_dir}/revision-history.md"
git show "refs/fwo/base:inventory/group_vars/all.yml" >"${work_dir}/base-all.yml"

# Only the tag names matter, so list them on the remote instead of fetching tag objects.
# They are read here, at run time, which is what makes a re-run pick up a new sealing tag.
git ls-remote --tags origin | sed 's#.*refs/tags/##; s#\^{}$##' | sort -u >"${work_dir}/tags.txt"

verdict_file="${work_dir}/verdict.json"
gate_exit=0
python3 scripts/ci/version_gate.py gate \
    --merged-file "${work_dir}/merged-all.yml" \
    --base-file "${work_dir}/base-all.yml" \
    --revision-history "${work_dir}/revision-history.md" \
    --tags-file "${work_dir}/tags.txt" >"$verdict_file" || gate_exit=$?

reason="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["reason"])' "$verdict_file")"

if [[ "$gate_exit" -eq 0 ]]; then
    echo "Version gate passed: ${reason}"
else
    echo "Version gate failed: ${reason}" >&2
fi

exit "$gate_exit"

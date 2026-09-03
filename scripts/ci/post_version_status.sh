#!/usr/bin/env bash
# Evaluate the version gate for one pull request and publish the result as the
# "fwo/version-gate" commit status on the pull request head commit.
#
# The gate is evaluated on refs/pull/<n>/merge, so a pull request that does not touch
# inventory/group_vars/all.yml automatically inherits the base branch version instead of
# being blocked for being out of date.
#
# This script only reads pull request content as data. It never checks out or executes
# code from the pull request, which is what makes it safe to run from pull_request_target.
#
# Usage: post_version_status.sh <pr-number> <head-sha> [base-branch]
# Requires: git, gh, python3 and GH_TOKEN in the environment.
#
# NOTE: this script's behavior is documented in
# documentation/developer-docs/github/version-gate-workflow.md - please keep that doc in
# sync when changing it.

set -euo pipefail

pr_number="$1"
head_sha="$2"
base_branch="${3:-develop}"

status_context="fwo/version-gate"
target_url="${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

post_status() {
    local state="$1"
    local description="$2"
    echo "PR #${pr_number} (${head_sha}): ${state} - ${description}"
    gh api -X POST "repos/${GITHUB_REPOSITORY}/statuses/${head_sha}" \
        -f state="$state" \
        -f context="$status_context" \
        -f description="$description" \
        -f target_url="$target_url" >/dev/null
}

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
    post_status "failure" "Cannot determine the merged product version, resolve the merge conflicts first."
    exit 1
fi

git fetch --quiet --no-tags --depth=1 origin "+refs/heads/${base_branch}:refs/fwo/base"

git show "refs/fwo/pr-merge:inventory/group_vars/all.yml" >"${work_dir}/merged-all.yml"
git show "refs/fwo/pr-merge:documentation/revision-history.md" >"${work_dir}/revision-history.md"
git show "refs/fwo/base:inventory/group_vars/all.yml" >"${work_dir}/base-all.yml"

# Only the tag names matter, so list them on the remote instead of fetching tag objects.
git ls-remote --tags origin | sed 's#.*refs/tags/##; s#\^{}$##' | sort -u >"${work_dir}/tags.txt"

verdict_file="${work_dir}/verdict.json"
gate_exit=0
python3 scripts/ci/version_gate.py gate \
    --merged-file "${work_dir}/merged-all.yml" \
    --base-file "${work_dir}/base-all.yml" \
    --revision-history "${work_dir}/revision-history.md" \
    --tags-file "${work_dir}/tags.txt" >"$verdict_file" || gate_exit=$?

description="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["description"])' "$verdict_file")"

if [[ "$gate_exit" -eq 0 ]]; then
    post_status "success" "$description"
else
    post_status "failure" "$description"
fi

exit "$gate_exit"

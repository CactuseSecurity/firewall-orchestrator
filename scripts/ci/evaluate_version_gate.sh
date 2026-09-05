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
# Requires: git, gh and python3, run from a checkout of the base branch with GH_TOKEN and
# GH_REPO set for read-only pull request access. PR_AUTHOR, PR_HEAD_REF and PR_HEAD_REPOSITORY
# carry the trusted pull_request_target event metadata used for automation exemptions.
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
    if [[ "$attempt" -lt 3 ]]; then
        sleep $((attempt * 10))
    fi
done

if [[ "$merge_ref_available" != "true" ]]; then
    if ! mergeable_state="$(gh pr view "$pr_number" --json mergeable --jq .mergeable)"; then
        echo "Cannot determine the merged product version or query pull request mergeability." >&2
        echo "Re-run the workflow after checking GitHub connectivity and pull request status." >&2
    elif [[ "$mergeable_state" == "CONFLICTING" ]]; then
        echo "Cannot determine the merged product version. GitHub reports merge conflicts." >&2
        echo "Resolve the merge conflicts and update the pull request." >&2
    else
        echo "Cannot determine the merged product version. GitHub reports mergeability as '${mergeable_state}'." >&2
        echo "The merge ref may still be computing or its fetch may have failed; re-run the workflow." >&2
    fi
    exit 1
fi

git fetch --quiet --no-tags --depth=1 origin "+refs/heads/${base_branch}:refs/fwo/base"

git show "refs/fwo/pr-merge:inventory/group_vars/all.yml" >"${work_dir}/merged-all.yml"
git show "refs/fwo/pr-merge:documentation/revision-history.md" >"${work_dir}/revision-history.md"
git show "refs/fwo/base:inventory/group_vars/all.yml" >"${work_dir}/base-all.yml"
git show "refs/fwo/base:documentation/revision-history.md" >"${work_dir}/base-revision-history.md"

changed_paths="$(git diff --name-only refs/fwo/base refs/fwo/pr-merge)"
pr_author="${PR_AUTHOR:-}"
pr_head_ref="${PR_HEAD_REF:-}"
pr_head_repository="${PR_HEAD_REPOSITORY:-}"
revision_history_arguments=()

if [[ "$pr_head_repository" == "$GH_REPO" && "$pr_author" == "dependabot[bot]" && "$pr_head_ref" == dependabot/* ]]; then
    revision_history_arguments+=(--skip-revision-history)
elif [[ "$pr_head_repository" == "$GH_REPO" && "$changed_paths" == ".agents" ]]; then
    if [[ "$pr_author" == "CactusAutomation" && "$pr_head_ref" == "automation/submodule_update" ]] || \
        [[ "$pr_author" == "github-actions[bot]" && "$pr_head_ref" == "bot/update-agents-submodule" ]]; then
        revision_history_arguments+=(--skip-revision-history)
    fi
fi

# Only the tag names matter, so list them on the remote instead of fetching tag objects.
# They are read here, at run time, which is what makes a re-run pick up a new sealing tag.
git ls-remote --tags origin | sed 's#.*refs/tags/##; s#\^{}$##' | sort -u >"${work_dir}/tags.txt"

verdict_file="${work_dir}/verdict.json"
gate_exit=0
python3 scripts/ci/version_gate.py gate \
    --merged-file "${work_dir}/merged-all.yml" \
    --base-file "${work_dir}/base-all.yml" \
    --revision-history "${work_dir}/revision-history.md" \
    --base-revision-history "${work_dir}/base-revision-history.md" \
    --tags-file "${work_dir}/tags.txt" \
    "${revision_history_arguments[@]}" >"$verdict_file" || gate_exit=$?

reason="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["reason"])' "$verdict_file")"

if [[ "$gate_exit" -eq 0 ]]; then
    echo "Version gate passed: ${reason}"
else
    echo "Version gate failed: ${reason}" >&2
fi

exit "$gate_exit"

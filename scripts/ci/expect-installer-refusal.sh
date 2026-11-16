#!/usr/bin/env bash
# Runs the installer and succeeds only if it is refused for the expected reason: the run
# has to fail, and its output has to contain the expected refusal text. A run that fails
# for any other reason does not count, so the negative tests in test-install.yml cannot
# pass because of an unrelated error that happens to stop the run first.
#
# Usage: expect-installer-refusal.sh <expected refusal text> <run-playbook-with-sudo.sh arguments...>
# The output of the refused run is always printed, so the CI log shows what was refused.
#
# NOTE: used by .github/workflows/test-install.yml, documented in
# documentation/developer-docs/github/test-install-workflow.md - please keep that doc in
# sync when changing it.
set -euo pipefail

if [[ "$#" -lt 2 ]]; then
    echo "Usage: $0 <expected refusal text> <run-playbook-with-sudo.sh arguments...>" >&2
    exit 2
fi

expected_refusal="$1"
shift

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
log_file="$(mktemp)"
trap 'rm -f "$log_file"' EXIT

run_status=0
"$repo_dir/scripts/run-playbook-with-sudo.sh" "$@" >"$log_file" 2>&1 || run_status=$?
cat "$log_file"

if [[ "$run_status" -eq 0 ]]; then
    echo "The installer run succeeded, but it should have been refused with: $expected_refusal" >&2
    exit 1
fi

if ! grep -qF -- "$expected_refusal" "$log_file"; then
    echo "The installer run failed (exit $run_status), but not with the expected refusal: $expected_refusal" >&2
    exit 1
fi

echo "Refused as expected: $expected_refusal"

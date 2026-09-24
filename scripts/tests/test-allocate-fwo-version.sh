#!/usr/bin/env bash
set -euo pipefail

readonly kRepositoryRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly kTemporaryDirectory="$(mktemp -d)"
readonly kAllocationDate="24.09.2026"

cleanup() {
    rm -rf "$kTemporaryDirectory"
}
trap cleanup EXIT

# Creates a fresh repository layout whose revision history contains the given placeholder heading.
prepare_case() {
    local case_directory="$1"
    local placeholder_heading="$2"

    mkdir -p "$case_directory/inventory/group_vars" \
        "$case_directory/documentation" \
        "$case_directory/roles/database/files/upgrade"
    cp "$kRepositoryRoot/scripts/allocate-fwo-version.sh" "$case_directory/"
    printf '%s\n' 'product_version: "999.0.0"' > "$case_directory/inventory/group_vars/all.yml"
    printf '%s\n' '# Revision history' '## 9.5.4 - 23.09.2026' "$placeholder_heading" 'placeholder notes' \
        > "$case_directory/documentation/revision-history.md"
    printf '%s\n' 'SELECT 1;' > "$case_directory/roles/database/files/upgrade/999.0.0.sql"
}

# Allocates 9.5.6 for the given placeholder heading and asserts the resulting heading.
assert_allocated_heading() {
    local case_name="$1"
    local placeholder_heading="$2"
    local expected_heading="$3"
    local case_directory="$kTemporaryDirectory/$case_name"

    prepare_case "$case_directory" "$placeholder_heading"
    (
        cd "$case_directory"
        FWO_ALLOCATION_DATE="$kAllocationDate" ./allocate-fwo-version.sh --allocate --target 9.5.6 --base-version 9.5.4
        ./allocate-fwo-version.sh --check --base-version 9.5.4
    )

    grep -qx 'product_version: "9.5.6"' "$case_directory/inventory/group_vars/all.yml"
    [[ -f "$case_directory/roles/database/files/upgrade/9.5.6.sql" ]]
    [[ ! -e "$case_directory/roles/database/files/upgrade/999.0.0.sql" ]]
    grep -qx "$expected_heading" "$case_directory/documentation/revision-history.md"
    grep -qx 'placeholder notes' "$case_directory/documentation/revision-history.md"
}

[[ "$(printf '%s\n' 9.5.4 999.0.0 | sort -V | tail -n 1)" == "999.0.0" ]]

assert_allocated_heading dated-with-suffix '## 999.0.0 - 01.01.1970 MAIN' "## 9.5.6 - $kAllocationDate MAIN"
assert_allocated_heading dated '## 999.0.0 - 01.01.1970' "## 9.5.6 - $kAllocationDate"
assert_allocated_heading without-date '## 999.0.0' "## 9.5.6 - $kAllocationDate"
assert_allocated_heading without-date-with-suffix '## 999.0.0 MAIN' "## 9.5.6 - $kAllocationDate MAIN"
assert_allocated_heading dash-without-date '## 999.0.0 - MAIN' "## 9.5.6 - $kAllocationDate MAIN"

# Runs a command in a case directory and fails the test if the command succeeds.
expect_failure() {
    local case_directory="$1"
    local description="$2"
    shift 2

    if (cd "$case_directory" && "$@" 2> /dev/null); then
        echo "Expected failure: $description" >&2
        exit 1
    fi
}

readonly kCaseDirectory="$kTemporaryDirectory/dated-with-suffix"

# An allocated version above develop is final.
expect_failure "$kCaseDirectory" "re-allocation of a current version" \
    ./allocate-fwo-version.sh --allocate --target 9.5.7 --base-version 9.5.4

# After develop moved to a new release line, a stale version is re-allocated.
(
    cd "$kCaseDirectory"
    FWO_ALLOCATION_DATE="$kAllocationDate" ./allocate-fwo-version.sh --allocate --target 9.6.1 --base-version 9.6.0
    ./allocate-fwo-version.sh --check --base-version 9.6.0
)
grep -qx 'product_version: "9.6.1"' "$kCaseDirectory/inventory/group_vars/all.yml"
[[ -f "$kCaseDirectory/roles/database/files/upgrade/9.6.1.sql" ]]
[[ ! -e "$kCaseDirectory/roles/database/files/upgrade/9.5.6.sql" ]]
grep -qx "## 9.6.1 - $kAllocationDate MAIN" "$kCaseDirectory/documentation/revision-history.md"

# The target must be above develop.
expect_failure "$kTemporaryDirectory/dated" "target below base version" \
    ./allocate-fwo-version.sh --allocate --target 9.5.3 --base-version 9.5.6

# The next minor and major lines may start; skipping a line fails.
(cd "$kCaseDirectory" && ./allocate-fwo-version.sh --check --base-version 9.5.4)
(
    cd "$kTemporaryDirectory/dated"
    FWO_ALLOCATION_DATE="$kAllocationDate" ./allocate-fwo-version.sh --allocate --target 10.0.0 --base-version 9.5.6
    ./allocate-fwo-version.sh --check --base-version 9.5.6
)
expect_failure "$kCaseDirectory" "release line skipping a minor version" \
    ./allocate-fwo-version.sh --check --base-version 9.4.4

# The placeholder never passes validation.
prepare_case "$kTemporaryDirectory/placeholder" '## 999.0.0'
expect_failure "$kTemporaryDirectory/placeholder" "unallocated placeholder" \
    ./allocate-fwo-version.sh --check --base-version 9.5.4

echo "All allocate-fwo-version tests passed."

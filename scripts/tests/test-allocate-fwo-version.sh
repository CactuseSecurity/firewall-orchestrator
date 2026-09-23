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
        FWO_ALLOCATION_DATE="$kAllocationDate" ./allocate-fwo-version.sh --allocate --target 9.5.6
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

readonly kMismatchDirectory="$kTemporaryDirectory/dated-with-suffix"
sed -i 's/9\.5\.6/9.6.0/g' "$kMismatchDirectory/inventory/group_vars/all.yml" \
    "$kMismatchDirectory/documentation/revision-history.md"
mv "$kMismatchDirectory/roles/database/files/upgrade/9.5.6.sql" \
    "$kMismatchDirectory/roles/database/files/upgrade/9.6.0.sql"
if (
    cd "$kMismatchDirectory"
    ./allocate-fwo-version.sh --check --base-version 9.5.4
); then
    echo "Expected a release-line mismatch to fail validation." >&2
    exit 1
fi

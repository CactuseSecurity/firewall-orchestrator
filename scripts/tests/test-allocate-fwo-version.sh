#!/usr/bin/env bash
set -euo pipefail

readonly kRepositoryRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly kTemporaryDirectory="$(mktemp -d)"

cleanup() {
    rm -rf "$kTemporaryDirectory"
}
trap cleanup EXIT

mkdir -p "$kTemporaryDirectory/inventory/group_vars" \
    "$kTemporaryDirectory/documentation" \
    "$kTemporaryDirectory/roles/database/files/upgrade"
cp "$kRepositoryRoot/scripts/allocate-fwo-version.sh" "$kTemporaryDirectory/"

printf '%s\n' 'product_version: "999.0.0"' > "$kTemporaryDirectory/inventory/group_vars/all.yml"
printf '%s\n' '# Revision history' '## 999.0.0 - 01.01.1970 MAIN' > "$kTemporaryDirectory/documentation/revision-history.md"
printf '%s\n' 'SELECT 1;' > "$kTemporaryDirectory/roles/database/files/upgrade/999.0.0.sql"

test "$(printf '%s\n' 9.5.4 999.0.0 | sort -V | tail -n 1)" = "999.0.0"

(
    cd "$kTemporaryDirectory"
    FWO_ALLOCATION_DATE=24.09.2026 ./allocate-fwo-version.sh --allocate --target 9.5.6
    ./allocate-fwo-version.sh --check --base-version 9.5.4
)

grep -qx 'product_version: "9.5.6"' "$kTemporaryDirectory/inventory/group_vars/all.yml"
test -f "$kTemporaryDirectory/roles/database/files/upgrade/9.5.6.sql"
test ! -e "$kTemporaryDirectory/roles/database/files/upgrade/999.0.0.sql"
grep -qx '## 9.5.6 - 24.09.2026 MAIN' "$kTemporaryDirectory/documentation/revision-history.md"

sed -i 's/9\.5\.6/9.6.0/g' "$kTemporaryDirectory/inventory/group_vars/all.yml" \
    "$kTemporaryDirectory/documentation/revision-history.md"
mv "$kTemporaryDirectory/roles/database/files/upgrade/9.5.6.sql" \
    "$kTemporaryDirectory/roles/database/files/upgrade/9.6.0.sql"
if (
    cd "$kTemporaryDirectory"
    ./allocate-fwo-version.sh --check --base-version 9.5.4
); then
    echo "Expected a release-line mismatch to fail validation." >&2
    exit 1
fi

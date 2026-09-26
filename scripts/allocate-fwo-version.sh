#!/usr/bin/env bash
# Reserve or validate the versioned files in a FWO pull request.
set -euo pipefail

readonly kProductVersionFile="inventory/group_vars/all.yml"
readonly kRevisionHistoryFile="documentation/revision-history.md"
readonly kUpgradeDirectory="roles/database/files/upgrade"
# This sorts after every released FWO version, so installer upgrade tests execute
# a candidate migration after all existing migrations before it is allocated.
readonly kPlaceholderVersion="999.0.0"
readonly kSourceHeadingPattern="( |$)"
readonly kAllocatedHeadingPattern=" - [0-9]{2}\\.[0-9]{2}\\.[0-9]{4}( |$)"

usage() {
    echo "Usage: $0 --allocate --target VERSION --base-version VERSION | --check --base-version VERSION" >&2
    exit 2
}

version_is_valid() {
    local version="$1"

    [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
}

version_is_greater_than() {
    local left_version="$1"
    local right_version="$2"

    [[ "$(printf '%s\n%s\n' "$left_version" "$right_version" | sort -V | tail -n 1)" == "$left_version" && "$left_version" != "$right_version" ]]
}

# Accepts the base release line and the start of the next minor or major line.
version_has_allowed_release_line() {
    local version="$1"
    local base_version="$2"
    local major minor base_major base_minor

    IFS=. read -r major minor _ <<< "$version"
    IFS=. read -r base_major base_minor _ <<< "$base_version"
    [[ "$major.$minor" == "$base_major.$base_minor" ||
        "$major.$minor" == "$base_major.$((base_minor + 1))" ||
        "$major.$minor" == "$((base_major + 1)).0" ]]
}

read_allocation_date() {
    local allocation_date

    allocation_date="${FWO_ALLOCATION_DATE:-$(TZ=Europe/Berlin date +%d.%m.%Y)}"
    if [[ ! "$allocation_date" =~ ^[0-9]{2}\.[0-9]{2}\.[0-9]{4}$ ]]; then
        echo "Allocation date must use DD.MM.YYYY format." >&2
        exit 1
    fi

    printf '%s\n' "$allocation_date"
}

read_product_version() {
    local version

    version="$(sed -nE 's/^product_version:[[:space:]]*"?([^"[:space:]]+)"?[[:space:]]*$/\1/p' "$kProductVersionFile")"
    if [[ "$(printf '%s\n' "$version" | sed '/^$/d' | wc -l)" -ne 1 ]] || ! version_is_valid "$version"; then
        echo "Expected exactly one semantic product_version in $kProductVersionFile." >&2
        exit 1
    fi

    printf '%s\n' "$version"
}

require_versioned_files() {
    local version="$1"
    local heading_pattern="$2"
    local upgrade_file="$kUpgradeDirectory/$version.sql"
    local history_entries

    if [[ ! -f "$upgrade_file" ]]; then
        echo "Missing upgrade script $upgrade_file." >&2
        exit 1
    fi

    history_entries="$(grep -Ec "^## ${version//./\\.}$heading_pattern" "$kRevisionHistoryFile" || true)"
    if [[ "$history_entries" -ne 1 ]]; then
        echo "Expected exactly one revision-history heading for $version." >&2
        exit 1
    fi
}

# Rewrites the source heading to "## TARGET - DATE", accepting it with or
# without a date and keeping any suffix such as MAIN.
replace_version_heading() {
    local source_version="$1"
    local target_version="$2"
    local allocation_date="$3"
    local heading
    local line_number
    local suffix

    heading="$(grep -nE "^## ${source_version//./\\.}$kSourceHeadingPattern" "$kRevisionHistoryFile")"
    line_number="${heading%%:*}"
    suffix="${heading#*:"## $source_version"}"
    suffix="${suffix# -}"
    if [[ "$suffix" =~ ^\ [0-9]{2}\.[0-9]{2}\.[0-9]{4}(.*)$ ]]; then
        suffix="${BASH_REMATCH[1]}"
    fi
    suffix="${suffix%"${suffix##*[![:space:]]}"}"

    FWO_HEADING="## $target_version - $allocation_date$suffix" \
        awk -v line_number="$line_number" 'NR == line_number { $0 = ENVIRON["FWO_HEADING"] } { print }' \
        "$kRevisionHistoryFile" > "$kRevisionHistoryFile.tmp"
    mv "$kRevisionHistoryFile.tmp" "$kRevisionHistoryFile"
}

mode="${1:-}"
case "$mode" in
    --allocate)
        [[ "$#" -eq 5 && "$2" == "--target" && "$4" == "--base-version" ]] || usage
        target_version="$3"
        base_version="$5"
        version_is_valid "$target_version" && version_is_valid "$base_version" || usage
        [[ "$target_version" != "$kPlaceholderVersion" ]] || usage

        # A stale version (not above develop, e.g. after a release-line change)
        # may be re-allocated; any other allocated version is final.
        source_version="$(read_product_version)"
        if [[ "$source_version" != "$kPlaceholderVersion" ]] &&
            version_is_greater_than "$source_version" "$base_version"; then
            echo "Allocation requires the $kPlaceholderVersion placeholder or a stale version, found $source_version." >&2
            exit 1
        fi
        version_is_greater_than "$target_version" "$base_version" || {
            echo "Target version $target_version must be greater than base version $base_version." >&2
            exit 1
        }
        require_versioned_files "$source_version" "$kSourceHeadingPattern"
        allocation_date="$(read_allocation_date)"

        sed -i "s/^product_version:.*/product_version: \"$target_version\"/" "$kProductVersionFile"
        mv "$kUpgradeDirectory/$source_version.sql" "$kUpgradeDirectory/$target_version.sql"
        replace_version_heading "$source_version" "$target_version" "$allocation_date"
        echo "Allocated FWO version $target_version."
        ;;
    --check)
        [[ "$#" -eq 3 && "$2" == "--base-version" ]] || usage
        base_version="$3"
        version_is_valid "$base_version" || usage

        product_version="$(read_product_version)"
        if [[ "$product_version" == "$kPlaceholderVersion" ]]; then
            echo "Version placeholder has not been allocated. Ask a maintainer to comment /allocate-fwo-version." >&2
            exit 1
        fi
        version_is_greater_than "$product_version" "$base_version" || {
            echo "PR version $product_version must be greater than base version $base_version." >&2
            exit 1
        }
        version_has_allowed_release_line "$product_version" "$base_version" || {
            echo "PR version $product_version must use the release line of $base_version or start the next minor or major line." >&2
            exit 1
        }
        require_versioned_files "$product_version" "$kAllocatedHeadingPattern"
        echo "Versioned files consistently reserve $product_version."
        ;;
    *) usage ;;
esac

#!/usr/bin/env bash
# Reserve or validate the versioned files in a FWO pull request.
set -euo pipefail

readonly kProductVersionFile="inventory/group_vars/all.yml"
readonly kRevisionHistoryFile="documentation/revision-history.md"
readonly kUpgradeDirectory="roles/database/files/upgrade"
# This sorts after every released FWO version, so installer upgrade tests execute
# a candidate migration after all existing migrations before it is allocated.
readonly kPlaceholderVersion="999.0.0"

usage() {
    echo "Usage: $0 --allocate --target VERSION | --check --base-version VERSION" >&2
    exit 2
}

version_is_valid() {
    [[ "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
}

version_is_greater_than() {
    local left_version="$1"
    local right_version="$2"

    [[ "$(printf '%s\n%s\n' "$left_version" "$right_version" | sort -V | tail -n 1)" == "$left_version" && "$left_version" != "$right_version" ]]
}

version_has_same_release_line() {
    local left_version="$1"
    local right_version="$2"

    [[ "${left_version%.*}" == "${right_version%.*}" ]]
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
    local upgrade_file="$kUpgradeDirectory/$version.sql"
    local history_entries

    if [[ ! -f "$upgrade_file" ]]; then
        echo "Missing upgrade script $upgrade_file." >&2
        exit 1
    fi

    history_entries="$(grep -Ec "^## ${version//./\\.} - " "$kRevisionHistoryFile" || true)"
    if [[ "$history_entries" -ne 1 ]]; then
        echo "Expected exactly one revision-history heading for $version." >&2
        exit 1
    fi
}

mode="${1:-}"
case "$mode" in
    --allocate)
        [[ "$#" -eq 3 && "$2" == "--target" ]] || usage
        target_version="$3"
        version_is_valid "$target_version" || usage

        source_version="$(read_product_version)"
        if [[ "$source_version" != "$kPlaceholderVersion" ]]; then
            echo "Allocation requires the $kPlaceholderVersion placeholder, found $source_version." >&2
            exit 1
        fi
        require_versioned_files "$source_version"
        [[ "$target_version" != "$kPlaceholderVersion" ]] || usage
        allocation_date="$(read_allocation_date)"
        if ! grep -Eq "^## ${source_version//./\\.} - [0-9]{2}\\.[0-9]{2}\\.[0-9]{4}" "$kRevisionHistoryFile"; then
            echo "The placeholder revision-history heading must contain a DD.MM.YYYY date." >&2
            exit 1
        fi

        sed -i "s/^product_version:.*/product_version: \"$target_version\"/" "$kProductVersionFile"
        mv "$kUpgradeDirectory/$source_version.sql" "$kUpgradeDirectory/$target_version.sql"
        sed -i -E "s/^## ${source_version//./\\.} - [0-9]{2}\\.[0-9]{2}\\.[0-9]{4}(.*)$/## $target_version - $allocation_date\\1/" "$kRevisionHistoryFile"
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
        version_has_same_release_line "$product_version" "$base_version" || {
            echo "PR version $product_version must use the same major.minor release line as $base_version." >&2
            exit 1
        }
        require_versioned_files "$product_version"
        echo "Versioned files consistently reserve $product_version."
        ;;
    *) usage ;;
esac

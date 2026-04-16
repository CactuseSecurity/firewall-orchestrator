#!/usr/bin/env bash

set -euo pipefail

sbom_file="${1:-documentation/SBOM/github-sbom.json}"

if ! command -v jq >/dev/null 2>&1; then
    echo "jq is required" >&2
    exit 1
fi

if [[ ! -f "$sbom_file" ]]; then
    echo "SBOM file not found: $sbom_file" >&2
    exit 1
fi

jq -r '
    .packages[]
    | [
        (.name // "-"),
        (.versionInfo // "-"),
        (.licenseConcluded // "-"),
        (
            [
                .externalRefs[]?
                | select(.referenceType == "purl")
                | .referenceLocator
            ][0] // "-"
        )
    ]
    | @tsv
' "$sbom_file" | column -t -s $'\t'

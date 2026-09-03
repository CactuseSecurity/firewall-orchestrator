#!/usr/bin/env bash
#
# Regression tests for the launcher guards in scripts/run-playbook-with-sudo.sh:
# require_ansible_core and the version-aware ensure_collections. The launcher is
# sourced and its functions driven with stubbed ansible/ansible-galaxy on PATH.

set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
launcher="$repo_root/run-playbook-with-sudo.sh"

pass=0
fail=0

# Stub `ansible` (prints "[core $1]", or nothing when $1=none) and `ansible-galaxy`
# (touches marker $2 when called) on a fresh PATH dir.
make_stub_bin() {
    local core_version="$1" galaxy_marker="$2" bin
    bin="$(mktemp -d)"

    if [[ "$core_version" == "none" ]]; then
        printf '#!/usr/bin/env bash\nexit 0\n' >"$bin/ansible"
    else
        printf '#!/usr/bin/env bash\necho "ansible [core %s]"\n' "$core_version" >"$bin/ansible"
    fi

    printf '#!/usr/bin/env bash\ntouch %q\nexit 0\n' "$galaxy_marker" >"$bin/ansible-galaxy"

    chmod +x "$bin/ansible" "$bin/ansible-galaxy"
    printf '%s' "$bin"
    return 0
}

# Fixture repo dir with collections/requirements.yml and optional repository-local
# and fallback community.general manifests ("" = no manifest).
make_fixture() {
    local local_cg_version="$1" fallback_cg_version="$2" dir manifest
    dir="$(mktemp -d)"
    mkdir -p "$dir/collections" "$dir/fallback"
    cat >"$dir/ansible.cfg" <<'INI'
[defaults]
collections_path = ./collections:./fallback
INI
    cat >"$dir/collections/requirements.yml" <<'YAML'
---
collections:
  - name: community.general
    version: 11.4.2
YAML

    if [[ -n "$local_cg_version" ]]; then
        manifest="$dir/collections/ansible_collections/community/general"
        mkdir -p "$manifest"
        printf '{\n  "collection_info": {\n    "version": "%s"\n  }\n}\n' \
            "$local_cg_version" >"$manifest/MANIFEST.json"
    fi

    if [[ -n "$fallback_cg_version" ]]; then
        manifest="$dir/fallback/ansible_collections/community/general"
        mkdir -p "$manifest"
        printf '{\n  "collection_info": {\n    "version": "%s"\n  }\n}\n' \
            "$fallback_cg_version" >"$manifest/MANIFEST.json"
    fi

    printf '%s' "$dir"
    return 0
}

# assert_case <name> <expected_rc> <expect_galaxy: yes|no> <core_version> <local_cg_version> <fallback_cg_version> <fallback_only_env: no|singular|plural> <func>
assert_case() {
    local name="$1" expected_rc="$2" expect_galaxy="$3" core_version="$4" local_cg_version="$5" fallback_cg_version="$6" fallback_only_env="$7" func="$8"
    local bin fixture marker rc galaxy_seen="no"

    marker="$(mktemp -u)"
    bin="$(make_stub_bin "$core_version" "$marker")"
    fixture="$(make_fixture "$local_cg_version" "$fallback_cg_version")"

    (
        cd "$fixture" || exit 99
        PATH="$bin:$PATH"
        if [[ "$fallback_only_env" == "singular" ]]; then
            ANSIBLE_COLLECTIONS_PATH="$fixture/fallback"
        elif [[ "$fallback_only_env" == "plural" ]]; then
            ANSIBLE_COLLECTIONS_PATHS="$fixture/fallback"
        fi
        # shellcheck disable=SC1090
        source "$launcher"
        "$func"
    ) >/dev/null 2>&1
    rc=$?

    [[ -f "$marker" ]] && galaxy_seen="yes"

    local ok=1
    [[ "$rc" -eq "$expected_rc" ]] || ok=0
    [[ "$galaxy_seen" == "$expect_galaxy" ]] || ok=0

    if [[ "$ok" -eq 1 ]]; then
        printf 'PASS  %s\n' "$name"
        pass=$((pass + 1))
    else
        printf 'FAIL  %s (rc=%s want %s; galaxy=%s want %s)\n' \
            "$name" "$rc" "$expected_rc" "$galaxy_seen" "$expect_galaxy"
        fail=$((fail + 1))
    fi

    rm -rf "$bin" "$fixture"
    rm -f "$marker"
    return 0
}

#           name                                                  rc  galaxy core     local   fallback env  func
assert_case "core >=2.16 passes"                                   0  no     2.19.7   11.4.2  ""       no   require_ansible_core
assert_case "core <2.16 rejected"                                  1  no     2.14.18  11.4.2  ""       no   require_ansible_core
assert_case "missing/unknown ansible rejected"                     1  no     none     11.4.2  ""       no   require_ansible_core
assert_case "correct local manifest: no galaxy call"               0  no     2.19.7   11.4.2  ""       no   ensure_collections
assert_case "matching fallback manifest: no galaxy call"           0  no     2.19.7   ""      11.4.2   no   ensure_collections
assert_case "stale local shadows fallback: reinstalls"             0  yes    2.19.7   6.6.2   11.4.2   no   ensure_collections
assert_case "stale local outside configured path: no galaxy call"  0  no     2.19.7   6.6.2   11.4.2   singular ensure_collections
assert_case "legacy fallback path variable: no galaxy call"        0  no     2.19.7   ""      11.4.2   plural   ensure_collections
assert_case "missing manifests: installs"                          0  yes    2.19.7   ""      ""       no   ensure_collections

printf '\n%d passed, %d failed\n' "$pass" "$fail"
[[ "$fail" -eq 0 ]]

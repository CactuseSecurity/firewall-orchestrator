#!/usr/bin/env bash
set -euo pipefail

sudoers_file=""

cleanup() {
    if [[ -n "$sudoers_file" ]] && [[ -f "$sudoers_file" ]]; then
        sudo rm -f "$sudoers_file"
    fi
}

trap cleanup EXIT
trap 'exit 130' HUP INT TERM

# Pinned collections need ansible-core >=2.16 (community.general 11.x); a distro
# package can be older and would only fail later at playbook runtime.
require_ansible_core() {
    local min_core="2.16"
    local core
    # '|| true': a non-matching grep must not trip 'set -e' before the guidance.
    core="$(ansible --version 2>/dev/null | grep -oE 'core [0-9]+\.[0-9]+' | head -1 | awk '{print $2}')" || true

    if [[ -z "$core" ]]; then
        echo "Could not determine the ansible-core version. Is Ansible installed and on PATH?" >&2
        echo "Activate the installer environment first:" >&2
        echo "    source scripts/install-ansible-from-venv.sh" >&2
        exit 1
    fi

    if [[ "$(printf '%s\n%s\n' "$min_core" "$core" | sort -V | head -1)" != "$min_core" ]]; then
        echo "ansible-core $core is too old; the pinned collections require >=$min_core." >&2
        echo "Use the pinned installer Ansible instead of the distro package:" >&2
        echo "    source scripts/install-ansible-from-venv.sh" >&2
        exit 1
    fi
}

# Return the configured collection search paths, with the environment override Ansible
# itself honors taking precedence. The repository config supplies the standard fallback
# paths when no override is present.
get_collection_search_paths() {
    local config_file configured_paths ansible_home

    if [[ -n "${ANSIBLE_COLLECTIONS_PATH:-}" ]]; then
        printf '%s\n' "$ANSIBLE_COLLECTIONS_PATH"
        return 0
    fi

    if [[ -n "${ANSIBLE_COLLECTIONS_PATHS:-}" ]]; then
        printf '%s\n' "$ANSIBLE_COLLECTIONS_PATHS"
        return 0
    fi

    config_file="${ANSIBLE_CONFIG:-ansible.cfg}"
    if [[ -f "$config_file" ]]; then
        configured_paths="$(awk -F= '
            /^[[:space:]]*collections_paths?[[:space:]]*=/ {
                value=$2
                sub(/^[[:space:]]+/, "", value)
                sub(/[[:space:]]+$/, "", value)
                print value
                exit
            }
        ' "$config_file")"
    fi

    ansible_home="${ANSIBLE_HOME:-$HOME/.ansible}"
    printf '%s\n' "${configured_paths:-$ansible_home/collections:/usr/share/ansible/collections}"
}

expand_collection_path() {
    local collection_path="$1"

    case "$collection_path" in
        "~") printf '%s\n' "$HOME" ;;
        \~/*) printf '%s/%s\n' "$HOME" "${collection_path#\~/}" ;;
        *) printf '%s\n' "$collection_path" ;;
    esac
}

collection_manifest_has_version() {
    local manifest="$1" expected_version="$2" installed_version

    [[ -f "$manifest" ]] || return 1
    installed_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$manifest" | head -1)"
    [[ "$installed_version" == "$expected_version" ]]
}

# ./collections is first in ansible.cfg's path. A stale collection there shadows
# every fallback, so it must be refreshed. When the repository copy is absent, a
# matching collection in an effective fallback path is safe to use offline.
ensure_collections() {
    local requirements_file="collections/requirements.yml"
    local collections_dir="collections/ansible_collections"
    local collection_search_paths repository_collections_path repository_path_is_effective=0

    if [[ ! -f "$requirements_file" ]]; then
        echo "Cannot find $requirements_file - run this script from the repository root." >&2
        exit 1
    fi

    collection_search_paths="$(get_collection_search_paths)"
    repository_collections_path="$(cd collections && pwd -P)"

    local fqcn version namespace name manifest collection_path expanded_path fallback_manifest needs=0
    local -a collection_path_list
    IFS=: read -r -a collection_path_list <<< "$collection_search_paths"
    for collection_path in "${collection_path_list[@]}"; do
        [[ -n "$collection_path" ]] || continue
        expanded_path="$(expand_collection_path "$collection_path")"
        if [[ -d "$expanded_path" ]] && [[ "$(cd "$expanded_path" && pwd -P)" == "$repository_collections_path" ]]; then
            repository_path_is_effective=1
            break
        fi
    done

    while read -r fqcn version; do
        [[ -n "$fqcn" ]] || continue
        namespace="${fqcn%%.*}"
        name="${fqcn#*.}"
        manifest="$collections_dir/$namespace/$name/MANIFEST.json"

        if [[ "$repository_path_is_effective" -eq 1 ]] && [[ -d "$collections_dir/$namespace/$name" ]]; then
            if ! collection_manifest_has_version "$manifest" "$version"; then
                needs=1
                break
            fi
            continue
        fi

        local found_in_fallback=0
        for collection_path in "${collection_path_list[@]}"; do
            [[ -n "$collection_path" ]] || continue
            expanded_path="$(expand_collection_path "$collection_path")"
            if [[ -d "$expanded_path" ]] && [[ "$(cd "$expanded_path" && pwd -P)" == "$repository_collections_path" ]]; then
                continue
            fi

            fallback_manifest="$expanded_path/ansible_collections/$namespace/$name/MANIFEST.json"
            if collection_manifest_has_version "$fallback_manifest" "$version"; then
                found_in_fallback=1
                break
            fi
        done

        if [[ "$found_in_fallback" -eq 0 ]]; then
            needs=1
            break
        fi
    done < <(awk '/- name:/ {n=$3} /version:/ {print n, $2}' "$requirements_file")

    if [[ "$needs" -eq 0 ]]; then
        echo "Required Ansible collections available at pinned versions."
        return 0
    fi

    echo "Refreshing required Ansible collections from $requirements_file ..."
    if ! ansible-galaxy collection install -r "$requirements_file" -p collections --force; then
        echo "Failed to install the required Ansible collections." >&2
        echo "Install them manually before running the playbook:" >&2
        echo "    ansible-galaxy collection install -r $requirements_file -p collections --force" >&2
        exit 1
    fi
}

main() {
    local -a args=("$@")

    if [[ "${#args[@]}" -eq 0 ]]; then
        args=(site.yml)
    fi

    require_ansible_core
    ensure_collections

    if [[ "$(id -u)" -ne 0 ]]; then
        if ! command -v sudo >/dev/null 2>&1; then
            echo "sudo is required to run the Firewall Orchestrator installer." >&2
            exit 1
        fi

        if ! sudo -k -n true 2>/dev/null; then
            echo "Enter sudo password to create a temporary sudoers entry for Ansible."
            sudo -v

            current_user="$(id -un)"
            sudoers_file="/etc/sudoers.d/fworch-ansible-$$"

            printf '%s ALL=(ALL) NOPASSWD: ALL\n' "$current_user" | sudo tee "$sudoers_file" >/dev/null
            sudo chmod 0440 "$sudoers_file"

            if command -v visudo >/dev/null 2>&1; then
                sudo visudo -cf "$sudoers_file" >/dev/null
            fi
        fi
    fi

    ansible-playbook "${args[@]}"
}

# Run the installer flow only when executed directly, not when sourced (tests).
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi

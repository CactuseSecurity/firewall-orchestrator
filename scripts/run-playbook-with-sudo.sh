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

# ./collections is first in ansible.cfg's path, so a stale version there silently
# shadows the pin. Reinstall with --force only when a pinned collection is missing
# or at the wrong version, so offline hosts with the right versions skip Galaxy.
ensure_collections() {
    local requirements_file="collections/requirements.yml"
    local collections_dir="collections/ansible_collections"

    if [[ ! -f "$requirements_file" ]]; then
        echo "Cannot find $requirements_file - run this script from the repository root." >&2
        exit 1
    fi

    local fqcn version namespace name manifest have needs=0
    while read -r fqcn version; do
        [[ -n "$fqcn" ]] || continue
        namespace="${fqcn%%.*}"
        name="${fqcn#*.}"
        manifest="$collections_dir/$namespace/$name/MANIFEST.json"
        have=""
        if [[ -f "$manifest" ]]; then
            have="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$manifest" | head -1)"
        fi
        if [[ "$have" != "$version" ]]; then
            needs=1
            break
        fi
    done < <(awk '/- name:/ {n=$3} /version:/ {print n, $2}' "$requirements_file")

    if [[ "$needs" -eq 0 ]]; then
        echo "Required Ansible collections already present at pinned versions."
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

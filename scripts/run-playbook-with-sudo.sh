#!/usr/bin/env bash
set -euo pipefail

args=("$@")

if [[ "${#args[@]}" -eq 0 ]]; then
    args=(site.yml)
fi

sudoers_file=""

cleanup() {
    if [[ -n "$sudoers_file" ]] && [[ -f "$sudoers_file" ]]; then
        sudo rm -f "$sudoers_file"
    fi
}

trap cleanup EXIT
trap 'exit 130' HUP INT TERM

# ansible-core (default on RedHat-like systems) does not bundle the collections
# needed by the playbooks - install them when any of them is missing
ensure_collections() {
    local requirements_file="collections/requirements.yml"

    [[ -f "$requirements_file" ]] || return 0

    local installed name missing=0
    installed="$(ansible-galaxy collection list 2>/dev/null || true)"

    while read -r name; do
        [[ -n "$name" ]] || continue
        if ! awk -v collection="$name" '$1 == collection { found = 1 } END { exit !found }' <<<"$installed"; then
            missing=1
            break
        fi
    done < <(awk '/- name:/ {print $3}' "$requirements_file")

    if [[ "$missing" -eq 1 ]]; then
        echo "Installing required Ansible collections from $requirements_file ..."
        if ! ansible-galaxy collection install -r "$requirements_file" -p collections; then
            echo "Failed to install the required Ansible collections." >&2
            echo "Install them manually before running the playbook:" >&2
            echo "    ansible-galaxy collection install -r $requirements_file -p collections --force" >&2
            exit 1
        fi
    fi
}

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

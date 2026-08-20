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

# The pinned collections declare a minimum ansible-core (community.general 11.x
# requires >=2.16.0). A distro-provided ansible-core can be older; it would still
# install the collection but fail at playbook runtime, so require the pinned pip
# Ansible from install-ansible-from-venv.sh instead of the distro package.
require_ansible_core() {
    local min_core="2.16"
    local core
    core="$(ansible --version 2>/dev/null | grep -oE 'core [0-9]+\.[0-9]+' | head -1 | awk '{print $2}')"

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

# Force-refresh the pinned collections into ./collections on every run. Without
# --force, a version already on disk (or a different one bundled with ansible)
# keeps its place and the pinned requirement is silently ignored. ansible.cfg
# lists ./collections first, so this is what actually executes.
ensure_collections() {
    local requirements_file="collections/requirements.yml"

    [[ -f "$requirements_file" ]] || return 0

    echo "Refreshing required Ansible collections from $requirements_file ..."
    if ! ansible-galaxy collection install -r "$requirements_file" -p collections --force; then
        echo "Failed to install the required Ansible collections." >&2
        exit 1
    fi
}

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

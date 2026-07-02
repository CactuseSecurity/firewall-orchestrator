#!/usr/bin/env bash
set -euo pipefail

args=()
for arg in "$@"; do
    case "$arg" in
        -K|--ask-become-pass|--ask-sudo-pass)
            ;;
        *)
            args+=("$arg")
            ;;
    esac
done

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

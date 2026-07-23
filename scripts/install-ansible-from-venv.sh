#!/bin/bash

# this script must be executed from within the repo directory using source as follows:
# source scripts/install-ansible-from-venv.sh

# for this you also need access to pypi.org (either directly or through proxy) 
# for downloading ansible

main() {
    local python_bin="python3"
    local venv_dir="${FWORCH_INSTALLER_VENV:-$HOME/.fwo/installer-venv}"
    local -a pip_install_options=(--default-timeout 3600)

    if [[ ! -f /etc/os-release ]]; then
        echo "Could not detect operating system: /etc/os-release missing."
        return 1
    fi

    . /etc/os-release

    # ansible==12.3.0 (see requirements.txt) requires Python >=3.11. On RHEL/Rocky 9
    # the platform python3 is 3.9, so the venv must be built with python3.11 there.
    case "${ID_LIKE:-$ID}" in
        *debian*)
            sudo apt update || return $?
            sudo apt install python3-venv -y || return $?
            ;;
        *rhel*|*fedora*)
            sudo dnf install python3.11 python3.11-pip -y || return $?
            python_bin="python3.11"
            ;;
        *)
            echo "Unsupported operating system family: ${ID_LIKE:-$ID}"
            return 1
            ;;
    esac

    mkdir -p "$(dirname "$venv_dir")" || return $?
    "$python_bin" -m venv --clear "$venv_dir" || return $?

    source "$venv_dir/bin/activate" || return $?
    # pip automatically honors the standard HTTP(S)_PROXY environment variables.
    # Keep the timeout command-local so this script never changes user pip config.
    pip install "${pip_install_options[@]}" -r requirements.txt || return $?
    if [[ -f scripts/requirements.txt ]]; then
        pip install "${pip_install_options[@]}" -r scripts/requirements.txt || return $?
    fi
    pip install "${pip_install_options[@]}" ansible || return $?
    ansible-galaxy collection install -r collections/requirements.yml -p collections --force || return $?
}

main "$@"
script_status=$?
if [[ "${BASH_SOURCE[0]}" != "$0" ]]; then
    return "$script_status"
fi
exit "$script_status"

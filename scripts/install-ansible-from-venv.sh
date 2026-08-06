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

    # requirements.txt pins the production Ansible version for Python 3.11.
    # On RHEL/Rocky 9 the platform python3 is 3.9, so the venv must use python3.11 there.
    case "${ID_LIKE:-$ID}" in
        *debian*)
            sudo apt update || return $?
            # python3-dev supplies Python.h, which python-ldap needs to build its C extension.
            sudo apt install python3-venv python3-dev build-essential libldap2-dev libsasl2-dev -y || return $?
            ;;
        *rhel*|*fedora*)
            # RHEL 10 and current Fedora ship Python 3.12 or newer as python3 and have no
            # python3.11 package, so only the older releases need the parallel installation.
            local os_major="${VERSION_ID%%.*}"
            local python_pkg="python3.11"
            if [[ "$os_major" =~ ^[0-9]+$ ]] && ((os_major >= 10)); then
                python_pkg="python3"
            fi
            # python3*-devel supplies Python.h, which python-ldap needs to build its C extension.
            sudo dnf install "$python_pkg" "$python_pkg-pip" "$python_pkg-devel" gcc openldap-devel cyrus-sasl-devel -y || return $?
            python_bin="$python_pkg"
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

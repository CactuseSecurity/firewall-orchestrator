#!/bin/bash

# this script must be executed from within the repo directory using source as follows:
# source scripts/install-ansible-from-venv.sh

# for this you also need access to pypi.org (either directly or through proxy) 
# for downloading ansible

set_pip_config_if_compatible() {
    local key="$1"
    local desired_value="$2"
    local current_value

    current_value="$(pip config get "$key" 2>/dev/null || true)"
    if [[ -z "$current_value" ]]; then
        pip config set "$key" "$desired_value"
    elif [[ "$current_value" != "$desired_value" ]]; then
        echo "Existing pip config $key=$current_value conflicts with requested value $desired_value." >&2
        echo "Please adjust the existing pip configuration manually and rerun this script." >&2
        return 1
    fi
    return 0
}

main() {
    local python_bin="python3"

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

    "$python_bin" -m venv --clear installer-venv || return $?

    source installer-venv/bin/activate || return $?
    if [[ "${http_proxy:-}" != "" ]];
    then
        set_pip_config_if_compatible global.proxy "$http_proxy" || return $?
    fi
    set_pip_config_if_compatible global.default-timeout 3600 || return $?
    pip install -r requirements.txt || return $?
    if [[ -f collections/requirements.txt ]]; then
        pip install -r collections/requirements.txt || return $?
    fi
    pip install ansible || return $?
    ansible-galaxy collection install -r collections/requirements.yml -p collections --force || return $?
}

main "$@"
script_status=$?
if [[ "${BASH_SOURCE[0]}" != "$0" ]]; then
    return "$script_status"
fi
exit "$script_status"

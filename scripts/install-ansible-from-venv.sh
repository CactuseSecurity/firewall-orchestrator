#!/bin/bash

# this script must be executed from within the repo directory using source as follows:
# source scripts/install-ansible-from-venv.sh

# for this you also need access to pypi.org (either directly or through proxy) 
# for downloading ansible

exit_script() {
    local exit_code="$1"
    return "$exit_code" 2>/dev/null || exit "$exit_code"
}

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
        exit_script 1
    fi
    return 0
}

if [[ ! -f /etc/os-release ]]; then
    echo "Could not detect operating system: /etc/os-release missing."
    exit_script 1
fi

. /etc/os-release

case "${ID_LIKE:-$ID}" in
    *debian*)
        sudo apt update
        sudo apt install python3-venv -y
        python3 -m venv --clear installer-venv
        ;;
    *rhel*|*fedora*)
        sudo dnf install python3 python3-pip -y
        ;;
    *)
        echo "Unsupported operating system family: ${ID_LIKE:-$ID}"
        exit_script 1
        ;;
esac

python3 -m venv installer-venv

source installer-venv/bin/activate
if [[ "${http_proxy:-}" != "" ]];
then
    set_pip_config_if_compatible global.proxy "$http_proxy"
fi
set_pip_config_if_compatible global.default-timeout 3600
pip install -r requirements.txt
if [[ -f collections/requirements.txt ]]; then
    pip install -r collections/requirements.txt
fi
pip install ansible
ansible-galaxy collection install -r collections/requirements.yml -p collections --force

#!/bin/bash

# this script must be executed from within the repo directory using source as follows:
# source scripts/install-ansible-from-venv.sh

# for this you also need access to pypi.org (either directly or through proxy) 
# for downloading ansible

if [ ! -f /etc/os-release ]; then
    echo "Could not detect operating system: /etc/os-release missing."
    return 1 2>/dev/null || exit 1
fi

. /etc/os-release

case "${ID_LIKE:-$ID}" in
    *debian*)
        sudo apt update
        sudo apt install python3-venv -y
        ;;
    *rhel*|*fedora*)
        sudo dnf install python3 python3-pip -y
        ;;
    *)
        echo "Unsupported operating system family: ${ID_LIKE:-$ID}"
        return 1 2>/dev/null || exit 1
        ;;
esac

sudo apt update
sudo apt install python3-venv -y
python3 -m venv installer-venv
source installer-venv/bin/activate
if [[ ! "$http_proxy" == "" ]];
then
    pip config set global.proxy $http_proxy
fi    
pip config set global.default-timeout 3600
pip install -r requirements.txt
if [ -f collections/requirements.txt ]; then
    pip install -r collections/requirements.txt
fi
pip install ansible
ansible-galaxy collection install -r collections/requirements.yml -p collections --force

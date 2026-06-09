#!/bin/bash

# this script must be executed from within the repo directory using source as follows:
# source scripts/install-ansible-from-venv.sh

# for this you also need access to pypi.org (either directly or through proxy) 
# for downloading ansible

sudo apt-get update
sudo apt-get install --yes python3-venv
python3 -m venv --clear installer-venv
source installer-venv/bin/activate
if [[ ! "$http_proxy" == "" ]];
then
    pip config set global.proxy $http_proxy
fi    
pip config set global.default-timeout 3600
pip install -r requirements.txt
pip install ansible

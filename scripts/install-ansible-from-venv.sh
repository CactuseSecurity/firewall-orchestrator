#!/bin/bash

# this script must be executed from within the repo directory using source as follows:
# source scripts/install-ansible-from-venv.sh

# for this you also need access to pypi.org (either directly or through proxy) 
# for downloading ansible

sudo apt install python3-venv
python3 -m venv ansible-venv
source ansible-venv/bin/activate
pip install ansible

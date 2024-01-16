#!/bin/sh

# running this script needs access to pypi.org (either directly or through proxy) 
# for downloading ansible

sudo apt install python3-venv
python3 -m venv ansible-venv
source ansible-venv/bin/activate
pip install ansible

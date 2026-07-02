#!/bin/sh
#firewall-orchestrator installer

set -e

#install required packages
apt-get install -y git ansible ssh sudo

#generate ssh key
# ssh-keygen -b 4096
# cat .ssh/id_rsa.pub >>.ssh/authorized_keys
# chmod 600 .ssh/authorized_keys

#tests
# ssh 127.0.0.1
# ansible -m ping 127.0.0.1

#clone repository and install firewall-orchestrator
git clone https://github.com/CactuseSecurity/firewall-orchestrator
cd firewall-orchestrator
ansible-galaxy collection install -r collections/requirements.yml -p collections --force
./scripts/run-playbook-with-sudo.sh site.yml

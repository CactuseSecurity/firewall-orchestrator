#!/bin/bash
#firewall-orchestrator installer

set -e

# install required packages (Ansible itself comes from the pinned installer venv
# below, not the distro package, so ansible-core meets the collection floor)
apt-get install -y git ssh sudo

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
# installs the pinned Ansible + collections into a venv and leaves it active
source scripts/install-ansible-from-venv.sh
./scripts/run-playbook-with-sudo.sh site.yml

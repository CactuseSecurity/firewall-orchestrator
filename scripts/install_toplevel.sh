#!/bin/sh
#firewall-orchestrator installer

#install required packages
apt-get install git ansible ssh sudo

#generate ssh key
# ssh-keygen -b 4096
# cat .ssh/id_rsa.pub >>.ssh/authorized_keys
# chmod 600 .ssh/authorized_keys

#tests
# ssh 127.0.0.1
# ansible -m ping 127.0.0.1

#clone repository and install firewall-orchestrator
git clone https://github.com/CactuseSecurity/firewall-orchestrator && cd firewall-orchestrator && ansible-playbook site.yml -K

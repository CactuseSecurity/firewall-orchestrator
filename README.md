# Firewall Orchestrator

Open Network Automation Framework 
- Import firewall configurations (rules) of various brands (Check Point, Fortinet, Juniper, Barracuda, Netscreen)
- Display reports on firewall configuration and changes
- Document changes and integrate with ticketing systems
- Demo: https://demo.itsecorg.de (user: admin, password: fworch.1)

## Installation instructions
use latest debian or ubuntu server with ssh service running

this will install various software components to your system. It is recommended to do so on a dedicated (test) system.

1) prepare your test system

       su -
       apt-get install git ansible sudo
       exit
       ssh-keygen -b 4096
       cat .ssh/id_rsa.pub >>.ssh/authorized_keys
       chmod 600 .ssh/authorized_keys

2) test system connectiviy necessary for installation

   test ssh connectivity to localhost (127.0.0.1) using public key auth (add .ssh/authorized_keys
   
       ssh 127.0.0.1
   make sure you can use ansible locally
   
       ansible -m ping 127.0.0.1

2) get Firewall Orchestrator with the following command

       git clone ssh://git@github.com/tpurschke/firewall-orchestrator.git

3) setup (install everything on localhost)

       cd firewall-orchestrator; ansible-playbook -i inventory site.yml -K
  


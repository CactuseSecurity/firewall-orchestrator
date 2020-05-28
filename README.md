# Firewall Orchestrator

- Import firewall configurations (rules) of various brands (Check Point, Fortinet, Juniper, Barracuda, Netscreen)
- Display reports on firewall configuration and changes
- Document changes and integrate with ticketing systems
- Demo: if you want to see what it looks like in advance, visit https://demo.itsecorg.de (user: admin, password: fworch.1)
- if your system lives behind a proxy, see https://github.com/CactuseSecurity/firewall-orchestrator/edit/master/INSTALL_ADVANCED.MD

## Installation instructions

### Server
use latest debian or ubuntu minimal server with ssh service running (need to install and configure sudo for debian)

this will install various software components to your system. It is recommended to do so on a dedicated (test) system.

1) prepare your test system (install packages needed for install script and create and autorize ssh key pair to allow ssh login to localhost for ansible connect) 

       su -
       apt-get install git ansible ssh sudo
       exit
       ssh-keygen -b 4096
       cat .ssh/id_rsa.pub >>.ssh/authorized_keys
       chmod 600 .ssh/authorized_keys

2) if not already configured, add your current user to sudo group (make sure to activate this change by starting new shell or even rebooting):

       usermod -a -G sudo `whoami`

3) test system connectiviy necessary for installation

   test ssh connectivity to localhost (127.0.0.1) using public key auth (add .ssh/authorized_keys) 
              
       ssh 127.0.0.1
       
   make sure you can use ansible locally   
       
       ansible -m ping 127.0.0.1
 
4) get Firewall Orchestrator with the following command
      
       git clone https://github.com/CactuseSecurity/firewall-orchestrator.git
       (or via ssh: git clone ssh://git@github.com/CactuseSecurity/firewall-orchestrator.git, needs ssh key to be uploaded)


5) setup (install everything on localhost)

       cd firewall-orchestrator; ansible-playbook -i inventory site.yml -K
       enter sudo password when prompted "BECOME or SUDO password:"

   that's it firewall-orchestrator is ready for usage

6) further documentation - how to use the program
- see https://github.com/CactuseSecurity/firewall-orchestrator/blob/master/documentation/get-started.MD

### Client - Eto.Forms
The client runs on Linux / Windows / MacOS

// TODO: FINISH INSTRUCTIONS  

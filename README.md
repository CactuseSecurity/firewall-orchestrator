# Firewall Orchestrator

Open Network Automation Framework 
- Import firewall configurations (rules) of various brands (Check Point, Fortinet, Juniper, Barracuda, Netscreen)
- Display reports on firewall configuration and changes
- Document changes and integrate with ticketing systems
- Demo: https://demo.itsecorg.de (user: admin, password: fworch.1)

## Installation instructions
use latest debian or ubuntu server with ssh service running

this will install various software components to your system.

It is stronly recommended to do so on a dedicated (test) system.

1) prepare your test system

       apt install git ansible sudo
       ssh-keygen -b 4096

2) test system connectiviy necessary for installation

   test ssh connectivity to localhost (127.0.0.1)
   
       ssh 127.0.0.1
   make sure you can use ansible locally
   
       ansible -m ping 127.0.0.1

2) get Firewall Orchestrator with the following command

       git clone ssh://git@github.com/tpurschke/firewall-orchestrator.git

3) setup (install everything on localhost)

       cd firewall-orchestrator; ansible-playbook -i inventory site.yml -K
  

## Advanced Installation 1: if your system lives behind a proxy

       cd firewall-orchestrator; ansible-playbook -i inventory -e "http_proxy=http://1.2.3.4:3128" site.yml -K
       
## Advanced Installation 2: distributed setup

if you want to distribute functionality to different hosts:

   modify firewall-orchestrator/inventory/hosts to your needs 

   change ip addresses) of hosts to install to, e.g. 

      isofront ansible_host=10.5.5.5
      isoback ansible_host=10.5.10.10
	
   put the hosts into the correct section ([frontends], [backends], [importers])
	   
   make sure all target hosts meet the requirements for ansible (user with pub key auth & full sudo rights)
	
   modify isohome/etc/iso.conf on frontend(s):
	
   enter the address of the database backend server, e.g.
		
	itsecorg database hostname              10.5.10.10
	
   modify /etc/postgresql/x.y/main/pg_hba.conf to allow secuadmins access from web frontend(s), e.g.
	
	host    all         +secuadmins         127.0.0.1/32           md5
	host    all         +secuadmins         10.5.5.5/32            md5
	host    all         dbadmin             10.5.10.10/32            md5

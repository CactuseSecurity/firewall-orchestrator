# Advanced installation options

## Installation behind a proxy (no direct Internet connection)
e.g. with IP 1.2.3.4, listening on port 3128  
note: this does not yet work 100%

       cd firewall-orchestrator; ansible-playbook -i inventory -e "http_proxy=http://1.2.3.4:3128 https_proxy=http://1.2.3.4:3128" site.yml -K
       

## Option "include_php_ui" to install old UI
Use the following command to install the old php based user interface on your server:

       cd firewall-orchestrator; ansible-playbook -i inventory -e "include_php_ui=1" site.yml -K
    
## Option "clean_install" to start with fresh database
if you want to drop the database and re-install from scratch, simply add the variable clean_install as follows:
    
       cd firewall-orchestrator; ansible-playbook -i inventory -e "clean_install=1" site.yml -K

## Option "connect_sting" to add Cactus test firewall CP R8x
The following command adds the sting test firewall to your fw orch system (needs VPN tunnel to Cactus)

       cd firewall-orchestrator; ansible-playbook -i inventory -e "connect_sting=1" site.yml -K

## Distributed setup with multiple servers

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


adding importer support for fortinet fortios 5.x devices
  
installation instructions:
add:   importer/CACTUS/ISO/import/fortinet.pm
replace: the whole web branch (due to changes/fixes in change-reporting, documentation, etc.)

execute SQL (FortiOS 5.x as stm_dev_typ and 3 fortinet specific track types; contained in install/database/stored-procedures/iso-fill-stm.sql):
insert into stm_dev_typ
(dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
VALUES (10,'Fortinet','5.x','Fortinet','');
insert into stm_track (track_id,track_name) VALUES (18,'all');
insert into stm_track (track_id,track_name) VALUES (19,'all start');
insert into stm_track (track_id,track_name) VALUES (20,'utm');

----

   prepare fortigate for ssh access with keys:
        ssh: http://kb.fortinet.com/kb/documentLink.do?externalID=11985
        scp:
    http://kb.fortinet.com/kb/microsites/search.do?cmd=displayKC&docType=kc&externalId=12002&sliceId=1&docTypeID=DT_KCARTICLE_1_1&dialogID=79467674&stateId=0%200%2079469011  
            scp admin@10.8.6.1:sys_config .    
            sys_config does not contain firewall rules?!
            
config system admin
    edit "admin"
        set accprofile "super_admin"
        set vdom "root"
        set ssh-public-key1 "ssh-rsa xxxx"
 
    fortinet device:
        generate key pair and configure the public key for the itsecorg user
        create user itsecorg with public key auth and read-only
        
create profile for user itsecorg:

config system accprofile
    edit "config_reader"
        set scope vdom
        set comments "read-only"
        set mntgrp read
        set admingrp read
        set updategrp read
        set authgrp read
        set sysgrp read
        set netgrp read
        set loggrp read
        set routegrp read
        set fwgrp read
        set vpngrp read
        set utmgrp read
        set wanoptgrp read
        set endpoint-control-grp read
        set wifi read
    next
end
config system admin
    edit "itsecorg"
        set accprofile "config_reader"
        set comments ''
        set vdom "root"
        set ssh-public-key1 "ssh-rsa xxxx"
        set password ENC xxx
    next
end
        
        e.g.
config system admin
edit "itsecorg"
set ssh-public-key2 "ssh-rsa xxx"
next
end

------

how to create devices for fortigates with vdoms:
- every vdom is to treated as a separate management system
- name all 3 fields (management_name, device_name, rulebase name) as
follows (hostname: found in config line "set hostname xxx")
  <hostname>___<vdom-name>
 
 
------
multi port / multi protocol services are transformed into groups with
newly created service objects as members - all of these contain the
string "_ISO-multiGRP-"
    DNS    group    
        DNS_ISO-multiGRP-53_tcp
        DNS_ISO-multiGRP-53_udp

ipv4 and v6 policies are shown in the same table, ipv6 policies have an
ID of the form "uid: ipv6.x"


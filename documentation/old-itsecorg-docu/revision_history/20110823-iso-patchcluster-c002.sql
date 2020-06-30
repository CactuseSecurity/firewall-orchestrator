-- $Id: 20110823-iso-patchcluster-c002.sql,v 1.1.2.2 2011-08-10 05:34:49 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20110823-iso-patchcluster-c002.sql,v $
-- this is a collection from 20070821 to 20071019

/*

Um auf einen einheitlichen definierten Stand zu kommen, werden alle Aenderungen nachgezogen, 
auch solche, die sich auf Import-Module beziehen, die ein Kunde nicht im Einsatz hat.

1) Auf altem System: Importprozess stoppen und Backup alter Datenbank machen bzw. pruefen ob letztes vorhandenes Backup aktuell ist

2) Auf altem System: Pruefen, wie derzeit die Checkpoint User importiert werden und diesen Zustand nach Patchen wieder herstellen 
	in $ISODIR/install/bin/agents/checkpoint-cp-config-locally.sh (ist export_users aktiv?)

Ab hier auf neuem System

3) Neues ITSecOrg-System gemaess Doku als debian-6.0 amd64 aufsetzen
	dabei eventuell benoetigte HW-Treiber einspielen (und dokumentieren)
	pruefen, ob das in der Doku drin ist: sudo aptitude install libexpect-perl
  	ohne db-init3-fill.sh --> das kommt aus Backup
	eventuell bei Einrichten von "aptitude install update/safe-upgrade" beraten 
   
4) Einspielen aller aktueller Sourcen von CVS

5) Datenbank-Backup einspielen
	User-Datenbank als erstes einspielen
   
6) Alle Funktionen neu definieren
	vorher Passwoerter in iso-set-vars.sh richtig setzen
	$ISODIR/bin/db-init2-functions-with-output.sh

*/

-- 7) R75 (und junos) DB Aenderungen:
   insert into isoadmin 
	(isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username) 
	VALUES (1,'Check Point Security Management Server Update Process','Check Point','auto');

  	insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
	VALUES (7,'Check Point','R7x','Check Point','');
	
	insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
	VALUES (8,'JUNOS','10.x','Juniper','');

update stm_dev_typ set dev_typ_predef_svc='any;0;0;65535;;junos-predefined-service;simple;
junos-aol;6;5190;5193;;junos-predefined-service;simple;
junos-bgp;6;179;;;junos-predefined-service;simple;
junos-biff;17;512;;;junos-predefined-service;simple;
junos-bootpc;17;68;;;junos-predefined-service;simple;
junos-bootps;17;67;;;junos-predefined-service;simple;
junos-chargen;17;19;;;junos-predefined-service;simple;
junos-cvspserver;6;2401;;;junos-predefined-service;simple;
junos-dhcp-client;17;68;;;junos-predefined-service;simple;
junos-dhcp-relay;17;67;;;junos-predefined-service;simple;
junos-dhcp-server;17;67;;;junos-predefined-service;simple;
junos-discard;17;9;;;junos-predefined-service;simple;
junos-dns-tcp;6;53;;;junos-predefined-service;simple;
junos-dns-udp;17;53;;;junos-predefined-service;simple;
junos-echo;17;7;;;junos-predefined-service;simple;
junos-finger;6;79;;;junos-predefined-service;simple;
junos-ftp;6;21;;;junos-predefined-service;simple;
junos-gnutella;17;6346;6347;;junos-predefined-service;simple;
junos-gopher;6;70;;;junos-predefined-service;simple;
junos-gre;47;;;;junos-predefined-service;simple;
junos-gtp;17;2123;;;junos-predefined-service;simple;
junos-h323;;;;;junos-predefined-service;simple;
junos-http;6;80;;;junos-predefined-service;simple;
junos-http-ext;6;7001;;;junos-predefined-service;simple;
junos-https;6;443;;;junos-predefined-service;simple;
junos-icmp-all;1;;;;junos-predefined-service;simple;
junos-icmp-ping;1;;;;junos-predefined-service;simple;
junos-icmp6-all;58;;;;junos-predefined-service;simple;
junos-icmp6-dst-unreach-addr;58;;;;junos-predefined-service;simple;
junos-icmp6-dst-unreach-admin;58;;;;junos-predefined-service;simple;
junos-icmp6-dst-unreach-beyond;58;;;;junos-predefined-service;simple;
junos-icmp6-dst-unreach-port;58;;;;junos-predefined-service;simple;
junos-icmp6-dst-unreach-route;58;;;;junos-predefined-service;simple;
junos-icmp6-echo-reply;58;;;;junos-predefined-service;simple;
junos-icmp6-echo-request;58;;;;junos-predefined-service;simple;
junos-icmp6-packet-to-big;58;;;;junos-predefined-service;simple;
junos-icmp6-param-prob-header;58;;;;junos-predefined-service;simple;
junos-icmp6-param-prob-nexthdr;58;;;;junos-predefined-service;simple;
junos-icmp6-param-prob-option;58;;;;junos-predefined-service;simple;
junos-icmp6-time-exceed-reassembly;58;;;;junos-predefined-service;simple;
junos-icmp6-time-exceed-transit;58;;;;junos-predefined-service;simple;
junos-ident;6;113;;;junos-predefined-service;simple;
junos-ike;17;500;;;junos-predefined-service;simple;
junos-ike-nat;17;4500;;;junos-predefined-service;simple;
junos-imap;6;143;;;junos-predefined-service;simple;
junos-imaps;6;993;;;junos-predefined-service;simple;
junos-internet-locator-service;6;389;;;junos-predefined-service;simple;
junos-irc;6;6660;6669;;junos-predefined-service;simple;
junos-l2tp;17;1701;;;junos-predefined-service;simple;
junos-ldap;6;389;;;junos-predefined-service;simple;
junos-ldp-tcp;6;646;;;junos-predefined-service;simple;
junos-ldp-udp;17;646;;;junos-predefined-service;simple;
junos-lpr;6;515;;;junos-predefined-service;simple;
junos-mail;6;25;;;junos-predefined-service;simple;
junos-mgcp;;;;;junos-predefined-service;group;junos-mgcp-ua|junos-mgcp-ca
junos-mgcp-ca;17;2427;;;junos-predefined-service;simple;
junos-mgcp-ua;17;2727;;;junos-predefined-service;simple;
junos-ms-rpc;;;;;junos-predefined-service;group;junos-ms-rpc-tcp|junos-ms-rpc-udp
junos-ms-rpc-epm;6;;;;junos-predefined-service;simple;
junos-ms-rpc-msexchange;;;;;junos-predefined-service;group;junos-ms-rpc-tcp|junos-ms-rpc-udp|junos-ms-rpc-epm|junos-ms-rpc-msexchange-directory-rfr|junos-ms-rpc-msexchange-info-store|junos-ms-rpc-msexchange-directory-nsp
junos-ms-rpc-msexchange-directory-nsp;6;;;;junos-predefined-service;simple;
junos-ms-rpc-msexchange-directory-rfr;6;;;;junos-predefined-service;simple;
junos-ms-rpc-msexchange-info-store;6;;;;junos-predefined-service;simple;
junos-ms-rpc-tcp;6;135;;;junos-predefined-service;simple;
junos-ms-rpc-udp;17;135;;;junos-predefined-service;simple;
junos-ms-sql;6;1433;;;junos-predefined-service;simple;
junos-msn;6;1863;;;junos-predefined-service;simple;
junos-nbds;17;138;;;junos-predefined-service;simple;
junos-nbname;17;137;;;junos-predefined-service;simple;
junos-netbios-session;6;139;;;junos-predefined-service;simple;
junos-nfs;17;111;;;junos-predefined-service;simple;
junos-nfsd-tcp;6;2049;;;junos-predefined-service;simple;
junos-nfsd-udp;17;2049;;;junos-predefined-service;simple;
junos-nntp;6;119;;;junos-predefined-service;simple;
junos-ns-global;6;15397;;;junos-predefined-service;simple;
junos-ns-global-pro;6;15397;;;junos-predefined-service;simple;
junos-nsm;17;69;;;junos-predefined-service;simple;
junos-ntalk;;;;;junos-predefined-service;simple;
junos-ntp;17;123;;;junos-predefined-service;simple;
junos-ospf;89;;;;junos-predefined-service;simple;
junos-pc-anywhere;17;5632;;;junos-predefined-service;simple;
junos-persistent-nat;255;65535;;;junos-predefined-service;simple;
junos-ping;1;;;;junos-predefined-service;simple;
junos-pingv6;58;;;;junos-predefined-service;simple;
junos-pop3;6;110;;;junos-predefined-service;simple;
junos-pptp;6;1723;;;junos-predefined-service;simple;
junos-printer;6;515;;;junos-predefined-service;simple;
junos-radacct;17;1813;;;junos-predefined-service;simple;
junos-radius;17;1812;;;junos-predefined-service;simple;
junos-realaudio;6;554;;;junos-predefined-service;simple;
junos-rip;17;520;;;junos-predefined-service;simple;
junos-routing-inbound;;;;;junos-predefined-service;group;junos-bgp|junos-rip|junos-ldp-tcp|junos-ldp-udp
junos-rsh;6;514;;;junos-predefined-service;simple;
junos-rtsp;6;554;;;junos-predefined-service;simple;
junos-sccp;6;2000;;;junos-predefined-service;simple;
junos-sctp-any;132;;;;junos-predefined-service;simple;
junos-sip;;;;;junos-predefined-service;simple;
junos-smb;;;;;junos-predefined-service;simple;
junos-smtp;6;25;;;junos-predefined-service;simple;
junos-snmp-agentx;6;705;;;junos-predefined-service;simple;
junos-snpp;6;444;;;junos-predefined-service;simple;
junos-sql-monitor;17;1434;;;junos-predefined-service;simple;
junos-sqlnet-v1;6;1525;;;junos-predefined-service;simple;
junos-sqlnet-v2;6;1521;;;junos-predefined-service;simple;
junos-ssh;6;22;;;junos-predefined-service;simple;
junos-stun;;;;;junos-predefined-service;simple;
junos-sun-rpc;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp
junos-sun-rpc-mountd;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp|junos-sun-rpc-portmap-tcp|junos-sun-rpc-portmap-udp|junos-sun-rpc-mountd-tcp|junos-sun-rpc-mountd-udp
junos-sun-rpc-mountd-tcp;6;;;;junos-predefined-service;simple;
junos-sun-rpc-mountd-udp;17;;;;junos-predefined-service;simple;
junos-sun-rpc-nfs;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp|junos-sun-rpc-portmap-tcp|junos-sun-rpc-portmap-udp|junos-sun-rpc-nfs-tcp|junos-sun-rpc-nfs-udp
junos-sun-rpc-nfs-access;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp|junos-sun-rpc-portmap-tcp|junos-sun-rpc-portmap-udp|junos-sun-rpc-nfs-tcp|junos-sun-rpc-nfs-udp|junos-sun-rpc-mountd-tcp|junos-sun-rpc-mountd-udp
junos-sun-rpc-nfs-tcp;6;;;;junos-predefined-service;simple;
junos-sun-rpc-nfs-udp;17;;;;junos-predefined-service;simple;
junos-sun-rpc-portmap;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp|junos-sun-rpc-portmap-tcp|junos-sun-rpc-portmap-udp
junos-sun-rpc-portmap-tcp;6;;;;junos-predefined-service;simple;
junos-sun-rpc-portmap-udp;17;;;;junos-predefined-service;simple;
junos-sun-rpc-status;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp|junos-sun-rpc-portmap-tcp|junos-sun-rpc-portmap-udp|junos-sun-rpc-status-tcp|junos-sun-rpc-status-udp
junos-sun-rpc-status-tcp;6;;;;junos-predefined-service;simple;
junos-sun-rpc-status-udp;17;;;;junos-predefined-service;simple;
junos-sun-rpc-tcp;6;111;;;junos-predefined-service;simple;
junos-sun-rpc-udp;17;111;;;junos-predefined-service;simple;
junos-sun-rpc-ypbind;;;;;junos-predefined-service;group;junos-sun-rpc-tcp|junos-sun-rpc-udp|junos-sun-rpc-portmap-tcp|junos-sun-rpc-portmap-udp|junos-sun-rpc-ypbind-tcp|junos-sun-rpc-ypbind-udp
junos-sun-rpc-ypbind-tcp;6;;;;junos-predefined-service;simple;
junos-sun-rpc-ypbind-udp;17;;;;junos-predefined-service;simple;
junos-syslog;17;514;;;junos-predefined-service;simple;
junos-tacacs;6;49;;;junos-predefined-service;simple;
junos-tacacs-ds;6;65;;;junos-predefined-service;simple;
junos-talk;;;;;junos-predefined-service;simple;
junos-tcp-any;6;1;65535;;junos-predefined-service;simple;
junos-telnet;6;23;;;junos-predefined-service;simple;
junos-tftp;17;69;;;junos-predefined-service;simple;
junos-udp-any;17;1;65535;;junos-predefined-service;simple;
junos-uucp;17;540;;;junos-predefined-service;simple;
junos-vdo-live;17;7000;7010;;junos-predefined-service;simple;
junos-vnc;6;5800;;;junos-predefined-service;simple;
junos-wais;6;210;;;junos-predefined-service;simple;
junos-who;17;513;;;junos-predefined-service;simple;
junos-whois;6;43;;;junos-predefined-service;simple;
junos-winframe;6;1494;;;junos-predefined-service;simple;
junos-wxcontrol;6;3578;;7560;junos-predefined-service;simple;
junos-x-windows;6;6000;6063;;junos-predefined-service;simple;
junos-xnm-clear-text;6;3221;;;junos-predefined-service;simple;
junos-xnm-ssl;6;3220;;;junos-predefined-service;simple;
junos-ymsg;6;5050;;;junos-predefined-service;simple;'
where dev_typ_id=8;

-- 8) Neues Feld dev_id in einer Regel anlegen und existierende Regeln mit dev_id fuellen:
alter table rule add column dev_id integer;

select * from create_rule_dev_initial_entry_all();

Create index "IX_Relationship186" on "rule" ("dev_id");
Alter table "rule" add  foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete restrict;

-- 9) Anschliessend alle Objekte loeschen und neu anlegen lassen, um Referenzen sauber zu definieren:
--	cd $ISODIR/importer
--	./iso-importer-reset-all.pl

-- 10) Imports testen
--	dabei Agenten auf Firewall-Managementsystem eventuell anpassen/einrichten

-- 11) Weitere Funktionsstests (Reporting, ...)

-- 12) Backup einrichten und testen

-- 13) Import-Prozess starten

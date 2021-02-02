
-- SET client_encoding=UTF8
-- \encoding UTF8

INSERT INTO language (name) VALUES('German');
INSERT INTO language (name) VALUES('English');

insert into uiuser (uiuser_id, uiuser_username, uuid) VALUES (0,'default', 'default');

insert into config (config_key, config_value, config_user) VALUES ('DefaultLanguage', 'English', 0);

INSERT INTO "report_format" ("report_format_name") VALUES ('json');
INSERT INTO "report_format" ("report_format_name") VALUES ('pdf');
INSERT INTO "report_format" ("report_format_name") VALUES ('csv');
INSERT INTO "report_format" ("report_format_name") VALUES ('html');

-- default report templates belong to user 0 
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=rules and time=now ','Current Rules','Currently active rules of all gateways', 0);
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=changes and time="this year" ','This year''s Rule Changes','All rule change performed in the current year', 0);
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=statistics and time=now ','Basic Statistics','Number of objects and rules per device', 0);
INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
    VALUES ('type=rules and time=now and (src=any or dst=any or svc=any or src=all or dst=all or svc=all) and not(action=drop or action=reject or action=deny) ',
        'Compliance: Pass rules with ANY','All pass rules that contain any as source, destination or service', 0);

insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');

insert into stm_usr_typ (usr_typ_id,usr_typ_name) VALUES (1,'group');
insert into stm_usr_typ (usr_typ_id,usr_typ_name) VALUES (2,'simple');

insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (1,'simple','standard services');
insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (2,'group','groups of services');
insert into stm_svc_typ (svc_typ_id,svc_typ_name,svc_typ_comment) VALUES (3,'rpc','special services, here: RPC');

insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (1,'network');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (2,'group');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (3,'host');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (4,'machines_range');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (5,'dynamic_net_obj');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (6,'sofaware_profiles_security_level');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (7,'gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (8,'cluster_member');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (9,'gateway_cluster');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (10,'domain');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (11,'group_with_exclusion');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (12,'ip_range');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (13,'uas_collection');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (14,'sofaware_gateway');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (15,'voip_gk');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (16,'gsn_handover_group');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (17,'voip_sip');
insert into stm_obj_typ (obj_typ_id,obj_typ_name) VALUES (18,'simple-gateway');

insert into stm_action (action_id,action_name) VALUES (1,'accept'); -- cp, fortinet
insert into stm_action (action_id,action_name) VALUES (2,'drop'); -- cp
insert into stm_action (action_id,action_name) VALUES (3,'deny'); -- netscreen, fortinet
insert into stm_action (action_id,action_name) VALUES (4,'access'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (5,'client encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (6,'client auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (7,'reject'); -- cp
insert into stm_action (action_id,action_name) VALUES (8,'encrypt'); -- cp
insert into stm_action (action_id,action_name) VALUES (9,'user auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (10,'session auth'); -- cp
insert into stm_action (action_id,action_name) VALUES (11,'permit'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (12,'permit webauth'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (13,'redirect'); -- phion
insert into stm_action (action_id,action_name) VALUES (14,'map'); -- phion
insert into stm_action (action_id,action_name) VALUES (15,'permit auth'); -- netscreen
insert into stm_action (action_id,action_name) VALUES (16,'tunnel l2tp'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (17,'tunnel vpn-group'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (18,'tunnel vpn'); -- netscreen vpn
insert into stm_action (action_id,action_name) VALUES (19,'actionlocalredirect'); -- phion
insert into stm_action (action_id,action_name) VALUES (20,'inner layer'); -- check point r8x

insert into stm_track (track_id,track_name) VALUES (1,'log');
insert into stm_track (track_id,track_name) VALUES (2,'none');
insert into stm_track (track_id,track_name) VALUES (3,'alert');
insert into stm_track (track_id,track_name) VALUES (4,'userdefined');
insert into stm_track (track_id,track_name) VALUES (5,'mail');
insert into stm_track (track_id,track_name) VALUES (6,'account');
insert into stm_track (track_id,track_name) VALUES (7,'userdefined 1');
insert into stm_track (track_id,track_name) VALUES (8,'userdefined 2');
insert into stm_track (track_id,track_name) VALUES (9,'userdefined 3');
insert into stm_track (track_id,track_name) VALUES (10,'snmptrap');
-- junos
insert into stm_track (track_id,track_name) VALUES (11,'log count');
insert into stm_track (track_id,track_name) VALUES (12,'count');
insert into stm_track (track_id,track_name) VALUES (13,'log alert');
insert into stm_track (track_id,track_name) VALUES (14,'log alert count');
insert into stm_track (track_id,track_name) VALUES (15,'log alert count alarm');
insert into stm_track (track_id,track_name) VALUES (16,'log count alarm');
insert into stm_track (track_id,track_name) VALUES (17,'count alarm');
-- fortinet:
insert into stm_track (track_id,track_name) VALUES (18,'all');
insert into stm_track (track_id,track_name) VALUES (19,'all start');
insert into stm_track (track_id,track_name) VALUES (20,'utm');
insert into stm_track (track_id,track_name) VALUES (22,'utm start');
-- check point R8x:
insert into stm_track (track_id,track_name) VALUES (21,'network log');

insert into request_type (request_type_id, request_type_name, request_type_comment) VALUES (1, 'ARS', 'Remedy ARS Ticket');

-- insert into stm_track (track_id,track_name) VALUES (13,'count traffic');  -- netscreen: traffic means traffic shaping not logging

insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (1,'Check Point NG','R5x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (2,'Netscreen','5.x','Netscreen', '');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (3,'Netscreen','6.x','Netscreen','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (4,'Check Point NGX','R6x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (5,'phion MC','3.x','phion','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (6,'phion netfence','3.x','phion','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (7,'Check Point','R7x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (8,'JUNOS','10.x','Juniper','any;0;0;65535;;junos-predefined-service;simple;');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (9,'Check Point','R8x','Check Point','');
insert into stm_dev_typ (dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES (10,'Fortinet','5.x','Fortinet','');

update stm_dev_typ set dev_typ_predef_svc=
'ANY;0;0;65535;1;other;simple;
MS-RPC-ANY;;0;0;1;other;rpc;
MS-AD-BR;;0;0;1;other;rpc;
MS-AD-DRSUAPI;;0;0;1;other;rpc;
MS-AD-DSROLE;;0;0;1;other;rpc;
MS-AD-DSSETUP;;0;0;1;other;rpc;
MS-DTC;;0;0;1;other;rpc;
MS-EXCHANGE-DATABASE;;0;0;1;other;rpc;
MS-EXCHANGE-DIRECTORY;;0;0;1;other;rpc;
MS-EXCHANGE-INFO-STORE;;0;0;1;other;rpc;
MS-EXCHANGE-MTA;;0;0;1;other;rpc;
MS-EXCHANGE-STORE;;0;0;1;other;rpc;
MS-EXCHANGE-SYSATD;;0;0;1;other;rpc;
MS-FRS;;0;0;1;other;rpc;
MS-IIS-COM;;0;0;1;other;rpc;
MS-IIS-IMAP4;;0;0;1;other;rpc;
MS-IIS-INETINFO;;0;0;1;other;rpc;
MS-IIS-NNTP;;0;0;1;other;rpc;
MS-IIS-POP3;;0;0;1;other;rpc;
MS-IIS-SMTP;;0;0;1;other;rpc;
MS-ISMSERV;;0;0;1;other;rpc;
MS-MESSENGER;;0;0;30;other;rpc;
MS-MQQM;;0;0;1;other;rpc;
MS-NETLOGON;;0;0;1;other;rpc;
MS-SCHEDULER;;0;0;1;other;rpc;
MS-WIN-DNS;;0;0;1;other;rpc;
MS-WINS;;0;0;1;other;rpc;
SUN-RPC;;0;0;1;other;rpc;
SUN-RPC-ANY;;0;0;1;other;rpc;
SUN-RPC-MOUNTD;;0;0;30;other;rpc;
SUN-RPC-NFS;;0;0;40;other;rpc;
SUN-RPC-NLOCKMGR;;0;0;1;other;rpc;
SUN-RPC-RQUOTAD;;0;0;30;other;rpc;
SUN-RPC-RSTATD;;0;0;30;other;rpc;
SUN-RPC-RUSERD;;0;0;30;other;rpc;
SUN-RPC-SADMIND;;0;0;30;other;rpc;
SUN-RPC-SPRAYD;;0;0;30;other;rpc;
SUN-RPC-STATUS;;0;0;30;other;rpc;
SUN-RPC-WALLD;;0;0;30;other;rpc;
SUN-RPC-YPBIND;;0;0;30;other;rpc;
ICMP Address Mask;1;0;65535;1;other;simple;
ICMP-ANY;1;0;65535;1;other;simple;
ICMP Dest Unreachable;1;0;65535;1;other;simple;
ICMP Fragment Needed;1;0;65535;1;other;simple;
ICMP Fragment Reassembly;1;0;65535;1;other;simple;
ICMP Host Unreachable;1;0;65535;1;other;simple;
ICMP-INFO;1;0;65535;1;other;simple;
ICMP Parameter Problem;1;0;65535;1;other;simple;
ICMP Port Unreachable;1;0;65535;1;other;simple;
ICMP Protocol Unreach;1;0;65535;1;other;simple;
ICMP Redirect;1;0;65535;1;other;simple;
ICMP Redirect Host;1;0;65535;1;other;simple;
ICMP Redirect TOS & Host;1;0;65535;1;other;simple;
ICMP Redirect TOS & Net;1;0;65535;1;other;simple;
ICMP Source Quench;1;0;65535;1;other;simple;
ICMP Source Route Fail;1;0;65535;1;other;simple;
ICMP Time Exceeded;1;0;65535;1;other;simple;
ICMP-TIMESTAMP;1;0;65535;1;other;simple;
PING;1;0;65535;1;other;simple;
TRACEROUTE;1;0;65535;1;other;simple;
AOL;6;5190;5194;30;remote;simple;
BGP;6;179;179;30;other;simple;
FINGER;6;79;79;30;info seeking;simple;
FTP;6;21;21;30;remote;simple;
FTP-Get;6;21;21;30;remote;simple;
FTP-Put;6;21;21;30;remote;simple;
GOPHER;6;70;70;30;info seeking;simple;
H.323;6;1720;1720;30;remote;simple;
HTTP;6;80;80;5;info seeking;simple;
HTTPS;6;443;443;30;security;simple;
IMAP;6;143;143;30;email;simple;
Internet Locator Service;6;389;389;30;info seeking;simple;
IRC;6;6660;6669;30;remote;simple;
LDAP;6;389;389;30;info seeking;simple;
MAIL;6;25;25;30;email;simple;
MSN;6;1863;1863;30;remote;simple;
NetMeeting;6;1720;1720;2160;remote;simple;
NNTP;6;119;119;30;info seeking;simple;
NS Global;6;15397;15397;30;remote;simple;
NS Global PRO;6;15397;15397;30;remote;simple;
POP3;6;110;110;30;email;simple;
PPTP;6;1723;1723;30;security;simple;
Real Media;6;7070;7070;30;info seeking;simple;
RLOGIN;6;513;513;30;remote;simple;
RSH;6;514;514;30;remote;simple;
RTSP;6;554;554;30;info seeking;simple;
SMB;6;139;139;30;remote;simple;
SMTP;6;25;25;30;email;simple;
SQL*Net V1;6;1525;1525;480;other;simple;
SQL*Net V2;6;1521;1521;480;other;simple;
SSH;6;22;22;480;security;simple;
TCP-ANY;6;0;65535;30;other;simple;
TELNET;6;23;23;480;remote;simple;
VDO Live;6;7000;7010;30;info seeking;simple;
WAIS;6;210;210;30;info seeking;simple;
WINFRAME;6;1494;1494;30;remote;simple;
X-WINDOWS;6;6000;6063;30;remote;simple;
YMSG;6;5050;5050;30;remote;simple;
DHCP-Relay;17;67;67;1;info seeking;simple;
DNS;17;53;53;1;info seeking;simple;
GNUTELLA;17;6346;6347;1;remote;simple;
IKE;17;500;500;1;security;simple;
L2TP;17;1701;1701;1;remote;simple;
MS-RPC-EPM;17;135;135;1;remote;simple;
NBNAME;17;137;137;1;remote;simple;
NBDS;17;138;138;1;remote;simple;
NFS;17;111;111;40;remote;simple;
NSM;17;69;69;1;other;simple;
NTP;17;123;123;1;other;simple;
PC-Anywhere;17;5632;5632;1;remote;simple;
RIP;17;520;520;1;other;simple;
SIP;17;5060;5060;1;other;simple;
SNMP;17;161;161;1;other;simple;
SUN-RPC-PORTMAPPER;17;111;111;40;remote;simple;
SYSLOG;17;514;514;1;other;simple;
TALK;17;517;518;1;other;simple;
TFTP;17;69;69;1;remote;simple;
UDP-ANY;17;0;65535;1;other;simple;
UUCP;17;540;540;1;remote;simple;
OSPF;89;0;65535;1;other;simple;
MS-SQL;6;1433;1433;30;other;simple;
LPR;6;515;515;30;other;simple;
REXEC;6;512;512;30;remote;simple;
IDENT;6;113;113;30;other;simple;
SCTP-ANY;132;0;65535;1;other;simple;
GRE;47;0;65535;60;remote;simple;
HTTP;6;80;80;5;info seeking;simple;
MGCP-UA;17;2427;2427;120;other;simple;
GTP;17;2123;2123;30;remote;simple;
MGCP-CA;17;2727;2727;120;other;simple;
WHOIS;6;43;43;30;info seeking;simple;
DISCARD;17;9;9;1;other;simple;
RADIUS;17;1812;1813;1;other;simple;
ECHO;17;7;7;1;other;simple;
VNC;6;5800;5800;30;other;simple;
CHARGEN;17;19;19;1;other;simple;
SQL Monitor;17;1434;1434;1;other;simple;
IKE-NAT;17;500;500;3;security;simple;'
where dev_typ_id=2;

update stm_dev_typ set dev_typ_predef_svc=
'ANY;0;0;65535;1;other;simple
AOL;6;5190;5194;30;remote;simple
APPLE-ICHAT-SNATMAP;17;5678;5678;1;other;simple
BGP;6;179;179;30;other;simple
CHARGEN;17;19;19;1;other;simple
DHCP-Relay;17;67;67;1;info seeking;simple
DISCARD;17;9;9;1;other;simple
DNS;17;53;53;1;info seeking;simple
ECHO;17;7;7;1;other;simple
FINGER;6;79;79;30;info seeking;simple
FTP;6;21;21;30;remote;simple
FTP-Get;6;21;21;30;remote;simple
FTP-Put;6;21;21;30;remote;simple
GNUTELLA;17;6346;6347;1;remote;simple
GOPHER;6;70;70;30;info seeking;simple
GRE;47;0;65535;60;remote;simple
GTP;6;3386;3386;30;remote;simple
H.323;6;1720;1720;30;remote;simple
HTTP;6;80;80;5;info seeking;simple
HTTP-EXT;6;8000;8001;5;info seeking;simple
HTTPS;6;443;443;30;security;simple
ICMP Address Mask;1;0;65535;1;other;simple
ICMP Dest Unreachable;1;0;65535;1;other;simple
ICMP Fragment Needed;1;0;65535;1;other;simple
ICMP Fragment Reassembly;1;0;65535;1;other;simple
ICMP Host Unreachable;1;0;65535;1;other;simple
ICMP Parameter Problem;1;0;65535;1;other;simple
ICMP Port Unreachable;1;0;65535;1;other;simple
ICMP Protocol Unreach;1;0;65535;1;other;simple
ICMP Redirect;1;0;65535;1;other;simple
ICMP Redirect Host;1;0;65535;1;other;simple
ICMP Redirect TOS & Host;1;0;65535;1;other;simple
ICMP Redirect TOS & Net;1;0;65535;1;other;simple
ICMP Source Quench;1;0;65535;1;other;simple
ICMP Source Route Fail;1;0;65535;1;other;simple
ICMP Time Exceeded;1;0;65535;1;other;simple
ICMP-ANY;1;0;65535;1;other;simple
ICMP-INFO;1;0;65535;1;other;simple
ICMP-TIMESTAMP;1;0;65535;1;other;simple
ICMP6 Dst Unreach addr;58;0;65535;30;other;simple
ICMP6 Dst Unreach admin;58;0;65535;30;other;simple
ICMP6 Dst Unreach beyond;58;0;65535;30;other;simple
ICMP6 Dst Unreach port;58;0;65535;30;other;simple
ICMP6 Dst Unreach route;58;0;65535;30;other;simple
ICMP6 Echo Reply;58;0;65535;30;other;simple
ICMP6 Echo Request;58;0;65535;30;other;simple
ICMP6 HAAD Reply;58;0;65535;30;other;simple
ICMP6 HAAD Request;58;0;65535;30;other;simple
ICMP6 MP Advertisement;58;0;65535;30;other;simple
ICMP6 MP Solicitation;58;0;65535;30;other;simple
ICMP6 Packet Too Big;58;0;65535;30;other;simple
ICMP6 Param Prob header;58;0;65535;30;other;simple
ICMP6 Param Prob nexthdr;58;0;65535;30;other;simple
ICMP6 Param Prob option;58;0;65535;30;other;simple
ICMP6 Time Exceed reasse;58;0;65535;30;other;simple
ICMP6 Time Exceed transi;58;0;65535;30;other;simple
ICMP6-ANY;58;0;65535;30;other;simple
IDENT;6;113;113;30;other;simple
IKE;17;500;500;1;security;simple
IKE-NAT;17;500;500;3;security;simple
IMAP;6;143;143;30;email;simple
Internet Locator Service;6;389;389;30;info seeking;simple
IRC;6;6660;6669;30;remote;simple
L2TP;17;1701;1701;1;remote;simple
LDAP;6;389;389;30;info seeking;simple
LPR;6;515;515;30;other;simple
MAIL;6;25;25;30;email;simple
MGCP-CA;17;2727;2727;120;other;simple
MGCP-UA;17;2427;2427;120;other;simple
MS-AD-BR;;;;1;other;rpc
MS-AD-DRSUAPI;;;;1;other;rpc
MS-AD-DSROLE;;;;1;other;rpc
MS-AD-DSSETUP;;;;1;other;rpc
MS-DTC;;;;1;other;rpc
MS-EXCHANGE-DATABASE;;;;30;other;rpc
MS-EXCHANGE-DIRECTORY;;;;30;other;rpc
MS-EXCHANGE-INFO-STORE;;;;30;other;rpc
MS-EXCHANGE-MTA;;;;30;other;rpc
MS-EXCHANGE-STORE;;;;30;other;rpc
MS-EXCHANGE-SYSATD;;;;30;other;rpc
MS-FRS;;;;1;other;rpc
MS-IIS-COM;;;;30;other;rpc
MS-IIS-IMAP4;;;;1;other;rpc
MS-IIS-INETINFO;;;;1;other;rpc
MS-IIS-NNTP;;;;1;other;rpc
MS-IIS-POP3;;;;1;other;rpc
MS-IIS-SMTP;;;;1;other;rpc
MS-ISMSERV;;;;1;other;rpc
MS-MESSENGER;;;;30;other;rpc
MS-MQQM;;;;1;other;rpc
MS-NETLOGON;;;;1;other;rpc
MS-RPC-ANY;;;;1;other;rpc
MS-RPC-EPM;17;135;135;30;remote;simple
MS-SCHEDULER;;;;1;other;rpc
MS-SQL;6;1433;1433;30;other;simple
MS-WIN-DNS;;;;1;other;rpc
MS-WINS;;;;1;other;rpc
MS-WMIC;;;;30;other;rpc
MSN;6;1863;1863;30;remote;simple
NBDS;17;138;138;1;remote;simple
NBNAME;17;137;137;1;remote;simple
NetMeeting;6;1720;1720;30;remote;simple
NFS;17;111;111;40;remote;simple
NNTP;6;119;119;30;info seeking;simple
NS Global;6;15397;15397;30;remote;simple
NS Global PRO;6;15397;15397;30;remote;simple
NSM;17;69;69;1;other;simple
NTP;17;123;123;1;other;simple
OSPF;89;0;65535;1;other;simple
PC-Anywhere;17;5632;5632;1;remote;simple
PING;1;0;65535;1;other;simple
PINGv6;58;0;65535;30;other;simple
POP3;6;110;110;30;email;simple
PPTP;6;1723;1723;30;security;simple
RADIUS;17;1812;1813;1;other;simple
Real Media;6;7070;7070;30;info seeking;simple
REXEC;6;512;512;30;remote;simple
RIP;17;520;520;1;other;simple
RLOGIN;6;513;513;30;remote;simple
RSH;6;514;514;30;remote;simple
RTSP;6;554;554;30;info seeking;simple
SCCP;6;2000;2000;30;other;simple
SCTP-ANY;132;0;65535;1;other;simple
SIP;17;5060;5060;1;other;simple
SMB;6;139;139;30;remote;simple
SMTP;6;25;25;30;email;simple
SNMP;17;161;161;1;other;simple
SQL Monitor;17;1434;1434;1;other;simple
SQL*Net V1;6;1525;1525;30;other;simple
SQL*Net V2;6;1521;1521;30;other;simple
SSH;6;22;22;30;security;simple
SUN-RPC;;;;1;other;rpc
SUN-RPC-ANY;;;;1;other;rpc
SUN-RPC-MOUNTD;;;;30;other;rpc
SUN-RPC-NFS;;;;40;other;rpc
SUN-RPC-NLOCKMGR;;;;1;other;rpc
SUN-RPC-PORTMAPPER;17;111;111;40;remote;simple
SUN-RPC-RQUOTAD;;;;30;other;rpc
SUN-RPC-RSTATD;;;;30;other;rpc
SUN-RPC-RUSERD;;;;30;other;rpc
SUN-RPC-SADMIND;;;;30;other;rpc
SUN-RPC-SPRAYD;;;;30;other;rpc
SUN-RPC-STATUS;;;;30;other;rpc
SUN-RPC-WALLD;;;;30;other;rpc
SUN-RPC-YPBIND;;;;30;other;rpc
SYSLOG;17;514;514;1;other;simple
TALK;17;517;518;1;other;simple
TCP-ANY;6;0;65535;30;other;simple
TELNET;6;23;23;30;remote;simple
TFTP;17;69;69;1;remote;simple
TRACEROUTE;1;0;65535;1;other;simple
UDP-ANY;17;0;65535;1;other;simple
UUCP;17;540;540;1;remote;simple
VDO Live;6;7000;7010;30;info seeking;simple
VNC;6;5800;5800;30;other;simple
WAIS;6;210;210;30;info seeking;simple
WHOIS;6;43;43;30;info seeking;simple
WINFRAME;6;1494;1494;30;remote;simple
X-WINDOWS;6;6000;6063;30;remote;simple
YMSG;6;5050;5050;30;remote;simple
APPLE-ICHAT;;;;;Apple iChat Services Group;group;AOL|APPLE-ICHAT-SNATMAP|DNS|HTTP|HTTPS|SIP
MGCP;;;;;Media Gateway Control Protoc;group;MGCP-CA|MGCP-UA
MS-AD;;;;;Microsoft Active Directory;group;MS-AD-BR|MS-AD-DRSUAPI|MS-AD-DSROLE|MS-AD-DSSETUP
MS-EXCHANGE;;;;;Microsoft Exchange;group;MS-EXCHANGE-DIRECTORY|MS-EXCHANGE-INFO-STORE|MS-EXCHANGE-MTA|MS-EXCHANGE-STORE|MS-EXCHANGE-SYSATD
MS-IIS;;;;;Microsoft IIS Server;group;MS-IIS-COM|MS-IIS-IMAP4|MS-IIS-INETINFO|MS-IIS-NNTP|MS-IIS-POP3|MS-IIS-SMTP
VOIP;;;;;VOIP Service Group;group;H.323|MGCP-CA|MGCP-UA|SCCP|SIP'
where dev_typ_id=3;


SET statement_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SET check_function_bodies = false;
SET client_min_messages = warning;
SET search_path = public, pg_catalog;

-- import
INSERT INTO txt VALUES ('initial_import',		'German',	'Dieses Element ist Teil der initial importierten Konfiguration.');
INSERT INTO txt VALUES ('initial_import', 		'English',	'This entity is part of the initially imported configuration.');

-- navivation
INSERT INTO txt VALUES ('reporting', 			'German',	'Reporting');
INSERT INTO txt VALUES ('reporting', 			'English',	'Reporting');
INSERT INTO txt VALUES ('settings', 			'German',	'Einstellungen');
INSERT INTO txt VALUES ('settings', 			'English',	'Settings');
INSERT INTO txt VALUES ('fworch_long',			'German',	'Firewall-Orchestrator');
INSERT INTO txt VALUES ('fworch_long',			'English',	'Firewall-Orchstrator');
INSERT INTO txt VALUES ('help',					'German',	'Hilfe');
INSERT INTO txt VALUES ('help', 				'English',	'Help');
INSERT INTO txt VALUES ('logout', 				'German',	'Abmelden');
INSERT INTO txt VALUES ('logout', 				'English',	'Logout');
INSERT INTO txt VALUES ('documentation', 		'German',	'Dokumentation');
INSERT INTO txt VALUES ('documentation', 		'English',	'Documentation');
INSERT INTO txt VALUES ('request', 				'German',	'Antrag');
INSERT INTO txt VALUES ('request', 				'English',	'Request');

-- reporting
INSERT INTO txt VALUES ('generate_report',		'German', 	'Report erstellen');
INSERT INTO txt VALUES ('generate_report',		'English', 	'Generate report');
INSERT INTO txt VALUES ('filter', 				'German', 	'Filter');
INSERT INTO txt VALUES ('filter', 				'English', 	'filter');
INSERT INTO txt VALUES ('number', 				'German', 	'Nr.');
INSERT INTO txt VALUES ('number', 				'English', 	'No.');
INSERT INTO txt VALUES ('name', 				'German', 	'Name');
INSERT INTO txt VALUES ('name', 				'English', 	'Name');
INSERT INTO txt VALUES ('source', 				'German', 	'Quelle');
INSERT INTO txt VALUES ('source', 				'English', 	'Ziel');
INSERT INTO txt VALUES ('destination', 			'German', 	'Ziel');
INSERT INTO txt VALUES ('destination', 			'English', 	'Destination');
INSERT INTO txt VALUES ('services', 			'German', 	'Dienste');
INSERT INTO txt VALUES ('services', 			'English', 	'Services');
INSERT INTO txt VALUES ('action', 				'German', 	'Aktion');
INSERT INTO txt VALUES ('action', 				'English', 	'Action');
INSERT INTO txt VALUES ('track', 				'German', 	'Logging');
INSERT INTO txt VALUES ('track', 				'English', 	'Logging');
INSERT INTO txt VALUES ('disabled',				'German', 	'deaktiviert');
INSERT INTO txt VALUES ('disabled',				'English', 	'disabled');
INSERT INTO txt VALUES ('comment',				'German', 	'Kommentar');
INSERT INTO txt VALUES ('comment',				'English', 	'Comment');

-- settings
INSERT INTO txt VALUES ('devices',				'German', 	'Devices');
INSERT INTO txt VALUES ('devices',				'English', 	'Devices');
INSERT INTO txt VALUES ('authorization',		'German', 	'Berechtigungen');
INSERT INTO txt VALUES ('authorization',		'English', 	'Authorization');

INSERT INTO text_msg VALUES ('INITIAL_IMPORT', 'Dieses Element ist Teil der initial importierten Konfiguration.', 'This entity is part of the initially imported configuration.');
INSERT INTO text_msg VALUES ('NON_SECURITY_RELEVANT_CHANGE', 'Keine sicherheitsrelevante Änderung', 'This was a non-security-relevant change.');
INSERT INTO text_msg VALUES ('FACTORY_SETTING', 'Dieses Element ist Teil der initialen Herstellerkonfiguration.', 'This entity is part of the initially factory settings.');
INSERT INTO text_msg VALUES ('IN_OPERATION', 'Dieses Element wurde im laufenden Betrieb veraendert.', 'This entity was changed during normal operation.');
INSERT INTO text_msg VALUES ('DOC_REQUEST_START', 'Bitte dokumentieren Sie', 'Please document');
INSERT INTO text_msg VALUES ('DOC_REQUEST_END', 'offene Änderungen', 'open changes');
INSERT INTO text_msg VALUES ('username', 'Benutzername', 'Username');
INSERT INTO text_msg VALUES ('password', 'Passwort', 'Password');
INSERT INTO text_msg VALUES ('login', 'Anmelden', 'Login');
INSERT INTO text_msg VALUES ('documentation', 'Dokumentation', 'Documentation');
INSERT INTO text_msg VALUES ('surname', 'Nachname', 'Surname');
INSERT INTO text_msg VALUES ('documentation_description', 'Dokumentieren der offenen Änderungen', 'Document open changes');
INSERT INTO text_msg VALUES ('change_documentation_description', 'Bereits vorhandene Dokumentation ändern', 'Change existing documentation');
INSERT INTO text_msg VALUES ('wrong_creds', 'Fehler bei der Anmeldung. Fehlernummer: A001.', 'Login error. Error code: A001.');
INSERT INTO text_msg VALUES ('expired', 'Fehler bei der Anmeldung. Fehlernummer: A002.', 'Login error. Error code: A002.');
INSERT INTO text_msg VALUES ('superuser_login', 'Fehler bei der Anmeldung. Fehlernummer: A003.', 'Login error. Error code: A003.');
INSERT INTO text_msg VALUES ('login_failure_generic', 'Fehler bei der Anmeldung. Fehlernummer: A000.', 'Login error. Error code: A000.');
INSERT INTO text_msg VALUES ('support_description', 'Bei Problemen mit fworch erfahren Sie hier, wie Sie den Support kontaktieren können.', 'In case of problems with fworch you can get the contact details of our support department.');
INSERT INTO text_msg VALUES ('manual', 'Handbuch', 'Manual');
INSERT INTO text_msg VALUES ('logout', 'Abmelden', 'Log Out');
INSERT INTO text_msg VALUES ('reporting_description', 'Hier können Reports über Konfigurations(-änderungen) erstellt werden.', 'You can generate various reports about configuration (changes) here.');
INSERT INTO text_msg VALUES ('manual_description', 'Die Online-Handbuch von fworch.', 'The online manual.');
INSERT INTO text_msg VALUES ('help', 'Hilfe', 'Help');
INSERT INTO text_msg VALUES ('help_description', 'Die kontextsensitive Online-Hilfe.', 'The context-sensitive online help.');
INSERT INTO text_msg VALUES ('logged_in', 'Angemeldet', 'Logged in');
INSERT INTO text_msg VALUES ('no_frames', 'Ihr Browser unterstützt entweder keine Frames oder ist so konfiguriert, dass er keine Frames unterstützt.', 'Your user agent does not support frames or is currently configured not to display frames.');
INSERT INTO text_msg VALUES ('form_send', 'Abschicken', 'Submit');
INSERT INTO text_msg VALUES ('form_reset', 'Zurücksetzen', 'Reset');
INSERT INTO text_msg VALUES ('documentation_title', 'Noch nicht dokumentierte Änderungen', 'Not yet documented changes');
INSERT INTO text_msg VALUES ('change_documentation_title', 'Bereits dokumentierte Änderungen', 'Changes already documented');
INSERT INTO text_msg VALUES ('all', 'Alle', 'all');
INSERT INTO text_msg VALUES ('sanctioned_by', 'Genehmigung durch', 'sanctioned by');
INSERT INTO text_msg VALUES ('request_type', 'Auftragsart', 'request type');
INSERT INTO text_msg VALUES ('request_number', 'Auftragsnummer', 'Request number');
INSERT INTO text_msg VALUES ('comment', 'Kommentar', 'Comment');
INSERT INTO text_msg VALUES ('request_data', 'Auftragsdaten', 'Request data');
INSERT INTO text_msg VALUES ('missing_request_number', 'Bitte mindestens eine Auftragsnummer eingeben.', 'Please enter at least one request number.');
INSERT INTO text_msg VALUES ('missing_request_type', 'Die eingetragenen Auftragsdaten passen nicht. Bitte zu jeder eingetragenen Auftragsnummer einen Auftragstyp auswählen.', 'Mismatch in request data. Please enter a request type for each request number.');
INSERT INTO text_msg VALUES ('missing_tenant_for_request', 'Die eingetragenen Auftragsdaten passen nicht. Bitte zu jeder eingetragenen Auftragsnummer einen Mandanten auswählen.', 'Mismatch in request data. Please enter a tenant name for each request number.');
INSERT INTO text_msg VALUES ('missing_comment', 'Bitte Kommentarfeld ausfüllen.', 'Please fill-in comment field.');
INSERT INTO text_msg VALUES ('exceeded_max_request_number', 'Die Anzahl der Auftragsfelder übersteigt die maximale Anzahl von 9. Bitte in der fworch-Konfiguration (gui.conf) anpassen.', 'Please set  request number to not more than 9 in gui.conf. ');
INSERT INTO text_msg VALUES ('no_change_selected', 'Bitte mindestens eine Änderung auswählen.', 'Please select at least one change.');
INSERT INTO text_msg VALUES ('all_systems', 'Alle Systeme', 'All systems');
INSERT INTO text_msg VALUES ('display_filter', 'Anzeigefilter', 'Display filter');
INSERT INTO text_msg VALUES ('refresh', 'Neu anzeigen', 'Refresh');
INSERT INTO text_msg VALUES ('from', 'von', 'from');
INSERT INTO text_msg VALUES ('to', 'bis', 'to');
INSERT INTO text_msg VALUES ('my_own', 'eigene', 'own');
INSERT INTO text_msg VALUES ('foreign', 'fremde', 'foreign');
INSERT INTO text_msg VALUES ('only_self_documented', 'nur selbstdok.', 'only self-doc.');
INSERT INTO text_msg VALUES ('total', 'insgesamt', 'total');
INSERT INTO text_msg VALUES ('document_filter', 'Dokufilter', 'Doc filter');
INSERT INTO text_msg VALUES ('request', 'Auftrag', 'Request');
INSERT INTO text_msg VALUES ('document_comment', 'Dok-Komm.', 'Doc-comm.');
INSERT INTO text_msg VALUES ('object_comment', 'Obj-Komm.', 'Obj-comm.');
INSERT INTO text_msg VALUES ('first_name', 'Vorname', 'First name');
INSERT INTO text_msg VALUES ('anonymous', 'anonym', 'anonymous');
INSERT INTO text_msg VALUES ('management_filter', 'Management-Filter', 'Management filter');
INSERT INTO text_msg VALUES ('systems', 'Systeme', 'systems');
INSERT INTO text_msg VALUES ('systems_capital', 'Systeme', 'Systems');
INSERT INTO text_msg VALUES ('no_tenant_selected', 'Bitte wählen Sie einen Mandanten aus.', 'Please select a tenant.');
INSERT INTO text_msg VALUES ('please_select', 'Bitte wählen Sie ...', 'Please select ...');
INSERT INTO text_msg VALUES ('no_device_selected', 'Bitte wählen Sie ein Device aus.', 'Please select a device.');
INSERT INTO text_msg VALUES ('select_on_left', 'Bitte links auswählen.', 'Please select on left hand side.');
INSERT INTO text_msg VALUES ('settings', 'Einstellungen', 'Settings');
INSERT INTO text_msg VALUES ('settings_description', 'Änderungen der Einstellungen von fworch.', 'Change the settings of fworch.');
INSERT INTO text_msg VALUES ('user_id', 'Benutzer-ID', 'User ID');
INSERT INTO text_msg VALUES ('tenant', 'Mandant', 'tenant');
INSERT INTO text_msg VALUES ('change_documentation', 'Dokumentation ändern', 'Change Documentation');
INSERT INTO text_msg VALUES ('config_time', 'Konfigurationsstand', 'Configuration state');
INSERT INTO text_msg VALUES ('report_time', 'Zeitpunkt', 'Time of report');
INSERT INTO text_msg VALUES ('of', 'von', 'of');
INSERT INTO text_msg VALUES ('destination', 'Ziel', 'Destination');
INSERT INTO text_msg VALUES ('source', 'Quelle', 'Source');
INSERT INTO text_msg VALUES ('service', 'Dienst', 'Service');
INSERT INTO text_msg VALUES ('show_only_active_rules', 'Nur aktive Regeln anzeigen', 'Show only active rules');
INSERT INTO text_msg VALUES ('special_filters', 'Spezialfilter', 'Special filters');
INSERT INTO text_msg VALUES ('rule_comment', 'Regelkommentar', 'Rule comment');
INSERT INTO text_msg VALUES ('user', 'Benutzer', 'User');
INSERT INTO text_msg VALUES ('ip_protocol', 'IP-Protokoll', 'IP protocol');
INSERT INTO text_msg VALUES ('show_only_rule_objects', 'Nur im Regelsatz vorkommende Objekte anzeigen', 'Show only objects that appear in rules');
INSERT INTO text_msg VALUES ('ip_filter_shows_any_objects', 'IP-Filter zeigt Any-Objekte', 'IP filter shows ''any objects''');
INSERT INTO text_msg VALUES ('ip_filter_shows_negated_rule_parts', 'IP-Filter zeigt negierte Regelanteile', 'IP filter shows negates rule parts');
INSERT INTO text_msg VALUES ('remove_filter', 'Filter entfernen', 'Remove filter');
INSERT INTO text_msg VALUES ('generate_report', 'Report erstellen', 'Generate report');
INSERT INTO text_msg VALUES ('change_password', 'Passwort ändern', 'Change password');
INSERT INTO text_msg VALUES ('change_devices', 'Systeme ändern', 'Change devices');
INSERT INTO text_msg VALUES ('change_tenants', 'Mandanten ändern', 'Change tenants');
INSERT INTO text_msg VALUES ('set_password', 'Passwort setzen', 'Set password');
INSERT INTO text_msg VALUES ('old_password', 'Altes Passwort', 'Old password');
INSERT INTO text_msg VALUES ('-- new_password', 'Neues Passwort', 'New password');
INSERT INTO text_msg VALUES ('new_password_repeat', 'Neues Passwort (Wiederholung)', 'New password (please repeat)');
INSERT INTO text_msg VALUES ('device_settings', 'Einstellungen Systeme', 'Device settings');
INSERT INTO text_msg VALUES ('existing_device', 'Vorhandenes Device', 'Existing device');
INSERT INTO text_msg VALUES ('create_new_device', 'Neues System anlegen', 'Create new device');
INSERT INTO text_msg VALUES ('new_device', 'Neues Device', 'New device');
INSERT INTO text_msg VALUES ('new_management', 'Neues Management', 'New management');
INSERT INTO text_msg VALUES ('device_created', 'Device angelegt am', 'Device created at');
INSERT INTO text_msg VALUES ('device_last_changed', 'Device zuletzt geändert am', 'Device last changed at');
INSERT INTO text_msg VALUES ('import_active', 'Import aktiv', 'Import active');
INSERT INTO text_msg VALUES ('device_type', 'Device Typ', 'Device type');
INSERT INTO text_msg VALUES ('rulebase_name', 'Regelwerksname', 'Rulebase name');
INSERT INTO text_msg VALUES ('yes', 'Ja', 'Yes');
INSERT INTO text_msg VALUES ('no', 'Nein', 'No');
INSERT INTO text_msg VALUES ('change', 'Bearbeiten', 'Edit');
INSERT INTO text_msg VALUES ('management_created', 'Management angelegt am', 'Management created at');
INSERT INTO text_msg VALUES ('management_last_changed', 'Management zuletzt geändert am', 'Management last changed at');
INSERT INTO text_msg VALUES ('save', 'Speichern', 'Save');
INSERT INTO text_msg VALUES ('cancel', 'Abbrechen', 'Cancel');
INSERT INTO text_msg VALUES ('tenant_settings', 'Einstellungen Mandanten', 'tenant settings');
INSERT INTO text_msg VALUES ('existing_tenant_information', 'Existierende Mandanteninformation', 'Existing tenant information');
INSERT INTO text_msg VALUES ('new_tenant', 'Neuer Mandant', 'New tenant');
INSERT INTO text_msg VALUES ('new_ip_network', 'Neues IP-Netzwerk', 'New IP network');
INSERT INTO text_msg VALUES ('create_new_tenants_or_networks', 'Neuen Mandanten oder Netzwerk anlegen', 'Create new tenant or network');
INSERT INTO text_msg VALUES ('tenant_id', 'Mandanten-ID', 'tenant ID');
INSERT INTO text_msg VALUES ('tenant_name', 'Mandantenname', 'tenant name');
INSERT INTO text_msg VALUES ('delete', 'Löschen', 'Delete');
INSERT INTO text_msg VALUES ('tenant_network', 'Mandanten IP-Netzwerk', 'tenant IP network');
INSERT INTO text_msg VALUES ('report_headline_changes', 'Änderungsreport Sicherheitskonfiguration', 'Security Configuration Change Report');
INSERT INTO text_msg VALUES ('report_headline_usage', 'Verwendungsstatistik der Elemente der Sicherheitskonfiguration', 'Statistics Report: Usage of Security Configuration Elements');
INSERT INTO text_msg VALUES ('report_headline_rulesearch', 'Globale Liste von Regeln, die Filterkriterien erfüllen', 'Rulesearch Report');
INSERT INTO text_msg VALUES ('report_headline_configuration', 'Report der Sicherheitskonfiguration', 'Security Configuration Report');
INSERT INTO text_msg VALUES ('report_changes_start_time', 'Startzeitpunkt', 'Start time');
INSERT INTO text_msg VALUES ('report_changes_end_time', 'Endzeitpunkt', 'End time');
INSERT INTO text_msg VALUES ('report_unused_objects', 'Elemente anzeigen, die nicht im Regelwerk vorkommen', 'Display elements that do not occur in rulebase');
INSERT INTO text_msg VALUES ('report_inactive_objects', 'Anzeige von nicht aktiven Konfigurationselementen', 'Display inactive rules');
INSERT INTO text_msg VALUES ('report_generated_by', 'erzeugt mit', 'generated by');
INSERT INTO text_msg VALUES ('report_change_time_of_change', 'Änderungszeitpunkt', 'Time of change');
INSERT INTO text_msg VALUES ('report_generation', 'Reportgenerierung', 'Report generation');
INSERT INTO text_msg VALUES ('report_type', 'Typ', 'Type');
INSERT INTO text_msg VALUES ('report_generation_took', 'dauerte', 'took');
INSERT INTO text_msg VALUES ('seconds', 'Sekunden', 'seconds');
INSERT INTO text_msg VALUES ('dev_hide_in_gui', 'Device in GUI verstecken', 'hide device in GUI');
INSERT INTO text_msg VALUES ('mgm_hide_in_gui', 'Management in GUI verstecken', 'hide management in GUI');
INSERT INTO text_msg VALUES ('password_policy', 'Mind. 9 Zeichen, 1 Sonderzeichen, 1 Ziffer, 1 Buchstabe', 'Min. 9 characters, 1 special char, 1 digit, 1 letter');
INSERT INTO text_msg (text_msg_id, text_msg_ger, text_msg_eng) VALUES ('report_headline_duplicate', 'Doppelt definierte Elemente der Sicherheitskonfiguration', 'Duplicate objects in Security Configuration');

-- UPDATE text_msg SET text_msg_ger='Neues Passwort', text_msg_eng='New password' WHERE text_msg_id='new_password';

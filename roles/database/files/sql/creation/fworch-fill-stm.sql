
-- SET client_encoding=UTF8
-- \encoding UTF8

INSERT INTO language ("name", "culture_info") VALUES('German', 'de-DE');
INSERT INTO language ("name", "culture_info") VALUES('English', 'en-US');

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

insert into parent_rule_type (id, name) VALUES (1, 'section');          -- do not restart numbering
insert into parent_rule_type (id, name) VALUES (2, 'guarded-layer');    -- restart numbering, rule restrictions are ANDed to all rules below it, layer is not entered if guard does not apply
insert into parent_rule_type (id, name) VALUES (3, 'unguarded-layer');  -- restart numbering, no further restrictions

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

-- languages
INSERT INTO txt VALUES ('English', 				'German',	'Englisch');
INSERT INTO txt VALUES ('English', 				'English',	'English');
INSERT INTO txt VALUES ('German', 				'German',	'Deutsch');
INSERT INTO txt VALUES ('German', 				'English',	'German');

-- general
INSERT INTO txt VALUES ('cancel', 				'German',	'Abbrechen');
INSERT INTO txt VALUES ('cancel', 				'English',	'Cancel');
INSERT INTO txt VALUES ('save', 				'German',	'Speichern');
INSERT INTO txt VALUES ('save', 				'English',	'Save');
INSERT INTO txt VALUES ('delete', 				'German',	'Löschen');
INSERT INTO txt VALUES ('delete', 				'English',	'Delete');
INSERT INTO txt VALUES ('clone', 				'German',	'Klonen');
INSERT INTO txt VALUES ('clone', 				'English',	'Clone');
INSERT INTO txt VALUES ('edit', 				'German',	'Bearbeiten');
INSERT INTO txt VALUES ('edit', 				'English',	'Edit');
INSERT INTO txt VALUES ('set', 				    'German',	'Setzen');
INSERT INTO txt VALUES ('set', 				    'English',	'Set');
INSERT INTO txt VALUES ('add', 				    'German',	'Hinzufügen');
INSERT INTO txt VALUES ('add', 				    'English',	'Add');
INSERT INTO txt VALUES ('search', 				'German',	'Suchen');
INSERT INTO txt VALUES ('search', 			    'English',	'Search');
INSERT INTO txt VALUES ('load', 				'German',	'Laden');
INSERT INTO txt VALUES ('load', 			    'English',	'Load');
INSERT INTO txt VALUES ('ok', 				    'German',	'Ok');
INSERT INTO txt VALUES ('ok', 			        'English',	'Ok');
INSERT INTO txt VALUES ('close', 				'German',	'Schliessen');
INSERT INTO txt VALUES ('close', 			    'English',	'Close');

-- login
INSERT INTO txt VALUES ('login', 				'German',	'Anmelden');
INSERT INTO txt VALUES ('login', 				'English',	'Login');
INSERT INTO txt VALUES ('username', 			'German',	'Nutzername');
INSERT INTO txt VALUES ('username', 			'English',	'Username');
INSERT INTO txt VALUES ('password', 			'German',	'Passwort');
INSERT INTO txt VALUES ('password', 			'English',	'Password');
INSERT INTO txt VALUES ('change_password', 		'German',	'Passwort ändern');
INSERT INTO txt VALUES ('change_password', 		'English',	'Change Password');
INSERT INTO txt VALUES ('old_password', 		'German',	'Altes Passwort');
INSERT INTO txt VALUES ('old_password', 		'English',	'Old Password');
INSERT INTO txt VALUES ('new_password', 		'German',	'Neues Passwort');
INSERT INTO txt VALUES ('new_password', 		'English',	'New Password');

-- navigation
INSERT INTO txt VALUES ('reporting', 			'German',	'Reporting');
INSERT INTO txt VALUES ('reporting', 			'English',	'Reporting');
INSERT INTO txt VALUES ('settings', 			'German',	'Einstellungen');
INSERT INTO txt VALUES ('settings', 			'English',	'Settings');
INSERT INTO txt VALUES ('fworch_long',			'German',	'Firewall-Orchestrator');
INSERT INTO txt VALUES ('fworch_long',			'English',	'Firewall-Orchestrator');
INSERT INTO txt VALUES ('help',					'German',	'Hilfe');
INSERT INTO txt VALUES ('help', 				'English',	'Help');
INSERT INTO txt VALUES ('logout', 				'German',	'Abmelden');
INSERT INTO txt VALUES ('logout', 				'English',	'Logout');
INSERT INTO txt VALUES ('documentation', 		'German',	'Dokumentation');
INSERT INTO txt VALUES ('documentation', 		'English',	'Documentation');
INSERT INTO txt VALUES ('request', 				'German',	'Antrag');
INSERT INTO txt VALUES ('request', 				'English',	'Request');
INSERT INTO txt VALUES ('scheduling', 			'German',	'Terminplanung');
INSERT INTO txt VALUES ('scheduling', 			'English',	'Scheduling');
INSERT INTO txt VALUES ('archive', 				'German',	'Archiv');
INSERT INTO txt VALUES ('archive', 				'English',	'Archive');

-- reporting
INSERT INTO txt VALUES ('select_device',		'German', 	'Device(s) auswählen');
INSERT INTO txt VALUES ('select_device',		'English', 	'Select device(s)');
INSERT INTO txt VALUES ('select_all',		    'German', 	'Alle auswählen');
INSERT INTO txt VALUES ('select_all',		    'English', 	'Select all');
INSERT INTO txt VALUES ('clear_all',		    'German', 	'Auswahl leeren');
INSERT INTO txt VALUES ('clear_all',		    'English', 	'Clear all');
INSERT INTO txt VALUES ('generate_report',		'German', 	'Report erstellen');
INSERT INTO txt VALUES ('generate_report',		'English', 	'Generate report');
INSERT INTO txt VALUES ('export_report',        'German', 	'Report exportieren');
INSERT INTO txt VALUES ('export_report',        'English', 	'Export Report');
INSERT INTO txt VALUES ('export_as',            'German', 	'Exportieren als...');
INSERT INTO txt VALUES ('export_as',            'English', 	'Export as...');
INSERT INTO txt VALUES ('export',               'German', 	'Exportieren');
INSERT INTO txt VALUES ('export',               'English', 	'Export');
INSERT INTO txt VALUES ('export_report_download','German', 	'Exportierten Report herunterladen');
INSERT INTO txt VALUES ('export_report_download','English', 'Export Report Download');
INSERT INTO txt VALUES ('download_csv',		    'German', 	'als CSV herunterladen');
INSERT INTO txt VALUES ('download_csv',		    'English', 	'Download CSV');
INSERT INTO txt VALUES ('download_pdf',		    'German', 	'als PDF herunterladen');
INSERT INTO txt VALUES ('download_pdf',		    'English', 	'Download PDF');
INSERT INTO txt VALUES ('download_html',		'German', 	'als HTML herunterladen');
INSERT INTO txt VALUES ('download_html',		'English', 	'Download HTML');
INSERT INTO txt VALUES ('download_json',		'German', 	'als JSON herunterladen');
INSERT INTO txt VALUES ('download_json',		'English', 	'Download JSON');
INSERT INTO txt VALUES ('save_as_template',		'German', 	'Als Vorlage speichern');
INSERT INTO txt VALUES ('save_as_template',		'English', 	'Save as Template');
INSERT INTO txt VALUES ('no_device_selected',	'German', 	'Kein Device ausgewählt.');
INSERT INTO txt VALUES ('no_device_selected',	'English', 	'No device(s) selected.');
INSERT INTO txt VALUES ('filter', 				'German', 	'Filter');
INSERT INTO txt VALUES ('filter', 				'English', 	'filter');
INSERT INTO txt VALUES ('number', 				'German', 	'Nr.');
INSERT INTO txt VALUES ('number', 				'English', 	'No.');
INSERT INTO txt VALUES ('name', 				'German', 	'Name');
INSERT INTO txt VALUES ('name', 				'English', 	'Name');
INSERT INTO txt VALUES ('source', 				'German', 	'Quelle');
INSERT INTO txt VALUES ('source', 				'English', 	'Source');
INSERT INTO txt VALUES ('destination', 			'German', 	'Ziel');
INSERT INTO txt VALUES ('destination', 			'English', 	'Destination');
INSERT INTO txt VALUES ('services', 			'German', 	'Dienste');
INSERT INTO txt VALUES ('services', 			'English', 	'Services');
INSERT INTO txt VALUES ('action', 				'German', 	'Aktion');
INSERT INTO txt VALUES ('action', 				'English', 	'Action');
INSERT INTO txt VALUES ('track', 				'German', 	'Logging');
INSERT INTO txt VALUES ('track', 				'English', 	'Logging');
INSERT INTO txt VALUES ('disabled',				'German', 	'Deaktiviert');
INSERT INTO txt VALUES ('disabled',				'English', 	'Disabled');
INSERT INTO txt VALUES ('comment',				'German', 	'Kommentar');
INSERT INTO txt VALUES ('comment',				'English', 	'Comment');
INSERT INTO txt VALUES ('templates',			'German', 	'Vorlagen');
INSERT INTO txt VALUES ('templates',			'English', 	'Templates');
INSERT INTO txt VALUES ('creation_date',		'German', 	'Erstelldatum');
INSERT INTO txt VALUES ('creation_date',		'English', 	'Creation Date');
INSERT INTO txt VALUES ('report_template',		'German', 	'Reportvorlage');
INSERT INTO txt VALUES ('report_template',		'English', 	'Report Template');
INSERT INTO txt VALUES ('glob_no_obj',		    'German', 	'Gesamtzahl der Objekte');
INSERT INTO txt VALUES ('glob_no_obj',		    'English', 	'Global number of Objects');
INSERT INTO txt VALUES ('total_no_obj_mgt',		'German', 	'Gesamtzahl der Objekte pro Management');
INSERT INTO txt VALUES ('total_no_obj_mgt',		'English', 	'Total number of Objects per Management');
INSERT INTO txt VALUES ('no_rules_gtw',		    'German', 	'Anzahl Regeln pro Gateway');
INSERT INTO txt VALUES ('no_rules_gtw',		    'English', 	'Number of Rules per Gateway');
INSERT INTO txt VALUES ('network_objects',		'German', 	'Netzwerkobjekte');
INSERT INTO txt VALUES ('network_objects',		'English', 	'Network objects');
INSERT INTO txt VALUES ('service_objects',		'German', 	'Serviceobjekte');
INSERT INTO txt VALUES ('service_objects',		'English', 	'Service objects');
INSERT INTO txt VALUES ('user_objects',		    'German', 	'Nutzerobjekte');
INSERT INTO txt VALUES ('user_objects',		    'English', 	'User objects');
INSERT INTO txt VALUES ('rules',		        'German', 	'Regeln');
INSERT INTO txt VALUES ('rules',		        'English', 	'Rules');
INSERT INTO txt VALUES ('no_of_rules',		    'German', 	'Anzahl Regeln');
INSERT INTO txt VALUES ('no_of_rules',		    'English', 	'Number of Rules');
INSERT INTO txt VALUES ('collapse_all',		    'German', 	'Alles einklappen');
INSERT INTO txt VALUES ('collapse_all',		    'English', 	'Collapse all');
INSERT INTO txt VALUES ('all',		            'German', 	'Alle');
INSERT INTO txt VALUES ('all',		            'English', 	'All');
INSERT INTO txt VALUES ('rule',		            'German', 	'Regel');
INSERT INTO txt VALUES ('rule',		            'English', 	'Rule');
INSERT INTO txt VALUES ('objects',		        'German', 	'Objekt');
INSERT INTO txt VALUES ('objects',		        'English', 	'Objects');
INSERT INTO txt VALUES ('change_time',		    'German', 	'Änderungszeit');
INSERT INTO txt VALUES ('change_time',		    'English', 	'Change Time');
INSERT INTO txt VALUES ('change_type',		    'German', 	'Änderungstyp');
INSERT INTO txt VALUES ('change_type',		    'English', 	'Change Type');
INSERT INTO txt VALUES ('source_zone',		    'German', 	'Quellzone');
INSERT INTO txt VALUES ('source_zone',		    'English', 	'Source Zone');
INSERT INTO txt VALUES ('destination_zone',		'German', 	'Zielzone');
INSERT INTO txt VALUES ('destination_zone',		'English', 	'Destination Zone');
INSERT INTO txt VALUES ('enabled',		        'German', 	'Aktiviert');
INSERT INTO txt VALUES ('enabled',		        'English', 	'Enabled');
INSERT INTO txt VALUES ('uid',		            'German', 	'UID');
INSERT INTO txt VALUES ('uid',		            'English', 	'UID');
INSERT INTO txt VALUES ('created',		        'German', 	'Angelegt');
INSERT INTO txt VALUES ('created',		        'English', 	'Created');
INSERT INTO txt VALUES ('last_modified',		'German', 	'Zuletzt geändert');
INSERT INTO txt VALUES ('last_modified',		'English', 	'Last Modified');
INSERT INTO txt VALUES ('first_hit',		    'German', 	'Erster Treffer');
INSERT INTO txt VALUES ('first_hit',		    'English', 	'First Hit');
INSERT INTO txt VALUES ('last_hit',		        'German', 	'Letzter Treffer');
INSERT INTO txt VALUES ('last_hit',		        'English', 	'Last Hit');
INSERT INTO txt VALUES ('ip',		            'German', 	'IP');
INSERT INTO txt VALUES ('ip',		            'English', 	'IP');
INSERT INTO txt VALUES ('zone',		            'German', 	'Zone');
INSERT INTO txt VALUES ('zone',		            'English', 	'Zone');
INSERT INTO txt VALUES ('last_changed',		    'German', 	'Zuletzt geändert');
INSERT INTO txt VALUES ('last_changed',		    'English', 	'Last changed');
INSERT INTO txt VALUES ('source_port',		    'German', 	'Quellport');
INSERT INTO txt VALUES ('source_port',		    'English', 	'Source Port');
INSERT INTO txt VALUES ('destination_port',		'German', 	'Zielport');
INSERT INTO txt VALUES ('destination_port',	    'English', 	'Destination Port');
INSERT INTO txt VALUES ('protocol',		        'German', 	'Protokoll');
INSERT INTO txt VALUES ('protocol',		        'English', 	'Protocol');
INSERT INTO txt VALUES ('code',		            'German', 	'Code');
INSERT INTO txt VALUES ('code',		            'English', 	'Code');
INSERT INTO txt VALUES ('timeout',		        'German', 	'Timeout');
INSERT INTO txt VALUES ('timeout',		        'English', 	'Timeout');
INSERT INTO txt VALUES ('real_name',		    'German', 	'Realer Name');
INSERT INTO txt VALUES ('real_name',		    'English', 	'Real Name');
INSERT INTO txt VALUES ('group_members',		'German', 	'Gruppenmitglieder');
INSERT INTO txt VALUES ('group_members',		'English', 	'Group Members');
INSERT INTO txt VALUES ('group_members_flat',	'German', 	'Gruppenmitglieder (geglättet)');
INSERT INTO txt VALUES ('group_members_flat',	'English', 	'Group Members (flattened)');

-- settings
INSERT INTO txt VALUES ('devices',				'German', 	'Geräte');
INSERT INTO txt VALUES ('devices',				'English', 	'Devices');
INSERT INTO txt VALUES ('managements',			'German', 	'Managements');
INSERT INTO txt VALUES ('managements',			'English', 	'Managements');
INSERT INTO txt VALUES ('gateways',		    	'German', 	'Gateways');
INSERT INTO txt VALUES ('gateways',		    	'English', 	'Gateways');
INSERT INTO txt VALUES ('import_status',       	'German', 	'Import Status');
INSERT INTO txt VALUES ('import_status',    	'English', 	'Import Status');
INSERT INTO txt VALUES ('authorization',		'German', 	'Berechtigungen');
INSERT INTO txt VALUES ('authorization',		'English', 	'Authorization');
INSERT INTO txt VALUES ('ldap_conns',	        'German', 	'LDAP-Verbindungen');
INSERT INTO txt VALUES ('ldap_conns',	        'English', 	'LDAP Connections');
INSERT INTO txt VALUES ('tenants',		        'German', 	'Mandanten');
INSERT INTO txt VALUES ('tenants',		        'English', 	'Tenants');
INSERT INTO txt VALUES ('users',		        'German', 	'Nutzer');
INSERT INTO txt VALUES ('users',		        'English', 	'Users');
INSERT INTO txt VALUES ('groups',		        'German', 	'Gruppen');
INSERT INTO txt VALUES ('groups',		        'English', 	'Groups');
INSERT INTO txt VALUES ('roles',		        'German', 	'Rollen');
INSERT INTO txt VALUES ('roles',		        'English', 	'Roles');
INSERT INTO txt VALUES ('defaults',		        'German', 	'Voreinstellungen');
INSERT INTO txt VALUES ('defaults',		        'English', 	'Defaults');
INSERT INTO txt VALUES ('standards',		    'German', 	'Standards');
INSERT INTO txt VALUES ('standards',		    'English', 	'Defaults');
INSERT INTO txt VALUES ('password_policy',      'German', 	'Passworteinstellungen');
INSERT INTO txt VALUES ('password_policy',      'English', 	'Password Policy');
INSERT INTO txt VALUES ('personal',             'German', 	'Persönlich');
INSERT INTO txt VALUES ('personal',             'English', 	'Personal');
INSERT INTO txt VALUES ('language',             'German', 	'Sprache');
INSERT INTO txt VALUES ('language',             'English', 	'Language');
INSERT INTO txt VALUES ('add_new_management',   'German', 	'Neues Management hinzufügen');
INSERT INTO txt VALUES ('add_new_management',   'English', 	'Add new management');
INSERT INTO txt VALUES ('edit_management',      'German', 	'Management bearbeiten');
INSERT INTO txt VALUES ('edit_management',      'English', 	'Edit Management');
INSERT INTO txt VALUES ('host',                 'German', 	'Host');
INSERT INTO txt VALUES ('host',                 'English', 	'Host');
INSERT INTO txt VALUES ('hostname',             'German', 	'Hostname');
INSERT INTO txt VALUES ('hostname',             'English', 	'Hostname');
INSERT INTO txt VALUES ('config_path',          'German', 	'Konfigurationspfad');
INSERT INTO txt VALUES ('config_path',          'English', 	'Config Path');
INSERT INTO txt VALUES ('importer_host',        'German', 	'Importer Host');
INSERT INTO txt VALUES ('importer_host',        'English', 	'Importer Host');
INSERT INTO txt VALUES ('import_disabled',      'German', 	'Import Deaktiviert');
INSERT INTO txt VALUES ('import_disabled',      'English', 	'Import Disabled');
INSERT INTO txt VALUES ('debug_level',          'German', 	'Debug Stufe');
INSERT INTO txt VALUES ('debug_level',          'English', 	'Debug Level');
INSERT INTO txt VALUES ('device_type',          'German', 	'Gerätetyp');
INSERT INTO txt VALUES ('device_type',          'English', 	'Device Type');
INSERT INTO txt VALUES ('port',                 'German', 	'Port');
INSERT INTO txt VALUES ('port',                 'English', 	'Port');
INSERT INTO txt VALUES ('import_user',          'German', 	'Import Nutzer');
INSERT INTO txt VALUES ('import_user',          'English', 	'Import User');
INSERT INTO txt VALUES ('login_secret',         'German', 	'Privater Schlüssel');
INSERT INTO txt VALUES ('login_secret',         'English', 	'Login Secret');
INSERT INTO txt VALUES ('public_key',           'German', 	'Öffentlicher Schlüssel');
INSERT INTO txt VALUES ('public_key',           'English', 	'Public Key');
INSERT INTO txt VALUES ('force_initial_import', 'German', 	'Initialen Import erzwingen');
INSERT INTO txt VALUES ('force_initial_import', 'English', 	'Force Initial Import');
INSERT INTO txt VALUES ('hide_in_ui',           'German', 	'Nicht sichtbar');
INSERT INTO txt VALUES ('hide_in_ui',           'English', 	'Hide in UI');
INSERT INTO txt VALUES ('add_new_gateway',      'German', 	'Neues Gateway hinzufügen');
INSERT INTO txt VALUES ('add_new_gateway',      'English', 	'Add new gateway');
INSERT INTO txt VALUES ('edit_gateway',         'German', 	'Gateway bearbeiten');
INSERT INTO txt VALUES ('edit_gateway',         'English', 	'Edit Gateway');
INSERT INTO txt VALUES ('management',           'German', 	'Management');
INSERT INTO txt VALUES ('management',           'English', 	'Management');
INSERT INTO txt VALUES ('rulebase',             'German', 	'Rulebase');
INSERT INTO txt VALUES ('rulebase',             'English', 	'Rulebase');
INSERT INTO txt VALUES ('details',              'German', 	'Details');
INSERT INTO txt VALUES ('details',              'English', 	'Details');
INSERT INTO txt VALUES ('import_status_details','German', 	'Import Status Details für ');
INSERT INTO txt VALUES ('import_status_details','English', 	'Import Status Details for ');
INSERT INTO txt VALUES ('last_incomplete',      'German', 	'Letzter Unvollendeter');
INSERT INTO txt VALUES ('last_incomplete',      'English', 	'Last Incomplete');
INSERT INTO txt VALUES ('rollback',             'German', 	'Zurücksetzen');
INSERT INTO txt VALUES ('rollback',             'English', 	'Rollback');
INSERT INTO txt VALUES ('last_success',         'German', 	'Letzter Erfolg');
INSERT INTO txt VALUES ('last_success',         'English', 	'Last Success');
INSERT INTO txt VALUES ('last_import',          'German', 	'Letzter Import');
INSERT INTO txt VALUES ('last_import',          'English', 	'Last Import');
INSERT INTO txt VALUES ('success',              'German', 	'Erfolg');
INSERT INTO txt VALUES ('success',              'English', 	'Success');
INSERT INTO txt VALUES ('errors',               'German', 	'Fehler');
INSERT INTO txt VALUES ('errors',               'English', 	'Errors');
INSERT INTO txt VALUES ('mgm_name',             'German', 	'MgmName');
INSERT INTO txt VALUES ('mgm_name',             'English', 	'MgmName');
INSERT INTO txt VALUES ('start',                'German', 	'Start');
INSERT INTO txt VALUES ('start',                'English', 	'Start');
INSERT INTO txt VALUES ('stop',                 'German', 	'Stop');
INSERT INTO txt VALUES ('stop',                 'English', 	'Stop');
INSERT INTO txt VALUES ('remove_sample_data',   'German', 	'Beispieldaten löschen');
INSERT INTO txt VALUES ('remove_sample_data',	'English', 	'Remove Sample Data');
INSERT INTO txt VALUES ('refresh', 				'German',	'Neu anzeigen');
INSERT INTO txt VALUES ('refresh', 				'English',	'Refresh');
INSERT INTO txt VALUES ('add_new_user',			'German',	'Neuen Nutzer hinzufügen');
INSERT INTO txt VALUES ('add_new_user',			'English',	'Add new user');
INSERT INTO txt VALUES ('edit_user',		    'German',	'Nutzer bearbeiten');
INSERT INTO txt VALUES ('edit_user',		    'English',	'Edit User');
INSERT INTO txt VALUES ('reset_password',       'German', 	'Passwort zurücksetzen');
INSERT INTO txt VALUES ('reset_password',       'English', 	'Reset Password');
INSERT INTO txt VALUES ('add_new_group',		'German',	'Neue Gruppe hinzufügen');
INSERT INTO txt VALUES ('add_new_group',		'English',	'Add New Group');
INSERT INTO txt VALUES ('edit_group',		    'German',	'Gruppe bearbeiten');
INSERT INTO txt VALUES ('edit_group',		    'English',	'Edit Group');
INSERT INTO txt VALUES ('delete_group',		    'German',	'Gruppe löschen');
INSERT INTO txt VALUES ('delete_group',		    'English',	'Delete Group');
INSERT INTO txt VALUES ('add_user_to_group',	'German',	'Nutzer zu Gruppe hinzufügen');
INSERT INTO txt VALUES ('add_user_to_group',	'English',	'Add user to group');
INSERT INTO txt VALUES ('delete_user_from_group','German',	'Nutzer von Gruppe löschen');
INSERT INTO txt VALUES ('delete_user_from_group','English',	'Delete user from group');
INSERT INTO txt VALUES ('add_user_to_role',	    'German',	'Nutzer/Gruppe zu Rolle hinzufügen');
INSERT INTO txt VALUES ('add_user_to_role',	    'English',	'Add user/group to role');
INSERT INTO txt VALUES ('delete_user_from_role','German',	'Nutzer/Gruppe von Rolle löschen');
INSERT INTO txt VALUES ('delete_user_from_role','English',	'Delete user/group from role');
INSERT INTO txt VALUES ('get_user_from_ldap',   'German',	'Nutzer von LDAP holen');
INSERT INTO txt VALUES ('get_user_from_ldap',   'English',	'Get user from LDAP');
INSERT INTO txt VALUES ('delete_user',          'German', 	'Nutzer löschen');
INSERT INTO txt VALUES ('delete_user',          'English', 	'Delete user');
INSERT INTO txt VALUES ('active_user',          'German', 	'Aktiver Nutzer');
INSERT INTO txt VALUES ('active_user',          'English', 	'Active User');
INSERT INTO txt VALUES ('user',                 'German', 	'Nutzer');
INSERT INTO txt VALUES ('user',                 'English', 	'User');
INSERT INTO txt VALUES ('group',                'German', 	'Gruppe');
INSERT INTO txt VALUES ('group',                'English', 	'Group');
INSERT INTO txt VALUES ('from_ldap',            'German', 	'von LDAP');
INSERT INTO txt VALUES ('from_ldap',            'English', 	'from LDAP');
INSERT INTO txt VALUES ('search_pattern',       'German', 	'Suchmuster');
INSERT INTO txt VALUES ('search_pattern',       'English', 	'Search Pattern');
INSERT INTO txt VALUES ('new_dn',               'German', 	'Neu (Dn)');
INSERT INTO txt VALUES ('new_dn',               'English', 	'New (Dn)');
INSERT INTO txt VALUES ('user_group',           'German', 	'Nutzer/Gruppe');
INSERT INTO txt VALUES ('user_group',           'English', 	'User/Group');
INSERT INTO txt VALUES ('add_gateway',          'German', 	'Gateway hinzufügen');
INSERT INTO txt VALUES ('add_gateway',          'English', 	'Add Gateway');
INSERT INTO txt VALUES ('delete_gateway',       'German', 	'Gateway löschen');
INSERT INTO txt VALUES ('delete_gateway',       'English', 	'Delete Gateway');
INSERT INTO txt VALUES ('gateway',              'German', 	'Gateway');
INSERT INTO txt VALUES ('gateway',              'English', 	'Gateway');
INSERT INTO txt VALUES ('add_new_ldap',         'German', 	'Neue LDAP-Verbindung hinzufügen');
INSERT INTO txt VALUES ('add_new_ldap',         'English', 	'Add new LDAP connection');
INSERT INTO txt VALUES ('edit_ldap',            'German', 	'LDAP-Verbindung bearbeiten');
INSERT INTO txt VALUES ('edit_ldap',            'English', 	'Edit LDAP connection');
INSERT INTO txt VALUES ('address',              'German', 	'Adresse');
INSERT INTO txt VALUES ('address',              'English', 	'Address');
INSERT INTO txt VALUES ('tenant_level',         'German', 	'Mandantenebene');
INSERT INTO txt VALUES ('tenant_level',         'English', 	'Tenant Level');
INSERT INTO txt VALUES ('type',                 'German', 	'Typ');
INSERT INTO txt VALUES ('type',                 'English', 	'Type');
INSERT INTO txt VALUES ('pattern_length',       'German', 	'Musterlänge');
INSERT INTO txt VALUES ('pattern_length',       'English', 	'Pattern Length');
INSERT INTO txt VALUES ('user_search_path',     'German', 	'Suchpfad Nutzer');
INSERT INTO txt VALUES ('user_search_path',     'English', 	'User Search Path');
INSERT INTO txt VALUES ('role_search_path',     'German', 	'Suchpfad Rollen');
INSERT INTO txt VALUES ('role_search_path',     'English', 	'Role Search Path');
INSERT INTO txt VALUES ('group_search_path',    'German', 	'Suchpfad Gruppen');
INSERT INTO txt VALUES ('group_search_path',    'English', 	'Group Search Path');
INSERT INTO txt VALUES ('search_user',          'German', 	'Nutzer für Suche');
INSERT INTO txt VALUES ('search_user',          'English', 	'Search User');
INSERT INTO txt VALUES ('search_user_pwd',      'German', 	'Passwort Nutzer für Suche');
INSERT INTO txt VALUES ('search_user_pwd',      'English', 	'Search User Pwd');
INSERT INTO txt VALUES ('write_user',           'German', 	'Schreibender Nutzer');
INSERT INTO txt VALUES ('write_user',           'English', 	'Write User');
INSERT INTO txt VALUES ('write_user_pwd',       'German', 	'Passwort Schreibender Nutzer');
INSERT INTO txt VALUES ('write_user_pwd',       'English', 	'Write User Pwd');
INSERT INTO txt VALUES ('tenant',               'German', 	'Mandant');
INSERT INTO txt VALUES ('tenant',               'English', 	'Tenant');
INSERT INTO txt VALUES ('min_length',           'German', 	'Mindestlänge');
INSERT INTO txt VALUES ('min_length',           'English', 	'Min Length');
INSERT INTO txt VALUES ('upper_case_req',       'German', 	'Grossbuchstaben enthalten');
INSERT INTO txt VALUES ('upper_case_req',       'English', 	'Upper Case Required');
INSERT INTO txt VALUES ('lower_case_req',       'German', 	'Kleinbuchstaben enthalten');
INSERT INTO txt VALUES ('lower_case_req',       'English', 	'Lower Case Required');
INSERT INTO txt VALUES ('number_req',           'German', 	'Ziffern enthalten');
INSERT INTO txt VALUES ('number_req',           'English', 	'Number Required');
INSERT INTO txt VALUES ('spec_char_req',        'German', 	'Sonderzeichen enthalten (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('spec_char_req',        'English', 	'Special Characters Required (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('default_language',     'German', 	'Standardsprache');
INSERT INTO txt VALUES ('default_language',     'English', 	'Default Language');
INSERT INTO txt VALUES ('elements_per_fetch',   'German', 	'Pro Abruf geholte Elemente');
INSERT INTO txt VALUES ('elements_per_fetch',   'English', 	'Elements per fetch');
INSERT INTO txt VALUES ('max_init_fetch_rsb',   'German', 	'Max initiale Abrufe rechte Randleiste');
INSERT INTO txt VALUES ('max_init_fetch_rsb',   'English', 	'Max initial fetches right sidebar');
INSERT INTO txt VALUES ('auto_fill_rsb',        'German', 	'Komplettes Füllen rechte Randleiste');
INSERT INTO txt VALUES ('auto_fill_rsb',        'English', 	'Completely auto-fill right sidebar');
INSERT INTO txt VALUES ('data_retention_time',  'German', 	'Datenaufbewahrungszeit (in Tagen)');
INSERT INTO txt VALUES ('data_retention_time',  'English', 	'Data retention time (in days)');
INSERT INTO txt VALUES ('import_sleep_time',    'German', 	'Import Intervall (in Sekunden)');
INSERT INTO txt VALUES ('import_sleep_time',    'English', 	'Import sleep time (in seconds)');
INSERT INTO txt VALUES ('language_settings',    'German', 	'Spracheinstellungen');
INSERT INTO txt VALUES ('language_settings',    'English', 	'Language Settings');
INSERT INTO txt VALUES ('apply_changes',        'German', 	'Änderungen anwenden');
INSERT INTO txt VALUES ('apply_changes',        'English', 	'Apply Changes');
INSERT INTO txt VALUES ('description',          'German', 	'Beschreibung');
INSERT INTO txt VALUES ('description',          'English', 	'Description');
INSERT INTO txt VALUES ('users_groups',         'German', 	'Nutzer/Gruppen');
INSERT INTO txt VALUES ('users_groups',         'English', 	'Users/Groups');
INSERT INTO txt VALUES ('user_action',          'German', 	'Nutzeraktion');
INSERT INTO txt VALUES ('user_action',          'English', 	'User Action');
INSERT INTO txt VALUES ('group_action',         'German', 	'Gruppenaktion');
INSERT INTO txt VALUES ('group_action',         'English', 	'Group Action');
INSERT INTO txt VALUES ('last_login',           'German', 	'Letzte Anmeldung');
INSERT INTO txt VALUES ('last_login',           'English', 	'Last Login');
INSERT INTO txt VALUES ('last_pw_change',       'German', 	'Letzte Passwortänderung');
INSERT INTO txt VALUES ('last_pw_change',       'English', 	'Last Pwd Change');
INSERT INTO txt VALUES ('pwd_chg_req',          'German', 	'PW Änd. erf.');
INSERT INTO txt VALUES ('pwd_chg_req',          'English', 	'Pwd Chg Req');
INSERT INTO txt VALUES ('project',              'German', 	'Projekt');
INSERT INTO txt VALUES ('project',              'English', 	'Project');
INSERT INTO txt VALUES ('view_all_devices',     'German', 	'Sicht auf alle Geräte');
INSERT INTO txt VALUES ('view_all_devices',     'English', 	'View All Devices');
INSERT INTO txt VALUES ('role_handling',        'German', 	'Rollenverwaltung');
INSERT INTO txt VALUES ('role_handling',        'English', 	'Role handling');
INSERT INTO txt VALUES ('group_handling',       'German', 	'Gruppenverwaltung');
INSERT INTO txt VALUES ('group_handling',       'English', 	'Group handling');

-- message titles
INSERT INTO txt VALUES ('unspecified_error',    'German', 	'Nicht spezifizierter Fehler');
INSERT INTO txt VALUES ('unspecified_error',    'English', 	'Unspecified Error');
INSERT INTO txt VALUES ('jwt_expiry',           'German', 	'JWT abgelaufen');
INSERT INTO txt VALUES ('jwt_expiry',           'English', 	'JWT Expiry');
INSERT INTO txt VALUES ('api_access',           'German', 	'Zugang zur API');
INSERT INTO txt VALUES ('api_access',           'English', 	'API access');
INSERT INTO txt VALUES ('object_fetch',         'German', 	'Abholen der Objekte');
INSERT INTO txt VALUES ('object_fetch',         'English', 	'Object Fetch');
INSERT INTO txt VALUES ('template_fetch',       'German', 	'Abholen der Vorlagen');
INSERT INTO txt VALUES ('template_fetch',       'English', 	'Report Template Fetch');
INSERT INTO txt VALUES ('save_template',        'German', 	'Speichern der Vorlage');
INSERT INTO txt VALUES ('save_template',        'English', 	'Save Report Template');
INSERT INTO txt VALUES ('edit_template',        'German', 	'Ändern der Vorlage');
INSERT INTO txt VALUES ('edit_template',        'English', 	'Edit Report Template');
INSERT INTO txt VALUES ('delete_template',      'German', 	'Löschen der Vorlage');
INSERT INTO txt VALUES ('delete_template',      'English', 	'Delete Report Template');
INSERT INTO txt VALUES ('read_config',          'German', 	'Lesen der Konfiguration');
INSERT INTO txt VALUES ('read_config',          'English', 	'Read Config');
INSERT INTO txt VALUES ('change_default',       'German', 	'Ändern der Voreinstellungen');
INSERT INTO txt VALUES ('change_default',       'English', 	'Change Default Settings');
INSERT INTO txt VALUES ('change_policy',        'German', 	'Ändern der Passworteinstellungen');
INSERT INTO txt VALUES ('change_policy',        'English', 	'Change Password Policy');
INSERT INTO txt VALUES ('ui_user',              'German', 	'Nutzerverwaltung');
INSERT INTO txt VALUES ('ui_user',              'English', 	'UI user handling');
INSERT INTO txt VALUES ('add_user',             'German', 	'Nutzer hinzufügen');
INSERT INTO txt VALUES ('add_user',             'English', 	'Add user');
INSERT INTO txt VALUES ('update_user',          'German', 	'Nutzer ändern');
INSERT INTO txt VALUES ('update_user',          'English', 	'Update user');
INSERT INTO txt VALUES ('save_user',            'German', 	'Nutzer in LDAP speichern');
INSERT INTO txt VALUES ('save_user',            'English', 	'Save user in LDAP');
INSERT INTO txt VALUES ('delete_user_ldap',     'German', 	'Nutzer in LDAP löschen');
INSERT INTO txt VALUES ('delete_user_ldap',     'English', 	'Delete user in LDAP');
INSERT INTO txt VALUES ('delete_user_local',    'German', 	'Nutzer lokal löschen');
INSERT INTO txt VALUES ('delete_user_local',    'English', 	'Delete user locally');
INSERT INTO txt VALUES ('fetch_groups',         'German', 	'Gruppen abholen');
INSERT INTO txt VALUES ('fetch_groups',         'English', 	'Fetch Groups');
INSERT INTO txt VALUES ('fetch_users',          'German', 	'Nutzer abholen');
INSERT INTO txt VALUES ('fetch_users',          'English', 	'Fetch Users');
INSERT INTO txt VALUES ('save_group',           'German', 	'Gruppe in LDAP speichern');
INSERT INTO txt VALUES ('save_group',           'English', 	'Save group in LDAP');
INSERT INTO txt VALUES ('fetch_roles',          'German', 	'Rollen abholen');
INSERT INTO txt VALUES ('fetch_roles',          'English', 	'Fetch Roles');
INSERT INTO txt VALUES ('fetch_ldap_conn',      'German', 	'LDAP-Verbindungen abholen');
INSERT INTO txt VALUES ('fetch_ldap_conn',      'English', 	'Fetch LDAP connections');
INSERT INTO txt VALUES ('search_users',         'German', 	'Nutzer suchen');
INSERT INTO txt VALUES ('search_users',         'English', 	'Search Users');
INSERT INTO txt VALUES ('new_user',             'German', 	'Neuer Nutzer');
INSERT INTO txt VALUES ('new_user',             'English', 	'New User');
INSERT INTO txt VALUES ('get_tenant_data',      'German', 	'Mandantendaten abholen');
INSERT INTO txt VALUES ('get_tenant_data',      'English', 	'Get tenant data');
INSERT INTO txt VALUES ('add_device_to_tenant', 'German', 	'Gateway dem Mandanten zuordnen');
INSERT INTO txt VALUES ('add_device_to_tenant', 'English', 	'Add gateway to tenant');
INSERT INTO txt VALUES ('delete_device_from_tenant','German','Device vom Mandanten löschen');
INSERT INTO txt VALUES ('delete_device_from_tenant','English','Delete device from tenant');
INSERT INTO txt VALUES ('delete_ldap_conn',     'German',   'LDAP-Verbindung löschen');
INSERT INTO txt VALUES ('delete_ldap_conn',     'English',  'Delete Ldap Connection');
INSERT INTO txt VALUES ('save_ldap_conn',       'German',   'LDAP-Verbindung speichern');
INSERT INTO txt VALUES ('save_ldap_conn',       'English',  'Save Ldap Connection');
INSERT INTO txt VALUES ('fetch_managements',    'German', 	'Managements abholen');
INSERT INTO txt VALUES ('fetch_managements',    'English', 	'Fetch Managements');
INSERT INTO txt VALUES ('delete_management',    'German', 	'Management löschen');
INSERT INTO txt VALUES ('delete_management',    'English', 	'Delete Management');
INSERT INTO txt VALUES ('save_management',      'German', 	'Management speichern');
INSERT INTO txt VALUES ('save_management',      'English', 	'Save Management');
INSERT INTO txt VALUES ('fetch_gateways',       'German', 	'Gateways abholen');
INSERT INTO txt VALUES ('fetch_gateways',       'English', 	'Fetch Gateways');
INSERT INTO txt VALUES ('save_gateway',         'German', 	'Gateway speichern');
INSERT INTO txt VALUES ('save_gateway',         'English', 	'Save Gateway');
INSERT INTO txt VALUES ('add_device_to_tenant0','German', 	'Gerät zu Mandant 0 zuordnen');
INSERT INTO txt VALUES ('add_device_to_tenant0','English', 	'Add device to tenant 0');
INSERT INTO txt VALUES ('fetch_import_status',  'German', 	'Import Status abholen');
INSERT INTO txt VALUES ('fetch_import_status',  'English', 	'Fetch Import Status');
INSERT INTO txt VALUES ('rollback_import',      'German', 	'Import zurücksetzen');
INSERT INTO txt VALUES ('rollback_import',      'English', 	'Rollback Import');
INSERT INTO txt VALUES ('report_settings',      'German', 	'Reporteinstellungen');
INSERT INTO txt VALUES ('report_settings',      'English', 	'Report Settings');
INSERT INTO txt VALUES ('change_language',      'German', 	'Ändern der Passworteinstellungen');
INSERT INTO txt VALUES ('change_language',      'English', 	'Change Language');


-- user messages (0-999: General, 1000-1999: Reporting, 5000-5999: Settings)
INSERT INTO txt VALUES ('U1002', 'German',  'Sind sie sicher, dass sie folgende Reportvorlage löschen wollen: ');
INSERT INTO txt VALUES ('U1002', 'English', 'Do you really want to delete report template');

INSERT INTO txt VALUES ('U5001', 'German',  'Bitte eine Einstellung auswählen.');
INSERT INTO txt VALUES ('U5001', 'English', 'Please choose a setting.');
INSERT INTO txt VALUES ('U5101', 'German',  'Sind sie sicher, dass sie folgendes Management löschen wollen: ');
INSERT INTO txt VALUES ('U5101', 'English', 'Are you sure you want to delete management: ');
INSERT INTO txt VALUES ('U5102', 'German',  'Löscht alle Beispielmanagements (auf "_demo" endend) und alle zugeordneten Gateways');
INSERT INTO txt VALUES ('U5102', 'English', 'Deletes all sample managements (ending with "_demo") and related Gateways');
INSERT INTO txt VALUES ('U5103', 'German',  'Sind sie sicher, dass sie folgendes Gateway löschen wollen: ');
INSERT INTO txt VALUES ('U5103', 'English', 'Are you sure you want to delete gateway: ');

INSERT INTO txt VALUES ('U5201', 'German',  'Sind sie sicher, dass sie folgenden Nutzer löschen wollen: ');
INSERT INTO txt VALUES ('U5201', 'English', 'Are you sure you want to delete user: ');
INSERT INTO txt VALUES ('U5202', 'German',  ' Nutzer von externen LDAPs werden nur lokal gelöscht.');
INSERT INTO txt VALUES ('U5202', 'English', ' Users from external LDAPs are only deleted locally.');
INSERT INTO txt VALUES ('U5203', 'German',  'Löscht alle Beispielnutzer (auf "_demo" endend) im lokalen LDAP.');
INSERT INTO txt VALUES ('U5203', 'English', 'Deletes all sample users (ending with "_demo") in local LDAP.');
INSERT INTO txt VALUES ('U5204', 'German',  'Sind sie sicher, dass sie folgende Gruppe löschen wollen: ');
INSERT INTO txt VALUES ('U5204', 'English', 'Are you sure you want to delete group: ');
INSERT INTO txt VALUES ('U5205', 'German',  'Löscht alle Beispielgruppen (auf "_demo" endend) im lokalen LDAP.');
INSERT INTO txt VALUES ('U5205', 'English', 'Deletes all sample groups (ending with "_demo") in local LDAP.');
INSERT INTO txt VALUES ('U5206', 'German',  '(es können keine Nutzer zugeordnet werden)');
INSERT INTO txt VALUES ('U5206', 'English', '(no users can be assigned)');
INSERT INTO txt VALUES ('U5207', 'German',  '(zu allen Gateways zugeordnet)');
INSERT INTO txt VALUES ('U5207', 'English', '(linked to all gateways)');

INSERT INTO txt VALUES ('U5301', 'German',  'Einstellungen geändert.');
INSERT INTO txt VALUES ('U5301', 'English', 'Settings changed.');
INSERT INTO txt VALUES ('U5302', 'German',  'Einstellungen geändert.');
INSERT INTO txt VALUES ('U5302', 'English', 'Policy changed.');
INSERT INTO txt VALUES ('U5401', 'German',  'Passwort geändert.');
INSERT INTO txt VALUES ('U5401', 'English', 'Password changed.');

-- error messages (1-999: General, 1000-1999: Reporting, 5000-5999: Settings)
INSERT INTO txt VALUES ('E0001', 'German',  'Nicht klassifizierter Fehler: ');
INSERT INTO txt VALUES ('E0001', 'English', 'Unclassified error: ');
INSERT INTO txt VALUES ('E0002', 'German',  'Für Details in den Log-Dateien nachsehen!');
INSERT INTO txt VALUES ('E0002', 'English', 'See log for details!');
INSERT INTO txt VALUES ('E0003', 'German',  'JWT in Sitzung abgelaufen - bitte erneut anmelden');
INSERT INTO txt VALUES ('E0003', 'English', 'JWT expired in session - please log in again');
INSERT INTO txt VALUES ('E0004', 'German',  'Ungenügende Zugriffsrechte');
INSERT INTO txt VALUES ('E0004', 'English', 'Insufficient access rights');
INSERT INTO txt VALUES ('E0011', 'German',  'Gültiger Nutzer aber keine Rolle zugeordnet. Bitte Administrator kontaktieren');
INSERT INTO txt VALUES ('E0011', 'English', 'Valid user but no role assigned. Please contact administrator');

INSERT INTO txt VALUES ('E1001', 'German',  'Vor dem Generieren des Reports bitte mindestens ein Device auf der linken Seite auswählen');
INSERT INTO txt VALUES ('E1001', 'English', 'Please select at least one device in the left side-bar before generating a report');
INSERT INTO txt VALUES ('E1002', 'German',  'Kein Report vorhanden zum exportieren. Bitte zuerst Report generieren!');
INSERT INTO txt VALUES ('E1002', 'English', 'No generated report to export. Please generate report first!');

INSERT INTO txt VALUES ('E5101', 'German',  'Löschen des Managements nicht erlaubt, da noch Gateways zugeordnet sind. Diese zuerst löschen wenn möglich');
INSERT INTO txt VALUES ('E5101', 'English', 'Deletion of management not allowed as there are related Gateways. Delete them first if possible');
INSERT INTO txt VALUES ('E5102', 'German',  'Bitte alle Pflichtfelder ausfüllen');
INSERT INTO txt VALUES ('E5102', 'English', 'Please fill all mandatory fields');
INSERT INTO txt VALUES ('E5103', 'German',  'Port muss im Bereich 1 - 65535 liegen');
INSERT INTO txt VALUES ('E5103', 'English', 'Port has to be in the range 1 - 65535');
INSERT INTO txt VALUES ('E5104', 'German',  'Wert der Debug Stufe muss im Bereich 0 - 9 liegen');
INSERT INTO txt VALUES ('E5104', 'English', 'Value for Debug Level has to be in the range 0 - 9');
INSERT INTO txt VALUES ('E5105', 'German',  'Es gibt bereits ein Management mit derselben Konfiguration und Import aktiviert');
INSERT INTO txt VALUES ('E5105', 'English', 'There is already a management in the same configuration with import enabled');
INSERT INTO txt VALUES ('E5111', 'German',  'Es gibt bereits ein Gateway mit derselben Konfiguration und Import aktiviert');
INSERT INTO txt VALUES ('E5111', 'English', 'There is already a gateway in the same configuration with import enabled');

INSERT INTO txt VALUES ('E5201', 'German',  'Fehler beim lesen der LDAP Verbindungen von der API');
INSERT INTO txt VALUES ('E5201', 'English', 'Error while fetching LDAP connections from API');
INSERT INTO txt VALUES ('E5202', 'German',  'Fehler beim lesen der Nutzer vom internen LDAP');
INSERT INTO txt VALUES ('E5202', 'English', 'Error while fetching users from internal LDAP');
INSERT INTO txt VALUES ('E5203', 'German',  'Fehler beim lesen der Nutzer von der API');
INSERT INTO txt VALUES ('E5203', 'English', 'Error while fetching users from API');
INSERT INTO txt VALUES ('E5204', 'German',  'Fehler beim lesen der Mandanten von der API');
INSERT INTO txt VALUES ('E5204', 'English', 'Error while fetching tenants from API');
INSERT INTO txt VALUES ('E5205', 'German',  'Fehler beim ändern des Nutzers über die API');
INSERT INTO txt VALUES ('E5205', 'English', 'Error while updating user data via API');
INSERT INTO txt VALUES ('E5206', 'German',  'Fehler beim hinzufügen des Nutzers über die API');
INSERT INTO txt VALUES ('E5206', 'English', 'Error while adding user data via API');
INSERT INTO txt VALUES ('E5207', 'German',  'kein internes LDAP gefunden');
INSERT INTO txt VALUES ('E5207', 'English', 'No internal LDAP found');
INSERT INTO txt VALUES ('E5208', 'German',  'Keine Nutzer gefunden');
INSERT INTO txt VALUES ('E5208', 'English', 'No users found');
INSERT INTO txt VALUES ('E5209', 'German',  'Nutzer konnte in der Datenbank nicht geändert werden');
INSERT INTO txt VALUES ('E5209', 'English', 'User could not be updated in database');
INSERT INTO txt VALUES ('E5210', 'German',  'Nutzer konnte nicht hinzugefügt werden');
INSERT INTO txt VALUES ('E5210', 'English', 'User could not be added');
INSERT INTO txt VALUES ('E5211', 'German',  'Dn und Passwort müssen gefüllt sein');
INSERT INTO txt VALUES ('E5211', 'English', 'Dn and Password have to be filled');
INSERT INTO txt VALUES ('E5212', 'German',  'Unbekannter Mandant');
INSERT INTO txt VALUES ('E5212', 'English', 'Unknown tenant');
INSERT INTO txt VALUES ('E5213', 'German',  'Nutzer konnte zum LDAP nicht hinzugefügt werden');
INSERT INTO txt VALUES ('E5213', 'English', 'No user could be added to LDAP');
INSERT INTO txt VALUES ('E5214', 'German',  'Nutzer konnte im LDAP nicht geändert werden');
INSERT INTO txt VALUES ('E5214', 'English', 'User could not be updated in LDAP');
INSERT INTO txt VALUES ('E5215', 'German',  'Löschen des eigenen Nutzers nicht erlaubt');
INSERT INTO txt VALUES ('E5215', 'English', 'Self deletion of user not allowed');
INSERT INTO txt VALUES ('E5216', 'German',  'Nutzer konnte im LDAP nicht gelöscht werden');
INSERT INTO txt VALUES ('E5216', 'English', 'User could not be deleted in LDAP');
INSERT INTO txt VALUES ('E5217', 'German',  'Passwort kann nur für interne Nutzer zurückgesetzt werden');
INSERT INTO txt VALUES ('E5217', 'English', 'Password can only be reset for internal users');
INSERT INTO txt VALUES ('E5218', 'German',  'Passwort muss ausgefüllt werden');
INSERT INTO txt VALUES ('E5218', 'English', 'Password has to be filled');
INSERT INTO txt VALUES ('E5219', 'German',  'Passwort konnte nicht geändert werden');
INSERT INTO txt VALUES ('E5219', 'English', 'Password could not be updated');
INSERT INTO txt VALUES ('E5220', 'German',  'Sie sind als Beispielnutzer angemeldet. Löschen ist nicht möglich');
INSERT INTO txt VALUES ('E5220', 'English', 'You are logged in as sample user. Delete not possible');
INSERT INTO txt VALUES ('E5221', 'German',  'Nutzer konnte nicht von allen Rollen und Gruppen entfernt werden');
INSERT INTO txt VALUES ('E5221', 'English', 'User could not be removed from all roles and groups');
INSERT INTO txt VALUES ('E5231', 'German',  'Keine Gruppen gefunden');
INSERT INTO txt VALUES ('E5231', 'English', 'No groups found');
INSERT INTO txt VALUES ('E5232', 'German',  'Fehler beim holen der LDAP Gruppen');
INSERT INTO txt VALUES ('E5232', 'English', 'Error while fetching LDAP groups');
INSERT INTO txt VALUES ('E5233', 'German',  'Fehler beim holen der Nutzer von der API');
INSERT INTO txt VALUES ('E5233', 'English', 'Error while fetching users from API');
INSERT INTO txt VALUES ('E5234', 'German',  'Name muss ausgefüllt sein');
INSERT INTO txt VALUES ('E5234', 'English', 'Name has to be filled');
INSERT INTO txt VALUES ('E5235', 'German',  'Name ist schon vorhanden');
INSERT INTO txt VALUES ('E5235', 'English', 'Name is already existing');
INSERT INTO txt VALUES ('E5236', 'German',  'Es konnte keine Gruppe hinzugefügt werden');
INSERT INTO txt VALUES ('E5236', 'English', 'No group could be added');
INSERT INTO txt VALUES ('E5237', 'German',  'Gruppe konnte nicht geändert werden');
INSERT INTO txt VALUES ('E5237', 'English', 'Group could not be updated');
INSERT INTO txt VALUES ('E5238', 'German',  'Löschen der Gruppe nicht erlaubt, da noch Nutzer zugewiesen sind');
INSERT INTO txt VALUES ('E5238', 'English', 'Deletion of group not allowed as there are still users assigned');
INSERT INTO txt VALUES ('E5239', 'German',  'Gruppe konnte nicht gelöscht werden');
INSERT INTO txt VALUES ('E5239', 'English', 'Group could not be deleted');
INSERT INTO txt VALUES ('E5240', 'German',  'Bitte einen Nutzer auswählen');
INSERT INTO txt VALUES ('E5240', 'English', 'Please select a user');
INSERT INTO txt VALUES ('E5241', 'German',  'Nutzer ist schon zu dieser Gruppe zugeordnet');
INSERT INTO txt VALUES ('E5241', 'English', 'User is already assigned to this group');
INSERT INTO txt VALUES ('E5242', 'German',  'Nutzer konnte nicht zur Gruppe im LDAP zugeordnet werden');
INSERT INTO txt VALUES ('E5242', 'English', 'User could not be added to group in LDAP');
INSERT INTO txt VALUES ('E5243', 'German',  'Nutzer konnte von keiner Gruppe in den LDAPs gelöscht werden');
INSERT INTO txt VALUES ('E5243', 'English', 'User could not be removed from any group in LDAPs');
INSERT INTO txt VALUES ('E5244', 'German',  'Zu löschender Nutzer nicht gefunden');
INSERT INTO txt VALUES ('E5244', 'English', 'User to delete not found');
INSERT INTO txt VALUES ('E5245', 'German',  'Nicht-Beispielnutzer zur Gruppe zugeordnet. Löschen nicht möglich');
INSERT INTO txt VALUES ('E5245', 'English', 'Non-sample user assigned to group. Delete not possible');
INSERT INTO txt VALUES ('E5251', 'German',  'Fehler: keine Rollen gefunden');
INSERT INTO txt VALUES ('E5251', 'English', 'Error: no roles found');
INSERT INTO txt VALUES ('E5252', 'German',  'Bitte nutzen sie ein Suchmuster mit Mindestlänge ');
INSERT INTO txt VALUES ('E5252', 'English', 'Please use pattern of min length ');
INSERT INTO txt VALUES ('E5253', 'German',  'Bitte einen richtigen Nutzer definieren');
INSERT INTO txt VALUES ('E5253', 'English', 'Please define a proper user');
INSERT INTO txt VALUES ('E5254', 'German',  'Nutzer ist dieser Rolle schon zugewiesen');
INSERT INTO txt VALUES ('E5254', 'English', 'User is already assigned to this role');
INSERT INTO txt VALUES ('E5255', 'German',  'Nutzer konnte der Rolle im LDAP nicht zugewiesen werden');
INSERT INTO txt VALUES ('E5255', 'English', 'User could not be added to role in LDAP');
INSERT INTO txt VALUES ('E5256', 'German',  'Der letzte Admin kann nicht gelöscht werden');
INSERT INTO txt VALUES ('E5256', 'English', 'Last admin user cannot be deleted');
INSERT INTO txt VALUES ('E5257', 'German',  'Nutzer konnte von keiner Rolle in den LDAPs gelöscht werden');
INSERT INTO txt VALUES ('E5257', 'English', 'User could not be removed from any role in ldaps');
INSERT INTO txt VALUES ('E5258', 'German',  'Zu löschender Nutzer nicht gefunden');
INSERT INTO txt VALUES ('E5258', 'English', 'User to delete not found');
INSERT INTO txt VALUES ('E5261', 'German',  'Löschen der LDAP-Verbindung nicht erlaubt, da sie die letzte ist');
INSERT INTO txt VALUES ('E5261', 'English', 'Deletion of LDAP Connection not allowed as it is the last one');
INSERT INTO txt VALUES ('E5262', 'German',  'Löschen der LDAP-Verbindung nicht erlaubt, da sie einen Rollensuchpfad enthält. Wenn möglich diesen zuerst löschen');
INSERT INTO txt VALUES ('E5262', 'English', 'Deletion of LDAP Connection not allowed as it contains role search path. Delete it first if possible');
INSERT INTO txt VALUES ('E5263', 'German',  'Musterlänge muss >= 0 sein');
INSERT INTO txt VALUES ('E5263', 'English', 'Pattern Length has to be >= 0');
INSERT INTO txt VALUES ('E5264', 'German',  'Es gibt bereits eine LDAP-Verbindung mit derselben Adresse und Port');
INSERT INTO txt VALUES ('E5264', 'English', 'There is already an LDAP connection with the same address and port');

INSERT INTO txt VALUES ('E5301', 'German',  'Konfiguration für defaultLanguage konnte nicht gelesen werden: Wert auf English gesetzt');
INSERT INTO txt VALUES ('E5301', 'English', 'Error reading Config for defaultLanguage: taking default English');
INSERT INTO txt VALUES ('E5302', 'German',  'Konfiguration für elementsPerFetch konnte nicht gelesen werden: Wert auf 100 gesetzt');
INSERT INTO txt VALUES ('E5302', 'English', 'Error reading Config for elementsPerFetch: taking value 100');
INSERT INTO txt VALUES ('E5303', 'German',  'Konfiguration für maxInitFetch konnte nicht gelesen werden: Wert auf 10 gesetzt');
INSERT INTO txt VALUES ('E5303', 'English', 'Error reading Config for maxInitFetch: taking value 10');
INSERT INTO txt VALUES ('E5304', 'German',  'Konfiguration für AutoFillRightSidebar konnte nicht gelesen werden: Wert auf false gesetzt');
INSERT INTO txt VALUES ('E5304', 'English', 'Error reading Config for AutoFillRightSidebar: taking value false');
INSERT INTO txt VALUES ('E5305', 'German',  'Konfiguration für dataRetentionTime konnte nicht gelesen werden: Wert auf 731 gesetzt');
INSERT INTO txt VALUES ('E5305', 'English', 'Error reading Config for dataRetentionTime: taking value 731');
INSERT INTO txt VALUES ('E5306', 'German',  'Konfiguration für importSleepTime konnte nicht gelesen werden: Wert auf 40 gesetzt');
INSERT INTO txt VALUES ('E5306', 'English', 'Error reading Config for importSleepTime: taking value 40');
INSERT INTO txt VALUES ('E5311', 'German',  'Konfiguration für minLength konnte nicht gelesen werden: Wert auf 10 gesetzt');
INSERT INTO txt VALUES ('E5311', 'English', 'Error reading Config for minLength: taking value 10');
INSERT INTO txt VALUES ('E5312', 'German',  'Konfiguration für UpperCaseRequired konnte nicht gelesen werden: Wert auf false gesetzt');
INSERT INTO txt VALUES ('E5312', 'English', 'Error reading Config for UpperCaseRequired: taking value false');
INSERT INTO txt VALUES ('E5313', 'German',  'Konfiguration für LowerCaseRequired konnte nicht gelesen werden: Wert auf false gesetzt');
INSERT INTO txt VALUES ('E5313', 'English', 'Error reading Config for LowerCaseRequired: taking value false');
INSERT INTO txt VALUES ('E5314', 'German',  'Konfiguration für NumberRequired konnte nicht gelesen werden: Wert auf false gesetzt');
INSERT INTO txt VALUES ('E5314', 'English', 'Error reading Config for NumberRequired: taking value false');
INSERT INTO txt VALUES ('E5315', 'German',  'Konfiguration für SpecialCharactersRequired konnte nicht gelesen werden: Wert auf false gesetzt');
INSERT INTO txt VALUES ('E5315', 'English', 'Error reading Config for SpecialCharactersRequired: taking value false');

INSERT INTO txt VALUES ('E5401', 'German',  'Bitte das alte Passwort eintragen');
INSERT INTO txt VALUES ('E5401', 'English', 'Please insert the old password');
INSERT INTO txt VALUES ('E5402', 'German',  'Bitte ein neues Passwort eintragen');
INSERT INTO txt VALUES ('E5402', 'English', 'Please insert a new password');
INSERT INTO txt VALUES ('E5403', 'German',  'Das neue Passwort muss sich vom alten unterscheiden');
INSERT INTO txt VALUES ('E5403', 'English', 'New password must differ from old one');
INSERT INTO txt VALUES ('E5404', 'German',  'Bitte das neue Passwort wiederholen');
INSERT INTO txt VALUES ('E5404', 'English', 'Please insert the same new password twice');
INSERT INTO txt VALUES ('E5411', 'German',  'Passwort erfordert eine Mindestlänge von ');
INSERT INTO txt VALUES ('E5411', 'English', 'Password must have a minimal length of ');
INSERT INTO txt VALUES ('E5412', 'German',  'Passwort muss mindestens einen Grossbuchstaben enthalten');
INSERT INTO txt VALUES ('E5412', 'English', 'Password must contain at least one upper case character');
INSERT INTO txt VALUES ('E5413', 'German',  'Passwort muss mindestens einen Kleinbuchstaben enthalten');
INSERT INTO txt VALUES ('E5413', 'English', 'Password must contain at least one lower case character');
INSERT INTO txt VALUES ('E5414', 'German',  'Passwort muss mindestens eine Ziffer enthalten');
INSERT INTO txt VALUES ('E5414', 'English', 'Password must contain at least one number');
INSERT INTO txt VALUES ('E5415', 'German',  'Passwort muss mindestens ein Sonderzeichen enthalten (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('E5415', 'English', 'Password must contain at least one special character (!?(){}=~$%&#*-+.,_)');
INSERT INTO txt VALUES ('E5421', 'German',  'Schlüssel nicht gefunden oder Wert nicht konvertierbar: Wert wird auf 100 gesetzt');
INSERT INTO txt VALUES ('E5421', 'English', 'Key not found or could not convert value to int: taking value 100');
INSERT INTO txt VALUES ('E5422', 'German',  'Fehler beim speichern der Reporteinstellungen');
INSERT INTO txt VALUES ('E5422', 'English', 'Error while saving report settings');


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

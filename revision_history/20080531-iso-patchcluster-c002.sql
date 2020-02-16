-- $Id: 20080531-iso-patchcluster-c002.sql,v 1.1.2.1 2008-06-02 17:04:15 tim Exp $
-- $Source: /home/cvs/iso/package/install/migration/Attic/20080531-iso-patchcluster-c002.sql,v $
-- this is a collection from 20070821 to 20080531

# die folgenden Dateien müssen aktualisiert werden:
# der gesamte $ISOHOME/importer-Zweig
# der gesamte $ISOHOME/web-Zweig

insert into stm_action (action_id,action_name) VALUES (19,'actionlocalredirect');

-- adds the following predefined services which are new in ScreenOS 5.4:
-- MS-SQL, LPR, REXEC, IDENT, SCTP-ANY, GRE, HTTP, MGCP-UA, GTP, MGCP-CA, WHOIS, DISCARD, RADIUS, ECHO, VNC, CHARGEN, SQL Monitor, IKE-NAT

update stm_dev_typ set dev_typ_predef_svc='ANY;0;0;65535;1;other;simple
MS-RPC-ANY;;0;0;1;other;rpc
MS-AD-BR;;0;0;1;other;rpc
MS-AD-DRSUAPI;;0;0;1;other;rpc
MS-AD-DSROLE;;0;0;1;other;rpc
MS-AD-DSSETUP;;0;0;1;other;rpc
MS-DTC;;0;0;1;other;rpc
MS-EXCHANGE-DATABASE;;0;0;1;other;rpc
MS-EXCHANGE-DIRECTORY;;0;0;1;other;rpc
MS-EXCHANGE-INFO-STORE;;0;0;1;other;rpc
MS-EXCHANGE-MTA;;0;0;1;other;rpc
MS-EXCHANGE-STORE;;0;0;1;other;rpc
MS-EXCHANGE-SYSATD;;0;0;1;other;rpc
MS-FRS;;0;0;1;other;rpc
MS-IIS-COM;;0;0;1;other;rpc
MS-IIS-IMAP4;;0;0;1;other;rpc
MS-IIS-INETINFO;;0;0;1;other;rpc
MS-IIS-NNTP;;0;0;1;other;rpc
MS-IIS-POP3;;0;0;1;other;rpc
MS-IIS-SMTP;;0;0;1;other;rpc
MS-ISMSERV;;0;0;1;other;rpc
MS-MESSENGER;;0;0;30;other;rpc
MS-MQQM;;0;0;1;other;rpc
MS-NETLOGON;;0;0;1;other;rpc
MS-SCHEDULER;;0;0;1;other;rpc
MS-WIN-DNS;;0;0;1;other;rpc
MS-WINS;;0;0;1;other;rpc
SUN-RPC;;0;0;1;other;rpc
SUN-RPC-ANY;;0;0;1;other;rpc
SUN-RPC-MOUNTD;;0;0;30;other;rpc
SUN-RPC-NFS;;0;0;40;other;rpc
SUN-RPC-NLOCKMGR;;0;0;1;other;rpc
SUN-RPC-RQUOTAD;;0;0;30;other;rpc
SUN-RPC-RSTATD;;0;0;30;other;rpc
SUN-RPC-RUSERD;;0;0;30;other;rpc
SUN-RPC-SADMIND;;0;0;30;other;rpc
SUN-RPC-SPRAYD;;0;0;30;other;rpc
SUN-RPC-STATUS;;0;0;30;other;rpc
SUN-RPC-WALLD;;0;0;30;other;rpc
SUN-RPC-YPBIND;;0;0;30;other;rpc
ICMP Address Mask;1;0;65535;1;other;simple
ICMP-ANY;1;0;65535;1;other;simple
ICMP Dest Unreachable;1;0;65535;1;other;simple
ICMP Fragment Needed;1;0;65535;1;other;simple
ICMP Fragment Reassembly;1;0;65535;1;other;simple
ICMP Host Unreachable;1;0;65535;1;other;simple
ICMP-INFO;1;0;65535;1;other;simple
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
ICMP-TIMESTAMP;1;0;65535;1;other;simple
PING;1;0;65535;1;other;simple
TRACEROUTE;1;0;65535;1;other;simple
AOL;6;5190;5194;30;remote;simple
BGP;6;179;179;30;other;simple
FINGER;6;79;79;30;info seeking;simple
FTP;6;21;21;30;remote;simple
FTP-Get;6;21;21;30;remote;simple
FTP-Put;6;21;21;30;remote;simple
GOPHER;6;70;70;30;info seeking;simple
H.323;6;1720;1720;30;remote;simple
HTTP;6;80;80;5;info seeking;simple
HTTPS;6;443;443;30;security;simple
IMAP;6;143;143;30;email;simple
Internet Locator Service;6;389;389;30;info seeking;simple
IRC;6;6660;6669;30;remote;simple
LDAP;6;389;389;30;info seeking;simple
MAIL;6;25;25;30;email;simple
MSN;6;1863;1863;30;remote;simple
NetMeeting;6;1720;1720;2160;remote;simple
NNTP;6;119;119;30;info seeking;simple
NS Global;6;15397;15397;30;remote;simple
NS Global PRO;6;15397;15397;30;remote;simple
POP3;6;110;110;30;email;simple
PPTP;6;1723;1723;30;security;simple
Real Media;6;7070;7070;30;info seeking;simple
RLOGIN;6;513;513;30;remote;simple
RSH;6;514;514;30;remote;simple
RTSP;6;554;554;30;info seeking;simple
SMB;6;139;139;30;remote;simple
SMTP;6;25;25;30;email;simple
SQL*Net V1;6;1525;1525;480;other;simple
SQL*Net V2;6;1521;1521;480;other;simple
SSH;6;22;22;480;security;simple
TCP-ANY;6;0;65535;30;other;simple
TELNET;6;23;23;480;remote;simple
VDO Live;6;7000;7010;30;info seeking;simple
WAIS;6;210;210;30;info seeking;simple
WINFRAME;6;1494;1494;30;remote;simple
X-WINDOWS;6;6000;6063;30;remote;simple
YMSG;6;5050;5050;30;remote;simple
DHCP-Relay;17;67;67;1;info seeking;simple
DNS;17;53;53;1;info seeking;simple
GNUTELLA;17;6346;6347;1;remote;simple
IKE;17;500;500;1;security;simple
L2TP;17;1701;1701;1;remote;simple
MS-RPC-EPM;17;135;135;1;remote;simple
NBNAME;17;137;137;1;remote;simple
NBDS;17;138;138;1;remote;simple
NFS;17;111;111;40;remote;simple
NSM;17;69;69;1;other;simple
NTP;17;123;123;1;other;simple
PC-Anywhere;17;5632;5632;1;remote;simple
RIP;17;520;520;1;other;simple
SIP;17;5060;5060;1;other;simple
SNMP;17;161;161;1;other;simple
SUN-RPC-PORTMAPPER;17;111;111;40;remote;simple
SYSLOG;17;514;514;1;other;simple
TALK;17;517;518;1;other;simple
TFTP;17;69;69;1;remote;simple
UDP-ANY;17;0;65535;1;other;simple
UUCP;17;540;540;1;remote;simple
OSPF;89;0;65535;1;other;simple
MS-SQL;6;1433;1433;30;other;simple
LPR;6;515;515;30;other;simple
REXEC;6;512;512;30;remote;simple
IDENT;6;113;113;30;other;simple
SCTP-ANY;132;0;65535;1;other;simple
GRE;47;0;65535;60;remote;simple
HTTP;6;80;80;5;info seeking;simple
MGCP-UA;17;2427;2427;120;other;simple
GTP;17;2123;2123;30;remote;simple
MGCP-CA;17;2727;2727;120;other;simple
WHOIS;6;43;43;30;info seeking;simple
DISCARD;17;9;9;1;other;simple
RADIUS;17;1812;1813;1;other;simple
ECHO;17;7;7;1;other;simple
VNC;6;5800;5800;30;other;simple
CHARGEN;17;19;19;1;other;simple
SQL Monitor;17;1434;1434;1;other;simple
IKE-NAT;17;500;500;3;security;simple' where dev_typ_id=3 or dev_typ_id=2;

INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (138, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (139, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (140, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (141, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (142, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (143, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (144, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (145, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (146, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (147, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (148, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (149, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (150, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (151, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (152, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (153, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (154, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (155, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (156, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (157, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (158, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (159, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (160, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (161, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (162, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (163, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (164, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (165, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (166, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (167, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (168, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (169, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (170, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (171, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (172, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (173, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (174, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (175, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (176, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (177, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (178, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (179, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (180, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (181, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (182, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (183, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (184, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (185, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (186, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (187, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (188, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (189, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (190, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (191, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (192, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (193, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (194, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (195, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (196, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (197, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (198, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (199, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (200, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (201, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (202, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (203, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (204, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (205, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (206, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (207, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (208, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (209, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (210, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (211, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (212, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (213, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (214, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (215, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (216, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (217, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (218, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (219, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (220, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (221, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (222, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (223, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (224, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (225, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (226, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (227, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (228, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (229, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (230, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (231, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (232, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (233, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (234, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (235, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (236, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (237, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (238, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (239, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (240, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (241, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (242, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (243, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (244, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (245, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (246, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (247, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (248, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (249, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (250, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (251, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (252, 'unassigned', '[IANA]');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (253, 'unassigned', 'experimental (RFC3692)');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (254, 'unassigned', 'experimental (RFC3692)');
INSERT INTO stm_ip_proto (ip_proto_id,ip_proto_name,ip_proto_comment) VALUES (255, 'unassigned', 'reserved [IANA]');

Create sequence "public"."import_changelog_seq"
Increment 1
Minvalue 1
Maxvalue 9223372036854775807
Cache 1;

Create table "import_changelog"
(
	"change_time" Timestamp,
	"management_name" Varchar,
	"changed_object_name" Varchar,
	"changed_object_uid" Varchar,
	"changed_object_type" Varchar,
	"change_action" Varchar NOT NULL,
	"change_admin" Varchar,
	"control_id" Integer NOT NULL,
	"import_changelog_nr" Integer,
	"import_changelog_id" Integer NOT NULL Default nextval('public.import_changelog_seq'::text) UNIQUE,
 primary key ("import_changelog_id")
) With Oids;

Alter Table "import_changelog" add Constraint "Alter_Key14" UNIQUE ("import_changelog_nr","control_id");

Create index "IX_Relationship185" on "import_changelog" ("control_id");
Alter table "import_changelog" add  foreign key ("control_id") references "import_control" ("control_id") on update restrict on delete restrict;

GRANT SELECT ON TABLE import_changelog_seq TO public;
GRANT UPDATE ON TABLE import_changelog_seq TO public;

Grant select on "import_changelog" to group "dbbackupusers";
Grant select on "import_changelog" to group "configimporters";
Grant update on "import_changelog" to group "configimporters";
Grant delete on "import_changelog" to group "configimporters";
Grant insert on "import_changelog" to group "configimporters";
Grant select on "import_changelog" to group "isoadmins";
Grant update on "import_changelog" to group "isoadmins";
Grant delete on "import_changelog" to group "isoadmins";
Grant insert on "import_changelog" to group "isoadmins";

-- this adds a GUI for manipulating device infos

UPDATE stm_dev_typ SET dev_typ_version='5.x' WHERE dev_typ_version='5.0';
UPDATE stm_dev_typ SET dev_typ_version='6.x' WHERE dev_typ_version='5.1';
UPDATE stm_dev_typ SET dev_typ_version='R5x' WHERE dev_typ_version='R55';
UPDATE stm_dev_typ SET dev_typ_version='R6x' WHERE dev_typ_version='R60';
UPDATE stm_dev_typ SET dev_typ_version='3.x' WHERE dev_typ_version='3.2';
insert into stm_dev_typ	(dev_typ_id,dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc)
	VALUES (6,'phion netfence','3.x','phion','');

-------------------------------------------------------------------------------
-- this adds import status reporint capability (both via web and via file)

-- als owner der Tabelle import_control:
ALTER TABLE import_control ADD COLUMN successful_import Boolean NOT NULL Default TRUE;
ALTER TABLE import_control ALTER COLUMN successful_import SET DEFAULT FALSE;
ALTER TABLE import_control ADD COLUMN import_errors Varchar;
UPDATE import_control SET successful_import=TRUE;  -- initialize all old imports as successful

-------------------------------------------------------------------------------

-- drop functions whose argument list will be changed:
DROP FUNCTION import_all_main (INTEGER);
DROP FUNCTION import_global_refhandler_main (INTEGER);

-------------------------------------------------------------------------------

-- either run iso/package/install/bin/db-init2-functions.sh
-- or run separately:

\i install/database/iso-import.sql
\i install/database/iso-report-basics.sql
\i install/database/iso-report.sql
\i install/database/iso-obj-import.sql
\i install/database/iso-svc-import.sql
\i install/database/iso-usr-import.sql
\i install/database/iso-rule-import.sql
\i install/database/iso-views.sql
\i install/database/iso-import-main.sql
\i install/database/iso-rule-refs.sql
\i install/database/iso-basic-procs.sql

-- check etc/gui.conf for:
--      usergroup isoadmins privileges:		admin-users admin-devices admin-clients
--                                          view-import-status

-- replace iso/package/install/bin/agents/checkpoint-cp-config-locally.sh
-- and test audittrail-functionality

-- test R65 import - if possible

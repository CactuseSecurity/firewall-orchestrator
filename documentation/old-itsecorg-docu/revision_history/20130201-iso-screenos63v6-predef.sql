/*
 * 
  	adding various (icmp6, etc.) services to netscreen predef services
  	
	changed:
		install:
			iso-fill-stm.sql
*/


SET statement_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = off;
SET check_function_bodies = false;
SET client_min_messages = warning;
SET escape_string_warning = off;
SET search_path = public, pg_catalog;

UPDATE stm_dev_typ SET dev_typ_predef_svc =
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
 WHERE dev_typ_id=3;
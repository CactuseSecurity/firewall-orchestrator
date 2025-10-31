name_to_port = {
  "aol": {
    "port": 5190,
    "protocols": ["TCP"],
    "description": "America Online"
  },
  "bgp": {
    "port": 179,
    "protocols": ["TCP"],
    "description": "Border Gateway Protocol, RFC 1163"
  },
  "biff": {
    "port": 512,
    "protocols": ["UDP"],
    "description": "Used by mail system to notify users that new mail is received"
  },
  "bootpc": {
    "port": 68,
    "protocols": ["UDP"],
    "description": "Bootstrap Protocol Client"
  },
  "bootps": {
    "port": 67,
    "protocols": ["UDP"],
    "description": "Bootstrap Protocol Server"
  },
  "chargen": {
    "port": 19,
    "protocols": ["TCP"],
    "description": "Character Generator"
  },
  "cifs": {
    "port": 3020,
    "protocols": ["TCP", "UDP"],
    "description": "Common Internet File System"
  },
  "citrix-ica": {
    "port": 1494,
    "protocols": ["TCP"],
    "description": "Citrix Independent Computing Architecture (ICA) protocol"
  },
  "cmd": {
    "port": 514,
    "protocols": ["TCP"],
    "description": "Similar to exec except that cmd has automatic authentication"
  },
  "ctiqbe": {
    "port": 2748,
    "protocols": ["TCP"],
    "description": "Computer Telephony Interface Quick Buffer Encoding"
  },
  "daytime": {
    "port": 13,
    "protocols": ["TCP"],
    "description": "Day time, RFC 867"
  },
  "discard": {
    "port": 9,
    "protocols": ["TCP", "UDP"],
    "description": "Discard"
  },
  "dnsix": {
    "port": 195,
    "protocols": ["UDP"],
    "description": "DNSIX Session Management Module Audit Redirector"
  },
  "domain": {
    "port": 53,
    "protocols": ["TCP", "UDP"],
    "description": "DNS"
  },
  "echo": {
    "port": 7,
    "protocols": ["TCP", "UDP"],
    "description": "Echo"
  },
  "exec": {
    "port": 512,
    "protocols": ["TCP"],
    "description": "Remote process execution"
  },
  "finger": {
    "port": 79,
    "protocols": ["TCP"],
    "description": "Finger"
  },
  "ftp": {
    "port": 21,
    "protocols": ["TCP"],
    "description": "File Transfer Protocol (control port)"
  },
  "ftp-data": {
    "port": 20,
    "protocols": ["TCP"],
    "description": "File Transfer Protocol (data port)"
  },
  "gopher": {
    "port": 70,
    "protocols": ["TCP"],
    "description": "Gopher"
  },
  "h323": {
    "port": 1720,
    "protocols": ["TCP"],
    "description": "H.323 call signaling"
  },
  "hostname": {
    "port": 101,
    "protocols": ["TCP"],
    "description": "NIC Host Name Server"
  },
  "http": {
    "port": 80,
    "protocols": ["TCP", "UDP"],
    "description": "World Wide Web HTTP"
  },
  "https": {
    "port": 443,
    "protocols": ["TCP"],
    "description": "HTTP over SSL"
  },
  "ident": {
    "port": 113,
    "protocols": ["TCP"],
    "description": "Ident authentication service"
  },
  "imap4": {
    "port": 143,
    "protocols": ["TCP"],
    "description": "Internet Message Access Protocol, version 4"
  },
  "irc": {
    "port": 194,
    "protocols": ["TCP"],
    "description": "Internet Relay Chat protocol"
  },
  "isakmp": {
    "port": 500,
    "protocols": ["UDP"],
    "description": "Internet Security Association and Key Management Protocol"
  },
  "kerberos": {
    "port": 750,
    "protocols": ["TCP", "UDP"],
    "description": "Kerberos"
  },
  "klogin": {
    "port": 543,
    "protocols": ["TCP"],
    "description": "KLOGIN"
  },
  "kshell": {
    "port": 544,
    "protocols": ["TCP"],
    "description": "Korn Shell"
  },
  "ldap": {
    "port": 389,
    "protocols": ["TCP"],
    "description": "Lightweight Directory Access Protocol"
  },
  "ldaps": {
    "port": 636,
    "protocols": ["TCP"],
    "description": "Lightweight Directory Access Protocol (SSL)"
  },
  "login": {
    "port": 513,
    "protocols": ["TCP"],
    "description": "Remote login"
  },
  "lotusnotes": {
    "port": 1352,
    "protocols": ["TCP"],
    "description": "IBM Lotus Notes"
  },
  "lpd": {
    "port": 515,
    "protocols": ["TCP"],
    "description": "Line Printer Daemon - printer spooler"
  },
  "mobile-ip": {
    "port": 434,
    "protocols": ["UDP"],
    "description": "Mobile IP-Agent"
  },
  "nameserver": {
    "port": 42,
    "protocols": ["UDP"],
    "description": "Host Name Server"
  },
  "netbios-dgm": {
    "port": 138,
    "protocols": ["UDP"],
    "description": "NetBIOS Datagram Service"
  },
  "netbios-ns": {
    "port": 137,
    "protocols": ["UDP"],
    "description": "NetBIOS Name Service"
  },
  "netbios-ssn": {
    "port": 139,
    "protocols": ["TCP"],
    "description": "NetBIOS Session Service"
  },
  "nfs": {
    "port": 2049,
    "protocols": ["TCP", "UDP"],
    "description": "Network File System - Sun Microsystems"
  },
  "nntp": {
    "port": 119,
    "protocols": ["TCP"],
    "description": "Network News Transfer Protocol"
  },
  "ntp": {
    "port": 123,
    "protocols": ["UDP"],
    "description": "Network Time Protocol"
  },
  "pcanywhere-data": {
    "port": 5631,
    "protocols": ["TCP"],
    "description": "pcAnywhere data"
  },
  "pcanywhere-status": {
    "port": 5632,
    "protocols": ["UDP"],
    "description": "pcAnywhere status"
  },
  "pim-auto-rp": {
    "port": 496,
    "protocols": ["TCP", "UDP"],
    "description": "Protocol Independent Multicast, reverse path flooding, dense mode"
  },
  "pop2": {
    "port": 109,
    "protocols": ["TCP"],
    "description": "Post Office Protocol - Version 2"
  },
  "pop3": {
    "port": 110,
    "protocols": ["TCP"],
    "description": "Post Office Protocol - Version 3"
  },
  "pptp": {
    "port": 1723,
    "protocols": ["TCP"],
    "description": "Point-to-Point Tunneling Protocol"
  },
  "radius": {
    "port": 1645,
    "protocols": ["UDP"],
    "description": "Remote Authentication Dial-In User Service"
  },
  "radius-acct": {
    "port": 1646,
    "protocols": ["UDP"],
    "description": "Remote Authentication Dial-In User Service (accounting)"
  },
  "rip": {
    "port": 520,
    "protocols": ["UDP"],
    "description": "Routing Information Protocol"
  },
  "rsh": {
    "port": 514,
    "protocols": ["TCP"],
    "description": "Remote Shell"
  },
  "rtsp": {
    "port": 554,
    "protocols": ["TCP"],
    "description": "Real Time Streaming Protocol"
  },
  "secureid-udp": {
    "port": 5510,
    "protocols": ["UDP"],
    "description": "SecureID over UDP"
  },
  "sip": {
    "port": 5060,
    "protocols": ["TCP", "UDP"],
    "description": "Session Initiation Protocol"
  },
  "smtp": {
    "port": 25,
    "protocols": ["TCP"],
    "description": "Simple Mail Transport Protocol"
  },
  "snmp": {
    "port": 161,
    "protocols": ["UDP"],
    "description": "Simple Network Management Protocol"
  },
  "snmptrap": {
    "port": 162,
    "protocols": ["UDP"],
    "description": "Simple Network Management Protocol - Trap"
  },
  "sqlnet": {
    "port": 1521,
    "protocols": ["TCP"],
    "description": "Structured Query Language Network"
  },
  "ssh": {
    "port": 22,
    "protocols": ["TCP"],
    "description": "Secure Shell"
  },
  "sunrpc": {
    "port": 111,
    "protocols": ["TCP", "UDP"],
    "description": "Sun Remote Procedure Call"
  },
  "syslog": {
    "port": 514,
    "protocols": ["UDP"],
    "description": "System Log"
  },
  "tacacs": {
    "port": 49,
    "protocols": ["TCP", "UDP"],
    "description": "Terminal Access Controller Access Control System Plus"
  },
  "talk": {
    "port": 517,
    "protocols": ["TCP", "UDP"],
    "description": "Talk"
  },
  "telnet": {
    "port": 23,
    "protocols": ["TCP"],
    "description": "RFC 854 Telnet"
  },
  "tftp": {
    "port": 69,
    "protocols": ["UDP"],
    "description": "Trivial File Transfer Protocol"
  },
  "time": {
    "port": 37,
    "protocols": ["UDP"],
    "description": "Time"
  },
  "uucp": {
    "port": 540,
    "protocols": ["TCP"],
    "description": "UNIX-to-UNIX Copy Program"
  },
  "vxlan": {
    "port": 4789,
    "protocols": ["UDP"],
    "description": "Virtual eXtensible Local Area Network (VXLAN)"
  },
  "who": {
    "port": 513,
    "protocols": ["UDP"],
    "description": "Who"
  },
  "whois": {
    "port": 43,
    "protocols": ["TCP"],
    "description": "Who Is"
  },
  "www": {
    "port": 80,
    "protocols": ["TCP", "UDP"],
    "description": "World Wide Web"
  },
  "xdmcp": {
    "port": 177,
    "protocols": ["UDP"],
    "description": "X Display Manager Control Protocol"
  }
}

protocol_map = {
  "ah": 51,
  "eigrp": 88,
  "esp": 50,
  "gre": 47,
  "icmp": 1,
  "icmp6": 58,
  "igmp": 2,
  "igrp": 9,
  "ip": 0,
  "ipinip": 4,
  "ipsec": 50,
  "nos": 94,
  "ospf": 89,
  "pcp": 108,
  "pim": 103,
  "pptp": 47,
  "sctp": 132,
  "snp": 77,
  "tcp": 6,
  "udp": 17
}
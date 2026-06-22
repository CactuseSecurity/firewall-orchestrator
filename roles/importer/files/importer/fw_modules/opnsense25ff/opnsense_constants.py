OPNSENSE_UUID_ALIAS = "@uuid"
PREDEFINED_RULE_UID_PREFIX = "opnsense-default-rule-"
MAX_DEPTH: int = 10
BUILTIN_SERVICE_PORTS: dict[str, int] = {
    "http": 80,
    "https": 443,
    "ssh": 22,
    "domain": 53,
}

# OPNsense rule protocols that are matched by port (services derive from destination ports).
# "any" is included so that protocol-less rules keep their "Any" service.
PORT_BASED_PROTOCOLS: frozenset[str] = frozenset({"any", "tcp", "udp", "tcp/udp"})

# IANA IP protocol numbers for non-port protocols selectable in OPNsense rules.
# Keys are lower-cased protocol names as used in the normalized service name.
IP_PROTO_NUMBERS: dict[str, int] = {
    "icmp": 1,
    "igmp": 2,
    "ggp": 3,
    "ipencap": 4,
    "tcp": 6,
    "udp": 17,
    "gre": 47,
    "esp": 50,
    "ah": 51,
    "icmpv6": 58,
    "ospf": 89,
    "pim": 103,
    "carp": 112,
    "pfsync": 240,
}

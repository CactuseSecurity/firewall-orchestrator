OPNSENSE_UUID_ALIAS = "@uuid"
PREDEFINED_RULE_UID_PREFIX = "opnsense-default-rule-"
# prefix for uids generated for legacy rules that carry no uuid in config.xml
FALLBACK_RULE_UID_PREFIX = "opnsense-rule-"
# rulebase collecting rules that cannot be assigned to a single interface rulebase
UNASSIGNED_RULEBASE_NAME = "unassigned"
# rulebase collecting the floating rules, which OPNsense processes before all interface rules
FLOATING_RULEBASE_NAME = "floating"
MAX_DEPTH: int = 10
# maximum nesting level walked by the sanitizer when redacting credential-bearing keys
MAX_SANITIZER_DEPTH: int = 25
BUILTIN_SERVICE_PORTS: dict[str, int] = {
    "http": 80,
    "https": 443,
    "imap": 143,
    "ssh": 22,
    "domain": 53,
}

# OPNsense rule protocols that are matched by port (services derive from destination ports).
# "any" is included so that protocol-less rules keep their "Any" service.
PORT_BASED_PROTOCOLS: frozenset[str] = frozenset({"any", "tcp", "udp", "tcp/udp"})

# Port-based protocols that can be expressed as the single ip_proto of a service object.
# "tcp/udp" cannot ("6 or 17") and therefore stays protocol-agnostic, like "any".
QUALIFIABLE_PORT_PROTOCOLS: frozenset[str] = frozenset({"tcp", "udp"})
# separates port and protocol in the name of a protocol-specific service (e.g. "https/tcp")
SERVICE_PROTOCOL_SEPARATOR = "/"
# separators OPNsense accepts between start and end port of a port range. ":" is the native
# syntax used in aliases and rule ports (e.g. "8000:8080"), "-" appears in imported configs.
PORT_RANGE_SEPARATORS: tuple[str, ...] = (":", "-")

# IANA IP protocol numbers for non-port protocols selectable in OPNsense rules
# (https://github.com/opnsense/core/blob/master/src/opnsense/mvc/app/models/OPNsense/Firewall/Filter.xml).
# Keys are lower-cased protocol names as used in the normalized service name.
# "ip" is intentionally absent: OPNsense uses it for "any IP protocol", while protocol
# number 0 would be imported as HOPOPT.
IP_PROTO_NUMBERS: dict[str, int] = {
    "icmp": 1,
    "igmp": 2,
    "ggp": 3,
    "ipencap": 4,
    "st": 5,
    "tcp": 6,
    "igp": 9,
    "udp": 17,
    "rdp": 27,
    "ipv6": 41,
    "ipv6-route": 43,
    "ipv6-frag": 44,
    "gre": 47,
    "esp": 50,
    "ah": 51,
    "icmpv6": 58,
    "ipv6-icmp": 58,
    "ipv6-nonxt": 59,
    "ipv6-opts": 60,
    "eigrp": 88,
    "ospf": 89,
    "etherip": 97,
    "pim": 103,
    "ipcomp": 108,
    "carp": 112,
    "vrrp": 112,
    "l2tp": 115,
    "isis": 124,
    "sctp": 132,
    "pfsync": 240,
}

from __future__ import annotations

from typing import NamedTuple

GC_AUTH_ENDPOINT = "/api/v3.0/authenticate"
GC_LABELS_ENDPOINT = "/api/v4.0/labels"
GC_LABEL_GROUPS_ENDPOINT = "/api/v4.0/label-groups"
GC_RULES_ENDPOINT = "/api/v4.0/visibility/policy/rules"
GC_ASSETS_ENDPOINT = "/api/v4.0/assets"
GC_PAGE_SIZE = 1000
GC_MAX_PAGES = 10000  # guards against an endless paging loop if the API keeps returning full pages
GC_TOKEN_KEYS = ("access_token", "token")

# dynamic label criteria that describe IP addresses, e.g. {"field": "numeric_ip_addresses", "op": "SUBNET", ...}
GC_IP_CRITERIA_FIELD = "numeric_ip_addresses"
GC_IP_CRITERIA_OPS = frozenset({"SUBNET", "EQUALS"})
GC_IP_RANGE_CRITERIA_OP = "RANGE"

GC_LABEL_NAME_SEPARATOR = ": "
GC_AND_LABEL_NAME_SEPARATOR = " & "
GC_AND_LABEL_UID_SEPARATOR = "&"
GC_ASSET_UID_PREFIX = "asset:"
GC_ANY_OBJECT_UID = "any"
GC_ANY_OBJECT_NAME = "Any"
GC_ANY_SERVICE_UID = "any"
GC_ANY_SERVICE_NAME = "Any"
GC_RULESET_NAME_NONE = ""


class GcSection(NamedTuple):
    number: int
    name: str


# the policy sections in the order Guardicore lists (and evaluates) them; the section number keeps the alphabetical
# order of the rulebases equal to this order
GC_SECTIONS: dict[str, GcSection] = {
    "OVERRIDE_ALLOW": GcSection(1, "Override Allow"),
    "OVERRIDE_ALERT": GcSection(2, "Override Alert"),
    "OVERRIDE_BLOCK": GcSection(3, "Override Block"),
    "ALLOW": GcSection(4, "Allow"),
    "ALERT": GcSection(5, "Alert"),
    "BLOCK": GcSection(6, "Block"),
}

GC_ACTION_ALLOW = "ALLOW"
GC_ACTION_ALLOW_AND_ENCRYPT = "ALLOW_AND_ENCRYPT"
GC_ACTION_ALERT = "ALERT"
GC_ACTION_BLOCK = "BLOCK"
GC_ACTION_BLOCK_AND_ALERT = "BLOCK_AND_ALERT"
GC_ALERTING_ACTIONS = frozenset({GC_ACTION_ALERT, GC_ACTION_BLOCK_AND_ALERT})

GC_ADDRESS_CLASSIFICATION_PRIVATE = "Private"
GC_ADDRESS_CLASSIFICATION_INTERNET = "Internet"
GC_PRIVATE_NETWORKS = ("10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")

IP_PROTO_ICMP = 1
IP_PROTO_TCP = 6
IP_PROTO_UDP = 17
IP_PROTO_ICMPV6 = 58
GC_IP_PROTOCOLS: dict[str, int] = {
    "ICMP": IP_PROTO_ICMP,
    "TCP": IP_PROTO_TCP,
    "UDP": IP_PROTO_UDP,
    "ICMPV6": IP_PROTO_ICMPV6,
}
GC_PORTED_PROTOCOLS = frozenset({IP_PROTO_TCP, IP_PROTO_UDP})
MIN_PORT = 0
MAX_PORT = 65535

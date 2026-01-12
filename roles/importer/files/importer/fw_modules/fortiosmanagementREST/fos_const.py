NW_OBJ_TYPES = [
    "firewall/address",
    "firewall/address6",
    "firewall/addrgrp",
    "firewall/addrgrp6",
    "firewall/ippool",
    "firewall/vip",
    "firewall/internet-service",
    "firewall/internet-service-group",
]

# internet-service is not a service as such but is used as dest (mainly)
SVC_OBJ_TYPES = [
    "application/list",
    "application/group",
    "firewall.service/custom",
    "firewall.service/group",
]

# build the product of all scope/type combinations
NW_OBJ_SCOPE = ["nw_obj_" + s1 for s1 in NW_OBJ_TYPES]
SVC_OBJ_SCOPE = ["svc_obj_" + s1 for s1 in SVC_OBJ_TYPES]

# TODO: ZONE_TYPES = ['zones_global', 'zones_adom']

USER_OBJ_TYPES = ["user/local", "user/group"]
USER_SCOPE = ["user_obj_" + s1 for s1 in USER_OBJ_TYPES]

RULE_ACCESS_SCOPE_V4 = ["rules"]
RULE_ACCESS_SCOPE_V6 = []

RULE_ACCESS_SCOPE = ["rules"]
RULE_NAT_SCOPE = ["rules_nat"]
RULE_SCOPE = RULE_ACCESS_SCOPE + RULE_NAT_SCOPE

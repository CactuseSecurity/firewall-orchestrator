# mapping the opnsense model into normalized import model

import json
from collections import Counter
from typing import Any

import fw_modules.opnsense25ff.opnsense_helper as os_helper
import fwo_const
from fw_modules.opnsense25ff.opnsense_model import (AliasTypeEnum,
                                                    FilterRuleActionEnum,
                                                    OPNsenseAccessRule,
                                                    OPNsenseAlias,
                                                    OPNsenseConfig,
                                                    OPNsenseHost,
                                                    OPNsenseHostAlias,
                                                    OPNsenseIfGroup,
                                                    OPNsenseInterface,
                                                    OPNsenseNetwork,
                                                    OPNsenseNetworkAlias,
                                                    OPNsensePort,
                                                    OPNsensePortAlias)
from fw_modules.opnsense25ff.opnsense_parser import _parse_opnsense_config
from fwo_base import ConfigAction
from fwo_base import \
    generate_hash_from_dict as fwo_base_generate_hash_from_dict
from fwo_base import sort_and_join
from fwo_const import RULE_NUM_NUMERIC_STEPS
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import \
    FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from model_controllers.interface_controller import Interface
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.networkobject import NetworkObject
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased
from models.serviceobject import ServiceObject
from netaddr import IPAddress, IPNetwork

MAX_DEPTH: int = 10
# ───────────────────────── helper ────────────────────────

def _create_network_object_from_host_definition(host: OPNsenseHost) -> NetworkObject:
    #FWOLogger.debug(f"[*] called _create_network_object_from_host_definition... ")
    return NetworkObject(
        obj_uid          = fwo_base_generate_hash_from_dict({'net_obj': host.name}),
        obj_name         = host.name,
        obj_ip           = host.host,
        obj_ip_end       = host.host_end if host.is_range else host.host,
        obj_color        = "",
        obj_typ          = "ip_range" if host.is_range else "host",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = host.name
    )

def _create_network_object_from_net_definition(net: OPNsenseNetwork) -> NetworkObject:
    #FWOLogger.debug(f"[*] called _create_network_object_from_net_definition... ")
    return NetworkObject(
        obj_uid          = fwo_base_generate_hash_from_dict({'net_obj': net.name}),
        obj_name         = net.name,
        obj_ip           = IPNetwork(f"{IPAddress(net.net.first)}"),
        obj_ip_end       = IPNetwork(f"{IPAddress(net.net.last)}"),
        obj_color        = "",
        obj_typ          = "network",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = net.name
    )

def _create_network_object_from_string(s: str) -> NetworkObject:
    #FWOLogger.debug(f"[*] called _create_network_object_from_string... ")
    return NetworkObject(
        obj_uid          = fwo_base_generate_hash_from_dict({'net_obj': s}),
        obj_name         = s,
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = s
    )

def _create_special_network_objects() -> list[NetworkObject]:

    self_obj = NetworkObject(
        obj_uid          = '(self)',
        obj_name         = '(self)',
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = "special network object references firewall itself"
    )
    any_v4 = NetworkObject(
        obj_uid          = 'Any-v4',
        obj_name         = 'Any-v4',
        obj_ip           = IPNetwork('0.0.0.0/32', version=4),
        obj_ip_end       = IPNetwork('255.255.255.255/32', version=4),
        obj_color        = "",
        obj_typ          = "ip_range",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = "special network object created during normalization"
    )
    any_v6 = NetworkObject(
        obj_uid          = 'Any-v6',
        obj_name         = 'Any-v6',
        obj_ip           = IPNetwork('::/128', version=6),
        obj_ip_end       = IPNetwork('ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128', version=6),
        obj_color        = "",
        obj_typ          = "ip_range",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = "special network object created during normalization"
    )
    any = NetworkObject(
        obj_uid          = 'Any',
        obj_name         = 'Any',
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = sort_and_join([any_v4.obj_name, any_v6.obj_name]),
        obj_member_names = sort_and_join([any_v4.obj_name, any_v6.obj_name]),
        obj_comment      = "special network object created during normalization"
    )
    return [any, any_v4, any_v6, self_obj]


def _create_network_object_from_alias(alias: OPNsenseHostAlias | OPNsenseNetworkAlias, normalized: dict[str, NetworkObject], depth: int) -> NetworkObject:

    #FWOLogger.debug(f"[*] _create_network_object_from_alias called... ")

    member: list[str] = []

    if isinstance(alias, OPNsenseHostAlias | OPNsenseNetworkAlias):
        for child in alias.childs:
            if isinstance(child, str):
                if child in normalized:
                    member.append(child)
                    continue
                child_obj = _create_network_object_from_string(child)
                normalized[child_obj.obj_name] = child_obj
                member.append(child_obj.obj_name)
            else:
                if child.name not in normalized:
                    if isinstance(child, OPNsenseHost):
                        child_obj = _create_network_object_from_host_definition(child)
                    elif isinstance(child, OPNsenseNetwork):
                        child_obj = _create_network_object_from_net_definition(child)
                    elif isinstance(child, OPNsenseHostAlias | OPNsenseNetworkAlias):
                        if depth >= MAX_DEPTH:
                            FWOLogger.warning(f"[-] depth {depth} reached maximum {MAX_DEPTH}. Abort recursion...")
                            continue
                        child_obj = _create_network_object_from_alias(child, normalized, depth + 1)
                    else:
                        FWOLogger.error(f"detected a unvalid object type for {child}")
                        continue
                    normalized[child_obj.obj_name] = child_obj
                    member.append(child_obj.obj_name)
                else:
                    member.append(child.name)
    else:
        for child in alias.value:
            member.append(child)

    #FWOLogger.debug(f"[*] creating net_obj for {alias.name}")
    net_obj = NetworkObject(
        obj_uid             = alias.uuid,
        obj_name            = alias.name,
        obj_ip              = None,
        obj_ip_end          = None,
        obj_color           = "",
        obj_typ             = "group",
        obj_member_refs     = sort_and_join(member) if len(member) > 0 else None,
        obj_member_names    = sort_and_join(member) if len(member) > 0 else None,
        obj_comment         = alias.description
    )
    #FWOLogger.debug(f"[*] created net_obj: {net_obj}")
    return net_obj

def _create_any_svc_object() -> ServiceObject:
    return ServiceObject(
        svc_uid          = 'Any',
        svc_name         = 'Any',
        svc_port         = 1,
        svc_port_end     = 65535,
        svc_color        = "",
        svc_typ          = "simple",
        ip_proto         = None,
        svc_member_refs  = None,
        svc_member_names = None,
        svc_comment      = "special service object created during normalization",
        svc_timeout      = None,
        rpc_nr           = None
    )

def _create_services_from_port_definition(port: OPNsensePort) -> ServiceObject:
    return ServiceObject(
        svc_uid          = fwo_base_generate_hash_from_dict({'svc_obj': port.name}),
        svc_name         = port.name,
        svc_port         = port.port,
        svc_port_end     = port.port_end if port.is_range else port.port,
        svc_color        = "",
        svc_typ          = "simple",
        ip_proto         = None,
        svc_member_refs  = None,
        svc_member_names = None,
        svc_comment      = port.name,
        svc_timeout      = None,
        rpc_nr           = None
    )


def _normalize_services_from_port_alias(alias: OPNsensePortAlias, normalized: dict[str, ServiceObject], depth: int) -> ServiceObject:

    # https://github.com/CactuseSecurity/firewall-orchestrator/blob/9c5c072addcb8c72dc3283b1dd8b60f94fe0d86b/roles/importer/files/importer/models/serviceobject.py#L6
    # ServiceObject: {
    #     svc_uid:          str                    // string: unique service id
    #     svc_name:         str                    // string: name of the service
    #     svc_port:         int  | None = None     // integer: 1-65535
    #     svc_port_end:     int  | None = None     // integer: 1-65535
    #     svc_color:        str                    // string: color of object, see Explanations
    #     svc_typ:          str  # TODO: ENUM      // string: type of service, can be any of the following: simple, group, rpc (see roles/database/files/sql/creation/fworch-fill-stm.sql)
    #     ip_proto:         int  | None = None     // integer: 0-255 procol number
    #     svc_member_refs:  str  | None = None     // string: uids of the referenced service objects separated by "|"
    #     svc_member_names: str  | None = None     // string: names of the group members separated by "|"
    #     svc_comment:      str  | None = None     // string: comment
    #     svc_timeout:      int  | None = None     // integer: idle timeout in seconds
    #     rpc_nr:           str  | None = None
    # }
    member: list[str] = []
    for child in alias.childs:
        if child.name not in normalized:
            if isinstance(child, OPNsensePort):
                child_svc = _create_services_from_port_definition(child)
                normalized[child_svc.svc_name] = child_svc
                member.append(child_svc.svc_name)
            elif isinstance(child, OPNsensePortAlias) and depth < MAX_DEPTH:
                svc = _normalize_services_from_port_alias(child, normalized, depth+1)
                normalized[svc.svc_name] = svc
                member.append(svc.svc_name)
            elif depth >= MAX_DEPTH:
                FWOLogger.warning(f"[-] depth {depth} reached maximum {MAX_DEPTH}. Abort recursion...")
                continue
        else:
            member.append(child.name)

    service = ServiceObject(
        svc_uid          = alias.uuid,
        svc_name         = alias.name,
        svc_port         = None,
        svc_port_end     = None,
        svc_color        = "",
        svc_typ          = "group",
        ip_proto         = None,
        svc_member_refs  = sort_and_join(member),
        svc_member_names = sort_and_join(member),
        svc_comment      = alias.description,
        svc_timeout      = None,
        rpc_nr           = None,
    )

    normalized[service.svc_name] = service

    return service

def _create_normalized_rule_from_access_rule(rule: OPNsenseAccessRule) -> RuleNormalized:

    #FWOLogger.debug(f"{rule}")
    # https://github.com/CactuseSecurity/firewall-orchestrator/blob/9c5c072addcb8c72dc3283b1dd8b60f94fe0d86b/roles/importer/files/importer/models/rule.py#L38-L66
    # RuleNormalized: {
    #     rule_num:           int                                  // integer: rule number for ordering
    #     rule_num_numeric:   float
    #     rule_disabled:      bool                                 // boolean: is the whole rule disabled
    #     rule_src_neg:       bool                                 // boolean: is the source field negated
    #     rule_src:           str                                  // string: list of source object names (if it contains user, use "@" as delimiter)
    #     rule_src_refs:      str                                  // string: source references
    #     rule_dst_neg:       bool                                 // boolean: is the destination field negated
    #     rule_dst:           str                                  // string: list of destination network object names
    #     rule_dst_refs:      str                                  // string: destination references
    #     rule_svc_neg:       bool                                 // boolean: is the service field negated
    #     rule_svc:           str                                  // string: list of service names
    #     rule_svc_refs:      str                                  // string: service references
    #     rule_action:        RuleAction                           // RuleAction: rule action options
    #     rule_track:         RuleTrack                            // RuleTrack: logging options
    #     rule_installon:     str | None = None                    // string: list of gateways this rule should be applied to
    #     rule_time:          str | None = None                    // string: any time restrictions of the rule
    #     rule_name:          str | None = None                    // string: optional name of the rule
    #     rule_uid:           str | None = None                    // string: unique rule id
    #     rule_custom_fields: str | None = None                    // string: json serialized user defined fields
    #     rule_implied:       bool                                 // boolean: is it an implied (check point) rule derived from settings
    #     rule_type:          RuleType = RuleType.SECTIONHEADER    // string: type of the nat rule: "access|combined|original|xlate", default "access"
    #     last_change_admin:  str | None = None
    #     parent_rule_uid:    str | None = None                    // string: for layers, the uid of the rule of layer above
    #     last_hit:           str | None = None
    #     rule_comment:       str | None = None                    // string: optional rule comment
    #     rule_src_zone:      str | None = None                    // string: source zone (if applicable) of the rule
    #     rule_dst_zone:      str | None = None                    // string: destination zone (if applicable) of the rule
    #     rule_head_text:     str | None = None                    // string: for section headers this is the field to use
    # }

    rule_action: RuleAction = RuleAction.ACCEPT
    if rule.action == FilterRuleActionEnum.PASS:
        rule_action = RuleAction.ACCEPT
    elif rule.action == FilterRuleActionEnum.BLOCK:
        rule_action = RuleAction.DROP
    elif rule.action == FilterRuleActionEnum.REJECT:
        rule_action = RuleAction.REJECT

    os_rule_custom = {
        'os_rule_l2proto'   : rule.ipprotocol,
        'os_rule_l3proto'   : rule.protocol,
        'os_rule_direction' : rule.direction,
        'os_rule_interface' : rule.interface if rule.interface else None
    }

    rule_source_objects = (rule.source_address or []) + (rule.source_network or [])
    rule_dest_objects = (rule.dest_address or []) + (rule.dest_network or [])

    rule_normalized = RuleNormalized(
        rule_num=0,
        rule_num_numeric=0,
        rule_disabled      = rule.disabled,
        rule_src_neg       = rule.source_neg,
        rule_src           = sort_and_join(rule_source_objects),
        rule_src_refs      = sort_and_join(rule_source_objects),
        rule_dst_neg       = rule.dest_neg,
        rule_dst           = sort_and_join(rule_dest_objects),
        rule_dst_refs      = sort_and_join(rule_dest_objects),
        rule_svc_neg       = False,
        rule_svc           = sort_and_join(rule.dest_port),
        rule_svc_refs      = sort_and_join(rule.dest_port),
        rule_action        = rule_action,
        rule_track         = RuleTrack.LOG if rule.logging else RuleTrack.NONE,
        rule_installon     = None,
        rule_time          = None,
        rule_name          = rule.description.split(':', 1)[0],
        rule_uid           = rule.uuid,
        rule_custom_fields = json.dumps(os_rule_custom),
        rule_implied       = False,
        rule_type          = RuleType.ACCESS,
        last_change_admin  = None,
        parent_rule_uid    = None,
        last_hit           = None,
        rule_comment       = rule.description,
        rule_src_zone      = None,
        rule_dst_zone      = None,
        rule_head_text     = None,
    )

    #FWOLogger.debug(f"{rule_normalized}")
    return rule_normalized

# ──────────────────────────────────────────────────────────

def _normalize_users(config: OPNsenseConfig) -> dict[str, Any]:

    normalized: dict[str, Any] = {}

    # user_object: {
    #     "control_id": 1,                                        // bigint: ID of the current import
    #     "user_typ": "simple",                                   // string: either "group" or "simple"
    #     "user_uid": "c4d28191-bd44-4d45-8887-df94f594a8ef",     // string: unique user id
    #     "user_name": "IA_User1",                                // string: user name
    #     "user_member_names": null,                              // string: names of the group members separated by "|"
    #     "user_member_refs": null,                               // string: uids of the referenced users separated by "|"
    #     "user_color": null,                                     // string: color of object, see Explanations
    #     "user_comment": null,                                   // string: comment
    #     "user_valid_until": null                                // string: user's "sell-by" date
    #     "user_scope": null,                                     // string: user scope
    # }

    for os_user in config.users:
        user = {
            "user_typ": "simple",
            "user_color": None
            }

        if os_user.uuid:
            user["user_uid"] = os_user.uuid
        if os_user.name:
            user["user_name"] = os_user.name
        if os_user.description:
            user["user_comment"] = os_user.description
        if os_user.expires:
            user["user_valid_until"] = os_user.expires
        if os_user.scope:
            user["user_scope"] = str(os_user.scope)

        if user["user_name"] not in normalized:
            normalized[user["user_name"]] = user

    return normalized

def _update_service_objects_from_access_rules(rules: list[OPNsenseAccessRule], svc_objs: dict[str, ServiceObject]) -> None:

    for rule in rules:
        # add all plain ports or port-ranges not currently normalized
        for dest_port in set(rule.dest_port) - set(svc_objs.keys()):
            plain_portlist_candidate = dest_port.split('-', 1)
            if os_helper._helper_is_int(plain_portlist_candidate[0]):
                port = int(plain_portlist_candidate[0])
                if len(plain_portlist_candidate) == 1:
                    #FWOLogger.debug(f"[*] detected plain port {dest_port} in rule:\n    {rule}")
                    svc = _create_services_from_port_definition(OPNsensePort(name=dest_port, is_range=False, port=port, port_end=None))
                    svc_objs[svc.svc_name] = svc
                else:
                    if os_helper._helper_is_int(plain_portlist_candidate[1]):
                        port_end = int(plain_portlist_candidate[1])
                        #FWOLogger.debug(f"[*] detected plain port-range {dest_port} in rule:\n    {rule}")
                        svc = _create_services_from_port_definition(OPNsensePort(name=dest_port, is_range=True, port=port, port_end=port_end))
                        svc_objs[svc.svc_name] = svc

def _normalize_services(os_config: OPNsenseConfig) -> dict[str, ServiceObject]:

    normalized: dict[str, ServiceObject] = {}

    for a in os_config.port_aliases:
        if a not in normalized:
            alias = os_config.port_aliases[a]
            _normalize_services_from_port_alias(alias, normalized, 0)

    # add special "Any" service objects
    svc_any = _create_any_svc_object()
    normalized[svc_any.svc_name] = svc_any

    _update_service_objects_from_access_rules(os_config.access_rules, normalized)

    return normalized

def _create_network_objects_from_geoip_alias(alias: OPNsenseAlias, nw_objs: dict[str, NetworkObject]) -> NetworkObject:

    # parse and add country codes as network objects
    members: list[str] = []
    for cc in alias.value:
        if cc not in nw_objs:
            cc_obj = NetworkObject(
                obj_uid          = fwo_base_generate_hash_from_dict({'geo_obj': cc}),
                obj_name         = cc,
                obj_ip           = None,
                obj_ip_end       = None,
                obj_color        = "",
                obj_typ          = "group",
                obj_member_refs  = None,
                obj_member_names = None,
                obj_comment      = f"{cc} country code: special network object created during normalization"
            )
            nw_objs[cc_obj.obj_name] = cc_obj
        members.append(cc)

    return NetworkObject(
        obj_uid          = alias.uuid,
        obj_name         = alias.name,
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = sort_and_join(members),
        obj_member_names = sort_and_join(members),
        obj_comment      = alias.description
    )

def _create_network_objects_from_urltable_alias(alias: OPNsenseAlias) -> NetworkObject:
    return NetworkObject(
        obj_uid          = alias.uuid,
        obj_name         = alias.name,
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = f"{alias.description}: {alias.value}"
    )

def _create_network_objects_from_ifgroup(ifgroup: OPNsenseIfGroup) -> NetworkObject:
    return NetworkObject(
        obj_uid          = ifgroup.uuid,
        obj_name         = ifgroup.name,
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = ifgroup.description
    )

def _create_network_objects_from_interface(interface: OPNsenseInterface) -> NetworkObject:
    return NetworkObject(
        obj_uid          = fwo_base_generate_hash_from_dict({'iface_obj': interface.name}),
        obj_name         = interface.name,
        obj_ip           = None,
        obj_ip_end       = None,
        obj_color        = "",
        obj_typ          = "group",
        obj_member_refs  = None,
        obj_member_names = None,
        obj_comment      = f"{interface.hw_interface}: {interface.ip4_address}{interface.ip4_subnet}|{interface.ip6_address}|{interface.ip6_subnet} :{interface.description}"
    )

def _update_network_objects_from_access_rules(os_config: OPNsenseConfig, nw_objs: dict[str, NetworkObject]) -> None:
    #FWOLogger.debug(f"[*] {nw_objs}")
    for rule in os_config.access_rules:
        for target in (set(rule.source_address or []) | set(rule.dest_address or []) | set(rule.source_network or []) | set(rule.dest_network or [])) - set(nw_objs.keys() or []):
            if target in nw_objs:
                continue
            if os_helper._helper_is_ip(target):
                # plain IP address
                obj = _create_network_object_from_host_definition(OPNsenseHost(name=target, is_range=False, host=IPAddress(target), host_end=None))
                nw_objs[target] = obj
            elif os_helper._helper_is_ip_subnet(target):
                # plain IP subnet
                obj = _create_network_object_from_net_definition(OPNsenseNetwork(name=target, net=IPNetwork(target)))
                nw_objs[target] = obj
            elif os_helper._helper_is_ip_range(target):
                # plain IP range
                obj = _create_network_object_from_host_definition(OPNsenseHost(name=target, is_range=True, host=IPAddress(target.split('-',1)[0]), host_end=IPAddress(target.split('-',1)[1])))
                nw_objs[target] = obj
            elif target in os_config.interface_groups:
                # normalize necessary interface groups
                obj = _create_network_objects_from_ifgroup(os_config.interface_groups[target])
                nw_objs[target] = obj
            elif target in set(os_config.interfaces.keys()) - set(os_config.interface_groups.keys()):
                # interface objects
                obj = _create_network_objects_from_interface(os_config.interfaces[target])
                nw_objs[target] = obj
            else:
                # currently unknown and not implemented network object
                FWOLogger.warning(f"[*] detected unknown network object {target} in rule:\n    {rule}")

def _normalize_network_objects(os_config: OPNsenseConfig) -> dict[str, NetworkObject]:
    #FWOLogger.debug("[*] _normalize_network_objects called...")

    normalized: dict[str, NetworkObject] = {}

    #https://github.com/CactuseSecurity/firewall-orchestrator/blob/9c5c072addcb8c72dc3283b1dd8b60f94fe0d86b/roles/importer/files/importer/models/networkobject.py#L7
    # NetworkObject: {
    #     obj_uid:          str
    #     obj_name:         str
    #     obj_ip:           IPNetwork   | None = None
    #     obj_ip_end:       IPNetwork   | None = None
    #     obj_color:        str
    #     obj_typ:          str                         // string: see types below
    #     obj_member_refs:  str         | None = None
    #     obj_member_names: str         | None = None
    #     obj_comment:      str         | None = None
    # }
    # obj_typ can be any of the following (see https://github.com/CactuseSecurity/firewall-orchestrator/blob/9c5c072addcb8c72dc3283b1dd8b60f94fe0d86b/roles/database/files/sql/creation/fworch-fill-stm.sql#L361): network, group, host, machines_range, dynamic_net_obj, sofaware_profiles_security_level, gateway, cluster_member, gateway_cluster, domain, group_with_exclusion, ip_range, uas_collection, sofaware_gateway, voip_gk, gsn_handover_group, voip_sip, simple-gateway

    # normalize host_aliases and net_aliases
    for alias_list in [os_config.host_aliases, os_config.net_aliases]:
        for a in alias_list:
            if a not in normalized:
                alias = alias_list[a]
                net_obj = _create_network_object_from_alias(alias, normalized, 0)
                normalized[net_obj.obj_name] = net_obj

    # normalize necessary misc aliases
    for a in os_config.aliases:
        if a in normalized:
            continue
        alias = os_config.aliases[a]
        if alias.type == AliasTypeEnum.GEOIP:
            nw_obj = _create_network_objects_from_geoip_alias(alias, normalized)
            normalized[nw_obj.obj_name] = nw_obj
        elif alias.type == AliasTypeEnum.URLTABLE:
            nw_obj = _create_network_objects_from_urltable_alias(alias)
            normalized[nw_obj.obj_name] = nw_obj

    # add special "Any" and "(self)" network object
    nwobj_any = _create_special_network_objects()
    for nany in nwobj_any:
        normalized[nany.obj_name] = nany

    _update_network_objects_from_access_rules(os_config, normalized)

    #TODO normalize non-necessary misc aliases
    #for a in os_config.aliases:
    #    net_obj = _create_network_object_from_alias(os_config.aliases[a], normalized, 0)
    #    normalized[net_obj.obj_name] = net_obj
    #    #FWOLogger.debug(f"[*] {net_obj}")

    #FWOLogger.debug("[*] _normalize_network_objects returns...")
    return normalized

#TODO OPNsense does not have zones instead interface groups could be used as a zone equivalent
#def _normalize_ifgroups_as_zone_objects(os_config: OPNsenseConfig) -> dict[str, Any]:
#    raise NotImplementedError("Importing Zone objects is not supported in dthe OPNsense 25ff import module.")
#    return

def _create_rulebases_from_access_rules(os_config: OPNsenseConfig, mgm_uid: str) -> list[Rulebase]:

    #https://github.com/CactuseSecurity/firewall-orchestrator/blob/9c5c072addcb8c72dc3283b1dd8b60f94fe0d86b/roles/importer/files/importer/models/rulebase.py#L6
    # Rulebase: {
    #       uid:        str
    #       name:       str
    #       mgm_uid:    str
    #       is_global:  bool
    #       rules:      dict[str, RuleNormalized]
    # }

    rbs_dict: dict[str, Rulebase] = {}
    rbs: list[Rulebase] = []
    rule_num = 0

    for rule in os_config.access_rules:
        r_normalized = _create_normalized_rule_from_access_rule(rule)
        # update rule priority
        r_normalized.rule_num = int(rule_num)
        r_normalized.rule_num_numeric = float(rule_num)
        rule_num += RULE_NUM_NUMERIC_STEPS
        # update rulebases based on ifgroups
        if rule.is_floating:
            # handle floating rules
            if "floating" in rbs_dict:
                # update "floating" rulebase
                rbs_dict["floating"].rules[r_normalized.rule_uid] = r_normalized
            else:
                # create "floating" Rulebase
                rb = Rulebase(
                    uid = fwo_base_generate_hash_from_dict({'rulebase': 'floating'}),
                    name = "floating",
                    mgm_uid = mgm_uid,
                    is_global = False,
                    rules = {
                        r_normalized.rule_uid: r_normalized
                    }
                )
                rbs_dict[rb.name] = rb
        elif len(rule.interface) == 1:
            # handle non floating rules with a defined interface
            if not rule.any_interface and not rule.interface_neg:
                iface = rule.interface[0]
                if iface in os_config.interface_groups:
                    if iface in rbs_dict:
                        # add rule to existing rulebase
                        rbs_dict[iface].rules[r_normalized.rule_uid] = r_normalized
                    else:
                        # create new rulebase for interface group
                        rb = Rulebase(
                            uid = fwo_base_generate_hash_from_dict({'rulebase': iface}),
                            name = iface,
                            mgm_uid = mgm_uid,
                            is_global = False,
                            rules = {
                                r_normalized.rule_uid: r_normalized
                            }
                        )
                        rbs_dict[rb.name] = rb
    for rb in rbs_dict:
        rbs.append(rbs_dict[rb])

    return rbs

def _get_rulebase_links_from_rulebases(rbs: list[Rulebase]) -> list[RulebaseLinkUidBased]:

    rb_links: list[RulebaseLinkUidBased] = []

    # RulebaseLinkUidBased: {
    #   from_rulebase_uid:  str | None = None
    #   from_rule_uid:      str | None = None
    #   to_rulebase_uid:    str
    #   link_type:          str = "section"                 // 'ordered', 'inline', 'concatenated' or 'domain' https://github.com/CactuseSecurity/firewall-orchestrator/blob/9c5c072addcb8c72dc3283b1dd8b60f94fe0d86b/roles/database/files/sql/creation/fworch-fill-stm.sql#L559
    #   is_initial:         bool
    #   is_global:          bool
    #   is_section:         bool
    # }

    for i in range(len(rbs)):
        link = RulebaseLinkUidBased(
            from_rulebase_uid = rbs[i-1].uid if i > 0 else None,
            from_rule_uid     = None,
            to_rulebase_uid   = rbs[i].uid,
            link_type         = "ordered",
            is_initial        = (i == 0),
            is_global         = True,
            is_section        = False,
        )
        rb_links.append(link)

    return rb_links

def _resolve_named_refs_in_rules(rbs: list[Rulebase], nw_objs: dict[str, NetworkObject], svc_obj: dict[str, ServiceObject]) -> None:
    for rb in rbs:
        for r_id in rb.rules:
            rule = rb.rules[r_id]
            old_src_refs = rule.rule_src_refs.split("|")
            old_dest_refs = rule.rule_dst_refs.split("|")
            old_svc_refs = rule.rule_svc_refs.split("|")

            new_src_refs = [nw_objs[src].obj_uid for src in old_src_refs]
            new_dest_refs = [nw_objs[dest].obj_uid for dest in old_dest_refs]
            new_svc_refs = [svc_obj[svc].svc_uid for svc in old_svc_refs]

            rb.rules[r_id].rule_src_refs = sort_and_join(new_src_refs)
            rb.rules[r_id].rule_dst_refs = sort_and_join(new_dest_refs)
            rb.rules[r_id].rule_svc_refs = sort_and_join(new_svc_refs)

def _normalize_interfaces(os_config: OPNsenseConfig) -> list[dict[str, Any]]:
    interfaces = []
    dev_id = 0

    for os_iface in os_config.interfaces:
        os_if = os_config.interfaces[os_iface]

        # ignore OPNsense interface groups here
        if os_if.type == "group":
            continue

        if os_if.ip4_address is not None and os_if.ip4_subnet is not None:

            iface4 = {
                "device_id"    : dev_id,                    # int
                "name"         : os_if.name + "_v4",        # str
                "ip"           : str(os_if.ip4_address),    # IPAddress
                "netmask_bits" : os_if.ip4_subnet,          # int
                "state_up"     : True ,                     # bool = True
                "ip_version"   : 4,                         # int = 4
            }

            dev_id += 1
            interfaces.append(iface4)

            iface6 = {
                "device_id"    : dev_id,                    # int
                "name"         : os_if.name + "_v4",        # str
                "ip"           : str(os_if.ip6_address),    # IPAddress
                "netmask_bits" : os_if.ip6_subnet,          # int
                "state_up"     : True ,                     # bool = True
                "ip_version"   : 6,                         # int = 4
            }

            dev_id += 1
            interfaces.append(iface6)

    return interfaces

def _normalize_opnsense_config(config_in: FwConfigManagerListController, import_state: ImportStateController) -> FwConfigManagerListController:

    # Parse the native configuration into structured objects
    FWOLogger.debug(f"[*] parsing native config...")
    native_config: OPNsenseConfig = _parse_opnsense_config(config_in.native_config)
    #FWOLogger.debug(f"[*] normalizing users...")
    #user_objects = _normalize_users(native_config)
    #FWOLogger.debug(f"[*] normalized {len(user_objects)} users...")
    FWOLogger.debug(f"[*] normalizing service objects...")
    svc_objects = _normalize_services(native_config)
    FWOLogger.debug(f"[*] normalized {len(svc_objects)} service objects...")
    FWOLogger.debug(f"[*] normalizing network objects...")
    network_objects = _normalize_network_objects(native_config)
    #[FWOLogger.debug(f"[*] {entry}:{network_object_map[entry]}") for entry in network_object_map]
    #FWOLogger.debug(f"[*] normalized network objects:\n{network_objects}")
    FWOLogger.debug(f"[*] normalized {len(network_objects)} network objects...")
    #TODO implement: normalizing OPNsense interface groups as Zones
    #FWOLogger.debug(f"[*] normalizing interface groups as zone objects...")
    #zone_objects = _normalize_ifgroups_as_zone_objects(native_config)
    #FWOLogger.debug(f"[*] normalized {len(network_objects)} interface groups as zone objects...")
    FWOLogger.debug(f"[*] normalizing access rules...")
    rulebases = _create_rulebases_from_access_rules(native_config, import_state.state.mgm_details.uid)
    [FWOLogger.debug(f"[*] normalized {len(rb.rules)} access rules in Rulebase {rb.name}...") for rb in rulebases]
    FWOLogger.debug(f"[*] normalizing interfaces for gateway definition...")
    interfaces = _normalize_interfaces(native_config)
    FWOLogger.debug(f"[*] normalized {len(interfaces)} interfaces for gateway definition...")

    rulebase_links = _get_rulebase_links_from_rulebases(rulebases)

    _resolve_named_refs_in_rules(rulebases, network_objects, svc_objects)
    new_nw_objects: dict[str, NetworkObject] = {}
    for name in network_objects:
        new_nw_objects[network_objects[name].obj_uid] = network_objects[name]
        if new_nw_objects[network_objects[name].obj_uid].obj_member_refs is None:
            continue
        member = []
        for m in new_nw_objects[network_objects[name].obj_uid].obj_member_refs.split("|"):
            member.append(network_objects[m].obj_uid)
        new_nw_objects[network_objects[name].obj_uid].obj_member_refs = sort_and_join(member)
    network_objects = new_nw_objects
    new_svc_objects: dict[str, ServiceObject] = {}
    for name in svc_objects:
        new_svc_objects[svc_objects[name].svc_uid] = svc_objects[name]
        if new_svc_objects[svc_objects[name].svc_uid].svc_member_refs is None:
            continue
        member = []
        for m in new_svc_objects[svc_objects[name].svc_uid].svc_member_refs.split("|"):
            member.append(svc_objects[m].svc_uid)
        new_svc_objects[svc_objects[name].svc_uid].svc_member_refs = sort_and_join(member)
    svc_objects = new_svc_objects


    FWOLogger.debug("[*] creating this gateway...")
    os_gateway = Gateway(
        Uid                   = native_config.hostname,
        Name                  = native_config.hostname,
        Routing               = [],
        #Interfaces            = interfaces, # not implemented on server-side yet
        Interfaces            = [],
        RulebaseLinks         = rulebase_links,
        GlobalPolicyUid       = None,
        EnforcedPolicyUids    = [],
        EnforcedNatPolicyUids = [],
        ImportDisabled        = False,
        ShowInUI              = True
    )

    normalized_config = FwConfigNormalized(
        action=ConfigAction.INSERT,
        network_objects = network_objects,
        service_objects = svc_objects,
        #users = user_objects,
        # currently firewall users != fwo users
        users = {},
        zone_objects = {},
        time_objects = {},
        rulebases = rulebases,
        gateways = [os_gateway]
    )

    config_in.ManagerSet[0].manager_uid = import_state.state.mgm_details.uid
    config_in.ManagerSet[0].configs = [normalized_config]

    return config_in

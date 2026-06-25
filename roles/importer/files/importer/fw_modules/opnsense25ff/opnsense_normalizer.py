# mapping the opnsense model into normalized import model

import json
from typing import Any

import fw_modules.opnsense25ff.opnsense_helper as os_helper
from fw_modules.opnsense25ff.opnsense_constants import (
    BUILTIN_SERVICE_PORTS,
    IP_PROTO_NUMBERS,
    MAX_DEPTH,
    PORT_BASED_PROTOCOLS,
)
from fw_modules.opnsense25ff.opnsense_model import (
    AliasTypeEnum,
    FilterRuleActionEnum,
    FilterRuleIPProtoEnum,
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
    OPNsensePortAlias,
)
from fw_modules.opnsense25ff.opnsense_parser import parse_opnsense_config
from fwo_base import ConfigAction, sort_and_join
from fwo_base import generate_hash_from_dict as fwo_base_generate_hash_from_dict
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.networkobject import NetworkObject
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased
from models.serviceobject import ServiceObject
from netaddr import IPAddress, IPNetwork

# ───────────────────────── helper ────────────────────────


def _create_network_object_from_host_definition(host: OPNsenseHost) -> NetworkObject:
    return NetworkObject(
        obj_uid=fwo_base_generate_hash_from_dict({"net_obj": host.name}),
        obj_name=host.name,
        obj_ip=IPNetwork(str(host.host)),
        obj_ip_end=IPNetwork(str(host.host_end if host.is_range else host.host)),
        obj_color="",
        obj_typ="ip_range" if host.is_range else "host",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=host.name,
    )


def _create_network_object_from_net_definition(net: OPNsenseNetwork) -> NetworkObject:
    return NetworkObject(
        obj_uid=fwo_base_generate_hash_from_dict({"net_obj": net.name}),
        obj_name=net.name,
        obj_ip=IPNetwork(f"{IPAddress(net.net.first)}"),
        obj_ip_end=IPNetwork(f"{IPAddress(net.net.last)}"),
        obj_color="",
        obj_typ="network",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=net.name,
    )


def _create_network_object_from_string(s: str) -> NetworkObject:
    return NetworkObject(
        obj_uid=fwo_base_generate_hash_from_dict({"net_obj": s}),
        obj_name=s,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=s,
    )


def _create_special_network_objects() -> list[NetworkObject]:

    self_obj = NetworkObject(
        obj_uid="(self)",
        obj_name="(self)",
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment="special network object references firewall itself",
    )
    any_v4 = NetworkObject(
        obj_uid="Any-v4",
        obj_name="Any-v4",
        obj_ip=IPNetwork("0.0.0.0/32", version=4),
        obj_ip_end=IPNetwork("255.255.255.255/32", version=4),
        obj_color="",
        obj_typ="ip_range",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment="special network object references any IPv4 address",
    )
    any_v6 = NetworkObject(
        obj_uid="Any-v6",
        obj_name="Any-v6",
        obj_ip=IPNetwork("::/128", version=6),
        obj_ip_end=IPNetwork("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128", version=6),
        obj_color="",
        obj_typ="ip_range",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment="special network object references any IPv6 address",
    )
    any_grp = NetworkObject(
        obj_uid="Any",
        obj_name="Any",
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=sort_and_join([any_v4.obj_name, any_v6.obj_name]),
        obj_member_names=sort_and_join([any_v4.obj_name, any_v6.obj_name]),
        obj_comment="special network object references any IPv4 and IPv6 address",
    )
    return [any_grp, any_v4, any_v6, self_obj]


def _network_ref_name(ref: str | OPNsenseHostAlias | OPNsenseNetworkAlias) -> str:
    if isinstance(ref, str):
        return ref
    return ref.name


def _service_ref_name(ref: str | OPNsensePortAlias) -> str:
    if isinstance(ref, str):
        return ref
    return ref.name


def _is_port_based_protocol(protocol: str) -> bool:
    return protocol.lower() in PORT_BASED_PROTOCOLS


def _protocol_service_name(rule: OPNsenseAccessRule) -> str:
    # OPNsense keeps "ICMP" in the protocol field even for IPv6 rules; disambiguate via the IP protocol.
    if rule.protocol.lower() == "icmp" and rule.ipprotocol == FilterRuleIPProtoEnum.INET6:
        return "ICMPv6"
    return rule.protocol.upper()


def _rule_service_names(rule: OPNsenseAccessRule) -> list[str]:
    # Port-based protocols (TCP/UDP and the "any" default) derive their services from the
    # destination ports. Non-port protocols (e.g. ICMP, ESP, GRE) become a protocol service.
    if _is_port_based_protocol(rule.protocol):
        return [_service_ref_name(ref) for ref in rule.dest_port]
    return [_protocol_service_name(rule)]


def _warn_max_depth_reached(depth: int) -> None:
    FWOLogger.warning(f"[-] depth {depth} reached maximum {MAX_DEPTH}. Abort recursion...")


def _member_name_for_string_child(child: str, normalized: dict[str, NetworkObject]) -> str:
    if child not in normalized:
        child_obj = _create_network_object_from_string(child)
        normalized[child_obj.obj_name] = child_obj
    return child


def _create_network_object_from_alias_child(
    child: OPNsenseHost | OPNsenseNetwork | OPNsenseHostAlias | OPNsenseNetworkAlias,
    normalized: dict[str, NetworkObject],
    depth: int,
) -> NetworkObject | None:
    if isinstance(child, OPNsenseHost):
        return _create_network_object_from_host_definition(child)
    if isinstance(child, OPNsenseNetwork):
        return _create_network_object_from_net_definition(child)
    if depth >= MAX_DEPTH:
        _warn_max_depth_reached(depth)
        return None
    return _create_network_object_from_alias(child, normalized, depth + 1)


def _member_name_for_alias_child(
    child: OPNsenseHost | OPNsenseNetwork | OPNsenseHostAlias | OPNsenseNetworkAlias,
    normalized: dict[str, NetworkObject],
    depth: int,
) -> str | None:
    if child.name in normalized:
        return child.name
    child_obj = _create_network_object_from_alias_child(child, normalized, depth)
    if child_obj is None:
        return None
    normalized[child_obj.obj_name] = child_obj
    return child_obj.obj_name


def _create_network_object_from_alias(
    alias: OPNsenseHostAlias | OPNsenseNetworkAlias, normalized: dict[str, NetworkObject], depth: int
) -> NetworkObject:
    member: list[str] = []

    for child in alias.childs:
        if isinstance(child, str):
            member.append(_member_name_for_string_child(child, normalized))
            continue
        child_name = _member_name_for_alias_child(child, normalized, depth)
        if child_name is not None:
            member.append(child_name)

    return NetworkObject(
        obj_uid=alias.uuid,
        obj_name=alias.name,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=sort_and_join(member) if len(member) > 0 else None,
        obj_member_names=sort_and_join(member) if len(member) > 0 else None,
        obj_comment=alias.description,
    )


def _create_any_svc_object() -> ServiceObject:
    return ServiceObject(
        svc_uid="Any",
        svc_name="Any",
        svc_port=1,
        svc_port_end=65535,
        svc_color="",
        svc_typ="simple",
        ip_proto=None,
        svc_member_refs=None,
        svc_member_names=None,
        svc_comment="special service object created during normalization",
        svc_timeout=None,
        rpc_nr=None,
    )


def _create_service_from_protocol(name: str) -> ServiceObject:
    return ServiceObject(
        svc_uid=fwo_base_generate_hash_from_dict({"svc_obj": name}),
        svc_name=name,
        svc_port=None,
        svc_port_end=None,
        svc_color="",
        svc_typ="simple",
        ip_proto=IP_PROTO_NUMBERS.get(name.lower()),
        svc_member_refs=None,
        svc_member_names=None,
        svc_comment=name,
        svc_timeout=None,
        rpc_nr=None,
    )


def _create_services_from_port_definition(port: OPNsensePort) -> ServiceObject:
    return ServiceObject(
        svc_uid=fwo_base_generate_hash_from_dict({"svc_obj": port.name}),
        svc_name=port.name,
        svc_port=port.port,
        svc_port_end=port.port_end if port.is_range else port.port,
        svc_color="",
        svc_typ="simple",
        ip_proto=None,
        svc_member_refs=None,
        svc_member_names=None,
        svc_comment=port.name,
        svc_timeout=None,
        rpc_nr=None,
    )


def _normalize_services_from_port_alias(
    alias: OPNsensePortAlias, normalized: dict[str, ServiceObject], depth: int
) -> ServiceObject:
    member: list[str] = []
    for child in alias.childs:
        if child.name not in normalized:
            if isinstance(child, OPNsensePort):
                child_svc = _create_services_from_port_definition(child)
                normalized[child_svc.svc_name] = child_svc
                member.append(child_svc.svc_name)
            elif depth < MAX_DEPTH:
                svc = _normalize_services_from_port_alias(child, normalized, depth + 1)
                normalized[svc.svc_name] = svc
                member.append(svc.svc_name)
            elif depth >= MAX_DEPTH:
                _warn_max_depth_reached(depth)
                continue
        else:
            member.append(child.name)

    service = ServiceObject(
        svc_uid=alias.uuid,
        svc_name=alias.name,
        svc_port=None,
        svc_port_end=None,
        svc_color="",
        svc_typ="group",
        ip_proto=None,
        svc_member_refs=sort_and_join(member),
        svc_member_names=sort_and_join(member),
        svc_comment=alias.description,
        svc_timeout=None,
        rpc_nr=None,
    )

    normalized[service.svc_name] = service

    return service


def _create_normalized_rule_from_access_rule(rule: OPNsenseAccessRule) -> RuleNormalized:
    rule_action: RuleAction = RuleAction.ACCEPT
    if rule.action == FilterRuleActionEnum.PASS:
        rule_action = RuleAction.ACCEPT
    elif rule.action == FilterRuleActionEnum.BLOCK:
        rule_action = RuleAction.DROP
    elif rule.action == FilterRuleActionEnum.REJECT:
        rule_action = RuleAction.REJECT

    os_rule_custom = {
        "os_rule_l2proto": rule.ipprotocol,
        "os_rule_l3proto": rule.protocol,
        "os_rule_direction": rule.direction,
        "os_rule_interface": rule.interface or None,
    }

    rule_source_objects = [_network_ref_name(ref) for ref in rule.source_address + rule.source_network]
    rule_dest_objects = [_network_ref_name(ref) for ref in rule.dest_address + rule.dest_network]
    rule_service_objects = _rule_service_names(rule)
    rule_name = rule.description or ""

    return RuleNormalized(
        rule_num=0,
        rule_num_numeric=0,
        rule_disabled=rule.disabled,
        rule_src_neg=rule.source_neg,
        rule_src=sort_and_join(rule_source_objects),
        rule_src_refs=sort_and_join(rule_source_objects),
        rule_dst_neg=rule.dest_neg,
        rule_dst=sort_and_join(rule_dest_objects),
        rule_dst_refs=sort_and_join(rule_dest_objects),
        rule_svc_neg=False,
        rule_svc=sort_and_join(rule_service_objects),
        rule_svc_refs=sort_and_join(rule_service_objects),
        rule_action=rule_action,
        rule_track=RuleTrack.LOG if rule.logging else RuleTrack.NONE,
        rule_installon=None,
        rule_time=None,
        rule_name=rule_name.split(":", 1)[0],
        rule_uid=rule.uuid,
        rule_custom_fields=json.dumps(os_rule_custom),
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=None,
        parent_rule_uid=None,
        last_hit=None,
        rule_comment=rule.description,
        rule_src_zone=None,
        rule_dst_zone=None,
        rule_head_text=None,
    )


# ──────────────────────────────────────────────────────────


def _port_service_from_dest_port(dest_port: str) -> ServiceObject | None:
    builtin_service_port = BUILTIN_SERVICE_PORTS.get(dest_port.lower())
    if builtin_service_port is not None:
        return _create_services_from_port_definition(
            OPNsensePort(name=dest_port, is_range=False, port=builtin_service_port, port_end=None)
        )
    plain_portlist_candidate = dest_port.split("-", 1)
    if not os_helper.is_int(plain_portlist_candidate[0]):
        return None
    port = int(plain_portlist_candidate[0])
    if len(plain_portlist_candidate) == 1:
        return _create_services_from_port_definition(
            OPNsensePort(name=dest_port, is_range=False, port=port, port_end=None)
        )
    if os_helper.is_int(plain_portlist_candidate[1]):
        return _create_services_from_port_definition(
            OPNsensePort(name=dest_port, is_range=True, port=port, port_end=int(plain_portlist_candidate[1]))
        )
    return None


def _update_service_objects_from_access_rules(
    rules: list[OPNsenseAccessRule], svc_objs: dict[str, ServiceObject]
) -> None:

    for rule in rules:
        # non-port protocols (ICMP, ESP, GRE, ...) become a dedicated protocol service
        if not _is_port_based_protocol(rule.protocol):
            protocol_name = _protocol_service_name(rule)
            if protocol_name not in svc_objs:
                svc_objs[protocol_name] = _create_service_from_protocol(protocol_name)
            continue

        # add all plain ports or port-ranges not currently normalized
        for dest_port in {ref for ref in rule.dest_port if isinstance(ref, str)} - set(svc_objs.keys()):
            svc = _port_service_from_dest_port(dest_port)
            if svc is not None:
                svc_objs[svc.svc_name] = svc


def _normalize_services(os_config: OPNsenseConfig) -> dict[str, ServiceObject]:

    normalized: dict[str, ServiceObject] = {}

    for a, alias in os_config.port_aliases.items():
        if a not in normalized:
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
                obj_uid=fwo_base_generate_hash_from_dict({"geo_obj": cc}),
                obj_name=cc,
                obj_ip=None,
                obj_ip_end=None,
                obj_color="",
                obj_typ="group",
                obj_member_refs=None,
                obj_member_names=None,
                obj_comment=f"{cc} country code: special network object created during normalization",
            )
            nw_objs[cc_obj.obj_name] = cc_obj
        members.append(cc)

    return NetworkObject(
        obj_uid=alias.uuid,
        obj_name=alias.name,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=sort_and_join(members),
        obj_member_names=sort_and_join(members),
        obj_comment=alias.description,
    )


def _create_network_objects_from_urltable_alias(alias: OPNsenseAlias) -> NetworkObject:
    return NetworkObject(
        obj_uid=alias.uuid,
        obj_name=alias.name,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=f"{alias.description}: {alias.value}",
    )


def _create_network_objects_from_ifgroup(ifgroup: OPNsenseIfGroup) -> NetworkObject:
    return NetworkObject(
        obj_uid=ifgroup.uuid,
        obj_name=ifgroup.name,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=ifgroup.description,
    )


def _create_network_objects_from_interface(interface: OPNsenseInterface) -> NetworkObject:
    return NetworkObject(
        obj_uid=fwo_base_generate_hash_from_dict({"iface_obj": interface.name}),
        obj_name=interface.name,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=f"{interface.hw_interface}: {interface.ip4_address}{interface.ip4_subnet}|{interface.ip6_address}|{interface.ip6_subnet} :{interface.description}",
    )


def _create_network_objects_from_iface_ip(interface: OPNsenseInterface) -> NetworkObject:
    return NetworkObject(
        obj_uid=fwo_base_generate_hash_from_dict({"iface_obj": f"{interface.name}ip"}),
        obj_name=f"{interface.name}ip",
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="group",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment=f"{interface.name}ip: {interface.ip4_address}{interface.ip4_subnet}|{interface.ip6_address}|{interface.ip6_subnet} :{interface.description}",
    )


def _access_rule_network_targets(rule: OPNsenseAccessRule) -> set[str]:
    return (
        {_network_ref_name(ref) for ref in rule.source_address}
        | {_network_ref_name(ref) for ref in rule.dest_address}
        | set(rule.source_network)
        | set(rule.dest_network)
    )


def _create_network_object_from_ip_range(target: str) -> NetworkObject:
    range_start, range_end = target.split("-", 1)
    return _create_network_object_from_host_definition(
        OPNsenseHost(
            name=target,
            is_range=True,
            host=IPAddress(range_start),
            host_end=IPAddress(range_end),
        )
    )


def _create_plain_network_object_from_target(target: str) -> NetworkObject | None:
    if os_helper.is_ip(target):
        return _create_network_object_from_host_definition(
            OPNsenseHost(name=target, is_range=False, host=IPAddress(target), host_end=None)
        )
    if os_helper.is_ip_subnet(target):
        return _create_network_object_from_net_definition(OPNsenseNetwork(name=target, net=IPNetwork(target)))
    if os_helper.is_ip_range(target):
        return _create_network_object_from_ip_range(target)
    return None


def _create_interface_network_object_from_target(target: str, os_config: OPNsenseConfig) -> NetworkObject | None:
    if target in os_config.interface_groups:
        return _create_network_objects_from_ifgroup(os_config.interface_groups[target])
    if target in os_config.interfaces and target not in os_config.interface_groups:
        return _create_network_objects_from_interface(os_config.interfaces[target])

    interface_name = target.removesuffix("ip")
    if interface_name in os_config.interfaces:
        return _create_network_objects_from_iface_ip(os_config.interfaces[interface_name])
    return None


def _create_network_object_from_rule_target(target: str, os_config: OPNsenseConfig) -> NetworkObject | None:
    obj = _create_plain_network_object_from_target(target)
    if obj is not None:
        return obj
    return _create_interface_network_object_from_target(target, os_config)


def _update_network_objects_from_access_rules(os_config: OPNsenseConfig, nw_objs: dict[str, NetworkObject]) -> None:
    for rule in os_config.access_rules:
        for target in _access_rule_network_targets(rule):
            if target in nw_objs:
                continue
            obj = _create_network_object_from_rule_target(target, os_config)
            if obj is None:
                FWOLogger.warning(f"[*] detected unknown network object {target} in rule:\n    {rule}")
                continue
            nw_objs[target] = obj


def _normalize_network_objects(os_config: OPNsenseConfig) -> dict[str, NetworkObject]:
    normalized: dict[str, NetworkObject] = {}

    # normalize host_aliases and net_aliases
    for alias_list in [os_config.host_aliases, os_config.net_aliases]:
        for a, alias in alias_list.items():
            if a not in normalized:
                net_obj = _create_network_object_from_alias(alias, normalized, 0)
                normalized[net_obj.obj_name] = net_obj

    # normalize necessary misc aliases
    for a, alias in os_config.aliases.items():
        if a in normalized:
            continue
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

    return normalized


def _create_rulebase(name: str, mgm_uid: str, rule_uid: str, rule: RuleNormalized) -> Rulebase:
    return Rulebase(
        uid=fwo_base_generate_hash_from_dict({"rulebase": name}),
        name=name,
        mgm_uid=mgm_uid,
        is_global=False,
        rules={rule_uid: rule},
    )


def _access_rule_rulebase_name(rule: OPNsenseAccessRule, os_config: OPNsenseConfig) -> str | None:
    if rule.is_floating:
        return "floating"
    has_single_positive_interface = len(rule.interface) == 1 and not rule.any_interface and not rule.interface_neg
    if has_single_positive_interface and (
        rule.interface[0] in os_config.interfaces or rule.interface[0] in os_config.interface_groups
    ):
        return rule.interface[0]
    return None


def _upsert_rulebase_rule(
    rbs_dict: dict[str, Rulebase], rulebase_name: str, mgm_uid: str, rule_uid: str, rule: RuleNormalized
) -> None:
    if rulebase_name not in rbs_dict:
        rbs_dict[rulebase_name] = _create_rulebase(rulebase_name, mgm_uid, rule_uid, rule)
        return
    rbs_dict[rulebase_name].rules[rule_uid] = rule


def _create_rulebases_from_access_rules(os_config: OPNsenseConfig, mgm_uid: str) -> list[Rulebase]:
    rbs_dict: dict[str, Rulebase] = {}

    for rule in os_config.access_rules:
        r_normalized = _create_normalized_rule_from_access_rule(rule)
        rule_uid = r_normalized.rule_uid
        if rule_uid is None:
            FWOLogger.warning(f"[*] skipping OPNsense rule without uid:\n    {rule}")
            continue
        rulebase_name = _access_rule_rulebase_name(rule, os_config)
        if rulebase_name is not None:
            _upsert_rulebase_rule(rbs_dict, rulebase_name, mgm_uid, rule_uid, r_normalized)
    return list(rbs_dict.values())


def _get_rulebase_links_from_rulebases(rbs: list[Rulebase]) -> list[RulebaseLinkUidBased]:

    rb_links: list[RulebaseLinkUidBased] = []

    for i in range(len(rbs)):
        link = RulebaseLinkUidBased(
            from_rulebase_uid=rbs[i - 1].uid if i > 0 else None,
            from_rule_uid=None,
            to_rulebase_uid=rbs[i].uid,
            link_type="ordered",
            is_initial=(i == 0),
            is_global=True,
            is_section=False,
        )
        rb_links.append(link)

    return rb_links


def _get_gateway_name(native_config: OPNsenseConfig, import_state: ImportStateController) -> str:
    mgm_details = import_state.state.mgm_details
    if mgm_details.devices and "name" in mgm_details.devices[0] and mgm_details.devices[0]["name"]:
        return str(mgm_details.devices[0]["name"])
    if mgm_details.name:
        return mgm_details.name
    if native_config.hostname:
        return native_config.hostname
    if mgm_details.hostname:
        return mgm_details.hostname
    raise FwoImporterError("Management details must contain a device name, management name, or hostname.")


def _resolve_named_refs_in_rules(
    rbs: list[Rulebase], nw_objs: dict[str, NetworkObject], svc_obj: dict[str, ServiceObject]
) -> None:
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
    interfaces: list[dict[str, Any]] = []
    dev_id = 0

    for os_if in os_config.interfaces.values():
        # ignore OPNsense interface groups here
        if os_if.type == "group":
            continue

        if os_if.ip4_address is not None and os_if.ip4_subnet is not None:
            iface4 = {
                "device_id": dev_id,  # int
                "name": os_if.name + "_v4",  # str
                "ip": str(os_if.ip4_address),  # IPAddress
                "netmask_bits": os_if.ip4_subnet,  # int
                "state_up": True,  # bool
                "ip_version": 4,  # int
            }

            dev_id += 1
            interfaces.append(iface4)

        if os_if.ip6_address is not None and os_if.ip6_subnet is not None:
            iface6 = {
                "device_id": dev_id,  # int
                "name": os_if.name + "_v6",  # str
                "ip": str(os_if.ip6_address),  # IPAddress
                "netmask_bits": os_if.ip6_subnet,  # int
                "state_up": True,  # bool
                "ip_version": 6,  # int
            }

            dev_id += 1
            interfaces.append(iface6)

    return interfaces


def normalize_opnsense_config(
    config_in: FwConfigManagerListController, import_state: ImportStateController
) -> FwConfigManagerListController:

    # Parse the native configuration into structured objects
    FWOLogger.debug("[*] parsing native config...")
    native_config: OPNsenseConfig = parse_opnsense_config(config_in.native_config or {})
    FWOLogger.debug("[*] normalizing service objects...")
    svc_objects = _normalize_services(native_config)
    FWOLogger.debug(f"[*] normalized {len(svc_objects)} service objects...")
    FWOLogger.debug("[*] normalizing network objects...")
    network_objects = _normalize_network_objects(native_config)
    FWOLogger.debug(f"[*] normalized {len(network_objects)} network objects...")
    FWOLogger.debug("[*] normalizing access rules...")
    rulebases = _create_rulebases_from_access_rules(native_config, import_state.state.mgm_details.uid)
    [FWOLogger.debug(f"[*] normalized {len(rb.rules)} access rules in Rulebase {rb.name}...") for rb in rulebases]
    FWOLogger.debug("[*] normalizing interfaces for gateway definition...")
    interfaces = _normalize_interfaces(native_config)
    FWOLogger.debug(f"[*] normalized {len(interfaces)} interfaces for gateway definition...")

    rulebase_links = _get_rulebase_links_from_rulebases(rulebases)

    _resolve_named_refs_in_rules(rulebases, network_objects, svc_objects)
    new_nw_objects: dict[str, NetworkObject] = {}
    for name in network_objects:
        new_nw_objects[network_objects[name].obj_uid] = network_objects[name]
        obj_member_refs = new_nw_objects[network_objects[name].obj_uid].obj_member_refs
        if obj_member_refs is None:
            continue
        member = [network_objects[m].obj_uid for m in obj_member_refs.split("|")]
        new_nw_objects[network_objects[name].obj_uid].obj_member_refs = sort_and_join(member)
    network_objects = new_nw_objects
    new_svc_objects: dict[str, ServiceObject] = {}
    for name in svc_objects:
        new_svc_objects[svc_objects[name].svc_uid] = svc_objects[name]
        svc_member_refs = new_svc_objects[svc_objects[name].svc_uid].svc_member_refs
        if svc_member_refs is None:
            continue
        member: list[str] = []
        for m in svc_member_refs.split("|"):
            member.append(svc_objects[m].svc_uid)
        new_svc_objects[svc_objects[name].svc_uid].svc_member_refs = sort_and_join(member)
    svc_objects = new_svc_objects

    FWOLogger.debug("[*] creating this gateway...")
    gateway_name = _get_gateway_name(native_config, import_state)
    os_gateway = Gateway(
        Uid=gateway_name,
        Name=gateway_name,
        Routing=[],
        Interfaces=[],
        RulebaseLinks=rulebase_links,
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True,
    )

    normalized_config = FwConfigNormalized(
        action=ConfigAction.INSERT,
        network_objects=network_objects,
        service_objects=svc_objects,
        # currently firewall users != fwo users
        users={},
        zone_objects={},
        time_objects={},
        rulebases=rulebases,
        gateways=[os_gateway],
    )

    config_in.ManagerSet[0].manager_uid = import_state.state.mgm_details.uid
    config_in.ManagerSet[0].configs = [normalized_config]

    return config_in

# normalizing OPNsense services (ports, port aliases and rule protocols) into service objects

import fw_modules.opnsense25ff.opnsense_helper as os_helper
from fw_modules.opnsense25ff.opnsense_constants import (
    BUILTIN_SERVICE_PORTS,
    IP_PROTO_NUMBERS,
    MAX_DEPTH,
    PORT_BASED_PROTOCOLS,
    PORT_RANGE_SEPARATORS,
    QUALIFIABLE_PORT_PROTOCOLS,
    SERVICE_PROTOCOL_SEPARATOR,
)
from fw_modules.opnsense25ff.opnsense_model import (
    FilterRuleIPProtoEnum,
    OPNsenseAccessRule,
    OPNsenseConfig,
    OPNsensePort,
    OPNsensePortAlias,
)
from fwo_base import generate_hash_from_dict as fwo_base_generate_hash_from_dict
from fwo_base import sort_and_join
from fwo_log import FWOLogger
from models.serviceobject import ServiceObject


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


def _rule_service_protocol(rule: OPNsenseAccessRule) -> str | None:
    # A service object carries a single IP protocol, so only rules restricted to TCP or UDP
    # can be qualified; "any" and "tcp/udp" stay protocol-agnostic.
    protocol = rule.protocol.lower()
    if protocol in QUALIFIABLE_PORT_PROTOCOLS:
        return protocol
    return None


def _qualified_service_name(name: str, protocol: str | None) -> str:
    return name if protocol is None else f"{name}{SERVICE_PROTOCOL_SEPARATOR}{protocol}"


def rule_service_names(rule: OPNsenseAccessRule) -> list[str]:
    # Port-based protocols (TCP/UDP and the "any" default) derive their services from the
    # destination ports. Non-port protocols (e.g. ICMP, ESP, GRE) become a protocol service.
    if _is_port_based_protocol(rule.protocol):
        protocol = _rule_service_protocol(rule)
        return [_qualified_service_name(_service_ref_name(ref), protocol) for ref in rule.dest_port]
    return [_protocol_service_name(rule)]


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
    ip_proto = IP_PROTO_NUMBERS.get(name.lower())
    if ip_proto is None:
        FWOLogger.warning(
            f"[-] _create_service_from_protocol: no IP protocol number known for {name} - "
            "creating service without protocol"
        )
    return ServiceObject(
        svc_uid=fwo_base_generate_hash_from_dict({"svc_obj": name}),
        svc_name=name,
        svc_port=None,
        svc_port_end=None,
        svc_color="",
        svc_typ="simple",
        ip_proto=ip_proto,
        svc_member_refs=None,
        svc_member_names=None,
        svc_comment=name,
        svc_timeout=None,
        rpc_nr=None,
    )


def _create_placeholder_service(name: str) -> ServiceObject:
    return ServiceObject(
        svc_uid=fwo_base_generate_hash_from_dict({"svc_obj": name}),
        svc_name=name,
        svc_port=None,
        svc_port_end=None,
        svc_color="",
        svc_typ="simple",
        ip_proto=None,
        svc_member_refs=None,
        svc_member_names=None,
        svc_comment=f"placeholder for unresolved named port {name} created during normalization",
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
                os_helper.warn_max_depth_reached(depth)
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


def _split_port_range(dest_port: str) -> list[str]:
    # OPNsense writes port ranges as "8000:8080", imported configs may use "8000-8080"
    for separator in PORT_RANGE_SEPARATORS:
        if separator in dest_port:
            return dest_port.split(separator, 1)
    return [dest_port]


def _port_service_from_dest_port(dest_port: str) -> ServiceObject | None:
    builtin_service_port = BUILTIN_SERVICE_PORTS.get(dest_port.lower())
    if builtin_service_port is not None:
        return _create_services_from_port_definition(
            OPNsensePort(name=dest_port, is_range=False, port=builtin_service_port, port_end=None)
        )
    plain_portlist_candidate = _split_port_range(dest_port)
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


def _qualify_service(base: ServiceObject, protocol: str, member_names: str | None) -> ServiceObject:
    qualified_name = _qualified_service_name(base.svc_name, protocol)
    return ServiceObject(
        svc_uid=fwo_base_generate_hash_from_dict({"svc_obj": qualified_name}),
        svc_name=qualified_name,
        svc_port=base.svc_port,
        svc_port_end=base.svc_port_end,
        svc_color="",
        svc_typ=base.svc_typ,
        # only the leaf services carry the protocol, groups stay without one like all other groups
        ip_proto=None if base.svc_typ == "group" else IP_PROTO_NUMBERS.get(protocol),
        svc_member_refs=member_names,
        svc_member_names=member_names,
        svc_comment=base.svc_comment,
        svc_timeout=None,
        rpc_nr=None,
    )


def _register_qualified_service(
    name: str, protocol: str, svc_objs: dict[str, ServiceObject], depth: int
) -> ServiceObject | None:
    # port aliases carry no protocol in OPNsense - the protocol comes from the referencing rule,
    # so an alias used by a TCP and a UDP rule needs one service object per protocol
    qualified_name = _qualified_service_name(name, protocol)
    if qualified_name in svc_objs:
        return svc_objs[qualified_name]
    base = svc_objs.get(name)
    if base is None:
        return None
    if depth >= MAX_DEPTH:
        os_helper.warn_max_depth_reached(depth)
        return None

    member_names: str | None = None
    if base.svc_member_names:
        members = [
            _qualified_member_name(member, protocol, svc_objs, depth) for member in base.svc_member_names.split("|")
        ]
        member_names = sort_and_join(members)

    qualified = _qualify_service(base, protocol, member_names)
    svc_objs[qualified_name] = qualified
    return qualified


def _qualified_member_name(member: str, protocol: str, svc_objs: dict[str, ServiceObject], depth: int) -> str:
    qualified_member = _register_qualified_service(member, protocol, svc_objs, depth + 1)
    return qualified_member.svc_name if qualified_member is not None else member


def _create_port_service_for_rule(dest_port: str, rule: OPNsenseAccessRule) -> ServiceObject:
    svc = _port_service_from_dest_port(dest_port)
    if svc is None:
        FWOLogger.warning(
            f"[-] _update_service_objects_from_access_rules: unresolved named port {dest_port} "
            f"in rule {rule.uuid} - creating placeholder service"
        )
        svc = _create_placeholder_service(dest_port)
    return svc


def _update_service_objects_from_rule_ports(rule: OPNsenseAccessRule, svc_objs: dict[str, ServiceObject]) -> None:
    protocol = _rule_service_protocol(rule)

    for ref in rule.dest_port:
        dest_port = _service_ref_name(ref)
        if _qualified_service_name(dest_port, protocol) in svc_objs:
            continue
        if protocol is not None and dest_port in svc_objs:
            # known alias (or the special "Any" service): derive the protocol variant from it
            _register_qualified_service(dest_port, protocol, svc_objs, 0)
            continue
        svc = _create_port_service_for_rule(dest_port, rule)
        if protocol is not None:
            svc = _qualify_service(svc, protocol, None)
        svc_objs[svc.svc_name] = svc


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

        _update_service_objects_from_rule_ports(rule, svc_objs)


def normalize_services(os_config: OPNsenseConfig) -> dict[str, ServiceObject]:

    normalized: dict[str, ServiceObject] = {}

    for a, alias in os_config.port_aliases.items():
        if a not in normalized:
            _normalize_services_from_port_alias(alias, normalized, 0)

    # add special "Any" service objects
    svc_any = _create_any_svc_object()
    normalized[svc_any.svc_name] = svc_any

    _update_service_objects_from_access_rules(os_config.access_rules, normalized)

    return normalized

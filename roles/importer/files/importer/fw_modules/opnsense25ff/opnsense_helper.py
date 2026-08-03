from typing import Any, TypeAlias, TypeGuard, cast

from fw_modules.opnsense25ff.opnsense_constants import MAX_DEPTH
from fw_modules.opnsense25ff.opnsense_model import (
    OPNsenseAccessRule,
    OPNsenseAlias,
    OPNsenseConfig,
    OPNsenseHost,
    OPNsenseHostAlias,
    OPNsenseNATRule,
    OPNsenseNetwork,
    OPNsenseNetworkAlias,
    OPNsensePort,
    OPNsensePortAlias,
)
from fwo_log import FWOLogger
from netaddr import IPAddress, IPNetwork
from netaddr.core import AddrFormatError

PortRef: TypeAlias = str | OPNsensePortAlias
AddressRef: TypeAlias = str | OPNsenseHostAlias | OPNsenseNetworkAlias
RuleRef: TypeAlias = OPNsenseAlias | OPNsenseAccessRule | OPNsenseNATRule
AliasChild: TypeAlias = str | OPNsenseHost | OPNsenseNetwork | OPNsenseHostAlias | OPNsenseNetworkAlias


def warn_max_depth_reached(depth: int) -> None:
    FWOLogger.warning(f"[-] depth {depth} reached maximum {MAX_DEPTH}. Abort recursion...")


# ── accessors for the xmltodict representation of config.xml ──
# xmltodict maps a single child element to a dict and repeated ones to a list, so every
# lookup has to cope with both shapes as well as with absent elements


def is_dict(value: object) -> TypeGuard[dict[str, Any]]:
    return isinstance(value, dict)


def as_object_list(value: Any) -> list[object]:
    return cast("list[object]", value)


def get_value(data: dict[str, Any], *keys: str) -> object:
    current: object = data
    for key in keys:
        if not is_dict(current):
            return None
        current = current.get(key)
    return current


def get_dict(data: dict[str, Any], *keys: str) -> dict[str, Any]:
    current = get_value(data, *keys)
    return current if is_dict(current) else {}


def as_dict_list(value: object) -> list[dict[str, Any]]:
    if is_dict(value):
        return [value]
    if isinstance(value, list):
        return [item for item in as_object_list(value) if is_dict(item)]
    return []


def is_int(s: str) -> bool:
    try:
        int(s)
        return True
    except ValueError:
        return False


def is_ip(s: str) -> bool:
    try:
        IPAddress(s)
        return True
    except (ValueError, AddrFormatError):
        return False


def is_ip_subnet(s: str) -> bool:
    if is_ip(s):
        return False
    try:
        IPNetwork(s)
        return True
    except (ValueError, AddrFormatError):
        return False


def is_ip_range(s: str) -> bool:
    try:
        start, end = s.split("-", 1)
        IPAddress(start)
        IPAddress(end)
        return True
    except (ValueError, AddrFormatError):
        return False


def link_opnsense_ports_from_port_aliases(config: OPNsenseConfig) -> None:
    port_aliases = config.port_aliases
    for alias_name, alias in port_aliases.items():
        for p in alias.value:
            if is_int(p.split(":", 1)[0]):
                # the plain port literal is used as name so that alias members and ports
                # referenced directly by a rule normalize to the same service object
                p_name = p
                p_is_range = False
                p_port = 0
                p_port_end = 0
                if ":" in p:
                    start, end = p.split(":", 1)
                    p_port = int(start)
                    p_port_end = int(end)
                    p_is_range = True
                else:
                    p_port = int(p)
                    p_port_end = int(p)
                    p_is_range = False
                alias.childs.append(OPNsensePort(name=p_name, is_range=p_is_range, port=p_port, port_end=p_port_end))
            elif p in port_aliases:
                alias.childs.append(port_aliases[p])
        if len(alias.childs) != len(alias.value):
            FWOLogger.warning(
                "[-] _link_opnsense_ports_from_port_aliases: "
                f"port alias child count inconsistent for {alias_name}:\n    {alias}"
            )


def _link_port_refs(
    refs: list[PortRef],
    port_aliases: dict[str, OPNsensePortAlias],
    used_by: OPNsenseAccessRule | OPNsenseNATRule,
) -> None:
    for port_ref in list(refs):
        if not isinstance(port_ref, str):
            continue
        alias = port_aliases.get(port_ref)
        if alias is None:
            continue
        refs.remove(port_ref)
        refs.append(alias)
        if used_by not in alias.is_used_by:
            alias.is_used_by.append(used_by)


def _link_address_refs(
    refs: list[AddressRef],
    host_aliases: dict[str, OPNsenseHostAlias],
    net_aliases: dict[str, OPNsenseNetworkAlias],
    used_by: OPNsenseAccessRule | OPNsenseNATRule,
) -> None:
    for address_ref in list(refs):
        if not isinstance(address_ref, str):
            continue
        alias: OPNsenseHostAlias | OPNsenseNetworkAlias | None = host_aliases.get(address_ref) or net_aliases.get(
            address_ref
        )
        if alias is None:
            continue
        refs.remove(address_ref)
        refs.append(alias)
        if used_by not in alias.is_used_by:
            alias.is_used_by.append(used_by)


def xlinking_rules_to_aliases(config: OPNsenseConfig) -> None:
    host_aliases, net_aliases, port_aliases = config.host_aliases, config.net_aliases, config.port_aliases

    for access_rule in config.access_rules:
        _link_port_refs(access_rule.source_port, port_aliases, access_rule)
        _link_port_refs(access_rule.dest_port, port_aliases, access_rule)
        _link_address_refs(access_rule.source_address, host_aliases, net_aliases, access_rule)
        _link_address_refs(access_rule.dest_address, host_aliases, net_aliases, access_rule)

    for nat_rule in config.nat_rules:
        _link_port_refs(nat_rule.source_port, port_aliases, nat_rule)
        _link_port_refs(nat_rule.dest_port, port_aliases, nat_rule)
        _link_port_refs(nat_rule.xlat_port, port_aliases, nat_rule)
        _link_address_refs(nat_rule.source_net, host_aliases, net_aliases, nat_rule)
        _link_address_refs(nat_rule.source_addr, host_aliases, net_aliases, nat_rule)
        _link_address_refs(nat_rule.dest_net, host_aliases, net_aliases, nat_rule)
        _link_address_refs(nat_rule.dest_addr, host_aliases, net_aliases, nat_rule)
        _link_address_refs(nat_rule.xlat_addr, host_aliases, net_aliases, nat_rule)


def _create_host_alias_child(value: str) -> OPNsenseHost:
    # the plain address literal is used as name so that alias members and addresses
    # referenced directly by a rule normalize to the same network object
    is_range = is_ip_range(value)
    start, end = value.split("-", 1) if is_range else (value, value)
    return OPNsenseHost(
        name=value,
        is_range=is_range,
        host=IPAddress(start),
        host_end=IPAddress(end),
    )


def _resolve_net_or_host_child(value: str, config: OPNsenseConfig) -> AliasChild:
    if value in config.host_aliases:
        return config.host_aliases[value]
    if value in config.net_aliases:
        return config.net_aliases[value]
    if is_ip(value) or is_ip_range(value):
        return _create_host_alias_child(value)
    if is_ip_subnet(value):
        return OPNsenseNetwork(name=value, net=IPNetwork(value))
    return value


def enrich_opnsense_net_and_hosts(config: OPNsenseConfig) -> None:

    for alias_list in [config.host_aliases, config.net_aliases]:
        for alias in alias_list.values():
            for value in alias.value:
                alias.childs.append(_resolve_net_or_host_child(value, config))

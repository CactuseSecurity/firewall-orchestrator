from typing import TypeAlias

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
                p_name = "__p_" + p
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


def enrich_opnsense_net_and_hosts(config: OPNsenseConfig) -> None:

    for alias_list in [config.host_aliases, config.net_aliases]:
        for alias in alias_list.values():
            for value in alias.value:
                if value in config.host_aliases:
                    alias.childs.append(config.host_aliases[value])
                elif value in config.net_aliases:
                    alias.childs.append(config.net_aliases[value])
                elif is_ip(value) or is_ip_range(value):
                    # single ips or ip ranges
                    host = OPNsenseHost(
                        name="__h_" + value,
                        is_range=is_ip_range(value),
                        host=IPAddress(value.split("-", 1)[0]),
                        host_end=IPAddress(value.split("-", 1)[1])
                        if is_ip_range(value)
                        else IPAddress(value.split("-", 1)[0]),
                    )
                    alias.childs.append(host)
                elif is_ip_subnet(value):
                    # ip subnet aliases
                    net = OPNsenseNetwork(name="__n_" + value, net=IPNetwork(value))
                    alias.childs.append(net)
                else:
                    # arbitrary values
                    alias.childs.append(value)

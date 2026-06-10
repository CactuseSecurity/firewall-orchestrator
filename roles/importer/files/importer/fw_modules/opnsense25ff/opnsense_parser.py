# parsing retrieved config.xml from OPNsense into opnsense_model

import json
import sys
from datetime import datetime
from typing import Any

import fw_modules.opnsense25ff.opnsense_helper as os_helper
import xmltodict
from fw_modules.opnsense25ff.opnsense_model import (AliasTypeEnum,
                                                    OPNsenseAccessRule,
                                                    OPNsenseAlias,
                                                    OPNsenseConfig,
                                                    OPNsenseGateway,
                                                    OPNsenseHost,
                                                    OPNsenseHostAlias,
                                                    OPNsenseIfGroup,
                                                    OPNsenseInterface,
                                                    OPNsenseNATRule,
                                                    OPNsenseNetwork,
                                                    OPNsenseNetworkAlias,
                                                    OPNsensePort,
                                                    OPNsensePortAlias,
                                                    OPNsenseUser,
                                                    OPNsenseUserGroup)
from fw_modules.opnsense25ff.opnsense_sanitizer import \
    remove_opnsense_sensitive_data
from fwo_log import FWOLogger
from netaddr import IPAddress, IPNetwork


def _helper_is_int(s):
    try:
        int(s)
        return True
    except ValueError:
        return False

def _helper_is_ip(s):
    try:
        IPAddress(s)
        return True
    except:
        return False

def _helper_is_ip_subnet(s):
    if _helper_is_ip(s):
        return False
    try:
        IPNetwork(s)
        return True
    except:
        return False

def _parse_opnsense_hostname(config: dict[str, Any]) -> str:
    hostname = config.get("opnsense", {}).get("system", {}).get("hostname", {})
    domain = config.get("opnsense", {}).get("system", {}).get("domain", {})

    if hostname and domain:
        return f"{hostname}.{domain}"
    else:
        FWOLogger.debug(f"[-] _parse_hostname: hostname or domain not defined")
        return None

def _parse_timestamp(seconds: str) -> str | None:
    if seconds:
        seconds = float(seconds)
        return datetime.fromtimestamp(seconds).isoformat()
    else:
        FWOLogger.debug(f"[-] _parse_timestamp: seconds not defined")
        return None

def _parse_opnsense_user_groups(config: dict[str, Any]) -> list[OPNsenseUserGroup] | None:
    groups = config.get("opnsense", {}).get("system", {}).get("group", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_user_groups: groups\n    {groups}")

    user_groups: list[OPNsenseUserGroup] = []

    for group in groups:
        group_parsed = OPNsenseUserGroup.model_validate(group)
        user_groups.append(group_parsed)

    return user_groups

def _parse_opnsense_users(config: dict[str, Any]) -> list[OPNsenseUser] | None:
    users = config.get("opnsense", {}).get("system", {}).get("user", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_users: users\n    {users}")

    users_parsed: list[OPNsenseUser] = []

    for user in users:
        user_parsed = OPNsenseUser.model_validate(user)
        users_parsed.append(user_parsed)

    return users_parsed

def _parse_opnsense_interfaces(config: dict[str, Any]) -> dict[str, OPNsenseInterface] | None:
    interfaces = config.get("opnsense", {}).get("interfaces", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_interfaces: interfaces\n    {interfaces}")

    ifaces_parsed: dict[str, OPNsenseInterface] = {}

    for iface in interfaces:
        if_parsed = OPNsenseInterface.model_validate(interfaces[iface])
        if_parsed.name = iface
        ifaces_parsed[if_parsed.name] = if_parsed

    return ifaces_parsed

def _parse_opnsense_if_groups(config: dict[str, Any]) -> list[OPNsenseIfGroup] | None:
    ifgroups = config.get("opnsense", {}).get("ifgroups", {}).get("ifgroupentry", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_if_groups: ifgroups\n    {ifgroups}")

    ifgroups_parsed: dict[str, OPNsenseInterface] = {}

    for ifgroup in ifgroups:
        ifgroup_parsed = OPNsenseIfGroup.model_validate(ifgroup)
        ifgroups_parsed[ifgroup_parsed.name] = ifgroup_parsed

    return ifgroups_parsed

def _parse_opnsense_access_rules(config: dict[str, Any]) -> list[OPNsenseAccessRule] | None:
    rules = config.get("opnsense", {}).get("filter", {}).get("rule", {})
    #FWOLogger.debug(f"[*] rules\n    {rules}")

    rules_parsed: list[OPNsenseAccessRule] = []

    for rule in rules:

        rule_parsed = OPNsenseAccessRule.model_validate(rule)
        if 'Any' in rule_parsed.interface:
            rule_parsed.any_interface = True
        if 'any' in rule.get("source", {}):
            rule_parsed.source_address = ['Any']
        if 'any' in rule.get("dest", {}):
            rule_parsed.dest_address = ['Any']
        rules_parsed.append(rule_parsed)

    return rules_parsed

def _parse_opnsense_nat_rules(config: dict[str, Any]) -> list[OPNsenseNATRule] | None:
    outbound_rules = config.get("opnsense", {}).get("nat", {}).get("outbound", {}).get("rule", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_nat_rules: rules\n    {outbound_rules}")

    rules_parsed: list[OPNsenseNATRule] = []

    for rule in outbound_rules:
        rule_parsed = OPNsenseNATRule.model_validate(rule)
        rule_parsed.is_outbound = True
        rules_parsed.append(rule_parsed)

    return rules_parsed

def _parse_opnsense_aliases(config: dict[str, Any]) -> tuple[dict[str, OPNsenseAlias], dict[str, OPNsenseHostAlias], list[OPNsenseNetworkAlias], dict[str, OPNsensePortAlias]]:
    aliases = config.get("opnsense", {}).get("OPNsense", {}).get("Firewall", {}).get("Alias", {}).get("aliases", {}).get("alias", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_aliases: aliases\n    {aliases}")

    misc_aliases_parsed: dict[str, OPNsenseAlias] = {}
    port_aliases_parsed: dict[str, OPNsensePortAlias] = {}
    host_aliases_parsed: dict[str, OPNsenseHostAlias] = {}
    net_aliases_parsed:  dict[str, OPNsenseNetAlias] = {}

    for alias in aliases:
        if alias.get("type") == AliasTypeEnum.HOST:
            alias_parsed = OPNsenseHostAlias.model_validate(alias)
            host_aliases_parsed[alias_parsed.name] = alias_parsed
        elif alias.get("type") == AliasTypeEnum.NETWORK:
            alias_parsed = OPNsenseNetworkAlias.model_validate(alias)
            net_aliases_parsed[alias_parsed.name] = alias_parsed
        elif alias.get("type") == AliasTypeEnum.PORT:
            alias_parsed = OPNsensePortAlias.model_validate(alias)
            port_aliases_parsed[alias_parsed.name] = alias_parsed
        else:
            alias_parsed = OPNsenseAlias.model_validate(alias)
            misc_aliases_parsed[alias_parsed.name] = alias_parsed

    return misc_aliases_parsed, host_aliases_parsed, net_aliases_parsed, port_aliases_parsed

def _parse_opnsense_host_aliases(config: dict[str, Any]) -> list[OPNsenseAlias] | None:
    aliases = config.get("opnsense", {}).get("OPNsense", {}).get("Firewall", {}).get("Alias", {}).get("aliases", {}).get("alias", {})
    #FWOLogger.debug(f"[*] _parse_opnsense_host_aliases: aliases\n    {aliases}")

    aliases_parsed: list[OPNsenseHostAlias] = []

    for alias in aliases:
        # parse only hosts here
        if alias.get("type") in AliasTypeEnum.HOST:
            alias_parsed = OPNsenseHostAlias.model_validate(alias)
            aliases_parsed.append(alias)

    return aliases_parsed

def _parse_opnsense_gateways(config: dict[str, Any]) -> list[OPNsenseIfGroup] | None:
    gateways = config.get("opnsense", {}).get("OPNsense", {}).get("Gateways", {}).get("gateway_item", {})

    gateways_parsed: list[OPNsenseGateway] = []

    for gw in gateways:
        gw_parsed = OPNsenseGateway.model_validate(gw)
        gateways_parsed.append(gw_parsed)

    return gateways_parsed

def _parse_opnsense_config(config: dict[str, Any]) -> OPNsenseConfig:

    hostname         = _parse_opnsense_hostname(config)
    last_change      = _parse_timestamp(config.get("opnsense", {}).get("revision", {}).get("time", {}))
    user_groups      = _parse_opnsense_user_groups(config)
    users            = _parse_opnsense_users(config)
    interfaces       = _parse_opnsense_interfaces(config)
    interface_groups = _parse_opnsense_if_groups(config)
    access_rules     = _parse_opnsense_access_rules(config)
    nat_rules        = _parse_opnsense_nat_rules(config)
    aliases, host_aliases, net_aliases, port_aliases = _parse_opnsense_aliases(config)
    gateways         = _parse_opnsense_gateways(config)

    config_parsed = OPNsenseConfig(
        hostname         = hostname,
        last_change      = last_change,
        user_groups      = user_groups,
        users            = users,
        interfaces       = interfaces,
        interface_groups = interface_groups,
        access_rules     = access_rules,
        nat_rules        = nat_rules,
        aliases          = aliases,
        port_aliases     = port_aliases,
        host_aliases     = host_aliases,
        net_aliases      = net_aliases,
        gateways         = gateways
    )

    # linking and data enrichment
    os_helper.link_opnsense_ports_from_port_aliases(config_parsed)
    os_helper.enrich_opnsense_net_and_hosts(config_parsed)

    return config_parsed

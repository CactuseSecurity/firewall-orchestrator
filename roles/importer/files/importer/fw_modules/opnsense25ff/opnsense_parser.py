# parsing retrieved config.xml from OPNsense into opnsense_model

from datetime import datetime, timezone
from typing import Any, TypeGuard, cast

import fw_modules.opnsense25ff.opnsense_helper as os_helper
from fw_modules.opnsense25ff.opnsense_constants import OPNSENSE_UUID_ALIAS, PREDEFINED_RULE_UID_PREFIX
from fw_modules.opnsense25ff.opnsense_model import (
    AliasTypeEnum,
    OPNsenseAccessRule,
    OPNsenseAlias,
    OPNsenseConfig,
    OPNsenseGateway,
    OPNsenseHostAlias,
    OPNsenseIfGroup,
    OPNsenseInterface,
    OPNsenseNATRule,
    OPNsenseNetworkAlias,
    OPNsensePortAlias,
    OPNsenseUser,
    OPNsenseUserGroup,
)
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger


def _is_dict(value: object) -> TypeGuard[dict[str, Any]]:
    return isinstance(value, dict)


def _as_object_list(value: Any) -> list[object]:
    return cast("list[object]", value)


def _get_value(data: dict[str, Any], *keys: str) -> object:
    current: object = data
    for key in keys:
        if not _is_dict(current):
            return None
        current = current.get(key)
    return current


def _get_dict(data: dict[str, Any], *keys: str) -> dict[str, Any]:
    current = _get_value(data, *keys)
    return current if _is_dict(current) else {}


def _as_dict_list(value: object) -> list[dict[str, Any]]:
    if _is_dict(value):
        return [value]
    if isinstance(value, list):
        return [item for item in _as_object_list(value) if _is_dict(item)]
    return []


def _parse_opnsense_hostname(config: dict[str, Any]) -> str:
    hostname = _get_value(config, "opnsense", "system", "hostname")
    domain = _get_value(config, "opnsense", "system", "domain")

    if isinstance(hostname, str) and isinstance(domain, str) and hostname and domain:
        return f"{hostname}.{domain}"
    FWOLogger.debug("[-] _parse_hostname: hostname or domain not defined")
    return ""


def _parse_timestamp(seconds: object) -> str | None:
    if seconds:
        timestamp = float(str(seconds))
        return datetime.fromtimestamp(timestamp, tz=timezone.utc).isoformat()
    FWOLogger.debug("[-] _parse_timestamp: seconds not defined")
    return None


def _parse_opnsense_user_groups(config: dict[str, Any]) -> list[OPNsenseUserGroup]:
    groups = _get_value(config, "opnsense", "system", "group")

    user_groups: list[OPNsenseUserGroup] = []

    for group in _as_dict_list(groups):
        group_parsed = OPNsenseUserGroup.model_validate(group)
        user_groups.append(group_parsed)

    return user_groups


def _parse_opnsense_users(config: dict[str, Any]) -> list[OPNsenseUser]:
    users = _get_value(config, "opnsense", "system", "user")

    users_parsed: list[OPNsenseUser] = []

    for user in _as_dict_list(users):
        user_parsed = OPNsenseUser.model_validate(user)
        users_parsed.append(user_parsed)

    return users_parsed


def _parse_opnsense_interfaces(config: dict[str, Any]) -> dict[str, OPNsenseInterface]:
    interfaces = _get_dict(config, "opnsense", "interfaces")

    ifaces_parsed: dict[str, OPNsenseInterface] = {}

    for iface, iface_config in interfaces.items():
        if not _is_dict(iface_config):
            continue
        if_parsed = OPNsenseInterface.model_validate(iface_config)
        if_parsed.name = iface
        ifaces_parsed[if_parsed.name] = if_parsed

    return ifaces_parsed


def _parse_opnsense_if_groups(config: dict[str, Any]) -> dict[str, OPNsenseIfGroup]:
    ifgroups = _get_value(config, "opnsense", "ifgroups", "ifgroupentry")

    ifgroups_parsed: dict[str, OPNsenseIfGroup] = {}

    for ifgroup in _as_dict_list(ifgroups):
        ifgroup_parsed = OPNsenseIfGroup.model_validate(ifgroup)
        ifgroups_parsed[ifgroup_parsed.name] = ifgroup_parsed

    return ifgroups_parsed


def _single_interface_name(rule: dict[str, Any]) -> str | None:
    rule_interface = rule.get("interface")
    if isinstance(rule_interface, str) and rule_interface:
        return rule_interface
    if isinstance(rule_interface, list):
        interface_names = _as_object_list(rule_interface)
        if len(interface_names) == 1 and isinstance(interface_names[0], str):
            return interface_names[0]
    return None


def _predefined_rule_uid(rule: dict[str, Any]) -> str | None:
    interface_name = _single_interface_name(rule)
    ipprotocol = rule.get("ipprotocol")
    source = _get_dict(rule, "source")
    destination = _get_dict(rule, "destination")

    if (
        interface_name is not None
        and ipprotocol in {"inet", "inet6"}
        and source.get("network") == interface_name
        and "any" in destination
    ):
        return f"{PREDEFINED_RULE_UID_PREFIX}{interface_name}-{ipprotocol}"
    return None


def _ensure_rule_uid(rule: dict[str, Any], rule_index: int) -> dict[str, Any]:
    if rule.get(OPNSENSE_UUID_ALIAS):
        return rule

    predefined_rule_uid = _predefined_rule_uid(rule)
    if predefined_rule_uid is not None:
        return {**rule, OPNSENSE_UUID_ALIAS: predefined_rule_uid}

    raise FwoImporterError(f"OPNsense rule at position {rule_index + 1} has no uuid and is not a predefined rule")


def _normalize_mvc_filter_rule(rule: dict[str, Any]) -> dict[str, Any]:
    source: dict[str, Any] = {
        "not": rule.get("source_not"),
    }
    if rule.get("source_port"):
        source["port"] = rule.get("source_port")
    if rule.get("source_net") == "any":
        source["any"] = None
    else:
        source["network"] = rule.get("source_net")

    destination: dict[str, Any] = {
        "not": rule.get("destination_not"),
    }
    if rule.get("destination_port"):
        destination["port"] = rule.get("destination_port")
    if rule.get("destination_net") == "any":
        destination["any"] = None
    else:
        destination["network"] = rule.get("destination_net")

    normalized_rule: dict[str, Any] = {
        OPNSENSE_UUID_ALIAS: rule.get(OPNSENSE_UUID_ALIAS),
        "type": rule.get("action"),
        "descr": rule.get("description"),
        "direction": rule.get("direction"),
        "ipprotocol": rule.get("ipprotocol"),
        "protocol": rule.get("protocol"),
        "log": rule.get("log"),
        "interfacenot": rule.get("interfacenot"),
        "source": source,
        "destination": destination,
    }

    if str(rule.get("enabled", "1")) == "0":
        normalized_rule["disabled"] = True

    rule_interface = rule.get("interface")
    if rule_interface:
        normalized_rule["interface"] = rule_interface
    else:
        normalized_rule["floating"] = "yes"

    return normalized_rule


def _get_legacy_filter_rules(config: dict[str, Any]) -> list[dict[str, Any]]:
    return _as_dict_list(_get_value(config, "opnsense", "filter", "rule"))


def _get_mvc_filter_rules(config: dict[str, Any]) -> list[dict[str, Any]]:
    mvc_rules = _as_dict_list(_get_value(config, "opnsense", "OPNsense", "Firewall", "Filter", "rules", "rule"))
    return sorted(mvc_rules, key=lambda rule: int(str(rule.get("sequence") or "0")))


def _has_rule_endpoint_value(endpoint: dict[str, Any], key: str) -> bool:
    value = endpoint.get(key)
    if value is None:
        return False
    if isinstance(value, str):
        return value != ""
    if isinstance(value, list):
        return len(_as_object_list(value)) > 0
    return True


def _apply_endpoint_any_defaults(rule: OPNsenseAccessRule, raw_rule: dict[str, Any]) -> None:
    source = _get_dict(raw_rule, "source")
    if "any" in source:
        rule.source_address = ["Any"]
        rule.source_network = []
    elif _has_rule_endpoint_value(source, "network") and not _has_rule_endpoint_value(source, "address"):
        rule.source_address = []

    destination = _get_dict(raw_rule, "destination")
    if "any" in destination:
        rule.dest_address = ["Any"]
        rule.dest_network = []
    elif _has_rule_endpoint_value(destination, "network") and not _has_rule_endpoint_value(destination, "address"):
        rule.dest_address = []


def _parse_opnsense_access_rules(config: dict[str, Any]) -> list[OPNsenseAccessRule]:
    legacy_rules = _get_legacy_filter_rules(config)
    mvc_rules = [_normalize_mvc_filter_rule(rule) for rule in _get_mvc_filter_rules(config)]
    rules = legacy_rules + mvc_rules

    rules_parsed: list[OPNsenseAccessRule] = []

    for rule_index, rule in enumerate(rules):
        rule_with_uid = _ensure_rule_uid(rule, rule_index)
        rule_parsed = OPNsenseAccessRule.model_validate(rule_with_uid)
        if "Any" in rule_parsed.interface:
            rule_parsed.any_interface = True
        _apply_endpoint_any_defaults(rule_parsed, rule_with_uid)
        rules_parsed.append(rule_parsed)

    return rules_parsed


def _parse_opnsense_nat_rules(config: dict[str, Any]) -> list[OPNsenseNATRule]:
    outbound_rules = _get_value(config, "opnsense", "nat", "outbound", "rule")

    rules_parsed: list[OPNsenseNATRule] = []

    for rule in _as_dict_list(outbound_rules):
        rule_parsed = OPNsenseNATRule.model_validate(rule)
        rule_parsed.is_outbound = True
        rules_parsed.append(rule_parsed)

    return rules_parsed


def _parse_opnsense_aliases(
    config: dict[str, Any],
) -> tuple[
    dict[str, OPNsenseAlias],
    dict[str, OPNsenseHostAlias],
    dict[str, OPNsenseNetworkAlias],
    dict[str, OPNsensePortAlias],
]:
    aliases = _get_value(config, "opnsense", "OPNsense", "Firewall", "Alias", "aliases", "alias")

    misc_aliases_parsed: dict[str, OPNsenseAlias] = {}
    port_aliases_parsed: dict[str, OPNsensePortAlias] = {}
    host_aliases_parsed: dict[str, OPNsenseHostAlias] = {}
    net_aliases_parsed: dict[str, OPNsenseNetworkAlias] = {}

    for alias in _as_dict_list(aliases):
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


def _parse_opnsense_gateways(config: dict[str, Any]) -> list[OPNsenseGateway]:
    gateways = _get_value(config, "opnsense", "OPNsense", "Gateways", "gateway_item")

    gateways_parsed: list[OPNsenseGateway] = []

    for gw in _as_dict_list(gateways):
        gw_parsed = OPNsenseGateway.model_validate(gw)
        gateways_parsed.append(gw_parsed)

    return gateways_parsed


def parse_opnsense_config(config: dict[str, Any]) -> OPNsenseConfig:

    hostname = _parse_opnsense_hostname(config)
    last_change = _parse_timestamp(_get_value(config, "opnsense", "revision", "time"))
    user_groups = _parse_opnsense_user_groups(config)
    users = _parse_opnsense_users(config)
    interfaces = _parse_opnsense_interfaces(config)
    interface_groups = _parse_opnsense_if_groups(config)
    access_rules = _parse_opnsense_access_rules(config)
    nat_rules = _parse_opnsense_nat_rules(config)
    aliases, host_aliases, net_aliases, port_aliases = _parse_opnsense_aliases(config)
    gateways = _parse_opnsense_gateways(config)

    config_parsed = OPNsenseConfig(
        hostname=hostname,
        last_change=last_change,
        user_groups=user_groups,
        users=users,
        interfaces=interfaces,
        interface_groups=interface_groups,
        access_rules=access_rules,
        nat_rules=nat_rules,
        aliases=aliases,
        port_aliases=port_aliases,
        host_aliases=host_aliases,
        net_aliases=net_aliases,
        gateways=gateways,
    )

    # linking and data enrichment
    os_helper.link_opnsense_ports_from_port_aliases(config_parsed)
    os_helper.enrich_opnsense_net_and_hosts(config_parsed)

    return config_parsed

from typing import Any, cast

import pytest
from fw_modules.opnsense25ff.opnsense_constants import PREDEFINED_RULE_UID_PREFIX
from fw_modules.opnsense25ff.opnsense_model import FilterRuleIPProtoEnum
from fw_modules.opnsense25ff.opnsense_parser import parse_opnsense_config
from fwo_exceptions import FwoImporterError


def _native_config() -> dict[str, Any]:
    return {
        "opnsense": {
            "system": {
                "hostname": "fw",
                "domain": "example.com",
                "group": [
                    {
                        "@uuid": "g1",
                        "gid": "1000",
                        "name": "admins",
                        "scope": "system",
                        "description": "admin group",
                        "priv": "page-all",
                        "member": "0",
                    }
                ],
                "user": [
                    {
                        "@uuid": "u1",
                        "uid": "0",
                        "name": "root",
                        "disabled": "0",
                        "scope": "system",
                        "email": None,
                        "priv": "page-all",
                        "expires": None,
                        "descr": "System Administrator",
                    }
                ],
            },
            "revision": {"time": "1700000000.0"},
            "interfaces": {"lan": {"enable": "1", "if": "em0", "descr": "LAN", "ipaddr": "192.0.2.1", "subnet": "24"}},
            "ifgroups": {"ifgroupentry": [{"@uuid": "ifg1", "ifname": "lan_group", "members": "lan", "descr": "grp"}]},
            "filter": {
                "rule": [
                    {
                        "@uuid": "r1",
                        "type": "pass",
                        "interface": "lan",
                        "descr": "allow web",
                        "source": {"address": "192.0.2.0/24"},
                        "destination": {"address": "198.51.100.10", "port": "web"},
                    }
                ]
            },
            "nat": {"outbound": {"rule": []}},
            "OPNsense": {
                "Firewall": {
                    "Alias": {
                        "aliases": {
                            "alias": [
                                {
                                    "@uuid": "a1",
                                    "enabled": "1",
                                    "name": "web",
                                    "type": "port",
                                    "content": "80\n443",
                                    "description": "web ports",
                                },
                                {
                                    "@uuid": "a2",
                                    "enabled": "1",
                                    "name": "internal",
                                    "type": "host",
                                    "content": "192.0.2.10",
                                    "description": "internal hosts",
                                },
                            ]
                        }
                    }
                },
                "Gateways": {"gateway_item": []},
            },
        }
    }


def _native_config_with_singletons() -> dict[str, Any]:
    config = _native_config()
    opnsense = config["opnsense"]

    opnsense["system"]["group"] = opnsense["system"]["group"][0]
    opnsense["system"]["user"] = opnsense["system"]["user"][0]
    opnsense["ifgroups"]["ifgroupentry"] = opnsense["ifgroups"]["ifgroupentry"][0]
    opnsense["filter"]["rule"] = opnsense["filter"]["rule"][0]
    opnsense["OPNsense"]["Firewall"]["Alias"]["aliases"]["alias"] = opnsense["OPNsense"]["Firewall"]["Alias"][
        "aliases"
    ]["alias"][0]
    opnsense["OPNsense"]["Gateways"]["gateway_item"] = {
        "@uuid": "gw1",
        "disabled": "0",
        "name": "wan_gateway",
        "interface": "wan",
        "gateway": "198.51.100.1",
        "defaultgw": "1",
    }

    return config


def _native_config_with_mvc_filter_rules() -> dict[str, Any]:
    config = _native_config()
    opnsense = cast("dict[str, Any]", config["opnsense"])
    opnsense["OPNsense"]["Firewall"]["Filter"] = {
        "rules": {
            "rule": [
                {
                    "@uuid": "mvc-cleanup",
                    "enabled": "1",
                    "sequence": "100",
                    "action": "reject",
                    "interface": None,
                    "direction": "in",
                    "ipprotocol": "inet",
                    "protocol": "any",
                    "source_net": "any",
                    "source_not": "0",
                    "destination_net": "any",
                    "destination_not": "0",
                    "log": "1",
                    "description": "clean-up",
                },
                {
                    "@uuid": "mvc-https",
                    "enabled": "1",
                    "sequence": "50",
                    "action": "pass",
                    "interface": None,
                    "direction": "in",
                    "ipprotocol": "inet",
                    "protocol": "TCP",
                    "source_net": "any",
                    "source_not": "0",
                    "destination_net": "(self)",
                    "destination_not": "0",
                    "destination_port": "https",
                    "log": "1",
                    "description": "allow https to fw",
                },
            ]
        }
    }
    return config


def test_parse_opnsense_config_builds_structured_model() -> None:
    config = parse_opnsense_config(_native_config())

    assert config.hostname == "fw.example.com"
    assert config.last_change is not None
    assert [user.name for user in config.users] == ["root"]
    assert [group.name for group in config.user_groups] == ["admins"]
    assert "lan" in config.interfaces
    assert "lan_group" in config.interface_groups
    assert len(config.access_rules) == 1
    assert config.access_rules[0].uuid == "r1"


def test_parse_opnsense_config_buckets_aliases_by_type() -> None:
    config = parse_opnsense_config(_native_config())

    assert "web" in config.port_aliases
    assert "internal" in config.host_aliases
    assert "web" not in config.host_aliases


def test_parse_opnsense_config_enriches_aliases() -> None:
    config = parse_opnsense_config(_native_config())

    # port alias linking expands "80" and "443" into two port children
    assert len(config.port_aliases["web"].childs) == 2
    # host alias enrichment resolves the literal IP into a child
    assert len(config.host_aliases["internal"].childs) == 1


def test_parse_opnsense_config_handles_singleton_sections() -> None:
    config = parse_opnsense_config(_native_config_with_singletons())

    assert [user.name for user in config.users] == ["root"]
    assert [group.name for group in config.user_groups] == ["admins"]
    assert list(config.interface_groups) == ["lan_group"]
    assert [rule.uuid for rule in config.access_rules] == ["r1"]
    assert list(config.port_aliases) == ["web"]
    assert [gateway.name for gateway in config.gateways] == ["wan_gateway"]


def test_parse_opnsense_config_defaults_missing_interface_description() -> None:
    native_config = _native_config()
    opnsense = cast("dict[str, Any]", native_config["opnsense"])
    interfaces = cast("dict[str, Any]", opnsense["interfaces"])
    assert isinstance(interfaces, dict)
    lan_interface = cast("dict[str, Any]", interfaces["lan"])
    assert isinstance(lan_interface, dict)
    lan_interface.pop("descr")

    config = parse_opnsense_config(native_config)

    assert config.interfaces["lan"].description == ""


def test_parse_opnsense_config_generates_uid_for_predefined_rule_without_uuid() -> None:
    native_config = _native_config()
    opnsense = cast("dict[str, Any]", native_config["opnsense"])
    filter_config = cast("dict[str, Any]", opnsense["filter"])
    rules = cast("list[dict[str, Any]]", filter_config["rule"])
    rules[0].pop("@uuid")
    rules[0]["ipprotocol"] = "inet"
    rules[0]["source"] = {"network": "lan"}
    rules[0]["destination"] = {"any": None}

    config = parse_opnsense_config(native_config)

    rule_uid = config.access_rules[0].uuid
    assert rule_uid == f"{PREDEFINED_RULE_UID_PREFIX}lan-inet"
    assert rule_uid == parse_opnsense_config(native_config).access_rules[0].uuid
    assert config.access_rules[0].source_address == []
    assert config.access_rules[0].source_network == ["lan"]
    assert config.access_rules[0].dest_address == ["Any"]
    assert config.access_rules[0].dest_network == []


def test_parse_opnsense_config_keeps_predefined_uid_when_non_identity_content_changes() -> None:
    native_config = _native_config()
    opnsense = cast("dict[str, Any]", native_config["opnsense"])
    filter_config = cast("dict[str, Any]", opnsense["filter"])
    rules = cast("list[dict[str, Any]]", filter_config["rule"])
    rules[0].pop("@uuid")
    rules[0]["ipprotocol"] = "inet"
    rules[0]["source"] = {"network": "lan"}
    rules[0]["destination"] = {"any": None}
    initial_uid = parse_opnsense_config(native_config).access_rules[0].uuid

    rules[0]["descr"] = "Changed rule description"
    rules[0]["type"] = "reject"

    assert parse_opnsense_config(native_config).access_rules[0].uuid == initial_uid


def test_parse_opnsense_config_rejects_unknown_uuidless_rule() -> None:
    native_config = _native_config()
    opnsense = cast("dict[str, Any]", native_config["opnsense"])
    filter_config = cast("dict[str, Any]", opnsense["filter"])
    rules = cast("list[dict[str, Any]]", filter_config["rule"])
    rules[0].pop("@uuid")

    with pytest.raises(FwoImporterError, match="has no uuid and is not a predefined rule"):
        parse_opnsense_config(native_config)


def test_parse_opnsense_config_reads_mvc_filter_rules() -> None:
    config = parse_opnsense_config(_native_config_with_mvc_filter_rules())

    mvc_rules = config.access_rules[1:]
    assert [rule.uuid for rule in mvc_rules] == ["mvc-https", "mvc-cleanup"]
    assert [rule.description for rule in mvc_rules] == ["allow https to fw", "clean-up"]
    assert all(rule.is_floating for rule in mvc_rules)
    assert mvc_rules[0].source_address == ["Any"]
    assert mvc_rules[0].source_network == []
    assert mvc_rules[0].dest_address == []
    assert mvc_rules[0].dest_network == ["(self)"]
    assert mvc_rules[0].dest_port == ["https"]
    assert mvc_rules[1].dest_address == ["Any"]
    assert mvc_rules[1].dest_network == []
    # "0" negation strings from MVC rules must not be treated as truthy
    assert all(not rule.source_neg for rule in mvc_rules)
    assert all(not rule.dest_neg for rule in mvc_rules)
    assert all(not rule.interface_neg for rule in mvc_rules)


def test_parse_opnsense_config_honors_mvc_negation_flags() -> None:
    native_config = _native_config_with_mvc_filter_rules()
    opnsense = cast("dict[str, Any]", native_config["opnsense"])
    https_rule = cast(
        "dict[str, Any]",
        opnsense["OPNsense"]["Firewall"]["Filter"]["rules"]["rule"][1],
    )
    https_rule["source_not"] = "1"
    https_rule["destination_not"] = "1"
    https_rule["interfacenot"] = "1"
    https_rule["ipprotocol"] = "inet6"

    config = parse_opnsense_config(native_config)

    negated_rule = next(rule for rule in config.access_rules if rule.uuid == "mvc-https")
    assert negated_rule.source_neg
    assert negated_rule.dest_neg
    assert negated_rule.interface_neg
    # the IP protocol of MVC rules must survive normalization (not silently default to IPv4)
    assert negated_rule.ipprotocol == FilterRuleIPProtoEnum.INET6

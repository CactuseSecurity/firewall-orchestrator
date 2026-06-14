from typing import Any

from fw_modules.opnsense25ff.opnsense_parser import parse_opnsense_config


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

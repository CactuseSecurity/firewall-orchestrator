# pyright: reportPrivateUsage=false
# tests target internal normalizer helpers, hence private-usage is allowed here
import json

import pytest
from fw_modules.opnsense25ff.opnsense_model import (
    AliasTypeEnum,
    FilterRuleActionEnum,
    OPNsenseAccessRule,
    OPNsenseAlias,
    OPNsenseConfig,
    OPNsenseHost,
    OPNsenseHostAlias,
    OPNsenseIfGroup,
    OPNsenseInterface,
    OPNsenseNetwork,
    OPNsensePort,
    OPNsensePortAlias,
)
from fw_modules.opnsense25ff.opnsense_normalizer import (
    _create_network_object_from_alias,
    _create_normalized_rule_from_access_rule,
    _create_rulebases_from_access_rules,
    _get_gateway_name,
    _get_rulebase_links_from_rulebases,
    _normalize_interfaces,
    _normalize_network_objects,
    _normalize_services,
    _normalize_services_from_port_alias,
    _resolve_named_refs_in_rules,
    _update_network_objects_from_access_rules,
    normalize_opnsense_config,
)
from fwo_exceptions import FwoImporterError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.networkobject import NetworkObject
from models.rule import RuleAction, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.serviceobject import ServiceObject
from pytest_mock import MockerFixture


def _host_alias(name: str) -> OPNsenseHostAlias:
    return OPNsenseHostAlias.model_validate(
        {"@uuid": f"uid-{name}", "enabled": True, "name": name, "content": "x", "description": name}
    )


def _port_alias(name: str) -> OPNsensePortAlias:
    return OPNsensePortAlias.model_validate(
        {"@uuid": f"uid-{name}", "enabled": True, "name": name, "content": "x", "description": name}
    )


def test_create_network_object_from_alias_builds_nested_group() -> None:
    child = _host_alias("child-grp")
    child.childs.append(OPNsenseHost.model_validate({"name": "h-10", "host": "192.0.2.10"}))
    parent = _host_alias("parent-grp")
    parent.childs.append(child)
    parent.childs.append("external-name")
    parent.childs.append(OPNsenseNetwork.model_validate({"name": "n-24", "net": "192.0.2.0/24"}))

    normalized: dict[str, NetworkObject] = {}
    obj = _create_network_object_from_alias(parent, normalized, 0)

    assert obj.obj_typ == "group"
    assert obj.obj_uid == "uid-parent-grp"
    assert obj.obj_member_names is not None
    assert set(obj.obj_member_names.split("|")) == {"child-grp", "external-name", "n-24"}

    # nested alias + all leaf members are registered with the right type
    assert normalized["child-grp"].obj_typ == "group"
    assert normalized["h-10"].obj_typ == "host"
    assert normalized["n-24"].obj_typ == "network"
    assert normalized["external-name"].obj_typ == "group"  # unresolved string -> placeholder group


def test_normalize_services_from_port_alias_builds_nested_group() -> None:
    inner = _port_alias("inner-ports")
    inner.childs.append(OPNsensePort(name="p-80", is_range=False, port=80, port_end=None))
    outer = _port_alias("outer-ports")
    outer.childs.append(inner)
    outer.childs.append(OPNsensePort(name="p-1000-2000", is_range=True, port=1000, port_end=2000))

    normalized: dict[str, ServiceObject] = {}
    svc = _normalize_services_from_port_alias(outer, normalized, 0)

    assert svc.svc_typ == "group"
    assert svc.svc_uid == "uid-outer-ports"
    assert svc.svc_member_names is not None
    assert set(svc.svc_member_names.split("|")) == {"inner-ports", "p-1000-2000"}

    assert normalized["p-80"].svc_port == 80
    assert (normalized["p-1000-2000"].svc_port, normalized["p-1000-2000"].svc_port_end) == (1000, 2000)
    assert normalized["inner-ports"].svc_typ == "group"
    assert normalized["inner-ports"].svc_member_names == "p-80"
    assert "outer-ports" in normalized  # the group registers itself too


def test_normalize_services_adds_builtin_named_ports() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-https",
            "type": "pass",
            "descr": "allow https to fw",
            "destination": {"network": "(self)", "port": "https"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = _normalize_services(config)

    assert services["https"].svc_port == 443
    assert services["https"].svc_port_end == 443


def test_normalize_services_creates_placeholder_for_unknown_named_port() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-smtp",
            "type": "pass",
            "descr": "allow smtp to fw",
            "destination": {"network": "(self)", "port": "smtp"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = _normalize_services(config)

    # unknown named ports must not vanish (they would crash ref resolution later)
    assert services["smtp"].svc_typ == "simple"
    assert services["smtp"].svc_port is None
    smtp_comment = services["smtp"].svc_comment
    assert smtp_comment is not None
    assert "placeholder" in smtp_comment


def test_update_network_objects_creates_placeholder_for_unknown_target() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r-unknown", "type": "pass", "descr": "d", "source": {"address": "unknown-target"}}
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    nw_objs: dict[str, NetworkObject] = {}
    _update_network_objects_from_access_rules(config, nw_objs)

    # unknown network targets become placeholder groups instead of being skipped
    assert nw_objs["unknown-target"].obj_typ == "group"
    assert nw_objs["unknown-target"].obj_name == "unknown-target"


def test_resolve_named_refs_keeps_unresolved_names_without_crashing() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-ghost",
            "type": "pass",
            "descr": "d",
            "source": {"address": "ghost-src"},
            "destination": {"address": "ghost-dst", "port": "ghost-svc"},
        }
    )
    normalized_rule = _create_normalized_rule_from_access_rule(rule)
    rb = Rulebase(uid="rb", name="rb", mgm_uid="m", is_global=False, rules={"r-ghost": normalized_rule})

    _resolve_named_refs_in_rules([rb], {}, {})

    assert rb.rules["r-ghost"].rule_src_refs == "ghost-src"
    assert rb.rules["r-ghost"].rule_dst_refs == "ghost-dst"
    assert rb.rules["r-ghost"].rule_svc_refs == "ghost-svc"


def test_normalize_services_creates_protocol_service_for_icmp() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-icmp",
            "type": "pass",
            "descr": "allow ping to fw",
            "protocol": "ICMP",
            "destination": {"network": "(self)"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = _normalize_services(config)

    assert "ICMP" in services
    assert services["ICMP"].svc_port is None
    assert services["ICMP"].ip_proto == 1
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == "ICMP"


def test_normalize_services_disambiguates_icmpv6() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-icmp6",
            "type": "pass",
            "descr": "allow ping6 to fw",
            "protocol": "ICMP",
            "ipprotocol": "inet6",
            "destination": {"network": "(self)"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = _normalize_services(config)

    assert "ICMPv6" in services
    assert services["ICMPv6"].ip_proto == 58
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == "ICMPv6"


@pytest.mark.parametrize(
    ("os_action", "expected"),
    [
        (FilterRuleActionEnum.PASS, RuleAction.ACCEPT),
        (FilterRuleActionEnum.BLOCK, RuleAction.DROP),
        (FilterRuleActionEnum.REJECT, RuleAction.REJECT),
    ],
)
def test_create_normalized_rule_action_mapping(os_action: FilterRuleActionEnum, expected: RuleAction) -> None:
    rule = OPNsenseAccessRule.model_validate({"@uuid": "r", "type": os_action, "descr": "name:detail"})
    normalized = _create_normalized_rule_from_access_rule(rule)

    assert normalized.rule_action == expected
    assert normalized.rule_uid == "r"
    assert normalized.rule_type == RuleType.ACCESS
    # rule_name is the description truncated at the first ':'
    assert normalized.rule_name == "name"
    assert normalized.rule_comment == "name:detail"


def test_create_normalized_rule_logging_controls_track() -> None:
    logged = OPNsenseAccessRule.model_validate({"@uuid": "r1", "type": "pass", "log": "1", "descr": "d"})
    plain = OPNsenseAccessRule.model_validate({"@uuid": "r2", "type": "pass", "descr": "d"})

    assert _create_normalized_rule_from_access_rule(logged).rule_track == RuleTrack.LOG
    assert _create_normalized_rule_from_access_rule(plain).rule_track == RuleTrack.NONE


def test_create_normalized_rule_maps_src_dst_svc_and_custom_fields() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r",
            "type": "pass",
            "descr": "web",
            "source": {"address": "192.0.2.0/24"},
            "destination": {"address": "198.51.100.10", "port": "443"},
        }
    )
    normalized = _create_normalized_rule_from_access_rule(rule)

    assert normalized.rule_src == "192.0.2.0/24"
    assert normalized.rule_dst == "198.51.100.10"
    # services derive from destination ports only
    assert normalized.rule_svc == "443"

    assert normalized.rule_custom_fields is not None
    custom = json.loads(normalized.rule_custom_fields)
    assert custom["os_rule_l3proto"] == "Any"
    assert custom["os_rule_direction"] == "in"


def test_create_normalized_rule_does_not_add_any_when_source_network_is_set() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r",
            "type": "pass",
            "descr": "default lan",
            "interface": "lan",
            "source": {"network": "lan"},
            "destination": {"any": None},
        }
    )
    rule.source_address = []
    normalized = _create_normalized_rule_from_access_rule(rule)

    assert normalized.rule_src == "lan"
    assert normalized.rule_src_refs == "lan"


def test_get_rulebase_links_orders_and_marks_initial() -> None:
    rbs = [Rulebase(uid=f"rb{i}", name=f"rb{i}", mgm_uid="m", is_global=False, rules={}) for i in range(3)]

    links = _get_rulebase_links_from_rulebases(rbs)

    assert len(links) == 3
    assert links[0].is_initial is True
    assert links[0].from_rulebase_uid is None
    assert links[0].to_rulebase_uid == "rb0"
    assert (links[1].from_rulebase_uid, links[1].to_rulebase_uid) == ("rb0", "rb1")
    assert (links[2].from_rulebase_uid, links[2].to_rulebase_uid) == ("rb1", "rb2")
    assert all(link.link_type == "ordered" for link in links)
    assert [link.is_initial for link in links] == [True, False, False]


def test_create_rulebases_from_access_rules_uses_physical_interface() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-lan",
            "type": "pass",
            "descr": "Default allow LAN to any rule",
            "interface": "lan",
            "source": {"network": "lan"},
            "destination": {"any": None},
        }
    )
    config = OPNsenseConfig(
        hostname="fw",
        interfaces={"lan": OPNsenseInterface.model_validate({"enable": "1", "if": "em0", "ipaddr": "10.1.1.87"})},
        access_rules=[rule],
    )

    rulebases = _create_rulebases_from_access_rules(config, "mgm-uid")

    assert len(rulebases) == 1
    assert rulebases[0].name == "lan"
    assert "r-lan" in rulebases[0].rules


def test_create_rulebases_from_access_rules_keeps_rule_uid() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "opnsense-default-rule-lan-inet",
            "type": "pass",
            "descr": "Default allow LAN to any rule",
            "interface": "lan",
            "source": {"network": "lan"},
            "destination": {"any": None},
        }
    )
    config = OPNsenseConfig(
        hostname="fw",
        interfaces={"lan": OPNsenseInterface.model_validate({"enable": "1", "if": "em0", "ipaddr": "10.1.1.87"})},
        access_rules=[rule],
    )

    rulebases = _create_rulebases_from_access_rules(config, "mgm-uid")

    assert "opnsense-default-rule-lan-inet" in rulebases[0].rules


def test_update_network_objects_detects_interface_address_object() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-lan-ip",
            "type": "pass",
            "descr": "Default allow LAN to LAN address",
            "source": {"network": "lan"},
            "destination": {"network": "lanip"},
        }
    )
    config = OPNsenseConfig(
        hostname="fw",
        interfaces={
            "lan": OPNsenseInterface.model_validate(
                {"name": "lan", "enable": "1", "if": "em0", "descr": None, "ipaddr": "10.1.1.87", "subnet": "24"}
            )
        },
        access_rules=[rule],
    )

    nw_objs = _normalize_network_objects(config)
    _update_network_objects_from_access_rules(config, nw_objs)

    assert "lanip" in nw_objs
    assert nw_objs["lanip"].obj_name == "lanip"
    assert nw_objs["lanip"].obj_typ == "group"


def test_update_network_objects_detects_interface_object() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-lan",
            "type": "pass",
            "descr": "Default allow LAN",
            "source": {"network": "lan"},
        }
    )
    config = OPNsenseConfig(
        hostname="fw",
        interfaces={
            "lan": OPNsenseInterface.model_validate(
                {"name": "lan", "enable": "1", "if": "em0", "descr": "LAN", "ipaddr": "10.1.1.87", "subnet": "24"}
            )
        },
        access_rules=[rule],
    )

    nw_objs: dict[str, NetworkObject] = {}
    _update_network_objects_from_access_rules(config, nw_objs)

    assert "lan" in nw_objs
    assert nw_objs["lan"].obj_name == "lan"
    assert nw_objs["lan"].obj_typ == "group"


def test_update_network_objects_detects_ips_subnets_ranges_and_ifgroups() -> None:
    ifgroup = OPNsenseIfGroup.model_validate({"@uuid": "ifg", "ifname": "lan_group", "members": "lan", "descr": "g"})
    host_rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r1", "type": "pass", "descr": "d1", "source": {"address": "192.0.2.5"}}
    )
    subnet_rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r2", "type": "pass", "descr": "d2", "destination": {"address": "198.51.100.0/24"}}
    )
    range_rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r3", "type": "pass", "descr": "d3", "source": {"address": "10.0.0.1-10.0.0.9"}}
    )
    ifgroup_rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r4", "type": "pass", "descr": "d4", "source": {"address": "lan_group"}}
    )
    config = OPNsenseConfig(
        hostname="fw",
        interface_groups={"lan_group": ifgroup},
        access_rules=[host_rule, subnet_rule, range_rule, ifgroup_rule],
    )

    nw_objs: dict[str, NetworkObject] = {}
    _update_network_objects_from_access_rules(config, nw_objs)

    assert nw_objs["192.0.2.5"].obj_typ == "host"
    assert nw_objs["198.51.100.0/24"].obj_typ == "network"
    assert nw_objs["10.0.0.1-10.0.0.9"].obj_typ == "ip_range"
    assert nw_objs["lan_group"].obj_typ == "group"
    assert nw_objs["lan_group"].obj_uid == "ifg"


def _net_obj(name: str, uid: str) -> NetworkObject:
    return NetworkObject(
        obj_uid=uid,
        obj_name=name,
        obj_ip=None,
        obj_ip_end=None,
        obj_color="",
        obj_typ="host",
        obj_member_refs=None,
        obj_member_names=None,
        obj_comment="",
    )


def _svc_obj(name: str, uid: str) -> ServiceObject:
    return ServiceObject(
        svc_uid=uid,
        svc_name=name,
        svc_port=80,
        svc_port_end=80,
        svc_color="",
        svc_typ="simple",
        ip_proto=None,
        svc_member_refs=None,
        svc_member_names=None,
        svc_comment="",
        svc_timeout=None,
        rpc_nr=None,
    )


def test_resolve_named_refs_replaces_names_with_uids() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r",
            "type": "pass",
            "descr": "d",
            "source": {"address": "src-name"},
            "destination": {"address": "dst-name", "port": "svc-name"},
        }
    )
    normalized_rule = _create_normalized_rule_from_access_rule(rule)
    rb = Rulebase(uid="rb", name="rb", mgm_uid="m", is_global=False, rules={"r": normalized_rule})

    nw_objs = {"src-name": _net_obj("src-name", "SRC-UID"), "dst-name": _net_obj("dst-name", "DST-UID")}
    svc_objs = {"svc-name": _svc_obj("svc-name", "SVC-UID")}

    _resolve_named_refs_in_rules([rb], nw_objs, svc_objs)

    assert rb.rules["r"].rule_src_refs == "SRC-UID"
    assert rb.rules["r"].rule_dst_refs == "DST-UID"
    assert rb.rules["r"].rule_svc_refs == "SVC-UID"


def test_get_gateway_name_prefers_configured_device_name(import_state_controller: ImportStateController) -> None:
    import_state_controller.state.mgm_details.devices = [{"name": "configured-gateway-uid"}]
    native_config = OPNsenseConfig(hostname="native-hostname")

    assert _get_gateway_name(native_config, import_state_controller) == "configured-gateway-uid"


@pytest.mark.parametrize(
    ("management_name", "native_hostname", "management_hostname", "expected"),
    [
        ("configured-management", "native-hostname", "mgm-hostname", "configured-management"),
        ("", "native-hostname", "mgm-hostname", "native-hostname"),
        ("", "", "mgm-hostname", "mgm-hostname"),
    ],
)
def test_get_gateway_name_uses_fallback_order(
    import_state_controller: ImportStateController,
    management_name: str,
    native_hostname: str,
    management_hostname: str,
    expected: str,
) -> None:
    import_state_controller.state.mgm_details.devices = []
    import_state_controller.state.mgm_details.name = management_name
    import_state_controller.state.mgm_details.hostname = management_hostname

    assert _get_gateway_name(OPNsenseConfig(hostname=native_hostname), import_state_controller) == expected


def test_get_gateway_name_requires_available_name(import_state_controller: ImportStateController) -> None:
    import_state_controller.state.mgm_details.devices = []
    import_state_controller.state.mgm_details.name = ""
    import_state_controller.state.mgm_details.hostname = ""

    with pytest.raises(FwoImporterError, match="must contain a device name"):
        _get_gateway_name(OPNsenseConfig(hostname=""), import_state_controller)


def test_normalize_network_objects_adds_geoip_and_urltable_aliases() -> None:
    geo_alias = OPNsenseAlias.model_validate(
        {
            "@uuid": "geo-uid",
            "enabled": True,
            "name": "geo-block",
            "type": AliasTypeEnum.GEOIP,
            "content": "DE\nUS",
            "description": "blocked countries",
        }
    )
    url_alias = OPNsenseAlias.model_validate(
        {
            "@uuid": "url-uid",
            "enabled": True,
            "name": "remote-list",
            "type": AliasTypeEnum.URLTABLE,
            "content": "https://example.invalid/list.txt",
            "description": "remote feed",
        }
    )
    config = OPNsenseConfig(hostname="fw", aliases={geo_alias.name: geo_alias, url_alias.name: url_alias})

    objects = _normalize_network_objects(config)

    assert objects["geo-block"].obj_uid == "geo-uid"
    assert objects["geo-block"].obj_member_names == "DE|US"
    assert objects["DE"].obj_typ == "group"
    assert objects["remote-list"].obj_uid == "url-uid"
    remote_list_comment = objects["remote-list"].obj_comment
    assert remote_list_comment is not None
    assert "https://example.invalid/list.txt" in remote_list_comment


def test_create_rulebases_from_access_rules_skips_rules_without_uid() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "type": "pass",
            "descr": "rule without uuid",
            "interface": "lan",
            "source": {"network": "lan"},
            "destination": {"any": None},
        }
    )
    config = OPNsenseConfig(
        hostname="fw",
        interfaces={"lan": OPNsenseInterface.model_validate({"name": "lan", "enable": "1", "if": "em0"})},
        access_rules=[rule],
    )

    assert _create_rulebases_from_access_rules(config, "mgm-uid") == []


def test_normalize_interfaces_skips_groups_and_adds_ipv4_ipv6() -> None:
    config = OPNsenseConfig(
        hostname="fw",
        interfaces={
            "lan": OPNsenseInterface.model_validate(
                {
                    "name": "lan",
                    "enable": "1",
                    "if": "em0",
                    "ipaddr": "192.0.2.1",
                    "subnet": "24",
                    "ipaddrv6": "2001:db8::1",
                    "subnetv6": "64",
                }
            ),
            "grp": OPNsenseInterface.model_validate({"name": "grp", "enable": "1", "if": "group0", "type": "group"}),
        },
    )

    interfaces = _normalize_interfaces(config)

    assert interfaces == [
        {
            "device_id": 0,
            "name": "lan_v4",
            "ip": "192.0.2.1",
            "netmask_bits": 24,
            "state_up": True,
            "ip_version": 4,
        },
        {
            "device_id": 1,
            "name": "lan_v6",
            "ip": "2001:db8::1",
            "netmask_bits": 64,
            "state_up": True,
            "ip_version": 6,
        },
    ]


def test_normalize_opnsense_config_builds_manager_config_with_uid_refs(
    mocker: MockerFixture,
    import_state_controller: ImportStateController,
) -> None:
    host_alias = _host_alias("web-hosts")
    host_alias.uuid = "uid-web-hosts"
    host_alias.childs.append(OPNsenseHost.model_validate({"name": "web01", "host": "192.0.2.10"}))
    port_alias = _port_alias("web-ports")
    port_alias.uuid = "uid-web-ports"
    port_alias.childs.append(OPNsensePort(name="tcp-8443", is_range=False, port=8443, port_end=None))
    access_rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "rule-uid",
            "type": "pass",
            "descr": "allow web hosts",
            "interface": "lan",
            "source": {"address": "web-hosts"},
            "destination": {"network": "lan", "port": "web-ports"},
        }
    )
    parsed_config = OPNsenseConfig(
        hostname="native-hostname",
        interfaces={
            "lan": OPNsenseInterface.model_validate(
                {"name": "lan", "enable": "1", "if": "em0", "ipaddr": "192.0.2.1", "subnet": "24"}
            )
        },
        host_aliases={host_alias.name: host_alias},
        port_aliases={port_alias.name: port_alias},
        access_rules=[access_rule],
    )
    config = FwConfigManagerListController.generate_empty_config()
    mocker.patch(
        "fw_modules.opnsense25ff.opnsense_normalizer.parse_opnsense_config",
        return_value=parsed_config,
    )

    normalized = normalize_opnsense_config(config, import_state_controller)

    manager = normalized.ManagerSet[0]
    normalized_config = manager.configs[0]
    rulebase = normalized_config.rulebases[0]
    rule = rulebase.rules["rule-uid"]
    assert manager.manager_uid == "mock-uid"
    assert normalized_config.gateways[0].Uid == "Mock Management"
    assert normalized_config.gateways[0].RulebaseLinks[0].to_rulebase_uid == rulebase.uid
    # normalized interfaces must be attached to the gateway
    assert normalized_config.gateways[0].Interfaces == [
        {
            "device_id": 0,
            "name": "lan_v4",
            "ip": "192.0.2.1",
            "netmask_bits": 24,
            "state_up": True,
            "ip_version": 4,
        }
    ]
    assert "uid-web-hosts" in normalized_config.network_objects
    assert "uid-web-ports" in normalized_config.service_objects
    assert rule.rule_src_refs == "uid-web-hosts"
    lan_object = next(obj for obj in normalized_config.network_objects.values() if obj.obj_name == "lan")
    assert set(rule.rule_dst_refs.split("|")) == {lan_object.obj_uid, "Any"}
    assert rule.rule_svc_refs == "uid-web-ports"

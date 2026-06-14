# pyright: reportPrivateUsage=false
# tests target internal normalizer helpers, hence private-usage is allowed here
import json

import pytest
from fw_modules.opnsense25ff.opnsense_model import (
    FilterRuleActionEnum,
    OPNsenseAccessRule,
    OPNsenseConfig,
    OPNsenseHost,
    OPNsenseHostAlias,
    OPNsenseIfGroup,
    OPNsenseNetwork,
    OPNsensePort,
    OPNsensePortAlias,
)
from fw_modules.opnsense25ff.opnsense_normalizer import (
    _create_network_object_from_alias,
    _create_normalized_rule_from_access_rule,
    _get_rulebase_links_from_rulebases,
    _normalize_services_from_port_alias,
    _resolve_named_refs_in_rules,
    _update_network_objects_from_access_rules,
)
from models.networkobject import NetworkObject
from models.rule import RuleAction, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.serviceobject import ServiceObject


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

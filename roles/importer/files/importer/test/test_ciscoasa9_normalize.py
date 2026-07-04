"""Tests for fw_modules/ciscoasa9/asa_normalize.py"""

from typing import Any
from unittest.mock import MagicMock

from fw_modules.ciscoasa9.asa_models import (
    AsaEnablePassword,
    AsaNetworkObject,
    AsaNetworkObjectGroup,
    AsaNetworkObjectGroupMember,
    AsaServiceObject,
    AsaServiceObjectGroup,
    Config,
    Names,
)
from fw_modules.ciscoasa9.asa_normalize import (
    normalize_all_network_objects,
    normalize_all_service_objects,
    normalize_config,
)
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController


def _minimal_config(**overrides: Any) -> Config:
    """Return a minimal valid Config with empty lists for optional fields."""
    defaults: dict[str, Any] = {
        "asa_version": "9.16",
        "hostname": "asa-test",
        "enable_password": AsaEnablePassword(password="", encryption_function=""),
        "service_modules": [],
        "additional_settings": [],
        "interfaces": [],
        "objects": [],
        "object_groups": [],
    }
    defaults.update(overrides)
    return Config(**defaults)


def _make_import_state(uid: str = "mgm-uid-1") -> MagicMock:
    state = MagicMock()
    state.mgm_details.uid = uid
    return state


def _make_config_in(native_config_dict: dict[str, Any]) -> FwConfigManagerListController:
    config_in = FwConfigManagerListController.generate_empty_config()
    config_in.native_config = native_config_dict
    return config_in


class TestNormalizeAllNetworkObjects:
    def test_empty_config_returns_empty_dict(self):
        cfg = _minimal_config()
        result = normalize_all_network_objects(cfg)
        assert result == {}

    def test_names_are_included(self):
        cfg = _minimal_config(names=[Names(name="gw1", ip_address="10.0.0.1")])
        result = normalize_all_network_objects(cfg)
        assert "gw1" in result
        assert result["gw1"].obj_typ == "host"

    def test_host_network_object_is_included(self):
        cfg = _minimal_config(objects=[AsaNetworkObject(name="h1", ip_address="10.0.0.1")])
        result = normalize_all_network_objects(cfg)
        assert "h1" in result
        assert result["h1"].obj_typ == "host"

    def test_subnet_network_object_is_included(self):
        cfg = _minimal_config(
            objects=[AsaNetworkObject(name="net1", ip_address="10.0.0.0", subnet_mask="255.255.255.0")]
        )
        result = normalize_all_network_objects(cfg)
        assert "net1" in result
        assert result["net1"].obj_typ == "network"

    def test_range_network_object_is_included(self):
        cfg = _minimal_config(
            objects=[AsaNetworkObject(name="rng1", ip_address="10.0.0.1", ip_address_end="10.0.0.10")]
        )
        result = normalize_all_network_objects(cfg)
        assert "rng1" in result
        assert result["rng1"].obj_typ == "ip_range"

    def test_network_object_group_is_included(self):
        cfg = _minimal_config(object_groups=[AsaNetworkObjectGroup(name="grp1", objects=[], description=None)])
        result = normalize_all_network_objects(cfg)
        assert "grp1" in result
        assert result["grp1"].obj_typ == "group"

    def test_names_and_objects_combined(self):
        cfg = _minimal_config(
            names=[Names(name="gw1", ip_address="10.0.0.1")],
            objects=[AsaNetworkObject(name="h1", ip_address="192.168.0.1")],
        )
        result = normalize_all_network_objects(cfg)
        assert "gw1" in result
        assert "h1" in result

    def test_group_referencing_named_object(self):
        cfg = _minimal_config(
            objects=[AsaNetworkObject(name="h1", ip_address="10.0.0.1")],
            object_groups=[
                AsaNetworkObjectGroup(
                    name="grp1",
                    objects=[AsaNetworkObjectGroupMember(kind="object", value="h1")],
                    description=None,
                )
            ],
        )
        result = normalize_all_network_objects(cfg)
        assert "grp1" in result
        obj_member_names = result["grp1"].obj_member_names
        assert obj_member_names is not None
        assert "h1" in obj_member_names

    def test_fqdn_object_creates_empty_group(self):
        cfg = _minimal_config(objects=[AsaNetworkObject(name="fqdn1", ip_address="", fqdn="example.com")])
        result = normalize_all_network_objects(cfg)
        assert "fqdn1" in result
        assert result["fqdn1"].obj_typ == "group"

    def test_empty_ip_with_no_extras_skipped(self):
        cfg = _minimal_config(objects=[AsaNetworkObject(name="nat-only", ip_address="")])
        result = normalize_all_network_objects(cfg)
        assert "nat-only" not in result


class TestNormalizeAllServiceObjects:
    def test_default_any_services_always_created(self):
        cfg = _minimal_config()
        result = normalize_all_service_objects(cfg)
        assert "any-tcp" in result
        assert "any-udp" in result
        assert "any-icmp" in result

    def test_individual_service_object_included(self):
        cfg = _minimal_config(service_objects=[AsaServiceObject(name="https", protocol="tcp", dst_port_eq="443")])
        result = normalize_all_service_objects(cfg)
        assert "https" in result

    def test_service_object_group_included(self):
        cfg = _minimal_config(
            service_object_groups=[
                AsaServiceObjectGroup(
                    name="web-svcs",
                    proto_mode="tcp",
                    ports_eq={"tcp": ["80", "443"]},
                    ports_range={},
                    nested_refs=[],
                    protocols=[],
                    description=None,
                )
            ]
        )
        result = normalize_all_service_objects(cfg)
        assert "web-svcs-tcp-80" in result or any("web-svcs" in k for k in result)

    def test_multiple_service_objects_all_included(self):
        cfg = _minimal_config(
            service_objects=[
                AsaServiceObject(name="http", protocol="tcp", dst_port_eq="80"),
                AsaServiceObject(name="dns", protocol="udp", dst_port_eq="53"),
            ]
        )
        result = normalize_all_service_objects(cfg)
        assert "http" in result
        assert "dns" in result

    def test_service_object_tcp_range_included(self):
        cfg = _minimal_config(
            service_objects=[AsaServiceObject(name="high-ports", protocol="tcp", dst_port_range=("1024", "65535"))]
        )
        result = normalize_all_service_objects(cfg)
        assert "high-ports" in result

    def test_icmp_service_object_included(self):
        cfg = _minimal_config(service_objects=[AsaServiceObject(name="ping", protocol="icmp")])
        result = normalize_all_service_objects(cfg)
        assert "ping" in result

    def test_empty_config_still_has_protocol_any_defaults(self):
        cfg = _minimal_config()
        result = normalize_all_service_objects(cfg)
        assert len(result) > 0


def _native_config_dict() -> dict[str, Any]:
    """Minimal valid native_config dict for Config.model_validate."""
    return {
        "asa_version": "9.16",
        "hostname": "asa-fw",
        "enable_password": {"password": "", "encryption_function": ""},
        "service_modules": [],
        "additional_settings": [],
        "interfaces": [],
        "objects": [],
        "object_groups": [],
        "service_objects": [],
        "service_object_groups": [],
        "access_lists": [],
        "access_group_bindings": [],
        "nat_rules": [],
        "routes": [],
        "mgmt_access": [],
        "names": [],
        "class_maps": [],
        "policy_maps": [],
        "service_policies": [],
        "protocol_groups": [],
    }


class TestNormalizeConfig:
    def test_returns_config_in_unchanged_type(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state()
        result = normalize_config(config_in, import_state)
        assert isinstance(result, FwConfigManagerListController)

    def test_normalized_config_placed_on_manager_set(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state()
        result = normalize_config(config_in, import_state)
        assert len(result.ManagerSet[0].configs) == 1

    def test_manager_uid_set_from_import_state(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state(uid="test-mgm-uid")
        normalize_config(config_in, import_state)
        assert config_in.ManagerSet[0].manager_uid == "test-mgm-uid"

    def test_empty_config_produces_no_rulebases(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert normalized.rulebases == []

    def test_empty_config_produces_one_gateway(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert len(normalized.gateways) == 1

    def test_gateway_uid_matches_hostname(self):
        native = _native_config_dict()
        native["hostname"] = "my-asa"
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        gw = config_in.ManagerSet[0].configs[0].gateways[0]
        assert gw.Uid == "my-asa"
        assert gw.Name == "my-asa"

    def test_network_objects_populated_from_objects(self):
        native = _native_config_dict()
        native["objects"] = [{"name": "h1", "ip_address": "10.0.0.1"}]
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert "h1" in normalized.network_objects

    def test_service_objects_include_default_any_protocols(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert "any-tcp" in normalized.service_objects

    def test_access_list_creates_rulebase(self):
        native = _native_config_dict()
        native["access_lists"] = [
            {
                "name": "outside-acl",
                "entries": [
                    {
                        "acl_name": "outside-acl",
                        "action": "permit",
                        "protocol": {"kind": "protocol", "value": "tcp"},
                        "src": {"kind": "any", "value": "any"},
                        "dst": {"kind": "any", "value": "any"},
                        "dst_port": {"kind": "any", "value": "any"},
                    }
                ],
            }
        ]
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert len(normalized.rulebases) == 1
        assert normalized.rulebases[0].name == "outside-acl"

    def test_multiple_access_lists_create_multiple_rulebases(self):
        def _acl_entry_dict(acl_name: str) -> dict[str, Any]:
            return {
                "acl_name": acl_name,
                "action": "permit",
                "protocol": {"kind": "protocol", "value": "tcp"},
                "src": {"kind": "any", "value": "any"},
                "dst": {"kind": "any", "value": "any"},
                "dst_port": {"kind": "any", "value": "any"},
            }

        native = _native_config_dict()
        native["access_lists"] = [
            {"name": "acl-1", "entries": [_acl_entry_dict("acl-1")]},
            {"name": "acl-2", "entries": [_acl_entry_dict("acl-2")]},
        ]
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert len(normalized.rulebases) == 2

    def test_rulebase_links_created_for_single_acl(self):
        native = _native_config_dict()
        native["access_lists"] = [
            {
                "name": "acl-1",
                "entries": [
                    {
                        "acl_name": "acl-1",
                        "action": "permit",
                        "protocol": {"kind": "protocol", "value": "tcp"},
                        "src": {"kind": "any", "value": "any"},
                        "dst": {"kind": "any", "value": "any"},
                        "dst_port": {"kind": "any", "value": "any"},
                    }
                ],
            }
        ]
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        gw = config_in.ManagerSet[0].configs[0].gateways[0]
        assert len(gw.RulebaseLinks) == 1
        assert gw.RulebaseLinks[0].is_initial is True

    def test_rulebase_links_chain_for_two_acls(self):
        def _acl(name: str) -> dict[str, Any]:
            return {
                "name": name,
                "entries": [
                    {
                        "acl_name": name,
                        "action": "permit",
                        "protocol": {"kind": "protocol", "value": "tcp"},
                        "src": {"kind": "any", "value": "any"},
                        "dst": {"kind": "any", "value": "any"},
                        "dst_port": {"kind": "any", "value": "any"},
                    }
                ],
            }

        native = _native_config_dict()
        native["access_lists"] = [_acl("acl-a"), _acl("acl-b")]
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        gw = config_in.ManagerSet[0].configs[0].gateways[0]
        assert len(gw.RulebaseLinks) == 2
        initial_links = [line for line in gw.RulebaseLinks if line.is_initial]
        chain_links = [line for line in gw.RulebaseLinks if not line.is_initial]
        assert len(initial_links) == 1
        assert len(chain_links) == 1
        assert chain_links[0].from_rulebase_uid is not None

    def test_named_host_in_acl_src_resolved(self):
        native = _native_config_dict()
        native["names"] = [{"name": "my-host", "ip_address": "10.0.0.5"}]
        native["access_lists"] = [
            {
                "name": "acl",
                "entries": [
                    {
                        "acl_name": "acl",
                        "action": "permit",
                        "protocol": {"kind": "protocol", "value": "tcp"},
                        "src": {"kind": "object", "value": "my-host"},
                        "dst": {"kind": "any", "value": "any"},
                        "dst_port": {"kind": "any", "value": "any"},
                    }
                ],
            }
        ]
        config_in = _make_config_in(native)
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert "my-host" in normalized.network_objects

    def test_zone_objects_always_empty_for_asa(self):
        config_in = _make_config_in(_native_config_dict())
        import_state = _make_import_state()
        normalize_config(config_in, import_state)
        normalized = config_in.ManagerSet[0].configs[0]
        assert normalized.zone_objects == {}

from typing import Any

from fw_modules.fortiadom5ff.fmgr_network import normalize_vip_object, normalize_vip_object_nat_ip
from fw_modules.fortiadom5ff.fmgr_rule import expand_vip_real_servers
from services.service_provider import ServiceProvider


def _normalized_config(network_objects: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    return {"network_objects": network_objects if network_objects is not None else []}


def test_normalize_vip_object_with_mappedip_creates_nat_object():
    obj_orig = {"name": "vip1", "extip": ["203.0.113.5"], "mappedip": ["10.0.0.5"]}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert obj["obj_nat_ip"] == "10.0.0.5"
    assert obj["obj_nat_ip_end"] == "10.0.0.5"
    assert "obj_nat_real_server_refs" not in obj
    assert len(nw_objects) == 1
    nat_obj = nw_objects[0]
    assert nat_obj["obj_ip"] == "10.0.0.5"
    assert nat_obj["obj_name"] == "10.0.0.5_NatNwObj"
    assert nat_obj["obj_uid"] == "10.0.0.5_NatNwObj"


def test_normalize_vip_object_with_empty_mappedip_does_not_crash():
    obj_orig = {"name": "LB-SSMTP", "extip": ["203.0.113.5"], "mappedip": []}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert "obj_nat_ip" not in obj
    assert nw_objects == []


def test_normalize_vip_object_with_missing_mappedip_key_does_not_crash():
    obj_orig = {"name": "LB-SSMTP", "extip": ["203.0.113.5"]}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert "obj_nat_ip" not in obj
    assert nw_objects == []


def test_normalize_vip_object_nat_ip_leaves_obj_untouched_when_mappedip_missing():
    obj_orig = {"name": "LB-SSMTP"}
    obj: dict[str, Any] = {}
    nat_obj: dict[str, Any] = {}

    normalize_vip_object_nat_ip(obj_orig, obj, nat_obj)

    assert "obj_nat_ip" not in obj
    assert nat_obj == {}


def test_normalize_vip_object_without_extip_field_skips_nat_handling():
    obj_orig = {"name": "vip_no_extip", "mappedip": ["10.0.0.5"]}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert obj["obj_typ"] == "host"
    assert "obj_nat_ip" not in obj
    assert nw_objects == []


def test_normalize_vip_object_with_multiple_extip_uses_first():
    obj_orig = {"name": "vip_multi_extip", "extip": ["203.0.113.7", "203.0.113.8"], "mappedip": ["10.0.0.7"]}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert obj["obj_ip"] == "203.0.113.7"


def test_normalize_vip_object_with_extip_range_sets_obj_ip_end():
    obj_orig = {"name": "vip_range", "extip": ["203.0.113.1-203.0.113.10"], "mappedip": []}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert obj["obj_ip"] == "203.0.113.1"
    assert obj["obj_ip_end"] == "203.0.113.10"


def test_normalize_vip_object_with_extip_range_and_host_mappedip_does_not_leak_extip_end():
    obj_orig = {"name": "vip_range_host_nat", "extip": ["1.1.1.1-1.1.1.5"], "mappedip": ["10.0.0.1"]}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert obj["obj_nat_ip_end"] == "10.0.0.1"
    nat_obj = nw_objects[0]
    assert nat_obj["obj_ip_end"] == "10.0.0.1"
    assert nat_obj["obj_name"] == "10.0.0.1_NatNwObj"


def test_normalize_vip_object_with_associated_interface_sets_nat_obj_zone():
    obj_orig = {
        "name": "vip_zone",
        "extip": ["203.0.113.9"],
        "mappedip": ["10.0.0.9"],
        "associated-interface": ["port1"],
    }
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert nw_objects[0]["obj_zone"] == "port1"


def test_normalize_vip_object_does_not_append_duplicate_nat_object():
    obj_orig = {"name": "vip_dup", "extip": ["203.0.113.11"], "mappedip": ["10.0.0.11"]}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, {}, nw_objects)
    normalize_vip_object(obj_orig, {}, nw_objects)

    assert len(nw_objects) == 1


def test_normalize_vip_object_nat_ip_with_multiple_mappedip_uses_first():
    obj_orig = {"name": "vip_multi_mappedip", "mappedip": ["10.0.0.1", "10.0.0.2"]}
    obj: dict[str, Any] = {}
    nat_obj: dict[str, Any] = {}

    normalize_vip_object_nat_ip(obj_orig, obj, nat_obj)

    assert obj["obj_nat_ip"] == "10.0.0.1"


def test_normalize_vip_object_nat_ip_with_range_mappedip_sets_range_fields():
    obj_orig = {"name": "vip_range_mappedip", "mappedip": ["10.0.0.5-10.0.0.10"]}
    obj: dict[str, Any] = {}
    nat_obj: dict[str, Any] = {}

    normalize_vip_object_nat_ip(obj_orig, obj, nat_obj)

    assert obj["obj_nat_ip"] == "10.0.0.5"
    assert obj["obj_nat_ip_end"] == "10.0.0.10"
    assert nat_obj["obj_name"] == "10.0.0.5-10.0.0.10_NatNwObj"


class TestNormalizeVipObjectRealServers:
    def test_virtual_server_vip_creates_one_object_per_real_server(self):
        obj_orig = {
            "name": "LB-SSMTP",
            "extip": ["203.0.113.5"],
            "mappedip": [],
            "realservers": [
                {"ip": "10.0.0.1", "port": 25, "weight": 1},
                {"ip": "10.0.0.2", "port": 25, "weight": 1},
            ],
        }
        obj: dict[str, Any] = {}
        nw_objects: list[dict[str, Any]] = []

        normalize_vip_object(obj_orig, obj, nw_objects)

        assert obj["obj_nat_real_server_refs"] == ["10.0.0.1_NatNwObj", "10.0.0.2_NatNwObj"]
        assert "obj_nat_ip" not in obj
        assert [nw_obj["obj_ip"] for nw_obj in nw_objects] == ["10.0.0.1", "10.0.0.2"]
        assert all(nw_obj["obj_typ"] == "host" for nw_obj in nw_objects)
        assert all(nw_obj["obj_ip_end"] == nw_obj["obj_ip"] for nw_obj in nw_objects)

    def test_real_server_objects_inherit_vip_zone(self):
        obj_orig = {
            "name": "LB-SSMTP",
            "extip": ["203.0.113.5"],
            "realservers": [{"ip": "10.0.0.1"}],
            "associated-interface": ["port2"],
        }
        nw_objects: list[dict[str, Any]] = []

        normalize_vip_object(obj_orig, {}, nw_objects)

        assert nw_objects[0]["obj_zone"] == "port2"

    def test_real_servers_shared_between_vips_are_not_duplicated(self):
        nw_objects: list[dict[str, Any]] = []
        for vip_name in ("LB-A", "LB-B"):
            normalize_vip_object(
                {"name": vip_name, "extip": ["203.0.113.5"], "realservers": [{"ip": "10.0.0.1"}]}, {}, nw_objects
            )

        assert len(nw_objects) == 1

    def test_malformed_real_server_entries_are_ignored(self):
        obj_orig = {
            "name": "LB-SSMTP",
            "extip": ["203.0.113.5"],
            "realservers": [{"ip": "10.0.0.1"}, {"port": 25}, {"ip": ""}, "not-a-dict"],
        }
        obj: dict[str, Any] = {}
        nw_objects: list[dict[str, Any]] = []

        normalize_vip_object(obj_orig, obj, nw_objects)

        assert obj["obj_nat_real_server_refs"] == ["10.0.0.1_NatNwObj"]
        assert len(nw_objects) == 1

    def test_non_list_realservers_is_ignored(self):
        obj_orig = {"name": "LB-broken", "extip": ["203.0.113.5"], "realservers": "not-a-list"}
        obj: dict[str, Any] = {}
        nw_objects: list[dict[str, Any]] = []

        normalize_vip_object(obj_orig, obj, nw_objects)

        assert "obj_nat_real_server_refs" not in obj
        assert nw_objects == []

    def test_mappedip_takes_precedence_over_realservers(self):
        obj_orig = {
            "name": "vip_both",
            "extip": ["203.0.113.5"],
            "mappedip": ["10.0.0.5"],
            "realservers": [{"ip": "10.0.0.9"}],
        }
        obj: dict[str, Any] = {}
        nw_objects: list[dict[str, Any]] = []

        normalize_vip_object(obj_orig, obj, nw_objects)

        assert obj["obj_nat_ip"] == "10.0.0.5"
        assert "obj_nat_real_server_refs" not in obj


class TestVipWithoutNatTarget:
    def test_vip_without_nat_target_creates_data_issue(self):
        obj_orig = {"name": "vip_no_nat", "extip": ["203.0.113.5"]}

        normalize_vip_object(obj_orig, {}, [])

        api_call: Any = ServiceProvider().get_global_state().import_state.api_call
        api_call.create_data_issue.assert_called_once()
        assert api_call.create_data_issue.call_args.kwargs["obj_name"] == "vip_no_nat"


class TestExpandVipRealServers:
    def test_replaces_virtual_server_vip_with_its_real_servers(self):
        normalized_config_adom = _normalized_config(
            [
                {
                    "obj_name": "LB-SSMTP",
                    "obj_nat_real_server_refs": ["10.0.0.1_NatNwObj", "10.0.0.2_NatNwObj"],
                }
            ]
        )

        dst_list, dst_refs_list = expand_vip_real_servers(
            ["LB-SSMTP"], ["lb-ssmtp-uid"], normalized_config_adom, _normalized_config()
        )

        assert dst_list == ["10.0.0.1_NatNwObj", "10.0.0.2_NatNwObj"]
        assert dst_refs_list == ["10.0.0.1_NatNwObj", "10.0.0.2_NatNwObj"]

    def test_leaves_classic_vip_and_plain_objects_untouched(self):
        normalized_config_adom = _normalized_config([{"obj_name": "vip1", "obj_nat_ip": "10.0.0.5"}])

        dst_list, dst_refs_list = expand_vip_real_servers(
            ["vip1", "some-net"], ["vip1-uid", "some-net-uid"], normalized_config_adom, _normalized_config()
        )

        assert dst_list == ["vip1", "some-net"]
        assert dst_refs_list == ["vip1-uid", "some-net-uid"]

    def test_finds_real_servers_in_global_config(self):
        normalized_config_global = _normalized_config(
            [{"obj_name": "LB-GLOBAL", "obj_nat_real_server_refs": ["10.0.0.3_NatNwObj"]}]
        )

        dst_list, _ = expand_vip_real_servers(
            ["LB-GLOBAL"], ["lb-global-uid"], _normalized_config(), normalized_config_global
        )

        assert dst_list == ["10.0.0.3_NatNwObj"]

    def test_deduplicates_real_servers_shared_by_two_vips(self):
        normalized_config_adom = _normalized_config(
            [
                {"obj_name": "LB-A", "obj_nat_real_server_refs": ["10.0.0.1_NatNwObj"]},
                {"obj_name": "LB-B", "obj_nat_real_server_refs": ["10.0.0.1_NatNwObj"]},
            ]
        )

        dst_list, _ = expand_vip_real_servers(
            ["LB-A", "LB-B"], ["lb-a-uid", "lb-b-uid"], normalized_config_adom, _normalized_config()
        )

        assert dst_list == ["10.0.0.1_NatNwObj"]

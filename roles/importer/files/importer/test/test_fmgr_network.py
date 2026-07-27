from typing import Any

from fw_modules.fortiadom5ff.fmgr_network import normalize_vip_object, normalize_vip_object_nat_ip


def test_normalize_vip_object_with_mappedip_creates_nat_object():
    obj_orig = {"name": "vip1", "extip": ["203.0.113.5"], "mappedip": ["10.0.0.5"]}
    obj: dict[str, Any] = {}
    nw_objects: list[dict[str, Any]] = []

    normalize_vip_object(obj_orig, obj, nw_objects)

    assert obj["obj_nat_ip"] == "10.0.0.5"
    assert obj["obj_nat_ip_end"] == "10.0.0.5"
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

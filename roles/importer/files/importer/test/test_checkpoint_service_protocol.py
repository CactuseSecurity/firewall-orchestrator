from typing import Any

import pytest
from fw_modules.checkpointR8x import cp_const, cp_getter, cp_service, fwcommon


def test_get_protocol_number_handles_string_protocol():
    obj = {"ip-protocol": "17"}

    assert cp_service._get_protocol_number(obj) == 17  # pyright: ignore[reportPrivateUsage]


def test_get_protocol_number_uses_any_protocol_for_static_any_uid():
    assert cp_service._get_protocol_number({"uid": cp_const.any_obj_uid}) == -1  # pyright: ignore[reportPrivateUsage]


def test_get_protocol_number_preserves_tcp_protocol_for_any_named_service():
    assert cp_service._get_protocol_number({"name": "Any", "type": "service-tcp"}) == 6  # pyright: ignore[reportPrivateUsage]


def test_get_protocol_number_does_not_treat_any_named_group_as_any_protocol():
    assert cp_service._get_protocol_number({"name": "Any", "type": "service-group"}) is None  # pyright: ignore[reportPrivateUsage]


def test_collect_single_svc_object_preserves_ports_for_any_named_tcp_service():
    obj: dict[str, Any] = {"name": "Any", "type": "service-tcp", "port": "443"}

    cp_service.collect_single_svc_object(obj)

    assert obj["proto"] == 6
    assert obj["port"] == "443"
    assert obj["port_end"] == "443"


def test_collect_svc_objects_imports_transformed_cpmi_any_object_as_simple_service(
    monkeypatch: pytest.MonkeyPatch,
):
    def get_special_object(uid: str, **_: str) -> dict[str, Any]:
        special_object = {
            "uid": uid,
            "name": "Any" if uid == cp_const.any_obj_uid else "other",
            "color": "black",
            "comments": None,
            "domain": {"uid": "domain-uid"},
            "type": "CpmiAnyObject" if uid == cp_const.any_obj_uid else "service-other",
        }
        if uid == cp_const.any_obj_uid:
            return cp_getter.handle_cpmi_any_object(special_object)
        return {"chunks": [{"objects": [special_object]}]}

    monkeypatch.setattr(fwcommon.cp_getter, "get_object_details_from_api", get_special_object)
    object_table: dict[str, Any] = {"type": "services-other", "chunks": []}
    service_objects: list[dict[str, Any]] = []

    fwcommon.add_special_objects_to_global_domain(object_table, "services-other", "sid", "https://mgm.invalid/")
    cp_service.collect_svc_objects(object_table, service_objects)

    any_service = next(service for service in service_objects if service["svc_uid"] == cp_const.any_obj_uid)
    assert any_service["svc_typ"] == "simple"
    assert any_service["ip_proto"] == -1
    assert any_service["svc_port"] is None
    assert any_service["svc_port_end"] is None


def test_get_rpc_number_stringifies_program_number():
    obj = {"program-number": 100235}

    assert cp_service._get_rpc_number(obj) == "100235"  # pyright: ignore[reportPrivateUsage]

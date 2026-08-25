from typing import Any

from fw_modules.checkpointR8x import cp_service


def test_get_protocol_number_handles_string_protocol():
    obj = {"ip-protocol": "17"}

    assert cp_service._get_protocol_number(obj) == 17  # pyright: ignore[reportPrivateUsage]


def test_get_protocol_number_uses_any_protocol_for_cpmi_any_object():
    assert cp_service._get_protocol_number({"type": "CpmiAnyObject"}) == -1  # pyright: ignore[reportPrivateUsage]


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


def test_collect_svc_objects_imports_cpmi_any_object_as_simple_service():
    object_table: dict[str, Any] = {
        "type": "CpmiAnyObject",
        "chunks": [
            {
                "objects": [
                    {
                        "uid": "any-uid",
                        "name": "Any",
                        "color": "black",
                        "comments": None,
                        "domain": {"uid": "domain-uid"},
                        "type": "CpmiAnyObject",
                    }
                ]
            }
        ],
    }
    service_objects: list[dict[str, Any]] = []

    cp_service.collect_svc_objects(object_table, service_objects)

    assert service_objects[0]["svc_typ"] == "simple"
    assert service_objects[0]["ip_proto"] == -1
    assert service_objects[0]["svc_port"] is None
    assert service_objects[0]["svc_port_end"] is None


def test_get_rpc_number_stringifies_program_number():
    obj = {"program-number": 100235}

    assert cp_service._get_rpc_number(obj) == "100235"  # pyright: ignore[reportPrivateUsage]

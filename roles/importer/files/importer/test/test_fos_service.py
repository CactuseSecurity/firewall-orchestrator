import fwo_const
from fw_modules.fortiosmanagementREST.fos_models import SvcObjCustom
from fw_modules.fortiosmanagementREST.fos_service import normalize_single_custom_service_object


def test_custom_service_multiple_port_ranges_creates_group():
    svc_obj = SvcObjCustom.model_validate(
        {
            "name": "svc-web",
            "q_origin_key": "svc-web",
            "protocol": "TCP/UDP/SCTP",
            "tcp-portrange": "80 443",
            "udp-portrange": "53",
            "comment": "web service",
        }
    )

    service_objects = list(normalize_single_custom_service_object(svc_obj))

    assert [svc.svc_name for svc in service_objects] == ["svc-web_tcp_80", "svc-web_tcp_443", "svc-web_udp", "svc-web"]
    assert [svc.ip_proto for svc in service_objects[:3]] == [6, 6, 17]
    assert service_objects[-1].svc_typ == "group"
    assert service_objects[-1].svc_member_names == fwo_const.LIST_DELIMITER.join(
        ["svc-web_tcp_80", "svc-web_tcp_443", "svc-web_udp"]
    )
    assert service_objects[-1].svc_comment == "web service"
    assert all(svc.svc_comment is None for svc in service_objects[:3])


def test_custom_service_single_port_range_keeps_comment_on_simple_object():
    svc_obj = SvcObjCustom.model_validate(
        {
            "name": "svc-ssh",
            "q_origin_key": "svc-ssh",
            "protocol": "TCP/UDP/SCTP",
            "tcp-portrange": "22",
            "comment": "ssh service",
        }
    )

    service_objects = list(normalize_single_custom_service_object(svc_obj))

    assert len(service_objects) == 1
    assert service_objects[0].svc_name == "svc-ssh"
    assert service_objects[0].ip_proto == 6
    assert service_objects[0].svc_port == 22
    assert service_objects[0].svc_port_end == 22
    assert service_objects[0].svc_comment == "ssh service"


def test_custom_service_ip_protocol_creates_simple_object():
    svc_obj = SvcObjCustom.model_validate(
        {
            "name": "svc-ip",
            "q_origin_key": "svc-ip",
            "protocol": "IP",
            "protocol-number": 47,
            "comment": "gre",
        }
    )

    service_objects = list(normalize_single_custom_service_object(svc_obj))

    assert len(service_objects) == 1
    assert service_objects[0].svc_typ == "simple"
    assert service_objects[0].ip_proto == 47
    assert service_objects[0].svc_comment == "gre"

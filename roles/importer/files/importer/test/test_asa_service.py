from typing import TYPE_CHECKING

from fw_modules.ciscoasa9.asa_models import AccessListEntry, EndpointKind
from fw_modules.ciscoasa9.asa_service import (
    create_any_protocol_service,
    create_protocol_any_service_objects,
    create_service_for_protocol_entry,
)

if TYPE_CHECKING:
    from models.serviceobject import ServiceObject


def test_protocol_any_service_uses_any_protocol_for_ip():
    services = create_protocol_any_service_objects()

    any_ip_service = services["ANY"]
    assert any_ip_service.svc_typ == "simple"
    assert any_ip_service.svc_uid == "ANY"
    assert any_ip_service.svc_name == "any-ip"
    assert any_ip_service.svc_port is None
    assert any_ip_service.svc_port_end is None
    assert any_ip_service.ip_proto == -1
    assert (services["any-tcp"].svc_port, services["any-tcp"].svc_port_end, services["any-tcp"].ip_proto) == (
        0,
        65535,
        6,
    )


def test_create_any_protocol_service_uses_any_protocol_for_ip():
    services: dict[str, ServiceObject] = {}

    service_name = create_any_protocol_service("ip", services)

    assert service_name == "ANY"
    assert services[service_name].svc_uid == "ANY"
    assert services[service_name].svc_name == "any-ip"
    assert services[service_name].svc_port is None
    assert services[service_name].svc_port_end is None
    assert services[service_name].ip_proto == -1


def test_acl_ip_protocol_reuses_seeded_any_service():
    entry = AccessListEntry(
        acl_name="inside_access_in",
        action="permit",
        protocol=EndpointKind(kind="protocol", value="ip"),
        src=EndpointKind(kind="any", value="any"),
        dst=EndpointKind(kind="any", value="any"),
        dst_port=EndpointKind(kind="any", value="any"),
    )
    services = create_protocol_any_service_objects()

    service_name = create_service_for_protocol_entry(entry, services)

    assert service_name == "ANY"
    assert len(services) == 4
    any_service = services[service_name]
    assert any_service.svc_typ == "simple"
    assert any_service.svc_uid == "ANY"
    assert any_service.svc_name == "any-ip"
    assert any_service.svc_port is None
    assert any_service.svc_port_end is None
    assert any_service.ip_proto == -1

from typing import TYPE_CHECKING

from fw_modules.ciscoasa9.asa_models import AccessListEntry, AsaServiceObjectGroup, EndpointKind
from fw_modules.ciscoasa9.asa_service import (
    create_any_protocol_service,
    create_protocol_any_service_objects,
    create_service_for_protocol_entry,
    create_service_object,
    normalize_service_object_groups,
)

if TYPE_CHECKING:
    from models.serviceobject import ServiceObject


def test_protocol_any_service_uses_any_protocol_for_ip():
    services: dict[str, ServiceObject] = {}

    create_protocol_any_service_objects(services)

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
    services: dict[str, ServiceObject] = {}
    create_protocol_any_service_objects(services)

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


def test_config_object_named_any_does_not_collide_with_canonical_any_ip_service():
    services: dict[str, ServiceObject] = {}
    create_protocol_any_service_objects(services)
    any_group = AsaServiceObjectGroup(
        name="ANY",
        proto_mode="tcp",
        ports_eq={"tcp": ["80"]},
        ports_range={},
        nested_refs=[],
        protocols=[],
        description=None,
    )

    normalize_service_object_groups([any_group], services)

    configured_any = services["ANY"]
    assert configured_any.svc_typ == "group"
    assert configured_any.svc_member_refs == "80-tcp"

    relocated_uid = create_any_protocol_service("ip", services)
    assert relocated_uid != "ANY"
    canonical_any_ip = services[relocated_uid]
    assert canonical_any_ip.svc_typ == "simple"
    assert canonical_any_ip.svc_name == "any-ip"
    assert canonical_any_ip.ip_proto == -1


def test_config_group_named_any_enabling_ip_protocol_does_not_self_reference():
    services: dict[str, ServiceObject] = {}
    create_protocol_any_service_objects(services)
    any_group = AsaServiceObjectGroup(
        name="ANY",
        proto_mode=None,
        ports_eq={},
        ports_range={},
        nested_refs=[],
        protocols=["ip"],
        description=None,
    )

    normalize_service_object_groups([any_group], services)

    configured_any = services["ANY"]
    assert configured_any.svc_typ == "group"
    assert configured_any.svc_member_refs != "ANY"

    member_uid = configured_any.svc_member_refs
    assert member_uid is not None
    canonical_any_ip = services[member_uid]
    assert canonical_any_ip.svc_typ == "simple"
    assert canonical_any_ip.svc_name == "any-ip"
    assert canonical_any_ip.ip_proto == -1


def test_config_object_named_any_tcp_with_narrow_port_does_not_narrow_any_any_acl():
    services: dict[str, ServiceObject] = {}
    services["any-tcp"] = create_service_object("any-tcp", 443, 443, "tcp", "user object")

    create_protocol_any_service_objects(services)

    # the configured object keeps its name and its narrow port range
    assert services["any-tcp"].svc_port == 443
    assert services["any-tcp"].svc_port_end == 443

    entry = AccessListEntry(
        acl_name="inside_access_in",
        action="permit",
        protocol=EndpointKind(kind="protocol", value="tcp"),
        src=EndpointKind(kind="any", value="any"),
        dst=EndpointKind(kind="any", value="any"),
        dst_port=EndpointKind(kind="any", value="any"),
    )
    service_name = create_service_for_protocol_entry(entry, services)

    # "permit tcp any any" must resolve to the wide-open synthetic object, not the narrow one
    assert service_name != "any-tcp"
    any_tcp_service = services[service_name]
    assert any_tcp_service.svc_typ == "simple"
    assert any_tcp_service.svc_port == 0
    assert any_tcp_service.svc_port_end == 65535


def test_config_group_named_any_processed_before_seeding_is_preserved():
    services: dict[str, ServiceObject] = {}
    any_group = AsaServiceObjectGroup(
        name="ANY",
        proto_mode="tcp",
        ports_eq={"tcp": ["80"]},
        ports_range={},
        nested_refs=[],
        protocols=[],
        description=None,
    )
    normalize_service_object_groups([any_group], services)

    create_protocol_any_service_objects(services)

    configured_any = services["ANY"]
    assert configured_any.svc_typ == "group"
    assert configured_any.svc_member_refs == "80-tcp"
    canonical_any_ip_uids = [
        uid for uid, obj in services.items() if obj.svc_name == "any-ip" and obj.svc_typ == "simple"
    ]
    assert len(canonical_any_ip_uids) == 1
    assert canonical_any_ip_uids[0] != "ANY"

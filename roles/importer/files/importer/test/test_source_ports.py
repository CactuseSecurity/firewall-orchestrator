from __future__ import annotations

from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import fwo_const
import pytest
from fw_modules.checkpointR8x.cp_service import collect_svc_objects
from fw_modules.ciscoasa9.asa_acl_parser import AsaConfigParseError
from fw_modules.ciscoasa9.asa_models import EndpointKind
from fw_modules.ciscoasa9.asa_normalize import normalize_config
from fw_modules.ciscoasa9.asa_parser import parse_asa_config
from fw_modules.ciscoasa9.asa_source_port import (
    _port_group_member_bounds,  # pyright: ignore[reportPrivateUsage]
    restrict_source_ports,
    service_leaves,
    store_qualified_service,
)
from fw_modules.fortiadom5ff.fmgr_service import normalize_service_object
from fw_modules.fortiosmanagementREST.fos_models import SvcObjCustom
from fw_modules.fortiosmanagementREST.fos_service import normalize_single_custom_service_object
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.serviceobject import ServiceObject, ServiceObjectForImport
from services.port_range import parse_port_range, parse_transport_ports

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized


def normalize_asa(text: str) -> FwConfigNormalized:
    config_in = FwConfigManagerListController.generate_empty_config()
    config_in.native_config = parse_asa_config("hostname test\n" + text).model_dump()
    state = MagicMock()
    state.mgm_details.uid = "asa"
    return normalize_config(config_in, state).ManagerSet[0].configs[0]


def rule_services(config: FwConfigNormalized) -> list[ServiceObject]:
    rules = [rule for rulebase in config.rulebases for rule in rulebase.rules.values()]
    assert len(rules) == 1
    leaves: list[ServiceObject] = []
    for reference in rules[0].rule_svc_refs.split(fwo_const.LIST_DELIMITER):
        leaves.extend(service_leaves(reference, config.service_objects))
    return leaves


def test_original_asa_acl_retains_source_range_and_any_destination_port() -> None:
    config = normalize_asa(
        "object network H-DMZ1-SDASI1\n host 192.0.2.1\n!\n"
        "object network H-DMZ2-Netz\n subnet 198.51.100.0 255.255.255.0\n!\n"
        "access-list dmz1_access_in extended permit tcp object H-DMZ1-SDASI1 "
        "range 7937 10000 object H-DMZ2-Netz\n"
    )
    service = rule_services(config)[0]
    assert (service.ip_proto, service.svc_port, service.svc_port_end) == (6, 0, 65535)
    assert (service.svc_source_port, service.svc_source_port_end) == (7937, 10000)
    payload = ServiceObjectForImport(service, 1, 2, 3, 4).to_dict()
    assert (payload["svc_source_port"], payload["svc_source_port_end"]) == (7937, 10000)
    assert ServiceObject.model_validate(service.model_dump()) == service


@pytest.mark.parametrize(
    ("selection", "start", "end"),
    [("eq domain", 53, 53), ("range 0 65535", 0, 65535), ("lt 1024", 0, 1023), ("gt 1023", 1024, 65535)],
)
@pytest.mark.parametrize("protocol", ["tcp", "udp", "sctp"])
def test_asa_inline_source_and_destination_ports(selection: str, start: int, end: int, protocol: str) -> None:
    config = normalize_asa(f"access-list ACL extended permit {protocol} any {selection} any eq 443\n")
    service = rule_services(config)[0]
    assert (service.svc_source_port, service.svc_source_port_end) == (start, end)
    assert (service.svc_port, service.svc_port_end) == (443, 443)


@pytest.mark.parametrize("protocol", ["tcp", "udp", "tcp-udp"])
@pytest.mark.parametrize("destination", ["", " destination eq https", " destination range 80 90"])
def test_asa_named_service_preserves_both_port_pairs(protocol: str, destination: str) -> None:
    config = normalize_asa(
        f"object service NAMED\n service {protocol} source range 1000 2000{destination}\n!\n"
        "access-list ACL extended permit object NAMED any any\n"
    )
    services = rule_services(config)
    assert {service.ip_proto for service in services} == (
        {6, 17} if protocol == "tcp-udp" else {6 if protocol == "tcp" else 17}
    )
    expected = (0, 65535) if not destination else ((443, 443) if "eq" in destination else (80, 90))
    assert {(service.svc_port, service.svc_port_end) for service in services} == {expected}
    assert {(service.svc_source_port, service.svc_source_port_end) for service in services} == {(1000, 2000)}


def test_asa_service_group_source_and_destination_remain_paired() -> None:
    config = normalize_asa(
        "object-group service INNER\n"
        " service-object tcp source eq 1024 destination eq 443\n"
        " service-object tcp source eq 2048 destination eq 80\n!\n"
        "object-group service OUTER\n group-object INNER\n!\n"
        "access-list ACL extended permit object-group OUTER any any\n"
    )
    assert {(service.svc_source_port, service.svc_port) for service in rule_services(config)} == {
        (1024, 443),
        (2048, 80),
    }


def test_asa_source_port_groups_and_protocol_groups() -> None:
    config = normalize_asa(
        "object-group service PORTS tcp-udp\n port-object eq domain\n port-object range 1000 1010\n!\n"
        "object-group protocol PROTOCOLS\n protocol-object tcp\n protocol-object udp\n!\n"
        "access-list ACL extended permit object-group PROTOCOLS any object-group PORTS any eq 443\n"
    )
    services = rule_services(config)
    assert len(services) == 4
    assert {service.ip_proto for service in services} == {6, 17}
    assert {(service.svc_source_port, service.svc_source_port_end) for service in services} == {(53, 53), (1000, 1010)}
    assert {service.svc_port for service in services} == {443}


def test_source_group_with_unrestricted_transport_preserves_all_source_ports() -> None:
    config = normalize_asa(
        "object-group service PORTS\n service-object tcp\n!\n"
        "access-list ACL extended permit tcp any object-group PORTS any eq 443\n"
    )
    service = rule_services(config)[0]
    assert (service.svc_source_port, service.svc_source_port_end) == (0, 65535)


def test_source_group_skips_other_protocols_and_intersects_destination_group() -> None:
    config = normalize_asa(
        "object-group service PORTS\n service-object tcp destination eq 1024\n service-object udp destination eq 53\n!\n"
        "object-group service DEST tcp\n port-object eq 80\n port-object eq 443\n!\n"
        "access-list ACL extended permit tcp any object-group PORTS any object-group DEST\n"
    )
    services = rule_services(config)
    assert {service.svc_source_port for service in services} == {1024}
    assert {service.svc_port for service in services} == {80, 443}


def test_source_group_with_intrinsic_source_constraint_is_rejected() -> None:
    with pytest.raises(ValueError, match="contains a service with source restrictions"):
        normalize_asa(
            "object-group service PORTS\n service-object tcp source eq 1024 destination eq 443\n!\n"
            "access-list ACL extended permit tcp any object-group PORTS any\n"
        )


def test_source_service_rejects_invalid_non_transport_protocol() -> None:
    with pytest.raises(AsaConfigParseError, match="transport protocol"):
        normalize_asa("object service BAD\n service gre source eq 10\n!\n")


def test_source_constraint_intersects_existing_named_service() -> None:
    config = normalize_asa(
        "object service NAMED\n service tcp source range 1000 2000 destination eq 443\n!\n"
        "access-list ACL extended permit tcp any range 1500 2500 any object NAMED\n"
    )
    service = rule_services(config)[0]
    assert (service.svc_source_port, service.svc_source_port_end) == (1500, 2000)
    assert config.service_objects["NAMED"].svc_source_port == 1000


@pytest.mark.parametrize(
    "definition",
    ["source eq", "source neq 80", "source range 100 10", "source eq 99999", "source eq 10 ignored"],
)
def test_invalid_named_source_constraints_stop_import(definition: str) -> None:
    with pytest.raises(AsaConfigParseError):
        normalize_asa(f"object service BAD\n service tcp {definition}\n!\n")


def test_source_ports_on_non_transport_protocol_stop_import() -> None:
    with pytest.raises(ValueError, match="require TCP"):
        normalize_asa("access-list ACL extended permit gre any eq 1000 any\n")


def test_unknown_and_cyclic_source_groups_stop_import() -> None:
    with pytest.raises(ValueError, match="Unknown service"):
        service_leaves("missing", {})
    cyclic = ServiceObject(
        svc_uid="CYCLE", svc_name="CYCLE", svc_typ="group", svc_color="black", svc_member_refs="CYCLE"
    )
    with pytest.raises(ValueError, match="exceeds"):
        service_leaves("CYCLE", {"CYCLE": cyclic})


@pytest.mark.parametrize(
    ("port", "port_end", "expected"),
    [(1000, 1010, (1000, 1010)), (1000, None, (1000, 1000)), (None, None, (0, 65535))],
)
def test_port_group_member_bounds(port: int | None, port_end: int | None, expected: tuple[int, int]) -> None:
    member = ServiceObject(
        svc_uid="member", svc_name="member", svc_typ="simple", svc_color="black", svc_port=port, svc_port_end=port_end
    )
    assert _port_group_member_bounds(member) == expected


def test_disjoint_source_ranges_do_not_widen_service() -> None:
    base = ServiceObject(
        svc_uid="base",
        svc_name="base",
        svc_typ="simple",
        svc_color="black",
        ip_proto=6,
        svc_source_port=10,
        svc_source_port_end=20,
    )
    with pytest.raises(ValueError, match="matches no service"):
        restrict_source_ports("base", EndpointKind(kind="range", value="30 40"), {"base": base})


def test_generated_service_uid_does_not_overwrite_configured_object() -> None:
    base = ServiceObject(svc_uid="base", svc_name="base", svc_typ="simple", svc_color="black", ip_proto=6)
    services: dict[str, ServiceObject] = {}
    reference = store_qualified_service(base, services)
    assert store_qualified_service(base, services) == reference
    configured = services[reference].model_copy(update={"svc_port": 80})
    services[reference] = configured
    alternative = store_qualified_service(base, services)
    assert alternative != reference
    assert services[reference] == configured


def test_generated_service_uid_respects_names_not_processed_yet() -> None:
    base = ServiceObject(svc_uid="base", svc_name="base", svc_typ="simple", svc_color="black", ip_proto=6)
    reference = store_qualified_service(base, {})
    services: dict[str, ServiceObject] = {}
    generated = store_qualified_service(base, services, frozenset({reference}))
    assert generated != reference
    assert reference not in services
    assert store_qualified_service(base, services, frozenset({reference})) == generated


def test_generated_named_service_members_do_not_collide_with_later_groups() -> None:
    definition = "object service NAMED\n service tcp-udp source eq 1024 destination eq 443\n!\n"
    acl = "access-list ACL extended permit object NAMED any any\n"
    initial = normalize_asa(definition + acl)
    reference = rule_services(initial)[0].svc_uid
    config = normalize_asa(definition + f"object-group service {reference} tcp\n port-object eq 80\n!\n" + acl)
    assert {(service.ip_proto, service.svc_source_port, service.svc_port) for service in rule_services(config)} == {
        (6, 1024, 443),
        (17, 1024, 443),
    }
    assert {service.svc_port for service in service_leaves(reference, config.service_objects)} == {80}


@pytest.mark.parametrize(
    ("source", "expected"),
    [
        (None, (None, None)),
        ("", (None, None)),
        ("1024", (1024, 1024)),
        ("7937-10000", (7937, 10000)),
        (">1023", (1024, 65535)),
        ("<1024", (1, 1023)),
    ],
)
@pytest.mark.parametrize("protocol", ["tcp", "udp"])
def test_checkpoint_source_ports(source: str | None, expected: tuple[int | None, int | None], protocol: str) -> None:
    native: dict[str, Any] = {
        "type": f"services-{protocol}",
        "chunks": [
            {
                "objects": [
                    {
                        "uid": "service",
                        "name": "service",
                        "type": f"service-{protocol}",
                        "port": "443",
                        "source-port": source,
                    }
                ]
            }
        ],
    }
    normalized: list[dict[str, Any]] = []
    collect_svc_objects(native, normalized)
    service = ServiceObject.model_validate(normalized[0])
    assert (service.svc_source_port, service.svc_source_port_end) == expected
    assert (service.svc_port, service.svc_port_end) == (443, 443)


@pytest.mark.parametrize("protocol", ["tcp", "udp", "sctp"])
def test_fortinet_source_pairs_keep_duplicate_destination_services_distinct(protocol: str) -> None:
    native: dict[str, Any] = {
        "name": "paired",
        "q_origin_key": "paired",
        "protocol": "TCP/UDP/SCTP",
        f"{protocol}-portrange": "443:1000-2000 443:3000-4000",
    }
    fortios = list(normalize_single_custom_service_object(SvcObjCustom.model_validate(native)))
    fmgr_native: dict[str, Any] = {
        "name": "paired",
        "protocol": 5,
        f"{protocol}-portrange": ["443:1000-2000", "443:3000-4000"],
    }
    fortimanager: list[dict[str, Any]] = []
    normalize_service_object(fmgr_native, fortimanager)
    for services in (fortios, [ServiceObject.model_validate(service) for service in fortimanager]):
        leaves = [service for service in services if service.svc_typ == "simple"]
        assert len(leaves) == 2
        assert len({service.svc_uid for service in leaves}) == 2
        assert {service.svc_port for service in leaves} == {443}
        assert {(service.svc_source_port, service.svc_source_port_end) for service in leaves} == {
            (1000, 2000),
            (3000, 4000),
        }
        group = next(service for service in services if service.svc_typ == "group")
        assert set((group.svc_member_refs or "").split(fwo_const.LIST_DELIMITER)) == {
            service.svc_uid for service in leaves
        }


def test_fortimanager_split_services_without_source_ports_keep_uids() -> None:
    fmgr_native: dict[str, Any] = {"name": "web", "protocol": 5, "tcp-portrange": ["80-90", "443"]}
    fortimanager: list[dict[str, Any]] = []
    normalize_service_object(fmgr_native, fortimanager)
    leaf_uids = {service["svc_uid"] for service in fortimanager if service["svc_typ"] == "simple"}
    assert leaf_uids == {"web_80", "web_443"}


@pytest.mark.parametrize(
    ("value", "expected"), [("0", (0, 0)), ("65535", (65535, 65535)), ("80-90", (80, 90)), ("Any", (1, 65535))]
)
def test_numeric_port_ranges(value: str, expected: tuple[int, int]) -> None:
    assert parse_port_range(value) == expected


@pytest.mark.parametrize("value", ["443:", "443:100-10", "65536:80", "443:65536", "443:80:90", "abc:80"])
def test_malformed_fortinet_ranges_fail(value: str) -> None:
    with pytest.raises(ValueError, match=r"[Ii]nvalid"):
        parse_transport_ports(value)


def test_absent_source_ports_are_serialized_as_null() -> None:
    service = ServiceObject(svc_uid="legacy", svc_name="legacy", svc_color="black", svc_typ="simple")
    payload = ServiceObjectForImport(service, 1, 2, 3, 4).to_dict()
    assert payload["svc_source_port"] is None
    assert payload["svc_source_port_end"] is None
    assert service != service.model_copy(update={"svc_source_port": 1024, "svc_source_port_end": 1024})

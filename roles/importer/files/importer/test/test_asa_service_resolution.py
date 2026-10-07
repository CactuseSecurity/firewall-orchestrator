"""
The services of normalized ASA rules keep the protocol, port and icmp type restrictions of the access-list entry.

Importing an entry with a broader meaning than on the device (e.g. any tcp instead of tcp/53) would make the imported
policy more permissive than the real one (GHSA-p8qh-59qx-rjj4).
"""

from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import fwo_const
import pytest
from fw_modules.ciscoasa9.asa_models import AccessListEntry, EndpointKind
from fw_modules.ciscoasa9.asa_normalize import normalize_config
from fw_modules.ciscoasa9.asa_parser import parse_asa_config
from fw_modules.ciscoasa9.asa_rule import create_service_for_protocol_group_entry
from fw_modules.ciscoasa9.asa_service import (
    MAX_SERVICE_GROUP_DEPTH,
    create_service_for_protocol_entry,
    create_service_group_object,
    restrict_service_to_protocol,
)
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized
    from models.serviceobject import ServiceObject

FIXTURE_CONFIG = Path(__file__).resolve().parents[1] / "fw_modules" / "ciscoasa9" / "test_asa.conf"
TCP = 6
UDP = 17
ICMP6 = 58
SCTP = 132
ESP = 50
DNS_PORT = 53
WWW_PORT = 80
EXTRA_GROUPS = "object-group service WEB_TCP tcp\n port-object eq www\n!\n"
ANY = EndpointKind(kind="any", value="any")

ServiceTuple = tuple[int | None, int | None, int | None]


def normalize_with(acl_lines: str) -> FwConfigNormalized:
    """Parse and normalize the fixture config with additional groups and access-list entries."""
    config_in = FwConfigManagerListController.generate_empty_config()
    config_in.native_config = parse_asa_config(
        FIXTURE_CONFIG.read_text().rstrip("\n") + "\n" + EXTRA_GROUPS + acl_lines
    ).model_dump()
    import_state = MagicMock()
    import_state.mgm_details.uid = "asa"
    return normalize_config(config_in, import_state).ManagerSet[0].configs[0]


def flat_services(config: FwConfigNormalized, service_refs: str) -> set[ServiceTuple]:
    """Resolve a rule's service references to (protocol, port, port end) of the simple services they contain."""
    services: set[ServiceTuple] = set()
    pending: list[str] = service_refs.split(fwo_const.LIST_DELIMITER)
    while pending:
        service = config.service_objects[pending.pop()]
        if service.svc_typ == "group":
            pending.extend((service.svc_member_refs or "").split(fwo_const.LIST_DELIMITER))
        else:
            services.add((service.ip_proto, service.svc_port, service.svc_port_end))
    return services


def service_of_entry(acl_line: str) -> tuple[FwConfigNormalized, str]:
    """Normalize one entry of access-list TEST and return the config and the service references of its rule."""
    config = normalize_with(acl_line + "\n")
    rules = [rule for rulebase in config.rulebases for rule in rulebase.rules.values() if rule.rule_name == "TEST"]
    assert len(rules) == 1
    return config, rules[0].rule_svc


@pytest.mark.parametrize(
    ("acl_line", "expected"),
    [
        (
            "access-list TEST extended permit object-group DEMO_OG_PROTO_TCP_UDP any any eq domain",
            {(TCP, DNS_PORT, DNS_PORT), (UDP, DNS_PORT, DNS_PORT)},
        ),
        (
            "access-list TEST extended permit object-group DEMO_OG_PROTO_TCP any any range 1000 2000",
            {(TCP, 1000, 2000)},
        ),
        ("access-list TEST extended permit sctp any any eq 2905", {(SCTP, 2905, 2905)}),
        ("access-list TEST extended permit 50 any any", {(ESP, None, None)}),
    ],
)
def test_protocol_and_port_restrictions_are_kept(acl_line: str, expected: set[ServiceTuple]) -> None:
    config, service_refs = service_of_entry(acl_line)

    assert flat_services(config, service_refs) == expected


@pytest.mark.parametrize(("protocol", "protocol_id"), [("tcp", TCP), ("udp", UDP)])
def test_port_group_only_applies_to_the_protocol_of_the_entry(protocol: str, protocol_id: int) -> None:
    config, service_refs = service_of_entry(
        f"access-list TEST extended permit {protocol} any any object-group DEMO_OG_SVC_PORTS"
    )

    assert service_refs == f"DEMO_OG_SVC_PORTS ({protocol})"
    assert {service[0] for service in flat_services(config, service_refs)} == {protocol_id}
    assert {service[0] for service in flat_services(config, "DEMO_OG_SVC_PORTS")} == {TCP, UDP}


def test_port_group_of_the_entry_protocol_only_is_used_as_it_is() -> None:
    _, service_refs = service_of_entry("access-list TEST extended permit tcp any any object-group WEB_TCP")

    assert service_refs == "WEB_TCP"


def test_protocol_group_with_port_group_uses_the_matching_protocols_only() -> None:
    config, service_refs = service_of_entry(
        "access-list TEST extended permit object-group DEMO_OG_PROTO_TCP_UDP any any object-group WEB_TCP"
    )

    assert flat_services(config, service_refs) == {(TCP, WWW_PORT, WWW_PORT)}


def test_icmp6_type_is_kept() -> None:
    config, service_refs = service_of_entry("access-list TEST extended permit icmp6 any6 any6 echo")

    assert service_refs == "icmp6-echo"
    assert config.service_objects[service_refs].ip_proto == ICMP6


def test_port_group_without_service_of_the_entry_protocol_fails() -> None:
    with pytest.raises(ValueError, match="contains no udp service"):
        service_of_entry("access-list TEST extended permit udp any any object-group WEB_TCP")


def test_port_on_protocol_without_ports_fails() -> None:
    entry = AccessListEntry(
        acl_name="TEST",
        action="permit",
        protocol=EndpointKind(kind="protocol", value="gre"),
        src=ANY,
        dst=ANY,
        dst_port=EndpointKind(kind="eq", value="80"),
    )

    with pytest.raises(ValueError, match="takes no port"):
        create_service_for_protocol_entry(entry, {})


def test_unknown_protocol_group_fails_instead_of_any() -> None:
    entry = AccessListEntry(
        acl_name="TEST",
        action="permit",
        protocol=EndpointKind(kind="protocol-group", value="NO_SUCH_GROUP"),
        src=ANY,
        dst=ANY,
        dst_port=ANY,
    )

    with pytest.raises(ValueError, match="unknown or empty"):
        create_service_for_protocol_group_entry(entry, [], {})


def test_restriction_of_unknown_reference_is_left_to_the_consistency_check() -> None:
    assert restrict_service_to_protocol("NO_SUCH_SERVICE", "tcp", {}) == "NO_SUCH_SERVICE"


def test_restriction_of_cyclic_groups_stops() -> None:
    services: dict[str, ServiceObject] = {
        "LOOP": create_service_group_object("LOOP", ["LOOP"]),
    }

    with pytest.raises(ValueError, match=f"deeper than {MAX_SERVICE_GROUP_DEPTH}"):
        restrict_service_to_protocol("LOOP", "tcp", services)

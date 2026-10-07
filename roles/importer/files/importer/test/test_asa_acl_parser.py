from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import fwo_base
import pytest
from fw_modules.ciscoasa9.asa_acl_parser import AsaConfigParseError, parse_access_list_entry
from fw_modules.ciscoasa9.asa_models import (
    AccessListEntry,
    AsaProtocolGroup,
    AsaServiceObject,
    AsaServiceObjectGroup,
    EndpointKind,
    Names,
)
from fw_modules.ciscoasa9.asa_network import get_network_rule_endpoint, normalize_names
from fw_modules.ciscoasa9.asa_normalize import normalize_config
from fw_modules.ciscoasa9.asa_parser import parse_asa_config
from fw_modules.ciscoasa9.asa_rule import create_rule_from_acl_entry, resolve_network_reference_for_rule
from fwo_exceptions import FwoImporterError
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized
    from models.networkobject import NetworkObject
    from models.serviceobject import ServiceObject

FIXTURE_CONFIG = Path(__file__).resolve().parents[1] / "fw_modules" / "ciscoasa9" / "test_asa.conf"
FIXTURE_ACL_LINES = 27
PROTOCOL_GROUPS: list[AsaProtocolGroup] = [AsaProtocolGroup(name="PROTO_TCP", protocols=["tcp"])]
SVC_OBJECTS: list[AsaServiceObject] = [
    AsaServiceObject(name="SVC_8443", protocol="tcp", dst_port_eq="8443"),
]
SVC_OBJ_GROUPS: list[AsaServiceObjectGroup] = [
    AsaServiceObjectGroup(
        name="SVC_PORTS",
        proto_mode="tcp",
        ports_eq={"tcp": ["135"]},
        ports_range={},
        nested_refs=[],
        protocols=[],
        description=None,
    ),
]
ACL_PREFIX = "access-list ACL extended permit"


def parse(line: str) -> AccessListEntry:
    return parse_access_list_entry(line, PROTOCOL_GROUPS, SVC_OBJECTS, SVC_OBJ_GROUPS)


def endpoint(kind: str, value: str, mask: str | None = None) -> EndpointKind:
    return EndpointKind.model_validate({"kind": kind, "value": value, "mask": mask})


# ───────────────────────── known forms keep their meaning ─────────────────────────


@pytest.mark.parametrize(
    ("address", "expected"),
    [
        ("any", endpoint("any", "any")),
        ("any4", endpoint("any", "any")),
        ("any6", endpoint("any6", "any6")),
        ("host 192.0.2.10", endpoint("host", "192.0.2.10")),
        ("host 2001:db8::1", endpoint("hostv6", "2001:db8::1")),
        ("host WEBSERVER", endpoint("host", "WEBSERVER")),
        ("object OBJ_NET", endpoint("object", "OBJ_NET")),
        ("object-group OG_NET", endpoint("object-group", "OG_NET")),
        ("10.0.0.0 255.255.255.128", endpoint("subnet", "10.0.0.0", "255.255.255.128")),
        ("INSIDE_NET 255.255.255.0", endpoint("subnet", "INSIDE_NET", "255.255.255.0")),
        ("2001:db8::/32", endpoint("subnetv6", "2001:db8::/32")),
    ],
)
def test_known_source_and_destination_forms_keep_their_meaning(address: str, expected: EndpointKind) -> None:
    as_source: AccessListEntry = parse(f"{ACL_PREFIX} ip {address} host 198.51.100.1")
    as_destination: AccessListEntry = parse(f"{ACL_PREFIX} ip host 198.51.100.1 {address}")

    assert as_source.src == expected
    assert as_destination.dst == expected


@pytest.mark.parametrize(
    ("service", "expected"),
    [
        ("eq 443", endpoint("eq", "443")),
        ("eq https", endpoint("eq", "https")),
        ("range 2000 2005", endpoint("range", "2000 2005")),
        ("lt 1024", endpoint("range", "0 1023")),
        ("gt 1023", endpoint("range", "1024 65535")),
        ("lt www", endpoint("range", "0 79")),
        ("object-group SVC_PORTS", endpoint("service-group", "SVC_PORTS")),
        ("object SVC_8443", endpoint("service", "SVC_8443")),
        ("", endpoint("any", "any")),
    ],
)
def test_destination_ports_keep_their_meaning(service: str, expected: EndpointKind) -> None:
    entry: AccessListEntry = parse(f"{ACL_PREFIX} tcp any host 198.51.100.1 {service}".rstrip())

    assert entry.dst_port == expected


def test_icmp_type_is_kept_instead_of_widening_to_any_icmp() -> None:
    entry: AccessListEntry = parse(f"{ACL_PREFIX} icmp any object-group OG_DMZ echo-reply")

    assert entry.dst_port == endpoint("eq", "echo-reply")


def test_service_protocol_argument_defines_the_service_without_port() -> None:
    by_group: AccessListEntry = parse(f"{ACL_PREFIX} object-group SVC_PORTS any any")
    by_object: AccessListEntry = parse(f"{ACL_PREFIX} object SVC_8443 any any")
    by_protocol_group: AccessListEntry = parse(f"{ACL_PREFIX} object-group PROTO_TCP any any eq 22")

    assert by_group.dst_port == endpoint("service-group", "SVC_PORTS")
    assert by_object.dst_port == endpoint("service", "SVC_8443")
    assert by_protocol_group.protocol == endpoint("protocol-group", "PROTO_TCP")
    assert by_protocol_group.dst_port == endpoint("eq", "22")


@pytest.mark.parametrize(
    "options",
    [
        "log",
        "log disable",
        "log default",
        "log 6",
        "log informational interval 300",
        "log interval 60",
        "log 4 inactive",
    ],
)
def test_log_options_are_accepted(options: str) -> None:
    entry: AccessListEntry = parse(f"{ACL_PREFIX} tcp any any eq 22 {options}")

    assert entry.dst_port == endpoint("eq", "22")
    assert entry.inactive == options.endswith("inactive")


def test_time_range_and_inactive_are_kept() -> None:
    entry: AccessListEntry = parse(f"{ACL_PREFIX} tcp any any eq 22 log time-range WORKHOURS inactive")

    assert entry.time_range == "WORKHOURS"
    assert entry.inactive


def test_line_number_and_deny_are_parsed() -> None:
    entry: AccessListEntry = parse("access-list ACL line 3 extended deny ip any any")

    assert entry.acl_name == "ACL"
    assert entry.action == "deny"


# ───────────────────────── everything else fails closed ─────────────────────────


@pytest.mark.parametrize(
    ("line", "token_position", "reason_part"),
    [
        (f"{ACL_PREFIX} ip", 5, "address is expected"),
        (f"{ACL_PREFIX} ip any", 6, "address is expected"),
        (f"{ACL_PREFIX} ip host", 6, "host name or address is expected"),
        (f"{ACL_PREFIX} ip any object-group", 7, "object-group name or address is expected"),
        (f"{ACL_PREFIX} tcp any any eq", 8, "port after 'eq' is expected"),
        (f"{ACL_PREFIX} tcp any any range 1000", 9, "end port of range is expected"),
        (f"{ACL_PREFIX} ip interface inside any", 5, "unsupported address 'interface inside'"),
        (f"{ACL_PREFIX} ip any interface inside", 6, "unsupported address 'interface inside'"),
        (f"{ACL_PREFIX} ip user LOCAL\\alice any any", 5, "unsupported address 'user LOCAL\\alice'"),
        (f"{ACL_PREFIX} ip object-group-security SG any any", 5, "unsupported address"),
        (f"{ACL_PREFIX} ip somethingunknown any", 5, "unsupported address 'somethingunknown'"),
        (f"{ACL_PREFIX} ip 10.0.0.0 0.0.0.255 any", 5, "unsupported address '10.0.0.0'"),
        (f"{ACL_PREFIX} ip 10.0.0.0 any", 5, "unsupported address '10.0.0.0'"),
        (f"{ACL_PREFIX} tcp any eq 1024 any", 6, "unsupported source port 'eq 1024'"),
        (f"{ACL_PREFIX} tcp any object-group SVC_PORTS any", 6, "unsupported source port object-group"),
        (f"{ACL_PREFIX} tcp any any neq 80", 7, "unsupported port operator 'neq 80'"),
        (f"{ACL_PREFIX} tcp any any lt 0", 7, "is empty"),
        (f"{ACL_PREFIX} tcp any any gt 65535", 7, "is empty"),
        (f"{ACL_PREFIX} tcp any any lt nosuchport", 8, "unknown port 'nosuchport'"),
        (f"{ACL_PREFIX} tcp any any eq nosuchport", 8, "unknown port 'nosuchport'"),
        (f"{ACL_PREFIX} tcp any any eq 99999", 8, "port '99999' out of range"),
        (f"{ACL_PREFIX} tcp any any range 1 99999", 9, "port '99999' out of range"),
        (f"{ACL_PREFIX} tcp any any range nosuchport 80", 8, "unknown port 'nosuchport'"),
        (f"{ACL_PREFIX} tcp any any range 100 10", 7, "port range '100 10' ends before it starts"),
        (f"{ACL_PREFIX} icmp any any unreachable 3", 8, "unsupported icmp code"),
        (f"{ACL_PREFIX} icmp any any nosuchtype", 7, "unknown icmp type"),
        (f"{ACL_PREFIX} tcp any any eq 22 garbage", 9, "unexpected token 'garbage'"),
        (f"{ACL_PREFIX} ip any any 22", 7, "unexpected token '22'"),
        (f"{ACL_PREFIX} tcp any any eq 22 log interval soon", 11, "invalid log interval"),
        (f"{ACL_PREFIX} tcp any any eq 22 time-range", 10, "time-range name is expected"),
        (f"{ACL_PREFIX} object-group NO_SUCH_GROUP any any", 5, "unknown protocol or service object-group"),
        (f"{ACL_PREFIX} object NO_SUCH_SERVICE any any", 5, "unknown service object"),
        ("access-list ACL extended allow ip any any", 3, "unknown action 'allow'"),
        ("access-list ACL standard permit host 10.0.0.1", 2, "unsupported access-list type 'standard'"),
        ("access-list ACL extended", 3, "action is expected"),
    ],
)
def test_unknown_truncated_and_unsupported_syntax_fails_closed(
    line: str, token_position: int, reason_part: str
) -> None:
    with pytest.raises(AsaConfigParseError) as raised:
        parse(line)

    assert raised.value.token_position == token_position
    assert reason_part in raised.value.reason


def test_parse_error_is_an_importer_error_that_stops_the_import() -> None:
    assert issubclass(AsaConfigParseError, FwoImporterError)


def test_interface_inside_entry_stops_the_config_parse_with_its_line_number() -> None:
    config: str = (
        ": Saved\n"
        ": Serial Number: DEMO\n"
        "ASA Version 9.16(4)\n"
        "hostname demo\n"
        "access-list OUTSIDE_IN extended permit tcp any any eq 22\n"
        "access-list OUTSIDE_IN extended permit ip interface inside any\n"
        "access-list OUTSIDE_IN extended deny ip any any\n"
    )

    with pytest.raises(AsaConfigParseError) as raised:
        parse_asa_config(config)

    error: AsaConfigParseError = raised.value
    assert error.line_number == 6
    assert error.token_position == 5
    assert error.line == "access-list OUTSIDE_IN extended permit ip interface inside any"
    assert "line 6 token 6: unsupported address 'interface inside'" in str(error)


def test_no_entry_is_silently_dropped() -> None:
    config: str = (
        "access-list OUTSIDE_IN extended deny tcp any eq 1024 any\naccess-list OUTSIDE_IN extended permit ip any any\n"
    )

    with pytest.raises(AsaConfigParseError):
        parse_asa_config(config)


def test_fixture_config_parses_every_entry() -> None:
    config = parse_asa_config(FIXTURE_CONFIG.read_text())

    entries: list[AccessListEntry] = [entry for acl in config.access_lists for entry in acl.entries]
    assert len(entries) == FIXTURE_ACL_LINES
    assert endpoint("eq", "echo-reply") in [entry.dst_port for entry in entries]


# ───────────────────────── normalization of the new endpoint kinds ─────────────────────────


def test_ipv6_endpoints_are_normalized_as_ipv6_objects() -> None:
    network_objects: dict[str, NetworkObject] = {}

    any6: NetworkObject = get_network_rule_endpoint(endpoint("any6", "any6"), network_objects)
    host6: NetworkObject = get_network_rule_endpoint(endpoint("hostv6", "2001:db8::1"), network_objects)
    prefix6: NetworkObject = get_network_rule_endpoint(endpoint("subnetv6", "2001:db8::/32"), network_objects)

    assert (str(any6.obj_ip), str(any6.obj_ip_end)) == ("::/128", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128")
    assert (str(host6.obj_ip), str(host6.obj_ip_end)) == ("2001:db8::1/128", "2001:db8::1/128")
    assert (str(prefix6.obj_ip), str(prefix6.obj_ip_end)) == (
        "2001:db8::/128",
        "2001:db8:ffff:ffff:ffff:ffff:ffff:ffff/128",
    )
    assert get_network_rule_endpoint(endpoint("any6", "any6"), network_objects) is any6


def test_name_aliases_resolve_to_their_address() -> None:
    network_objects: dict[str, NetworkObject] = normalize_names(
        [Names(name="WEBSERVER", ip_address="192.0.2.10"), Names(name="INSIDE_NET", ip_address="10.1.0.0")]
    )
    subnet: EndpointKind = endpoint("subnet", "INSIDE_NET", "255.255.0.0")

    host: NetworkObject = get_network_rule_endpoint(endpoint("host", "WEBSERVER"), network_objects)
    alias_subnet: NetworkObject = get_network_rule_endpoint(subnet, network_objects)

    assert str(host.obj_ip) == "192.0.2.10/32"
    assert (str(alias_subnet.obj_ip), str(alias_subnet.obj_ip_end)) == ("10.1.0.0/32", "10.1.255.255/32")
    assert resolve_network_reference_for_rule(subnet, network_objects) == "10.1.0.0/16"


@pytest.mark.parametrize("unknown", [endpoint("host", "NO_SUCH_NAME"), endpoint("subnet", "NO_SUCH_NET", "255.0.0.0")])
def test_unknown_name_aliases_fail(unknown: EndpointKind) -> None:
    with pytest.raises(ValueError, match="neither an IPv4 address nor a defined name"):
        get_network_rule_endpoint(unknown, {})


# ───────────────────────── rule uids and time-range ─────────────────────────


def test_rule_uid_of_entries_without_time_range_is_unchanged() -> None:
    entry: AccessListEntry = parse(f"{ACL_PREFIX} tcp any host 198.51.100.1 eq 22")
    dump_before_time_range_existed: dict[str, object] = entry.model_dump()
    del dump_before_time_range_existed["time_range"]

    rule = create_rule_from_acl_entry("ACL", entry, PROTOCOL_GROUPS, {}, {})

    assert rule.rule_uid == fwo_base.generate_hash_from_dict(dump_before_time_range_existed)
    assert rule.rule_time is None


def test_time_range_is_imported_as_rule_time() -> None:
    entry: AccessListEntry = parse(f"{ACL_PREFIX} tcp any host 198.51.100.1 eq 22 time-range WORKHOURS")
    service_objects: dict[str, ServiceObject] = {}

    rule = create_rule_from_acl_entry("ACL", entry, PROTOCOL_GROUPS, {}, service_objects)
    rule_without_time_range = create_rule_from_acl_entry(
        "ACL", entry.model_copy(update={"time_range": None}), PROTOCOL_GROUPS, {}, service_objects
    )

    assert rule.rule_time == "WORKHOURS"
    assert rule.rule_uid != rule_without_time_range.rule_uid


def normalize_fixture_with(extra_lines: str) -> FwConfigNormalized:
    """Parse and normalize the fixture config with additional lines, as the import does."""
    config_in = FwConfigManagerListController.generate_empty_config()
    config_in.native_config = parse_asa_config(
        FIXTURE_CONFIG.read_text().rstrip("\n") + "\n" + extra_lines
    ).model_dump()
    import_state = MagicMock()
    import_state.mgm_details.uid = "asa"
    return normalize_config(config_in, import_state).ManagerSet[0].configs[0]


def test_time_range_is_imported_as_time_object_and_passes_the_consistency_check() -> None:
    normalized: FwConfigNormalized = normalize_fixture_with(
        "access-list TIMED extended permit tcp any any eq 22 time-range WORKHOURS\n"
        "access-group TIMED in interface inside\n"
    )
    checker = FwConfigImportCheckConsistency(MagicMock())

    checker.check_time_object_consistency(normalized, None)

    assert "WORKHOURS" in [rule.rule_time for rulebase in normalized.rulebases for rule in rulebase.rules.values()]
    assert normalized.time_objects["WORKHOURS"].time_obj_name == "WORKHOURS"
    assert checker.issues == {}


def test_config_without_time_range_has_no_time_objects() -> None:
    assert normalize_fixture_with("").time_objects == {}

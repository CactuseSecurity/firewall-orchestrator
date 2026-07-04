"""Tests for fw_modules/ciscoasa9/asa_service.py"""

from typing import Any, Literal

import pytest
from fw_modules.ciscoasa9.asa_models import AccessListEntry, AsaServiceObject, AsaServiceObjectGroup, EndpointKind
from fw_modules.ciscoasa9.asa_service import (
    create_any_protocol_service,
    create_protocol_any_service_objects,
    create_protocol_service_object,
    create_service_for_acl_entry,
    create_service_for_port,
    create_service_for_port_range,
    create_service_for_protocol_entry,
    create_service_for_protocol_entry_with_single_protocol,
    create_service_group_object,
    create_service_object,
    normalize_service_object_groups,
    normalize_service_objects,
    process_fully_enabled_protocols,
    process_mixed_protocol_eq_ports,
    process_mixed_protocol_group,
    process_mixed_protocol_range_ports,
    process_single_protocol_eq_ports,
    process_single_protocol_group,
    process_single_protocol_range_ports,
)
from models.serviceobject import ServiceObject


def _acl_entry(
    proto_kind: Literal[
        "any",
        "host",
        "subnet",
        "object",
        "object-group",
        "service",
        "protocol-group",
        "protocol",
        "eq",
        "range",
        "service-group",
    ] = "protocol",
    proto_value: str = "tcp",
    dst_kind: Literal[
        "any",
        "host",
        "subnet",
        "object",
        "object-group",
        "service",
        "protocol-group",
        "protocol",
        "eq",
        "range",
        "service-group",
    ] = "any",
    dst_value: str = "any",
) -> AccessListEntry:
    return AccessListEntry(
        acl_name="test-acl",
        action="permit",
        protocol=EndpointKind(kind=proto_kind, value=proto_value),
        src=EndpointKind(kind="any", value="any"),
        dst=EndpointKind(kind="any", value="any"),
        dst_port=EndpointKind(kind=dst_kind, value=dst_value),
    )


def _service_group(
    name: str = "grp",
    proto_mode: Literal["tcp", "udp", "tcp-udp"] | None = None,
    ports_eq: dict[str, Any] | None = None,
    ports_range: dict[str, Any] | None = None,
    nested_refs: list[str] | None = None,
    protocols: list[str] | None = None,
    description: str | None = None,
) -> AsaServiceObjectGroup:
    return AsaServiceObjectGroup(
        name=name,
        proto_mode=proto_mode,
        ports_eq=ports_eq or {},
        ports_range=ports_range or {},
        nested_refs=nested_refs or [],
        protocols=protocols or [],
        description=description,
    )


class TestCreateServiceObject:
    def test_sets_uid_and_name_to_given_name(self):
        obj = create_service_object("https-svc", 443, 443, "tcp")
        assert obj.svc_uid == "https-svc"
        assert obj.svc_name == "https-svc"

    def test_sets_ports(self):
        obj = create_service_object("range-svc", 1024, 65535, "tcp")
        assert obj.svc_port == 1024
        assert obj.svc_port_end == 65535

    def test_maps_protocol_to_ip_proto(self):
        obj = create_service_object("tcp-svc", 80, 80, "tcp")
        assert obj.ip_proto == 6

    def test_unknown_protocol_defaults_to_zero(self):
        obj = create_service_object("x-svc", 0, 0, "unknown")
        assert obj.ip_proto == 0

    def test_comment_is_stored(self):
        obj = create_service_object("svc", 80, 80, "tcp", comment="web")
        assert obj.svc_comment == "web"

    def test_type_is_simple(self):
        obj = create_service_object("svc", 80, 80, "tcp")
        assert obj.svc_typ == "simple"


class TestCreateProtocolServiceObject:
    def test_sets_uid_and_name(self):
        obj = create_protocol_service_object("icmp-svc", "icmp")
        assert obj.svc_uid == "icmp-svc"
        assert obj.svc_name == "icmp-svc"

    def test_maps_protocol(self):
        obj = create_protocol_service_object("icmp-svc", "icmp")
        assert obj.ip_proto == 1

    def test_no_port_fields(self):
        obj = create_protocol_service_object("icmp-svc", "icmp")
        assert obj.svc_port is None
        assert obj.svc_port_end is None

    def test_type_is_simple(self):
        obj = create_protocol_service_object("gre-svc", "gre")
        assert obj.svc_typ == "simple"


class TestCreateServiceGroupObject:
    def test_sets_uid_and_name(self):
        obj = create_service_group_object("my-grp", ["svc-a", "svc-b"])
        assert obj.svc_uid == "my-grp"
        assert obj.svc_name == "my-grp"

    def test_type_is_group(self):
        obj = create_service_group_object("grp", ["a"])
        assert obj.svc_typ == "group"

    def test_members_are_sorted_and_joined(self):
        obj = create_service_group_object("grp", ["b-svc", "a-svc"])
        svc_member_names = obj.svc_member_names
        assert svc_member_names is not None
        assert "a-svc" in svc_member_names
        assert "b-svc" in svc_member_names

    def test_comment_stored(self):
        obj = create_service_group_object("grp", ["a"], comment="notes")
        assert obj.svc_comment == "notes"


class TestNormalizeServiceObjects:
    def test_numeric_eq_port(self):
        svc = AsaServiceObject(name="https", protocol="tcp", dst_port_eq="443")
        result = normalize_service_objects([svc])
        assert "https" in result
        assert result["https"].svc_port == 443
        assert result["https"].svc_port_end == 443

    def test_named_eq_port_resolves_via_name_to_port(self):
        svc = AsaServiceObject(name="http-svc", protocol="tcp", dst_port_eq="http")
        result = normalize_service_objects([svc])
        assert result["http-svc"].svc_port == 80

    def test_numeric_port_range(self):
        svc = AsaServiceObject(name="hi-ports", protocol="tcp", dst_port_range=("1024", "65535"))
        result = normalize_service_objects([svc])
        assert result["hi-ports"].svc_port == 1024
        assert result["hi-ports"].svc_port_end == 65535

    def test_named_port_range_resolves_names(self):
        svc = AsaServiceObject(name="ftp-range", protocol="tcp", dst_port_range=("ftp-data", "ftp"))
        result = normalize_service_objects([svc])
        assert result["ftp-range"].svc_port == 20
        assert result["ftp-range"].svc_port_end == 21

    def test_protocol_only_no_ports(self):
        svc = AsaServiceObject(name="gre-svc", protocol="gre")
        result = normalize_service_objects([svc])
        assert "gre-svc" in result
        assert result["gre-svc"].svc_port is None

    def test_multiple_objects_all_added(self):
        svcs = [
            AsaServiceObject(name="a", protocol="tcp", dst_port_eq="80"),
            AsaServiceObject(name="b", protocol="udp", dst_port_eq="53"),
        ]
        result = normalize_service_objects(svcs)
        assert len(result) == 2


class TestCreateProtocolAnyServiceObjects:
    def test_creates_four_protocols(self):
        result = create_protocol_any_service_objects()
        assert set(result.keys()) == {"any-tcp", "any-udp", "any-icmp", "any-ip"}

    def test_tcp_covers_full_port_range(self):
        result = create_protocol_any_service_objects()
        assert result["any-tcp"].svc_port == 0
        assert result["any-tcp"].svc_port_end == 65535

    def test_each_has_correct_ip_proto(self):
        result = create_protocol_any_service_objects()
        assert result["any-tcp"].ip_proto == 6
        assert result["any-udp"].ip_proto == 17
        assert result["any-icmp"].ip_proto == 1


class TestCreateServiceForPort:
    def test_numeric_port_creates_entry(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port("443", "tcp", svcs)
        assert name == "443-tcp"
        assert svcs["443-tcp"].svc_port == 443

    def test_named_port_resolves_to_number(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port("https", "tcp", svcs)
        assert name == "https-tcp"
        assert svcs["https-tcp"].svc_port == 443

    def test_existing_entry_not_overwritten(self):
        existing = ServiceObject(svc_uid="443-tcp", svc_name="443-tcp", svc_typ="simple", ip_proto=6, svc_color="black")
        svcs: dict[str, ServiceObject] = {"443-tcp": existing}
        create_service_for_port("443", "tcp", svcs)
        assert svcs["443-tcp"] is existing

    def test_icmp_creates_icmp_object(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port("8", "icmp", svcs)
        assert name == "icmp-8"
        assert svcs["icmp-8"].ip_proto == 1


class TestCreateServiceForPortRange:
    def test_same_start_end_uses_single_port_name(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port_range(("80", "80"), "tcp", svcs)
        assert name == "80-tcp"

    def test_different_start_end_uses_range_name(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port_range(("1024", "65535"), "tcp", svcs)
        assert name == "1024-65535-tcp"
        assert svcs[name].svc_port == 1024
        assert svcs[name].svc_port_end == 65535

    def test_named_start_resolves(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port_range(("ftp-data", "21"), "tcp", svcs)
        assert svcs[name].svc_port == 20

    def test_named_end_resolves(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port_range(("20", "ftp"), "tcp", svcs)
        assert svcs[name].svc_port_end == 21

    def test_both_named_description_combines(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_service_for_port_range(("ftp-data", "ftp"), "tcp", svcs)
        svc_comment = svcs[name].svc_comment
        assert svc_comment is not None
        assert ";" in svc_comment

    def test_existing_range_not_overwritten(self):
        existing = ServiceObject(svc_uid="80-tcp", svc_name="80-tcp", svc_typ="simple", ip_proto=6, svc_color="black")
        svcs = {"80-tcp": existing}
        create_service_for_port_range(("80", "80"), "tcp", svcs)
        assert svcs["80-tcp"] is existing


class TestCreateAnyProtocolService:
    def test_tcp_gets_full_port_range(self):
        svcs: dict[str, ServiceObject] = {}
        name = create_any_protocol_service("tcp", svcs)
        assert name == "any-tcp"
        assert svcs["any-tcp"].svc_port == 0
        assert svcs["any-tcp"].svc_port_end == 65535

    def test_icmp_has_no_port_range(self):
        svcs: dict[str, ServiceObject] = {}
        create_any_protocol_service("icmp", svcs)
        assert svcs["any-icmp"].svc_port is None
        assert svcs["any-icmp"].svc_port_end is None

    def test_existing_entry_not_overwritten(self):
        existing = ServiceObject(svc_uid="any-tcp", svc_name="any-tcp", svc_typ="simple", ip_proto=6, svc_color="black")
        svcs = {"any-tcp": existing}
        create_any_protocol_service("tcp", svcs)
        assert svcs["any-tcp"] is existing


class TestCreateServiceForProtocolEntryWithSingleProtocol:
    def test_eq_creates_single_port_service(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="tcp", dst_kind="eq", dst_value="443")
        name = create_service_for_protocol_entry_with_single_protocol(entry, svcs)
        assert name == "443-tcp"

    def test_range_creates_range_service(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(dst_kind="range", dst_value="1024 65535")
        name = create_service_for_protocol_entry_with_single_protocol(entry, svcs)
        assert "1024" in name
        assert "65535" in name

    def test_any_creates_any_protocol_service(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(dst_kind="any", dst_value="any")
        name = create_service_for_protocol_entry_with_single_protocol(entry, svcs)
        assert name == "any-tcp"

    def test_service_kind_returns_value_directly(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(dst_kind="service", dst_value="my-svc-obj")
        name = create_service_for_protocol_entry_with_single_protocol(entry, svcs)
        assert name == "my-svc-obj"

    def test_service_group_kind_returns_value_directly(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(dst_kind="service-group", dst_value="my-grp")
        name = create_service_for_protocol_entry_with_single_protocol(entry, svcs)
        assert name == "my-grp"

    def test_unknown_dst_kind_falls_back_to_any_protocol(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(dst_kind="host", dst_value="irrelevant")
        name = create_service_for_protocol_entry_with_single_protocol(entry, svcs)
        assert name == "any-tcp"


class TestCreateServiceForProtocolEntry:
    def test_tcp_delegates_to_single_protocol(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="tcp", dst_kind="eq", dst_value="80")
        name = create_service_for_protocol_entry(entry, svcs)
        assert name == "80-tcp"

    def test_udp_delegates_to_single_protocol(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="udp", dst_kind="any", dst_value="any")
        name = create_service_for_protocol_entry(entry, svcs)
        assert name == "any-udp"

    def test_icmp_delegates_to_single_protocol(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="icmp", dst_kind="any", dst_value="any")
        name = create_service_for_protocol_entry(entry, svcs)
        assert name == "any-icmp"

    def test_ip_creates_any_group_for_all_protocols(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="ip", dst_kind="any", dst_value="any")
        name = create_service_for_protocol_entry(entry, svcs)
        assert name == "ANY"
        assert svcs["ANY"].svc_typ == "group"

    def test_unknown_protocol_falls_back_to_any(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="ospf", dst_kind="any", dst_value="any")
        name = create_service_for_protocol_entry(entry, svcs)
        assert name == "any-ospf"


class TestCreateServiceForAclEntry:
    def test_protocol_kind_delegates(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol", proto_value="tcp", dst_kind="eq", dst_value="443")
        name = create_service_for_acl_entry(entry, svcs)
        assert name == "443-tcp"

    def test_service_group_kind_returns_value(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="service-group", proto_value="my-svc-grp")
        name = create_service_for_acl_entry(entry, svcs)
        assert name == "my-svc-grp"

    def test_service_kind_returns_value(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="service", proto_value="my-svc")
        name = create_service_for_acl_entry(entry, svcs)
        assert name == "my-svc"

    def test_protocol_group_kind_returns_value(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="protocol-group", proto_value="proto-grp")
        name = create_service_for_acl_entry(entry, svcs)
        assert name == "proto-grp"

    def test_unknown_kind_creates_tcp_udp_icmp_any(self):
        svcs: dict[str, ServiceObject] = {}
        entry = _acl_entry(proto_kind="any", proto_value="any")
        name = create_service_for_acl_entry(entry, svcs)
        assert "any-tcp" in name
        assert "any-udp" in name
        assert "any-icmp" in name


class TestProcessMixedProtocolEqPorts:
    def test_single_protocol_port(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(ports_eq={"tcp": ["80"]})
        result = process_mixed_protocol_eq_ports(group, svcs)
        assert "80-tcp" in result

    def test_tcp_udp_split_creates_both(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(ports_eq={"tcp-udp": ["53"]})
        result = process_mixed_protocol_eq_ports(group, svcs)
        assert "53-tcp" in result
        assert "53-udp" in result

    def test_multiple_ports(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(ports_eq={"tcp": ["80", "443"]})
        result = process_mixed_protocol_eq_ports(group, svcs)
        assert "80-tcp" in result
        assert "443-tcp" in result


class TestProcessMixedProtocolRangePorts:
    def test_creates_range_entry(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(ports_range={"tcp": [("1024", "65535")]})
        result = process_mixed_protocol_range_ports(group, svcs)
        assert any("1024" in n for n in result)

    def test_multiple_ranges(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(ports_range={"tcp": [("80", "80"), ("443", "443")]})
        result = process_mixed_protocol_range_ports(group, svcs)
        assert len(result) == 2


class TestProcessFullyEnabledProtocols:
    def test_creates_any_for_each_protocol(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(protocols=["tcp", "udp"])
        result = process_fully_enabled_protocols(group, svcs)
        assert "any-tcp" in result
        assert "any-udp" in result


class TestProcessMixedProtocolGroup:
    def test_combines_eq_range_protocols_and_refs(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(
            ports_eq={"tcp": ["80"]},
            ports_range={"udp": [("53", "53")]},
            protocols=["icmp"],
            nested_refs=["existing-svc"],
        )
        result = process_mixed_protocol_group(group, svcs)
        assert "80-tcp" in result
        assert "53-udp" in result
        assert "any-icmp" in result
        assert "existing-svc" in result


class TestProcessSingleProtocolEqPorts:
    def test_creates_port_entries(self):
        svcs: dict[str, ServiceObject] = {}
        result = process_single_protocol_eq_ports("tcp", ["80", "443"], svcs)
        assert "80-tcp" in result
        assert "443-tcp" in result


class TestProcessSingleProtocolRangePorts:
    def test_creates_range_entries(self):
        svcs: dict[str, ServiceObject] = {}
        result = process_single_protocol_range_ports("tcp", [("1024", "65535")], svcs)
        assert len(result) == 1
        assert "1024" in result[0]


class TestProcessSingleProtocolGroup:
    def test_tcp_group_with_eq_ports(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(proto_mode="tcp", ports_eq={"tcp": ["443"]})
        result = process_single_protocol_group(group, svcs)
        assert "443-tcp" in result

    def test_tcp_udp_split_processes_both_protocols(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(proto_mode="tcp-udp", ports_eq={"tcp-udp": ["53"]})
        result = process_single_protocol_group(group, svcs)
        assert "53-tcp" in result
        assert "53-udp" in result

    def test_nested_refs_included(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(proto_mode="tcp", nested_refs=["base-svc"])
        result = process_single_protocol_group(group, svcs)
        assert "base-svc" in result

    def test_missing_proto_mode_raises(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(proto_mode=None)
        with pytest.raises(ValueError, match="missing proto_mode"):
            process_single_protocol_group(group, svcs)

    def test_unknown_protocol_in_group_raises(self):
        svcs: dict[str, ServiceObject] = {}
        group = AsaServiceObjectGroup(
            name="bad-grp",
            proto_mode="tcp",
            ports_eq={},
            ports_range={},
            nested_refs=[],
            protocols=[],
            description=None,
        )
        object.__setattr__(group, "proto_mode", "unknown-protocol")
        with pytest.raises(ValueError, match="Unknown protocol"):
            process_single_protocol_group(group, svcs)


class TestNormalizeServiceObjectGroups:
    def test_single_proto_group_added_to_result(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(name="web-svcs", proto_mode="tcp", ports_eq={"tcp": ["80", "443"]})
        result = normalize_service_object_groups([group], svcs)
        assert "web-svcs" in result
        assert result["web-svcs"].svc_typ == "group"

    def test_mixed_proto_group_added(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(name="mixed", ports_eq={"tcp": ["80"]})
        result = normalize_service_object_groups([group], svcs)
        assert "mixed" in result

    def test_duplicate_member_refs_deduplicated(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(
            name="dedup-grp",
            proto_mode="tcp",
            ports_eq={"tcp": ["80", "80"]},
        )
        result = normalize_service_object_groups([group], svcs)
        assert result["dedup-grp"].svc_typ == "group"
        members = result["dedup-grp"].svc_member_names.split(",") if result["dedup-grp"].svc_member_names else []
        assert len(members) == len(set(members))

    def test_multiple_groups_all_added(self):
        svcs: dict[str, ServiceObject] = {}
        g1 = _service_group(name="g1", proto_mode="tcp", ports_eq={"tcp": ["80"]})
        g2 = _service_group(name="g2", proto_mode="udp", ports_eq={"udp": ["53"]})
        result = normalize_service_object_groups([g1, g2], svcs)
        assert "g1" in result
        assert "g2" in result

    def test_existing_service_objects_preserved(self):
        existing = ServiceObject(
            svc_uid="pre-existing", svc_name="pre-existing", svc_typ="simple", ip_proto=6, svc_color="black"
        )
        svcs: dict[str, ServiceObject] = {"pre-existing": existing}
        group = _service_group(name="new-grp", proto_mode="tcp", ports_eq={"tcp": ["443"]})
        result = normalize_service_object_groups([group], svcs)
        assert result["pre-existing"] is existing


class TestServicePortNameLookupErrors:
    def test_create_service_for_port_unknown_name_raises(self):
        with pytest.raises(KeyError):
            create_service_for_port("no-such-port", "tcp", {})

    def test_create_service_for_port_range_unknown_start_raises(self):
        with pytest.raises(KeyError):
            create_service_for_port_range(("no-such-port", "80"), "tcp", {})

    def test_create_service_for_port_range_unknown_end_raises(self):
        with pytest.raises(KeyError):
            create_service_for_port_range(("80", "no-such-port"), "tcp", {})

    def test_normalize_service_objects_unknown_eq_port_raises(self):
        svc = AsaServiceObject(name="bad", protocol="tcp", dst_port_eq="no-such-port")
        with pytest.raises(KeyError):
            normalize_service_objects([svc])

    def test_normalize_service_objects_unknown_range_start_raises(self):
        svc = AsaServiceObject(name="bad", protocol="tcp", dst_port_range=("no-such-port", "443"))
        with pytest.raises(KeyError):
            normalize_service_objects([svc])


class TestSingleProtocolGroupTcpUdpKeyMismatch:
    def test_tcp_udp_protomode_with_matching_key(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(proto_mode="tcp-udp", ports_eq={"tcp-udp": ["53"]})
        result = process_single_protocol_group(group, svcs)
        assert "53-tcp" in result
        assert "53-udp" in result

    def test_tcp_protomode_mismatched_key_produces_no_ports(self):
        svcs: dict[str, ServiceObject] = {}
        group = _service_group(proto_mode="tcp", ports_eq={"udp": ["53"]})
        result = process_single_protocol_group(group, svcs)
        assert "53-tcp" not in result


class TestMixedGroupOnlyNestedRefs:
    def test_group_with_only_nested_refs(self):
        svcs: dict[str, ServiceObject] = {}
        grp = AsaServiceObjectGroup(
            name="nested-only",
            proto_mode=None,
            ports_eq={},
            ports_range={},
            nested_refs=["base-svc", "other-svc"],
            protocols=[],
            description=None,
        )
        result = normalize_service_object_groups([grp], svcs)
        assert "nested-only" in result
        svc_member_names = result["nested-only"].svc_member_names
        assert svc_member_names is not None
        assert "base-svc" in svc_member_names
        assert "other-svc" in svc_member_names

"""Tests for fw_modules/ciscoasa9/asa_parser_functions.py"""

from typing import Literal

import pytest
from fw_modules.ciscoasa9.asa_models import (
    AsaProtocolGroup,
    AsaServiceObject,
    AsaServiceObjectGroup,
    EndpointKind,
)
from fw_modules.ciscoasa9.asa_parser_functions import (
    _consume_port_objects,  # pyright: ignore[reportPrivateUsage]
    _consume_service_definitions,  # pyright: ignore[reportPrivateUsage]
    _consume_service_references,  # pyright: ignore[reportPrivateUsage]
    _create_network_object_from_parts,  # pyright: ignore[reportPrivateUsage]
    _parse_access_list_entry_dest_port,  # pyright: ignore[reportPrivateUsage]
    _parse_access_list_entry_protocol,  # pyright: ignore[reportPrivateUsage]
    _parse_policy_class_block,  # pyright: ignore[reportPrivateUsage]
    clean_lines,
    consume_block,
    parse_access_list_entry,
    parse_class_map_block,
    parse_dns_inspect_policy_map_block,
    parse_endpoint,
    parse_icmp_object_group_block,
    parse_interface_block,
    parse_network_object_block,
    parse_network_object_group_block,
    parse_policy_map_block,
    parse_protocol_object_group_block,
    parse_service_object_block,
    parse_service_object_group_block,
)


class TestParseEndpoint:
    def test_empty_tokens_returns_any(self):
        ep, consumed = parse_endpoint([])
        assert ep.kind == "any"
        assert consumed == 0

    def test_any_token(self):
        ep, consumed = parse_endpoint(["any"])
        assert ep.kind == "any"
        assert consumed == 1

    def test_host_with_ip(self):
        ep, consumed = parse_endpoint(["host", "10.0.0.1"])
        assert ep.kind == "host"
        assert ep.value == "10.0.0.1"
        assert consumed == 2

    def test_host_without_ip_falls_back_to_any(self):
        ep, consumed = parse_endpoint(["host"])
        assert ep.kind == "any"
        assert consumed == 1

    def test_object_keyword(self):
        ep, consumed = parse_endpoint(["object", "my-obj"])
        assert ep.kind == "object"
        assert ep.value == "my-obj"
        assert consumed == 2

    def test_object_group_keyword(self):
        ep, consumed = parse_endpoint(["object-group", "my-grp"])
        assert ep.kind == "object-group"
        assert ep.value == "my-grp"
        assert consumed == 2

    def test_subnet_notation(self):
        ep, consumed = parse_endpoint(["10.0.0.0", "255.255.255.0"])
        assert ep.kind == "subnet"
        assert ep.value == "10.0.0.0"
        assert ep.mask == "255.255.255.0"
        assert consumed == 2

    def test_single_ip_without_mask_falls_back_to_any(self):
        ep, consumed = parse_endpoint(["10.0.0.1"])
        assert ep.kind == "any"
        assert consumed == 1

    def test_garbage_token_falls_back_to_any(self):
        ep, consumed = parse_endpoint(["garbage"])
        assert ep.kind == "any"
        assert consumed == 1


class TestConsumeBlock:
    def test_header_only_block(self):
        lines = ["interface GigabitEthernet0/0", "hostname asa"]
        block, next_idx = consume_block(lines, 0)
        assert block == ["interface GigabitEthernet0/0"]
        assert next_idx == 1

    def test_block_with_indented_lines(self):
        lines = ["interface GigabitEthernet0/0", " nameif inside", " ip address 10.0.0.1 255.255.255.0", "hostname asa"]
        block, next_idx = consume_block(lines, 0)
        assert len(block) == 3
        assert next_idx == 3

    def test_block_ends_at_exclamation(self):
        lines = ["interface GigabitEthernet0/0", " nameif inside", "!", "hostname asa"]
        block, next_idx = consume_block(lines, 0)
        assert " nameif inside" in block
        assert next_idx == 3


class TestParseServiceObjectBlock:
    def test_unsupported_protocol_returns_none(self):
        block = ["object service my-esp", " service esp"]
        assert parse_service_object_block(block) is None

    def test_missing_protocol_returns_none(self):
        block = ["object service orphan", " description no protocol line"]
        assert parse_service_object_block(block) is None

    def test_tcp_with_eq_port(self):
        block = ["object service my-https", " service tcp destination eq 443"]
        result = parse_service_object_block(block)
        assert result is not None
        assert result.protocol == "tcp"
        assert result.dst_port_eq == "443"

    def test_icmp_protocol(self):
        block = ["object service my-icmp", " service icmp"]
        result = parse_service_object_block(block)
        assert result is not None
        assert result.protocol == "icmp"

    def test_gre_protocol(self):
        block = ["object service my-gre", " service gre"]
        result = parse_service_object_block(block)
        assert result is not None
        assert result.protocol == "gre"

    def test_tcp_with_port_range(self):
        block = ["object service my-range", " service tcp destination range 1024 65535"]
        result = parse_service_object_block(block)
        assert result is not None
        assert result.dst_port_range == ("1024", "65535")

    def test_description_captured(self):
        block = ["object service my-svc", " description web traffic", " service tcp destination eq 80"]
        result = parse_service_object_block(block)
        assert result is not None
        assert result.description == "web traffic"


class TestParseServiceObjectGroupBlock:
    def test_unsupported_proto_mode_treated_as_mixed(self):
        block = ["object-group service my-grp ip", " service-object tcp destination eq 80"]
        result = parse_service_object_group_block(block)
        assert result.proto_mode is None
        assert "tcp" in result.ports_eq

    def test_tcp_proto_mode_consumes_port_objects(self):
        block = ["object-group service my-grp tcp", " port-object eq 80", " port-object eq 443"]
        result = parse_service_object_group_block(block)
        assert result.proto_mode == "tcp"
        assert "80" in result.ports_eq.get("tcp", [])
        assert "443" in result.ports_eq.get("tcp", [])

    def test_tcp_udp_proto_mode(self):
        block = ["object-group service my-grp tcp-udp", " port-object eq 53"]
        result = parse_service_object_group_block(block)
        assert result.proto_mode == "tcp-udp"
        assert "53" in result.ports_eq.get("tcp-udp", [])

    def test_mixed_group_with_nested_ref(self):
        block = ["object-group service my-grp", " group-object other-grp"]
        result = parse_service_object_group_block(block)
        assert result.proto_mode is None
        assert "other-grp" in result.nested_refs

    def test_port_range_in_tcp_group(self):
        block = ["object-group service my-grp tcp", " port-object range 1024 65535"]
        result = parse_service_object_group_block(block)
        assert ("1024", "65535") in result.ports_range.get("tcp", [])


class TestParseAccessListEntryProtocol:
    def test_unknown_object_group_raises(self):
        parts = ["access-list", "acl", "extended", "permit", "object-group", "missing-grp"]
        with pytest.raises(ValueError, match="Unknown object-group"):
            _parse_access_list_entry_protocol(parts, [], [], [])

    def test_known_protocol_group_recognized(self):
        pg = AsaProtocolGroup(name="pg1", protocols=["tcp"], description=None)
        parts = ["access-list", "acl", "extended", "permit", "object-group", "pg1", "any", "any"]
        proto, _ = _parse_access_list_entry_protocol(parts, [pg], [], [])
        assert proto.kind == "protocol-group"
        assert proto.value == "pg1"

    def test_known_service_group_recognized(self):
        svc_grp = AsaServiceObjectGroup(
            name="sg1", proto_mode="tcp", ports_eq={}, ports_range={}, nested_refs=[], protocols=[], description=None
        )
        parts = ["access-list", "acl", "extended", "permit", "object-group", "sg1", "any", "any"]
        proto, _ = _parse_access_list_entry_protocol(parts, [], [], [svc_grp])
        assert proto.kind == "service-group"

    def test_unknown_service_object_raises(self):
        parts = ["access-list", "acl", "extended", "permit", "object", "missing-svc"]
        with pytest.raises(ValueError, match="Unknown service object"):
            _parse_access_list_entry_protocol(parts, [], [], [])

    def test_known_service_object_recognized(self):
        svc = AsaServiceObject(name="my-svc", protocol="tcp", dst_port_eq="443")
        parts = ["access-list", "acl", "extended", "permit", "object", "my-svc", "any", "any"]
        proto, _ = _parse_access_list_entry_protocol(parts, [], [svc], [])
        assert proto.kind == "service"
        assert proto.value == "my-svc"

    def test_plain_protocol_token(self):
        parts = ["access-list", "acl", "extended", "permit", "tcp", "any", "any"]
        proto, _ = _parse_access_list_entry_protocol(parts, [], [], [])
        assert proto.kind == "protocol"
        assert proto.value == "tcp"


class TestParseAccessListEntryDestPort:
    def _proto(
        self,
        kind: Literal[
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
        value: str = "tcp",
    ) -> EndpointKind:
        return EndpointKind(kind=kind, value=value)

    def test_eq_port(self):
        dst_port, _ = _parse_access_list_entry_dest_port(["eq", "443", "inactive"], self._proto())
        assert dst_port.kind == "eq"
        assert dst_port.value == "443"

    def test_range_port(self):
        dst_port, _ = _parse_access_list_entry_dest_port(["range", "1024", "65535"], self._proto())
        assert dst_port.kind == "range"
        assert "1024" in dst_port.value
        assert "65535" in dst_port.value

    def test_object_group_port(self):
        dst_port, _ = _parse_access_list_entry_dest_port(["object-group", "svc-grp"], self._proto())
        assert dst_port.kind == "service-group"
        assert dst_port.value == "svc-grp"

    def test_no_port_defaults_to_any(self):
        dst_port, _ = _parse_access_list_entry_dest_port([], self._proto())
        assert dst_port.kind == "any"

    def test_service_group_protocol_propagates_to_dst_port(self):
        proto = self._proto(kind="service-group", value="my-sg")
        dst_port, _ = _parse_access_list_entry_dest_port([], proto)
        assert dst_port.kind == "service-group"
        assert dst_port.value == "my-sg"

    def test_service_protocol_propagates_to_dst_port(self):
        proto = self._proto(kind="service", value="my-svc")
        dst_port, _ = _parse_access_list_entry_dest_port([], proto)
        assert dst_port.kind == "service"
        assert dst_port.value == "my-svc"


class TestCreateNetworkObjectFromParts:
    def test_all_none_returns_none(self):
        assert _create_network_object_from_parts("nat-only", None, None, None, None, None, None) is None

    def test_host_creates_host_object(self):
        result = _create_network_object_from_parts("h1", "10.0.0.1", None, None, None, None, "desc")
        assert result is not None
        assert result.ip_address == "10.0.0.1"
        assert result.description == "desc"

    def test_subnet_creates_subnet_object(self):
        result = _create_network_object_from_parts("net1", None, "10.0.0.0", "255.255.0.0", None, None, None)
        assert result is not None
        assert result.subnet_mask == "255.255.0.0"

    def test_ip_range_creates_range_object(self):
        result = _create_network_object_from_parts("rng1", None, None, None, ("10.0.0.1", "10.0.0.10"), None, None)
        assert result is not None
        assert result.ip_address == "10.0.0.1"
        assert result.ip_address_end == "10.0.0.10"

    def test_fqdn_creates_fqdn_object(self):
        result = _create_network_object_from_parts("fqdn1", None, None, None, None, "example.com", None)
        assert result is not None
        assert result.fqdn == "example.com"

    def test_subnet_wins_when_both_host_and_subnet_set(self):
        result = _create_network_object_from_parts("net1", "10.0.0.1", "10.0.0.0", "255.255.255.0", None, None, None)
        assert result is not None
        assert result.ip_address == "10.0.0.0"
        assert result.subnet_mask == "255.255.255.0"


class TestCleanLines:
    def test_colon_lines_removed(self):
        text = ": Saved\n: end\nhostname fw\n"
        lines = clean_lines(text)
        assert "hostname fw" in lines
        assert not any(line.startswith(":") for line in lines)

    def test_regular_lines_preserved(self):
        text = "ASA Version 9.1\nhostname asa\n"
        lines = clean_lines(text)
        assert "ASA Version 9.1" in lines
        assert "hostname asa" in lines

    def test_trailing_whitespace_stripped(self):
        text = "hostname asa   \n"
        lines = clean_lines(text)
        assert lines[0] == "hostname asa"

    def test_empty_string_returns_empty_list(self):
        assert clean_lines("") == []

    def test_only_colon_lines_returns_empty(self):
        assert clean_lines(": a\n: b\n") == []


class TestParseInterfaceBlock:
    def test_basic_interface_with_ip(self):
        block = [
            "interface GigabitEthernet0/0",
            " nameif inside",
            " security-level 100",
            " ip address 10.0.0.1 255.255.255.0",
        ]
        iface = parse_interface_block(block)
        assert iface.name == "GigabitEthernet0/0"
        assert iface.nameif == "inside"
        assert iface.security_level == 100
        assert iface.ip_address == "10.0.0.1"
        assert iface.subnet_mask == "255.255.255.0"

    def test_interface_without_ip(self):
        block = ["interface GigabitEthernet0/1", " nameif mgmt", " security-level 50"]
        iface = parse_interface_block(block)
        assert iface.ip_address is None
        assert iface.subnet_mask is None

    def test_interface_without_nameif_defaults_to_ifname(self):
        block = ["interface GigabitEthernet0/2", " security-level 0"]
        iface = parse_interface_block(block)
        assert iface.nameif == "GigabitEthernet0/2"

    def test_interface_missing_security_level_defaults_to_zero(self):
        block = ["interface GigabitEthernet0/3", " nameif outside"]
        iface = parse_interface_block(block)
        assert iface.security_level == 0

    def test_interface_with_bridge_group(self):
        block = ["interface BVI1", " nameif inside", " bridge-group 1", " security-level 100"]
        iface = parse_interface_block(block)
        assert iface.bridge_group == "1"

    def test_interface_with_description(self):
        block = ["interface GigabitEthernet0/0", " nameif inside", " security-level 100", " description LAN"]
        iface = parse_interface_block(block)
        assert iface.description == "LAN"

    def test_ip_address_line_without_valid_mask_ignored(self):
        block = ["interface GigabitEthernet0/0", " nameif outside", " security-level 0", " ip address dhcp"]
        iface = parse_interface_block(block)
        assert iface.ip_address is None


class TestParseNetworkObjectBlock:
    def test_host_object(self):
        block = ["object network h1", " host 10.0.0.1"]
        obj, nat = parse_network_object_block(block)
        assert obj is not None
        assert obj.name == "h1"
        assert obj.ip_address == "10.0.0.1"
        assert nat is None

    def test_subnet_object(self):
        block = ["object network net1", " subnet 10.0.0.0 255.255.255.0"]
        obj, _ = parse_network_object_block(block)
        assert obj is not None
        assert obj.ip_address == "10.0.0.0"
        assert obj.subnet_mask == "255.255.255.0"

    def test_range_object(self):
        block = ["object network rng1", " range 10.0.0.1 10.0.0.10"]
        obj, _ = parse_network_object_block(block)
        assert obj is not None
        assert obj.ip_address == "10.0.0.1"
        assert obj.ip_address_end == "10.0.0.10"

    def test_fqdn_object(self):
        block = ["object network fqdn1", " fqdn v4 example.com"]
        obj, _ = parse_network_object_block(block)
        assert obj is not None
        assert obj.fqdn == "example.com"

    def test_nat_dynamic_extracted(self):
        block = ["object network nat-obj", " nat (inside,outside) dynamic interface"]
        _, nat = parse_network_object_block(block)
        assert nat is not None
        assert nat.nat_type == "dynamic"
        assert nat.translated_object is None

    def test_nat_static_with_translated_ip(self):
        block = ["object network nat-obj", " host 10.0.0.1", " nat (inside,outside) static 203.0.113.10"]
        _, nat = parse_network_object_block(block)
        assert nat is not None
        assert nat.nat_type == "static"
        assert nat.translated_object == "203.0.113.10"

    def test_description_captured(self):
        block = ["object network h1", " description my host", " host 10.0.0.1"]
        obj, _ = parse_network_object_block(block)
        assert obj is not None
        assert obj.description == "my host"

    def test_no_address_returns_none_object(self):
        block = ["object network nat-only", " nat (inside,outside) static interface"]
        obj, nat = parse_network_object_block(block)
        assert obj is None
        assert nat is not None


class TestParseNetworkObjectGroupBlock:
    def test_object_member(self):
        block = ["object-group network grp", " network-object object h1"]
        grp = parse_network_object_group_block(block)
        assert grp.objects[0].kind == "object"
        assert grp.objects[0].value == "h1"

    def test_host_ipv4_member(self):
        block = ["object-group network grp", " network-object host 10.0.0.1"]
        grp = parse_network_object_group_block(block)
        assert grp.objects[0].kind == "host"

    def test_hostv6_member(self):
        block = ["object-group network grp", " network-object host 2001:db8::1"]
        grp = parse_network_object_group_block(block)
        assert grp.objects[0].kind == "hostv6"

    def test_subnet_ipv4_member(self):
        block = ["object-group network grp", " network-object 10.0.0.0 255.255.255.0"]
        grp = parse_network_object_group_block(block)
        assert grp.objects[0].kind == "subnet"
        assert grp.objects[0].mask == "255.255.255.0"

    def test_subnetv6_member(self):
        block = ["object-group network grp", " network-object 2001:db8::/32"]
        grp = parse_network_object_group_block(block)
        assert grp.objects[0].kind == "subnetv6"

    def test_group_object_member(self):
        block = ["object-group network grp", " group-object other-grp"]
        grp = parse_network_object_group_block(block)
        assert grp.objects[0].kind == "object-group"
        assert grp.objects[0].value == "other-grp"

    def test_description_captured(self):
        block = ["object-group network grp", " description my group", " network-object host 10.0.0.1"]
        grp = parse_network_object_group_block(block)
        assert grp.description == "my group"

    def test_empty_group(self):
        block = ["object-group network grp"]
        grp = parse_network_object_group_block(block)
        assert grp.objects == []


class TestConsumePortObjects:
    def test_port_range_consumed(self):
        block = [" port-object range 1024 65535", " port-object eq 80"]
        eq_list, range_list = _consume_port_objects(block, "tcp")
        assert ("tcp", ("1024", "65535")) in range_list
        assert ("tcp", "80") in eq_list


class TestConsumeServiceDefinitions:
    def test_bare_protocol_captured(self):
        block = [" service-object icmp", " service-object tcp"]
        _, _, protocols = _consume_service_definitions(block)
        assert "icmp" in protocols
        assert "tcp" in protocols

    def test_service_range_captured(self):
        block = [" service-object tcp destination range 1024 65535"]
        _, ranges, _ = _consume_service_definitions(block)
        assert ("tcp", ("1024", "65535")) in ranges

    def test_service_eq_captured(self):
        block = [" service-object udp destination eq 53"]
        eq_list, _, _ = _consume_service_definitions(block)
        assert ("udp", "53") in eq_list


class TestConsumeServiceReferences:
    def test_service_object_reference_captured(self):
        block = [" service-object object my-svc"]
        refs = _consume_service_references(block)
        assert "my-svc" in refs

    def test_group_object_reference_captured(self):
        block = [" group-object other-grp"]
        refs = _consume_service_references(block)
        assert "other-grp" in refs


class TestParseClassMapBlock:
    def test_basic_class_map(self):
        block = ["class-map inspection_default", " match default-inspection-traffic"]
        cm = parse_class_map_block(block)
        assert cm.name == "inspection_default"
        assert "default-inspection-traffic" in cm.matches

    def test_multiple_match_lines(self):
        block = ["class-map my-class", " match access-list acl1", " match port tcp eq 80"]
        cm = parse_class_map_block(block)
        assert len(cm.matches) == 2

    def test_empty_class_map(self):
        block = ["class-map empty"]
        cm = parse_class_map_block(block)
        assert cm.matches == []


class TestParseDnsInspectPolicyMapBlock:
    def test_parameters_message_length_client_auto(self):
        block = ["policy-map type inspect dns pm-dns", " parameters", "  message-length maximum client auto"]
        pm = parse_dns_inspect_policy_map_block(block, "pm-dns")
        assert pm.name == "pm-dns"
        assert pm.parameters_dns is not None
        assert pm.parameters_dns.message_length_max_client == "auto"

    def test_parameters_message_length_client_numeric(self):
        block = ["policy-map type inspect dns pm-dns", " parameters", "  message-length maximum client 512"]
        pm = parse_dns_inspect_policy_map_block(block, "pm-dns")
        assert pm.parameters_dns is not None
        assert pm.parameters_dns.message_length_max_client == 512

    def test_parameters_message_length_max(self):
        block = ["policy-map type inspect dns pm-dns", " parameters", "  message-length maximum 1024"]
        pm = parse_dns_inspect_policy_map_block(block, "pm-dns")
        assert pm.parameters_dns is not None
        assert pm.parameters_dns.message_length_max == 1024

    def test_parameters_no_tcp_inspection(self):
        block = ["policy-map type inspect dns pm-dns", " parameters", "  no tcp-inspection"]
        pm = parse_dns_inspect_policy_map_block(block, "pm-dns")
        assert pm.parameters_dns is not None
        assert pm.parameters_dns.tcp_inspection is False

    def test_no_parameters_block(self):
        block = ["policy-map type inspect dns pm-dns"]
        pm = parse_dns_inspect_policy_map_block(block, "pm-dns")
        assert pm.name == "pm-dns"


class TestParsePolicyMapBlock:
    def test_basic_policy_map_with_class(self):
        block = [
            "policy-map global_policy",
            " class inspection_default",
            "  inspect ftp",
            "  inspect dns pm-dns",
        ]
        pm = parse_policy_map_block(block, "global_policy")
        assert pm.name == "global_policy"
        assert len(pm.classes) == 1
        assert pm.classes[0].class_name == "inspection_default"
        inspected = [i.protocol for i in pm.classes[0].inspections]
        assert "ftp" in inspected
        assert "dns" in inspected

    def test_inspect_with_policy_map_ref(self):
        block = ["policy-map global_policy", " class cls1", "  inspect dns pm-dns"]
        pm = parse_policy_map_block(block, "global_policy")
        dns_action = pm.classes[0].inspections[0]
        assert dns_action.protocol == "dns"
        assert dns_action.policy_map == "pm-dns"

    def test_inspect_without_policy_map_ref(self):
        block = ["policy-map global_policy", " class cls1", "  inspect ftp"]
        pm = parse_policy_map_block(block, "global_policy")
        assert pm.classes[0].inspections[0].policy_map is None

    def test_non_class_line_ignored(self):
        block = ["policy-map global_policy", " description some policy"]
        pm = parse_policy_map_block(block, "global_policy")
        assert pm.classes == []

    def test_empty_policy_map(self):
        block = ["policy-map global_policy"]
        pm = parse_policy_map_block(block, "global_policy")
        assert pm.classes == []


class TestParseAccessListEntryDestPortObject:
    def _proto(
        self,
        kind: Literal[
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
        value: str = "tcp",
    ):
        return EndpointKind(kind=kind, value=value)

    def test_object_dst_port(self):
        dst_port, _ = _parse_access_list_entry_dest_port(["object", "my-svc", "inactive"], self._proto())
        assert dst_port.kind == "service"
        assert dst_port.value == "my-svc"


class TestParseAccessListEntry:
    def test_basic_ip_permit(self):
        line = "access-list acl1 extended permit ip host 10.0.0.1 host 10.0.0.2"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.acl_name == "acl1"
        assert entry.action == "permit"
        assert entry.protocol.value == "ip"

    def test_tcp_deny_with_eq_port(self):
        line = "access-list acl1 extended deny tcp any host 10.0.0.1 eq 443"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.action == "deny"
        assert entry.dst_port.kind == "eq"
        assert entry.dst_port.value == "443"

    def test_inactive_flag_parsed(self):
        line = "access-list acl1 extended permit tcp any any inactive"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.inactive is True

    def test_active_entry_not_inactive(self):
        line = "access-list acl1 extended permit tcp any any"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.inactive is False

    def test_subnet_source(self):
        line = "access-list acl1 extended permit tcp 10.0.0.0 255.255.255.0 any"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.src.kind == "subnet"
        assert entry.src.mask == "255.255.255.0"

    def test_range_dst_port(self):
        line = "access-list acl1 extended permit tcp any any range 1024 65535"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.dst_port.kind == "range"

    def test_object_group_protocol(self):
        pg = AsaProtocolGroup(name="pg1", protocols=["tcp"], description=None)
        line = "access-list acl1 extended permit object-group pg1 any any"
        entry = parse_access_list_entry(line, [pg], [], [])
        assert entry.protocol.kind == "protocol-group"
        assert entry.protocol.value == "pg1"

    def test_service_object_protocol(self):
        svc = AsaServiceObject(name="my-svc", protocol="tcp", dst_port_eq="443")
        line = "access-list acl1 extended permit object my-svc any any"
        entry = parse_access_list_entry(line, [], [svc], [])
        assert entry.protocol.kind == "service"

    def test_service_group_protocol(self):
        svc_grp = AsaServiceObjectGroup(
            name="sg1", proto_mode="tcp", ports_eq={}, ports_range={}, nested_refs=[], protocols=[], description=None
        )
        line = "access-list acl1 extended permit object-group sg1 any any"
        entry = parse_access_list_entry(line, [], [], [svc_grp])
        assert entry.protocol.kind == "service-group"

    def test_icmp_entry(self):
        line = "access-list acl1 extended permit icmp any any"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.protocol.value == "icmp"

    def test_object_group_dst_port(self):
        line = "access-list acl1 extended permit tcp any any object-group svc-grp"
        entry = parse_access_list_entry(line, [], [], [])
        assert entry.dst_port.kind == "service-group"
        assert entry.dst_port.value == "svc-grp"


class TestParseProtocolObjectGroupBlock:
    def test_single_protocol(self):
        block = ["object-group protocol pg1", " protocol-object tcp"]
        pg = parse_protocol_object_group_block(block)
        assert pg.name == "pg1"
        assert "tcp" in pg.protocols

    def test_multiple_protocols(self):
        block = ["object-group protocol pg1", " protocol-object tcp", " protocol-object udp"]
        pg = parse_protocol_object_group_block(block)
        assert len(pg.protocols) == 2

    def test_with_description(self):
        block = ["object-group protocol pg1", " description TCP and UDP", " protocol-object tcp"]
        pg = parse_protocol_object_group_block(block)
        assert pg.description == "TCP and UDP"

    def test_empty_group(self):
        block = ["object-group protocol pg1"]
        pg = parse_protocol_object_group_block(block)
        assert pg.protocols == []


class TestParseIcmpObjectGroupBlock:
    def test_icmp_types_parsed(self):
        block = ["object-group icmp-type icmp-grp", " icmp-object echo", " icmp-object echo-reply"]
        grp = parse_icmp_object_group_block(block)
        assert grp.name == "icmp-grp"
        assert "echo" in grp.ports_eq["icmp"]
        assert "echo-reply" in grp.ports_eq["icmp"]

    def test_with_description(self):
        block = ["object-group icmp-type icmp-grp", " description ICMP types", " icmp-object echo"]
        grp = parse_icmp_object_group_block(block)
        assert grp.description == "ICMP types"

    def test_empty_group(self):
        block = ["object-group icmp-type icmp-grp"]
        grp = parse_icmp_object_group_block(block)
        assert grp.ports_eq == {"icmp": []}


class TestConsumePortObjectsElseBranch:
    def test_non_matching_line_skipped(self):
        block = [" description blah", " port-object eq 80"]
        eq_list, range_list = _consume_port_objects(block, "tcp")
        assert ("tcp", "80") in eq_list
        assert range_list == []


class TestParseDnsInspectPolicyMapBlockNonParamLine:
    def test_non_parameters_line_skipped(self):
        block = [
            "policy-map type inspect dns pm-dns",
            " description ignored",
            " parameters",
            "  message-length maximum 512",
        ]
        pm = parse_dns_inspect_policy_map_block(block, "pm-dns")
        assert pm.parameters_dns is not None
        assert pm.parameters_dns.message_length_max == 512


class TestParsePolicyClassBlockOutOfBounds:
    def test_start_idx_beyond_block_returns_none(self):
        block = ["policy-map global"]
        cls, next_idx = _parse_policy_class_block(block, start_idx=5)
        assert cls is None
        assert next_idx == 6

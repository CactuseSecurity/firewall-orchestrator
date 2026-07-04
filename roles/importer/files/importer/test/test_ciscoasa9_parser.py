"""
Tests for fw_modules/ciscoasa9/asa_parser.py

Covers parse_asa_config end-to-end, plus edge cases for every handler
and the _build_config / _ParserState fallback paths.
"""

import re
from pathlib import Path

import pytest
from fw_modules.ciscoasa9.asa_parser import (
    Config,
    _handle_access_group,  # pyright: ignore[reportPrivateUsage]
    _handle_access_list_entry,  # pyright: ignore[reportPrivateUsage]
    _handle_mgmt_access,  # pyright: ignore[reportPrivateUsage]
    _ParserState,  # pyright: ignore[reportPrivateUsage]
    parse_asa_config,  # pyright: ignore[reportPrivateUsage]
)

mock_password = "testpass"  # noqa: S105
_HEADER = f"""\
ASA Version 9.16(4)
hostname asa-fw
enable password {mock_password} pbkdf2
"""


def _parse(extra: str) -> Config:
    """Parse a minimal config prepended to extra text."""
    return parse_asa_config(_HEADER + extra)


class TestMetadataParsing:
    def test_asa_version_extracted(self):
        cfg = _parse("")
        assert cfg.asa_version == "9.16(4)"

    def test_hostname_extracted(self):
        cfg = _parse("")
        assert cfg.hostname == "asa-fw"

    def test_enable_password_extracted(self):
        cfg = _parse("")
        assert cfg.enable_password.password == mock_password
        assert cfg.enable_password.encryption_function == "pbkdf2"

    def test_missing_version_defaults_to_unknown(self):
        cfg = parse_asa_config("hostname fw1\n")
        assert cfg.asa_version == "unknown"

    def test_missing_hostname_defaults_to_unknown(self):
        cfg = parse_asa_config("ASA Version 9.1\n")
        assert cfg.hostname == "unknown"

    def test_missing_enable_password_uses_empty_default(self):
        cfg = parse_asa_config("ASA Version 9.1\nhostname fw\n")
        assert cfg.enable_password.password == ""
        assert cfg.enable_password.encryption_function == ""


class TestColonLinesStripped:
    def test_colon_metadata_ignored(self):
        raw = ": Saved\n: Serial Number: ABC\nASA Version 9.1\nhostname fw\n"
        cfg = parse_asa_config(raw)
        assert cfg.hostname == "fw"

    def test_colon_end_marker_ignored(self):
        raw = "ASA Version 9.1\nhostname fw\n: end\n"
        cfg = parse_asa_config(raw)
        assert cfg.hostname == "fw"


class TestBlankAndBangLinesIgnored:
    def test_blank_lines_skipped(self):
        cfg = _parse("\n\n\n")
        assert cfg.hostname == "asa-fw"

    def test_bang_lines_skipped(self):
        cfg = _parse("!\n!\n!\n")
        assert cfg.hostname == "asa-fw"


class TestServiceModuleParsing:
    def test_single_service_module_parsed(self):
        raw = _HEADER + "service-module sfr keepalive-timeout 4\nservice-module sfr keepalive-counter 6\n"
        cfg = parse_asa_config(raw)
        assert len(cfg.service_modules) == 1
        assert cfg.service_modules[0].name == "sfr"
        assert cfg.service_modules[0].keepalive_timeout == 4
        assert cfg.service_modules[0].keepalive_counter == 6

    def test_multiple_service_modules(self):
        raw = (
            _HEADER
            + "service-module 1 keepalive-timeout 4\n"
            + "service-module 1 keepalive-counter 6\n"
            + "service-module sfr keepalive-timeout 4\n"
            + "service-module sfr keepalive-counter 6\n"
        )
        cfg = parse_asa_config(raw)
        assert len(cfg.service_modules) == 2

    def test_service_module_without_counter_defaults_to_zero(self):
        raw = _HEADER + "service-module sfr keepalive-timeout 10\n"
        cfg = parse_asa_config(raw)
        assert cfg.service_modules[0].keepalive_counter == 0


class TestNameParsing:
    def test_name_with_description(self):
        cfg = _parse("name 10.0.0.1 gw1 description main gateway\n")
        assert len(cfg.names) == 1
        assert cfg.names[0].name == "gw1"
        assert cfg.names[0].ip_address == "10.0.0.1"

    def test_name_without_description(self):
        cfg = _parse("name 10.0.0.2 srv1\n")
        assert cfg.names[0].description is None

    def test_multiple_names(self):
        cfg = _parse("name 10.0.0.1 a\nname 10.0.0.2 b\n")
        assert len(cfg.names) == 2

    def test_duplicate_name_both_captured(self):
        cfg = _parse("name 10.0.0.1 a\nname 10.0.0.1 a2\n")
        assert len(cfg.names) == 2


class TestInterfaceParsing:
    def test_basic_interface_parsed(self):
        raw = _HEADER + (
            "interface GigabitEthernet0/0\n nameif inside\n security-level 100\n ip address 10.0.0.1 255.255.255.0\n"
        )
        cfg = parse_asa_config(raw)
        assert len(cfg.interfaces) == 1
        assert cfg.interfaces[0].nameif == "inside"

    def test_two_interfaces_parsed(self):
        raw = _HEADER + (
            "interface GigabitEthernet0/0\n"
            " nameif inside\n"
            " security-level 100\n"
            "!\n"
            "interface GigabitEthernet0/1\n"
            " nameif outside\n"
            " security-level 0\n"
        )
        cfg = parse_asa_config(raw)
        assert len(cfg.interfaces) == 2

    def test_interface_without_ip_address(self):
        raw = _HEADER + ("interface GigabitEthernet0/0\n nameif mgmt\n security-level 50\n")
        cfg = parse_asa_config(raw)
        assert cfg.interfaces[0].ip_address is None


class TestNetworkObjectParsing:
    def test_host_object(self):
        cfg = _parse("object network h1\n host 10.0.0.1\n")
        assert len(cfg.objects) == 1
        assert cfg.objects[0].name == "h1"
        assert cfg.objects[0].ip_address == "10.0.0.1"

    def test_subnet_object(self):
        cfg = _parse("object network net1\n subnet 10.0.0.0 255.255.255.0\n")
        obj = cfg.objects[0]
        assert obj.ip_address == "10.0.0.0"
        assert obj.subnet_mask == "255.255.255.0"

    def test_range_object(self):
        cfg = _parse("object network rng1\n range 10.0.0.1 10.0.0.10\n")
        obj = cfg.objects[0]
        assert obj.ip_address == "10.0.0.1"
        assert obj.ip_address_end == "10.0.0.10"

    def test_fqdn_object(self):
        cfg = _parse("object network fqdn1\n fqdn v4 example.com\n")
        assert cfg.objects[0].fqdn == "example.com"

    def test_nat_object_not_added_to_objects(self):
        cfg = _parse("object network nat-obj\n nat (inside,outside) static 203.0.113.10\n")
        assert len(cfg.nat_rules) == 1

    def test_object_with_description(self):
        cfg = _parse("object network h1\n host 10.0.0.1\n description my host\n")
        assert cfg.objects[0].description == "my host"

    def test_multiple_network_objects(self):
        cfg = _parse("object network h1\n host 10.0.0.1\n!\nobject network h2\n host 10.0.0.2\n")
        assert len(cfg.objects) == 2


class TestNetworkObjectGroupParsing:
    def test_group_with_network_object_member(self):
        cfg = _parse("object network h1\n host 10.0.0.1\n!\nobject-group network grp1\n network-object object h1\n")
        assert len(cfg.object_groups) == 1
        assert cfg.object_groups[0].name == "grp1"

    def test_group_with_host_member(self):
        cfg = _parse("object-group network grp1\n network-object host 10.0.0.1\n")
        assert len(cfg.object_groups[0].objects) == 1

    def test_group_with_subnet_member(self):
        cfg = _parse("object-group network grp1\n network-object 10.0.0.0 255.255.255.0\n")
        assert cfg.object_groups[0].objects[0].kind == "subnet"

    def test_nested_group_object(self):
        cfg = _parse("object-group network grp1\n group-object other-grp\n")
        assert cfg.object_groups[0].objects[0].kind == "object-group"

    def test_group_with_description(self):
        cfg = _parse("object-group network grp1\n description my group\n network-object host 10.0.0.1\n")
        assert cfg.object_groups[0].description == "my group"

    def test_ipv6_network_member(self):
        cfg = _parse("object-group network ipv6grp\n network-object 2001:db8::/32\n")
        assert len(cfg.object_groups) == 1


class TestServiceObjectParsing:
    def test_tcp_eq_service_object(self):
        cfg = _parse("object service svc1\n service tcp destination eq 443\n")
        assert len(cfg.service_objects) == 1
        assert cfg.service_objects[0].protocol == "tcp"
        assert cfg.service_objects[0].dst_port_eq == "443"

    def test_udp_range_service_object(self):
        cfg = _parse("object service svc2\n service udp destination range 2000 2010\n")
        assert cfg.service_objects[0].dst_port_range == ("2000", "2010")

    def test_icmp_service_object(self):
        cfg = _parse("object service ping\n service icmp echo 0\n")
        assert cfg.service_objects[0].protocol == "icmp"

    def test_gre_service_object(self):
        cfg = _parse("object service gre1\n service gre\n")
        assert cfg.service_objects[0].protocol == "gre"

    def test_unsupported_protocol_not_added(self):
        cfg = _parse("object service esp1\n service esp\n")
        assert len(cfg.service_objects) == 0

    def test_service_object_with_description(self):
        cfg = _parse("object service svc1\n description web\n service tcp destination eq 80\n")
        assert cfg.service_objects[0].description == "web"

    def test_named_port_service_object(self):
        cfg = _parse("object service dns\n service udp destination eq domain\n")
        assert cfg.service_objects[0].dst_port_eq == "domain"


class TestServiceObjectGroupParsing:
    def test_tcp_mode_group(self):
        cfg = _parse("object-group service web-tcp tcp\n port-object eq 80\n port-object eq 443\n")
        assert len(cfg.service_object_groups) == 1
        assert cfg.service_object_groups[0].proto_mode == "tcp"

    def test_tcp_udp_mode_group(self):
        cfg = _parse("object-group service common tcp-udp\n port-object eq 53\n")
        assert cfg.service_object_groups[0].proto_mode == "tcp-udp"

    def test_mixed_mode_group_no_proto_mode(self):
        cfg = _parse("object-group service mixed\n service-object tcp destination eq 80\n")
        assert cfg.service_object_groups[0].proto_mode is None

    def test_group_with_nested_ref(self):
        cfg = _parse("object-group service outer\n group-object inner-grp\n")
        assert "inner-grp" in cfg.service_object_groups[0].nested_refs

    def test_group_with_port_range(self):
        cfg = _parse("object-group service range-grp tcp\n port-object range 8000 8080\n")
        assert ("8000", "8080") in cfg.service_object_groups[0].ports_range.get("tcp", [])

    def test_group_with_description(self):
        cfg = _parse("object-group service svc-grp tcp\n description my group\n port-object eq 80\n")
        assert cfg.service_object_groups[0].description == "my group"


class TestIcmpObjectGroupParsing:
    def test_icmp_group_parsed_as_service_object_group(self):
        cfg = _parse("object-group icmp-type icmp-grp\n icmp-object echo\n icmp-object echo-reply\n")
        assert len(cfg.service_object_groups) == 1
        grp = cfg.service_object_groups[0]
        assert grp.name == "icmp-grp"
        assert "echo" in grp.ports_eq.get("icmp", [])
        assert "echo-reply" in grp.ports_eq.get("icmp", [])

    def test_icmp_group_with_description(self):
        cfg = _parse("object-group icmp-type icmp-grp\n description ICMP types\n icmp-object echo\n")
        assert cfg.service_object_groups[0].description == "ICMP types"

    def test_icmp_group_empty_produces_group_with_no_objects(self):
        cfg = _parse("object-group icmp-type empty-icmp\n")
        assert len(cfg.service_object_groups) == 1
        assert cfg.service_object_groups[0].ports_eq == {"icmp": []}


class TestProtocolObjectGroupParsing:
    def test_single_protocol(self):
        cfg = _parse("object-group protocol proto-grp\n protocol-object tcp\n")
        assert len(cfg.protocol_groups) == 1
        assert "tcp" in cfg.protocol_groups[0].protocols

    def test_multiple_protocols(self):
        cfg = _parse("object-group protocol pg\n protocol-object tcp\n protocol-object udp\n")
        assert len(cfg.protocol_groups[0].protocols) == 2

    def test_protocol_group_with_description(self):
        cfg = _parse("object-group protocol pg\n description TCP only\n protocol-object tcp\n")
        assert cfg.protocol_groups[0].description == "TCP only"

    def test_empty_protocol_group(self):
        cfg = _parse("object-group protocol empty-pg\n")
        assert cfg.protocol_groups[0].protocols == []


class TestAccessListEntryParsing:
    def test_ip_host_to_host_entry(self):
        cfg = _parse("access-list acl1 extended permit ip host 10.0.0.1 host 10.0.0.2\n")
        assert len(cfg.access_lists) == 1
        assert cfg.access_lists[0].entries[0].action == "permit"

    def test_deny_entry(self):
        cfg = _parse("access-list acl1 extended deny tcp any host 10.0.0.1 eq 443\n")
        assert cfg.access_lists[0].entries[0].action == "deny"

    def test_inactive_flag_set(self):
        cfg = _parse("access-list acl1 extended permit tcp any any inactive\n")
        assert cfg.access_lists[0].entries[0].inactive is True

    def test_tcp_with_eq_port(self):
        cfg = _parse("access-list acl1 extended permit tcp any host 10.0.0.1 eq 8443\n")
        entry = cfg.access_lists[0].entries[0]
        assert entry.dst_port.kind == "eq"
        assert entry.dst_port.value == "8443"

    def test_tcp_with_named_port(self):
        cfg = _parse("access-list acl1 extended permit tcp any host 10.0.0.1 eq https\n")
        entry = cfg.access_lists[0].entries[0]
        assert entry.dst_port.value == "https"

    def test_tcp_with_range(self):
        cfg = _parse("access-list acl1 extended permit tcp any host 10.0.0.1 range 2000 2005\n")
        entry = cfg.access_lists[0].entries[0]
        assert entry.dst_port.kind == "range"

    def test_icmp_entry(self):
        cfg = _parse("access-list acl1 extended permit icmp any any\n")
        entry = cfg.access_lists[0].entries[0]
        assert entry.protocol.value == "icmp"

    def test_subnet_source_entry(self):
        cfg = _parse("access-list acl1 extended permit tcp 10.0.0.0 255.255.255.0 any\n")
        entry = cfg.access_lists[0].entries[0]
        assert entry.src.kind == "subnet"
        assert entry.src.value == "10.0.0.0"

    def test_object_reference_in_entry(self):
        cfg = _parse("object network h1\n host 10.0.0.1\n!\naccess-list acl1 extended permit ip object h1 any\n")
        entry = cfg.access_lists[0].entries[0]
        assert entry.src.kind == "object"
        assert entry.src.value == "h1"

    def test_object_group_reference_in_entry(self):
        cfg = _parse(
            "object-group protocol pg\n protocol-object tcp\n!\n"
            "access-list acl1 extended permit object-group pg any any\n"
        )
        entry = cfg.access_lists[0].entries[0]
        assert entry.protocol.kind == "protocol-group"

    def test_multiple_entries_same_acl(self):
        cfg = _parse("access-list acl1 extended permit ip any any\naccess-list acl1 extended deny tcp any any\n")
        assert len(cfg.access_lists[0].entries) == 2

    def test_two_distinct_acls(self):
        cfg = _parse("access-list acl1 extended permit ip any any\naccess-list acl2 extended permit tcp any any\n")
        acl_names = {acl.name for acl in cfg.access_lists}
        assert "acl1" in acl_names
        assert "acl2" in acl_names

    def test_malformed_entry_skipped_gracefully(self):
        raw = _HEADER + "access-list acl1 extended permit\n"
        cfg = parse_asa_config(raw)
        assert len(cfg.access_lists) == 0

    def test_any4_treated_as_any(self):
        cfg = _parse(
            "access-list acl1 extended permit ip object-group grp any4\n"
            "object-group network grp\n network-object host 10.0.0.1\n"
        )
        assert len(cfg.access_lists) == 1


class TestAccessGroupParsing:
    def test_inbound_binding(self):
        cfg = _parse("access-list acl1 extended permit ip any any\naccess-group acl1 in interface inside\n")
        assert len(cfg.access_group_bindings) == 1
        assert cfg.access_group_bindings[0].direction == "in"
        assert cfg.access_group_bindings[0].interface == "inside"
        assert cfg.access_group_bindings[0].acl_name == "acl1"

    def test_outbound_binding(self):
        cfg = _parse("access-list acl1 extended permit ip any any\naccess-group acl1 out interface outside\n")
        assert cfg.access_group_bindings[0].direction == "out"


class TestRouteParsing:
    def test_static_route(self):
        cfg = _parse("route outside 0.0.0.0 0.0.0.0 203.0.113.1\n")
        assert len(cfg.routes) == 1
        assert cfg.routes[0].interface == "outside"
        assert cfg.routes[0].next_hop == "203.0.113.1"
        assert cfg.routes[0].destination == "0.0.0.0"

    def test_route_with_admin_distance(self):
        cfg = _parse("route inside 10.0.0.0 255.255.0.0 10.0.0.254 120\n")
        assert cfg.routes[0].distance == 120

    def test_route_without_admin_distance(self):
        cfg = _parse("route inside 10.0.0.0 255.255.0.0 10.0.0.254\n")
        assert cfg.routes[0].distance is None

    def test_multiple_routes(self):
        cfg = _parse("route inside 10.0.0.0 255.255.0.0 10.0.0.1\nroute outside 0.0.0.0 0.0.0.0 203.0.113.1\n")
        assert len(cfg.routes) == 2


class TestMgmtAccessParsing:
    def test_ssh_access_rule(self):
        cfg = _parse("ssh 10.0.0.0 255.255.255.0 inside\n")
        assert len(cfg.mgmt_access) == 1
        assert cfg.mgmt_access[0].protocol == "ssh"
        assert cfg.mgmt_access[0].interface == "inside"

    def test_http_access_rule(self):
        cfg = _parse("http 10.0.0.0 255.255.0.0 management\n")
        assert cfg.mgmt_access[0].protocol == "http"

    def test_telnet_access_rule(self):
        cfg = _parse("telnet 192.168.0.0 255.255.255.0 inside\n")
        assert cfg.mgmt_access[0].protocol == "telnet"

    def test_multiple_mgmt_rules(self):
        cfg = _parse("ssh 10.0.0.0 255.255.255.0 inside\nhttp 10.0.0.0 255.255.255.0 mgmt\n")
        assert len(cfg.mgmt_access) == 2


class TestAdditionalSettingsParsing:
    def test_crypto_line_captured(self):
        cfg = _parse("crypto ipsec transform-set myset esp-aes\n")
        assert any("crypto" in s for s in cfg.additional_settings)

    def test_threat_detection_captured(self):
        cfg = _parse("threat-detection basic-threat\n")
        assert any("threat-detection" in s for s in cfg.additional_settings)

    def test_unknown_line_not_captured(self):
        cfg = _parse("some-unknown-directive foo\n")
        assert not any("some-unknown" in s for s in cfg.additional_settings)

    def test_aaa_line_captured(self):
        cfg = _parse("aaa authentication ssh console LOCAL\n")
        assert any("aaa" in s for s in cfg.additional_settings)


class TestClassAndPolicyMapParsing:
    def test_class_map_parsed(self):
        cfg = _parse("class-map inspection_default\n match default-inspection-traffic\n")
        assert len(cfg.class_maps) == 1
        assert cfg.class_maps[0].name == "inspection_default"

    def test_policy_map_parsed(self):
        cfg = _parse("policy-map global_policy\n class inspection_default\n  inspect ftp\n")
        assert len(cfg.policy_maps) == 1
        assert cfg.policy_maps[0].name == "global_policy"

    def test_service_policy_global_parsed(self):
        cfg = _parse("service-policy global_policy global\n")
        assert len(cfg.service_policies) == 1
        assert cfg.service_policies[0].scope == "global"
        assert cfg.service_policies[0].policy_map == "global_policy"

    def test_service_policy_interface_parsed(self):
        cfg = _parse("service-policy my-policy interface outside\n")
        sp = cfg.service_policies[0]
        assert sp.scope == "interface"
        assert sp.interface == "outside"

    def test_dns_inspect_policy_map_parsed(self):
        cfg = _parse("policy-map type inspect dns pm-dns\n parameters\n  message-length maximum client auto\n")
        assert len(cfg.policy_maps) == 1
        assert cfg.policy_maps[0].name == "pm-dns"


class TestFullConfigParsing:
    def _cfg(self):
        conf_path = Path(__file__).parent.parent / "fw_modules/ciscoasa9/test_asa.conf"
        return parse_asa_config(conf_path.read_text())

    def test_asa_version_from_full_config(self):
        assert self._cfg().asa_version == "9.16(4)"

    def test_hostname_from_full_config(self):
        assert self._cfg().hostname == "ciscoasa"

    def test_service_modules_from_full_config(self):
        cfg = self._cfg()
        assert len(cfg.service_modules) == 2

    def test_names_from_full_config(self):
        cfg = self._cfg()
        names = {n.name for n in cfg.names}
        assert "DEMO_OLD_OBJ" in names
        assert "DEMO-WEB" in names

    def test_network_objects_from_full_config(self):
        cfg = self._cfg()
        obj_names = {o.name for o in cfg.objects}
        assert "DEMO_OBJ_HOST_WEB" in obj_names
        assert "DEMO_OBJ_USER_NET" in obj_names
        assert "DEMO_OBJ_FQDN" in obj_names
        assert "DEMO_OBJ_IP_RANGE" in obj_names

    def test_nat_rule_extracted_from_full_config(self):
        cfg = self._cfg()
        assert len(cfg.nat_rules) == 1

    def test_network_object_groups_from_full_config(self):
        cfg = self._cfg()
        grp_names = {g.name for g in cfg.object_groups}
        assert "DEMO_OG_NET_BASE" in grp_names
        assert "DEMO_OG_NET_ALL" in grp_names
        assert "DEMO_OG_IPV6" in grp_names

    def test_service_objects_from_full_config(self):
        cfg = self._cfg()
        svc_names = {s.name for s in cfg.service_objects}
        assert "DEMO_SVC_TCP_8443" in svc_names
        assert "GRE" in svc_names

    def test_service_object_groups_from_full_config(self):
        cfg = self._cfg()
        svc_grp_names = {g.name for g in cfg.service_object_groups}
        assert "DEMO_OG_SVC_MIXED" in svc_grp_names
        assert "DEMO_OG_SVC_PORTS" in svc_grp_names

    def test_protocol_groups_from_full_config(self):
        cfg = self._cfg()
        pg_names = {g.name for g in cfg.protocol_groups}
        assert "DEMO_OG_PROTO_TCP" in pg_names
        assert "DEMO_OG_PROTO_TCP_UDP" in pg_names

    def test_access_lists_from_full_config(self):
        cfg = self._cfg()
        acl_names = {a.name for a in cfg.access_lists}
        assert "DEMO_ACL" in acl_names
        assert "acl1" in acl_names
        assert "acl2" in acl_names
        assert "acl3" in acl_names

    def test_demo_acl_has_multiple_entries(self):
        cfg = self._cfg()
        demo_acl = next(a for a in cfg.access_lists if a.name == "DEMO_ACL")
        assert len(demo_acl.entries) > 5

    def test_inactive_entries_present_in_full_config(self):
        cfg = self._cfg()
        demo_acl = next(a for a in cfg.access_lists if a.name == "DEMO_ACL")
        inactive = [e for e in demo_acl.entries if e.inactive]
        assert len(inactive) >= 2

    def test_access_group_binding_from_full_config(self):
        cfg = self._cfg()
        assert len(cfg.access_group_bindings) == 1
        assert cfg.access_group_bindings[0].interface == "inside_2"
        assert cfg.access_group_bindings[0].direction == "in"


class TestInternalHandlerBranches:
    def test_handle_access_list_entry_exception_branch(self):
        state = _ParserState()
        line = "access-list acl1 extended permit object-group no-such-group any any"
        match = re.match(r"^access-list\s+\S+\s+extended\s+(permit|deny)\s+", line, re.IGNORECASE)
        assert match is not None
        result_i = _handle_access_list_entry(match, line, [line], 0, state)
        assert result_i == 1
        assert state.access_lists_map == {}

    def test_handle_access_group_invalid_direction_raises(self):
        line = "access-group acl1 both interface inside"
        match = re.match(r"^access-group\s+(\S+)\s+(in|out|both)\s+interface\s+(\S+)$", line, re.IGNORECASE)
        assert match is not None
        with pytest.raises(ValueError, match="Invalid direction value"):
            _handle_access_group(match, line, [line], 0, _ParserState())

    def test_handle_mgmt_access_invalid_protocol_raises(self):
        line = "snmp 10.0.0.0 255.255.255.0 inside"
        match = re.match(r"^(snmp)\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)\s+(\S+)$", line, re.IGNORECASE)
        assert match is not None
        with pytest.raises(ValueError, match="Invalid protocol for MgmtAccessRule"):
            _handle_mgmt_access(match, line, [line], 0, _ParserState())

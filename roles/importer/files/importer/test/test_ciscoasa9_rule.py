"""Tests for fw_modules/ciscoasa9/asa_rule.py"""

from unittest.mock import patch

import pytest
from fw_modules.ciscoasa9.asa_models import (
    AccessList,
    AccessListEntry,
    AsaProtocolGroup,
    EndpointKind,
)
from fw_modules.ciscoasa9.asa_rule import (
    build_rulebases_from_access_lists,
    create_service_for_protocol_group_entry,
    resolve_network_reference_for_rule,
    resolve_service_reference_for_rule,
)
from models.rule import RuleAction


def _permit_entry() -> AccessListEntry:
    return AccessListEntry(
        acl_name="acl",
        action="permit",
        protocol=EndpointKind(kind="protocol", value="tcp"),
        src=EndpointKind(kind="any", value="any"),
        dst=EndpointKind(kind="any", value="any"),
        dst_port=EndpointKind(kind="any", value="any"),
    )


class TestCreateServiceForProtocolGroupEntry:
    def test_found_group_creates_any_services(self):
        pg = AsaProtocolGroup(name="pg1", protocols=["tcp", "udp"], description=None)
        ref = create_service_for_protocol_group_entry("pg1", [pg], {})
        assert "any-tcp" in ref
        assert "any-udp" in ref

    def test_missing_group_falls_back_to_tcp_udp_icmp(self):
        ref = create_service_for_protocol_group_entry("missing-pg", [], {})
        assert "any-tcp" in ref
        assert "any-udp" in ref
        assert "any-icmp" in ref

    def test_empty_protocol_list_falls_back(self):
        pg = AsaProtocolGroup(name="empty-pg", protocols=[], description=None)
        ref = create_service_for_protocol_group_entry("empty-pg", [pg], {})
        assert "any-tcp" in ref


class TestResolveNetworkReferenceForRule:
    def test_host_endpoint_returns_uid(self):
        ep = EndpointKind(kind="host", value="10.0.0.1")
        assert resolve_network_reference_for_rule(ep, {}) == "10.0.0.1"

    def test_subnet_endpoint_returns_cidr(self):
        ep = EndpointKind(kind="subnet", value="10.0.0.0", mask="255.255.255.0")
        assert resolve_network_reference_for_rule(ep, {}) == "10.0.0.0/24"

    def test_any_endpoint_returns_any(self):
        ep = EndpointKind(kind="any", value="any")
        assert resolve_network_reference_for_rule(ep, {}) == "any"


class TestBuildRulebasesFromAccessLists:
    def test_empty_access_lists_returns_empty(self):
        assert build_rulebases_from_access_lists([], "mgm-uid", [], {}, {}) == []

    def test_single_access_list_creates_rulebase(self):
        acl = AccessList(name="outside-acl", entries=[_permit_entry()])
        result = build_rulebases_from_access_lists([acl], "mgm-uid", [], {}, {})
        assert len(result) == 1
        assert result[0].name == "outside-acl"

    def test_permit_entry_creates_accept_rule(self):
        acl = AccessList(name="acl", entries=[_permit_entry()])
        rules = list(build_rulebases_from_access_lists([acl], "mgm-uid", [], {}, {})[0].rules.values())
        assert rules[0].rule_action == RuleAction.ACCEPT

    def test_deny_entry_creates_drop_rule(self):
        entry = AccessListEntry(
            acl_name="acl",
            action="deny",
            protocol=EndpointKind(kind="protocol", value="tcp"),
            src=EndpointKind(kind="any", value="any"),
            dst=EndpointKind(kind="any", value="any"),
            dst_port=EndpointKind(kind="any", value="any"),
        )
        acl = AccessList(name="acl", entries=[entry])
        rules = list(build_rulebases_from_access_lists([acl], "mgm-uid", [], {}, {})[0].rules.values())
        assert rules[0].rule_action == RuleAction.DROP

    def test_inactive_entry_sets_rule_disabled(self):
        entry = AccessListEntry(
            acl_name="acl",
            action="permit",
            protocol=EndpointKind(kind="protocol", value="tcp"),
            src=EndpointKind(kind="any", value="any"),
            dst=EndpointKind(kind="any", value="any"),
            dst_port=EndpointKind(kind="any", value="any"),
            inactive=True,
        )
        acl = AccessList(name="acl", entries=[entry])
        rules = list(build_rulebases_from_access_lists([acl], "mgm-uid", [], {}, {})[0].rules.values())
        assert rules[0].rule_disabled is True


class TestResolveServiceReferenceForRule:
    def test_protocol_group_kind_routes_through_protocol_group_resolver(self):
        pg = AsaProtocolGroup(name="pg1", protocols=["tcp", "udp"], description=None)
        entry = AccessListEntry(
            acl_name="acl",
            action="permit",
            protocol=EndpointKind(kind="protocol-group", value="pg1"),
            src=EndpointKind(kind="any", value="any"),
            dst=EndpointKind(kind="any", value="any"),
            dst_port=EndpointKind(kind="any", value="any"),
        )
        ref = resolve_service_reference_for_rule(entry, [pg], {})
        assert "any-tcp" in ref
        assert "any-udp" in ref

    def test_non_protocol_group_kind_routes_through_acl_entry_resolver(self):
        entry = AccessListEntry(
            acl_name="acl",
            action="permit",
            protocol=EndpointKind(kind="protocol", value="tcp"),
            src=EndpointKind(kind="any", value="any"),
            dst=EndpointKind(kind="any", value="any"),
            dst_port=EndpointKind(kind="any", value="any"),
        )
        ref = resolve_service_reference_for_rule(entry, [], {})
        assert "tcp" in ref


class TestBuildRulebasesRuleUidGuard:
    def test_none_rule_uid_raises_value_error(self):
        entry = _permit_entry()
        acl = AccessList(name="acl", entries=[entry])
        with (
            patch("fw_modules.ciscoasa9.asa_rule.fwo_base.generate_hash_from_dict", return_value=None),
            pytest.raises(ValueError, match="Rule UID generation failed"),
        ):
            build_rulebases_from_access_lists([acl], "mgm-uid", [], {}, {})

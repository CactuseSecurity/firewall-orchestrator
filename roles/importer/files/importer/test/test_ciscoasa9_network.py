"""Tests for fw_modules/ciscoasa9/asa_network.py"""

from unittest.mock import MagicMock, patch

import fwo_const
import pytest
from fw_modules.ciscoasa9.asa_models import (
    AsaNetworkObject,
    AsaNetworkObjectGroup,
    AsaNetworkObjectGroupMember,
    EndpointKind,
    Names,
)
from fw_modules.ciscoasa9.asa_network import (
    create_network_group_member,
    create_network_subnet,
    get_network_group_member,
    get_network_group_member_host,
    get_network_group_member_ref,
    get_network_rule_endpoint,
    normalize_names,
    normalize_network_object_groups,
    normalize_network_objects,
)
from models.networkobject import NetworkObject
from netaddr import IPNetwork


def _nw_obj(name: str) -> NetworkObject:
    return NetworkObject(
        obj_uid=name,
        obj_name=name,
        obj_typ="host",
        obj_ip=IPNetwork("10.0.0.1/32"),
        obj_ip_end=IPNetwork("10.0.0.1/32"),
        obj_color=fwo_const.DEFAULT_COLOR,
    )


class TestCreateNetworkSubnet:
    def test_ipv4_with_valid_mask(self):
        obj = create_network_subnet("net1", "10.0.0.0", "255.255.255.0", None, ip_version=4)
        assert obj.obj_typ == "network"
        assert obj.obj_uid == "net1"

    def test_ipv4_with_none_mask_raises(self):
        with pytest.raises(ValueError, match="Subnet mask is required"):
            create_network_subnet("net1", "10.0.0.0", None, None, ip_version=4)

    def test_ipv6_cidr_notation(self):
        obj = create_network_subnet("v6net", "2001:db8::/32", None, None, ip_version=6)
        assert obj.obj_typ == "network"


class TestGetNetworkGroupMemberRef:
    def test_subnet_without_mask_raises(self):
        member = AsaNetworkObjectGroupMember(kind="subnet", value="10.0.0.0")
        with pytest.raises(ValueError, match="Subnet mask is required"):
            get_network_group_member_ref(member)

    def test_subnet_with_mask_returns_cidr_string(self):
        member = AsaNetworkObjectGroupMember(kind="subnet", value="10.0.0.0", mask="255.255.255.0")
        assert get_network_group_member_ref(member) == "10.0.0.0/255.255.255.0"

    def test_host_returns_value(self):
        member = AsaNetworkObjectGroupMember(kind="host", value="10.0.0.1")
        assert get_network_group_member_ref(member) == "10.0.0.1"


class TestGetNetworkGroupMember:
    def test_object_not_found_raises(self):
        member = AsaNetworkObjectGroupMember(kind="object", value="missing")
        with pytest.raises(ValueError, match="not found"):
            get_network_group_member(member, {})

    def test_object_group_not_found_raises(self):
        member = AsaNetworkObjectGroupMember(kind="object-group", value="missing-grp")
        with pytest.raises(ValueError, match="not found"):
            get_network_group_member(member, {})

    def test_existing_ref_returned_from_cache(self):
        existing = _nw_obj("10.0.0.1")
        member = AsaNetworkObjectGroupMember(kind="host", value="10.0.0.1")
        assert get_network_group_member(member, {"10.0.0.1": existing}) is existing

    def test_host_member_creates_new_object(self):
        member = AsaNetworkObjectGroupMember(kind="host", value="192.168.1.1")
        result = get_network_group_member(member, {})
        assert result.obj_uid == "192.168.1.1"
        assert result.obj_typ == "host"

    def test_subnet_member_creates_network_object(self):
        member = AsaNetworkObjectGroupMember(kind="subnet", value="10.0.0.0", mask="255.255.255.0")
        result = get_network_group_member(member, {})
        assert result.obj_typ == "network"


class TestGetNetworkRuleEndpoint:
    def test_any_endpoint_creates_any_object(self):
        obj = get_network_rule_endpoint(EndpointKind(kind="any", value="any"), {})
        assert obj.obj_uid == "any"

    def test_any_endpoint_returns_cached(self):
        existing = _nw_obj("any")
        result = get_network_rule_endpoint(EndpointKind(kind="any", value="any"), {"any": existing})
        assert result is existing

    def test_host_endpoint_creates_host(self):
        obj = get_network_rule_endpoint(EndpointKind(kind="host", value="10.0.0.1"), {})
        assert obj.obj_uid == "10.0.0.1"
        assert obj.obj_typ == "host"

    def test_host_endpoint_returns_cached(self):
        existing = _nw_obj("10.0.0.1")
        result = get_network_rule_endpoint(EndpointKind(kind="host", value="10.0.0.1"), {"10.0.0.1": existing})
        assert result is existing

    def test_subnet_endpoint_creates_cidr_keyed_object(self):
        ep = EndpointKind(kind="subnet", value="10.0.0.0", mask="255.255.255.0")
        objs: dict[str, NetworkObject] = {}
        obj = get_network_rule_endpoint(ep, objs)
        assert obj.obj_typ == "network"
        assert "10.0.0.0/24" in objs

    def test_object_endpoint_found(self):
        existing = _nw_obj("my-obj")
        result = get_network_rule_endpoint(EndpointKind(kind="object", value="my-obj"), {"my-obj": existing})
        assert result is existing

    def test_object_endpoint_not_found_raises(self):
        with pytest.raises(ValueError, match="not found"):
            get_network_rule_endpoint(EndpointKind(kind="object", value="missing"), {})

    def test_unknown_endpoint_kind_raises(self):
        ep = EndpointKind(kind="host", value="dummy")
        ep.__dict__["kind"] = "unknown-kind"  # pyright: ignore[reportIndexIssue]
        with pytest.raises((ValueError, Exception)):
            get_network_rule_endpoint(ep, {})


class TestNormalizeNetworkObjects:
    def test_fqdn_creates_empty_group(self):
        obj = AsaNetworkObject(name="fqdn-obj", ip_address="", fqdn="example.com")
        result = normalize_network_objects([obj])
        assert result["fqdn-obj"].obj_typ == "group"
        assert result["fqdn-obj"].obj_member_names == ""

    def test_empty_ip_address_with_no_other_fields_skipped(self):
        obj = AsaNetworkObject(name="nat-only", ip_address="")
        result = normalize_network_objects([obj])
        assert "nat-only" not in result

    def test_host_object(self):
        obj = AsaNetworkObject(name="h1", ip_address="10.0.0.1")
        assert normalize_network_objects([obj])["h1"].obj_typ == "host"

    def test_subnet_object(self):
        obj = AsaNetworkObject(name="net1", ip_address="10.0.0.0", subnet_mask="255.255.255.0")
        assert normalize_network_objects([obj])["net1"].obj_typ == "network"

    def test_range_object(self):
        obj = AsaNetworkObject(name="rng1", ip_address="10.0.0.1", ip_address_end="10.0.0.10")
        assert normalize_network_objects([obj])["rng1"].obj_typ == "ip_range"


class TestNormalizeNetworkObjectGroups:
    def test_empty_group_creates_group_with_no_members(self):
        group = AsaNetworkObjectGroup(name="empty-grp", objects=[], description=None)
        result = normalize_network_object_groups([group], {})
        assert result["empty-grp"].obj_typ == "group"
        assert result["empty-grp"].obj_member_names == ""

    def test_broken_object_reference_skipped_with_warning(self):
        member = AsaNetworkObjectGroupMember(kind="object", value="missing")
        group = AsaNetworkObjectGroup(name="grp", objects=[member], description=None)
        result = normalize_network_object_groups([group], {})
        assert "grp" in result
        assert result["grp"].obj_member_names == ""

    def test_valid_host_member_included(self):
        member = AsaNetworkObjectGroupMember(kind="host", value="10.0.0.1")
        group = AsaNetworkObjectGroup(name="grp", objects=[member], description=None)
        result = normalize_network_object_groups([group], {})
        obj_member_names = result["grp"].obj_member_names
        assert obj_member_names is not None
        assert "10.0.0.1" in obj_member_names

    def test_existing_objects_preserved(self):
        existing = _nw_obj("pre-existing")
        group = AsaNetworkObjectGroup(name="grp", objects=[], description=None)
        result = normalize_network_object_groups([group], {"pre-existing": existing})
        assert result["pre-existing"] is existing


class TestNormalizeNames:
    def test_empty_list_returns_empty(self):
        assert normalize_names([]) == {}

    def test_single_name_entry(self):
        name = Names(name="server1", ip_address="10.0.0.1", description="web server")
        result = normalize_names([name])
        assert result["server1"].obj_typ == "host"


class TestGetNetworkGroupMemberHost:
    def test_host_kind_ipv4(self):
        member = AsaNetworkObjectGroupMember(kind="host", value="10.0.0.1")
        obj = get_network_group_member_host(member)
        assert obj.obj_typ == "host"
        assert obj.obj_uid == "10.0.0.1"

    def test_hostv6_kind_uses_ipv6(self):
        member = AsaNetworkObjectGroupMember(kind="hostv6", value="2001:db8::1")
        obj = get_network_group_member_host(member)
        assert obj.obj_typ == "host"
        assert obj.obj_uid == "2001:db8::1"
        assert ":" in str(obj.obj_ip)


class TestCreateNetworkGroupMember:
    def test_host_kind(self):
        member = AsaNetworkObjectGroupMember(kind="host", value="10.0.0.1")
        obj = create_network_group_member("10.0.0.1", member)
        assert obj.obj_typ == "host"

    def test_hostv6_kind(self):
        member = AsaNetworkObjectGroupMember(kind="hostv6", value="2001:db8::1")
        obj = create_network_group_member("2001:db8::1", member)
        assert obj.obj_typ == "host"
        assert ":" in str(obj.obj_ip)

    def test_subnet_kind(self):
        member = AsaNetworkObjectGroupMember(kind="subnet", value="10.0.0.0", mask="255.255.255.0")
        obj = create_network_group_member("10.0.0.0/255.255.255.0", member)
        assert obj.obj_typ == "network"

    def test_subnetv6_kind(self):
        member = AsaNetworkObjectGroupMember(kind="subnetv6", value="2001:db8::/32")
        obj = create_network_group_member("2001:db8::/32", member)
        assert obj.obj_typ == "network"

    def test_unsupported_kind_raises(self):
        member = AsaNetworkObjectGroupMember(kind="host", value="10.0.0.1")
        member.__dict__["kind"] = "unknown"  # pyright: ignore[reportIndexIssue]
        with pytest.raises(ValueError, match="Unsupported member kind"):
            create_network_group_member("10.0.0.1", member)


class TestGetNetworkRuleEndpointSubnetBranches:
    def test_cached_subnet_returned_directly(self):
        existing = _nw_obj("10.0.0.0/24")
        ep = EndpointKind(kind="subnet", value="10.0.0.0", mask="255.255.255.0")
        result = get_network_rule_endpoint(ep, {"10.0.0.0/24": existing})
        assert result is existing

    def test_subnet_mask_none_raises_value_error(self):
        ep = EndpointKind(kind="subnet", value="10.0.0.0", mask=None)
        mock_net = MagicMock()
        mock_net.__str__ = lambda self: "10.0.0.0/0"  # type: ignore  # noqa: ARG005, PGH003
        with (
            patch("fw_modules.ciscoasa9.asa_network.IPNetwork", return_value=mock_net),
            pytest.raises(ValueError, match="Subnet mask is required"),
        ):
            get_network_rule_endpoint(ep, {})

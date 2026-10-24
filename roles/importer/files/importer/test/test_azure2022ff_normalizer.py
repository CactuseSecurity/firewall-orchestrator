from __future__ import annotations

import json
from pathlib import Path
from typing import TYPE_CHECKING, Any

import fwo_const
import pytest
from fw_modules.azure2022ff.azure_models import AzureConfig, AzureDevice, AzureIpGroup, AzureRule
from fw_modules.azure2022ff.azure_network import normalize_ip_groups, parse_address, resolve_ip_group
from fw_modules.azure2022ff.azure_normalizer import normalize_config
from fw_modules.azure2022ff.azure_rule import priority_sort_key
from fw_modules.azure2022ff.azure_service import get_rule_services, parse_port_range
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.rule import RuleAction
from netaddr import IPNetwork

if TYPE_CHECKING:
    from models.networkobject import NetworkObject
    from pytest_mock import MockerFixture

NATIVE_CONFIG_FILE = Path(__file__).parent / "data" / "azure2022ff_native_config.json"
POLICY_NAME = "FirewallPolicy_AzureFirewall1"
RCG_UID = (
    "/subscriptions/00000000-0000-0000-0000-000000000000/resourcegroups/fworch-test1/providers/"
    "microsoft.network/firewallpolicies/firewallpolicy_azurefirewall1/rulecollectiongroups/rulecollgroup1"
)
IP_GROUP_UID = (
    "/subscriptions/00000000-0000-0000-0000-000000000000/resourcegroups/fworch-test1/providers/"
    "microsoft.network/ipgroups/ipgroup1"
)
DEVICE = AzureDevice(uid="azfw1-uid", name="AzureFirewall1", policy_name=POLICY_NAME)
DEVICES = [DEVICE]
TCP_PROTOCOLS = ["TCP"]
TCP_UDP_PROTOCOLS = ["TCP", "UDP"]
ICMP_PROTOCOLS = ["ICMP"]
ANY_PROTOCOLS = ["Any"]
ANY_PORTS = ["*"]
TWO_PORTS = ["443", "80"]
NO_PROTOCOLS: list[str] = []
NO_PORTS: list[str] = []
IP_GROUP_ADDRESSES = ["1.2.3.4", "10.0.0.0/24"]
KUBE_SELECTOR_GROUP_ID = (
    "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/FWORCH-TEST1/providers/"
    "Microsoft.Network/firewallPolicies/FirewallPolicy_AzureFirewall1/kubeSelectorGroups/frontend-pods"
)
COUNTRY_CODES = ["US", "CA"]


def load_native_config() -> AzureConfig:
    return AzureConfig.model_validate(json.loads(NATIVE_CONFIG_FILE.read_text()))


def rule_uid(collection: str, rule: str) -> str:
    return f"{RCG_UID}/ruleCollections/{collection}/rules/{rule}"


class TestParseAddress:
    def test_host(self) -> None:
        nw_obj = parse_address("1.2.3.4")
        assert nw_obj.obj_typ == "host"
        assert nw_obj.obj_uid == "1.2.3.4"
        assert nw_obj.obj_ip == IPNetwork("1.2.3.4/32")
        assert nw_obj.obj_ip_end == IPNetwork("1.2.3.4/32")

    def test_network(self) -> None:
        nw_obj = parse_address("10.0.0.0/24")
        assert nw_obj.obj_typ == "network"
        assert nw_obj.obj_ip == IPNetwork("10.0.0.0/32")
        assert nw_obj.obj_ip_end == IPNetwork("10.0.0.255/32")

    def test_ipv6_network(self) -> None:
        nw_obj = parse_address("2001:db8::/64")
        assert nw_obj.obj_typ == "network"
        assert nw_obj.obj_ip == IPNetwork("2001:db8::/128")
        assert nw_obj.obj_ip_end == IPNetwork("2001:db8::ffff:ffff:ffff:ffff/128")

    def test_range(self) -> None:
        nw_obj = parse_address("10.1.1.1-10.1.1.20")
        assert nw_obj.obj_typ == "ip_range"
        assert nw_obj.obj_ip == IPNetwork("10.1.1.1/32")
        assert nw_obj.obj_ip_end == IPNetwork("10.1.1.20/32")

    def test_single_address_range_is_host(self) -> None:
        assert parse_address("10.1.1.1-10.1.1.1").obj_typ == "host"

    def test_reversed_range_fails(self) -> None:
        with pytest.raises(FwoImporterError, match="Invalid Azure IP range"):
            parse_address("10.1.1.20-10.1.1.1")

    def test_any(self) -> None:
        nw_obj = parse_address("*")
        assert nw_obj.obj_uid == "any"
        assert nw_obj.obj_ip == IPNetwork(fwo_const.ANY_IP_START)
        assert nw_obj.obj_ip_end == IPNetwork(fwo_const.ANY_IP_END)

    def test_service_tag(self) -> None:
        nw_obj = parse_address("AzureCloud.WestEurope")
        assert nw_obj.obj_typ == "dynamic_net_obj"
        assert nw_obj.obj_ip is None

    def test_service_tag_with_dash(self) -> None:
        assert parse_address("Sql-Management").obj_typ == "dynamic_net_obj"


class TestIpGroups:
    def test_group_members_and_case_insensitive_lookup(self) -> None:
        ip_group = AzureIpGroup.model_validate(
            {
                "id": "/subscriptions/x/resourceGroups/RG/providers/Microsoft.Network/ipGroups/G1",
                "name": "G1",
                "properties": {"ipAddresses": IP_GROUP_ADDRESSES},
            }
        )
        network_objects: dict[str, NetworkObject] = {}

        groups = normalize_ip_groups([ip_group], network_objects)
        group = resolve_ip_group("/subscriptions/x/resourcegroups/rg/providers/microsoft.network/ipgroups/g1", groups)

        assert group.obj_typ == "group"
        assert group.obj_member_refs == "1.2.3.4|10.0.0.0/24"
        assert set(network_objects) == {"1.2.3.4", "10.0.0.0/24", group.obj_uid}

    def test_unknown_group_fails(self) -> None:
        with pytest.raises(FwoImporterError, match="not found"):
            resolve_ip_group("/subscriptions/x/ipGroups/unknown", {})


class TestServices:
    def test_port_range(self) -> None:
        assert parse_port_range("1000-2000") == (1000, 2000)
        assert parse_port_range("80") == (80, 80)
        assert parse_port_range("*") == (0, 65535)

    @pytest.mark.parametrize("port", ["abc", "70000", "20-10", "-1"])
    def test_invalid_port_fails(self, port: str) -> None:
        with pytest.raises(FwoImporterError, match="Invalid Azure destination port"):
            parse_port_range(port)

    def test_protocol_port_product(self) -> None:
        services = get_rule_services(TCP_UDP_PROTOCOLS, TWO_PORTS)
        assert [svc.svc_uid for svc in services] == ["TCP_443", "TCP_80", "UDP_443", "UDP_80"]
        assert services[0].ip_proto == 6
        assert services[2].ip_proto == 17

    def test_any_protocol_any_port(self) -> None:
        services = get_rule_services(ANY_PROTOCOLS, ANY_PORTS)
        assert len(services) == 1
        assert services[0].svc_uid == "any"
        assert services[0].ip_proto == fwo_const.ANY_IP_PROTOCOL_ID

    def test_tcp_any_port(self) -> None:
        service = get_rule_services(TCP_PROTOCOLS, ANY_PORTS)[0]
        assert service.svc_uid == "TCP_any"
        assert (service.svc_port, service.svc_port_end) == (0, 65535)

    def test_icmp_ignores_ports(self) -> None:
        services = get_rule_services(ICMP_PROTOCOLS, TWO_PORTS)
        assert [svc.svc_uid for svc in services] == ["ICMP"]
        assert services[0].svc_port is None

    def test_missing_protocols_fails(self) -> None:
        with pytest.raises(FwoImporterError, match="without IP protocols"):
            get_rule_services(NO_PROTOCOLS, TWO_PORTS)

    def test_missing_ports_fails(self) -> None:
        with pytest.raises(FwoImporterError, match="without destination ports"):
            get_rule_services(TCP_PROTOCOLS, NO_PORTS)


class TestNormalizeConfig:
    def test_rulebase_contains_only_network_rules_of_filter_collections(self) -> None:
        normalized = normalize_config(load_native_config(), DEVICES, "mgm-uid")

        assert len(normalized.rulebases) == 1
        rulebase = normalized.rulebases[0]
        assert rulebase.uid == POLICY_NAME
        assert list(rulebase.rules) == [
            rule_uid("RuleColl1", "rule1"),
            rule_uid("RuleColl1", "secondrule"),
            rule_uid("AllowColl", "allow-any"),
            rule_uid("AllowColl", "allow-icmp-range"),
        ]

    def test_rules_are_ordered_by_group_and_collection_priority(self) -> None:
        native_config = load_native_config()
        rule_collection_groups = native_config.firewall_policies[0].rule_collection_groups
        group = rule_collection_groups[1]
        # list the collections against their priorities: AllowColl (9999), RuleColl1DNAT, RuleColl1 (2100)
        group.properties.rule_collections.reverse()
        # a group listed last, which the firewall processes first due to its lower priority
        first_group = group.model_copy(deep=True, update={"id": f"{group.id}-first"})
        first_group.properties.priority = 1000
        first_group.properties.rule_collections = first_group.properties.rule_collections[:1]
        rule_collection_groups.append(first_group)

        rules = normalize_config(native_config, DEVICES, "mgm-uid").rulebases[0].rules

        first_group_rule_prefix = f"{RCG_UID}-first/ruleCollections/AllowColl/rules/"
        assert list(rules) == [
            first_group_rule_prefix + "allow-any",
            first_group_rule_prefix + "allow-icmp-range",
            rule_uid("RuleColl1", "rule1"),
            rule_uid("RuleColl1", "secondrule"),
            rule_uid("AllowColl", "allow-any"),
            rule_uid("AllowColl", "allow-icmp-range"),
        ]

    def test_missing_priority_sorts_last(self) -> None:
        assert sorted([None, 65000, 100], key=priority_sort_key) == [100, 65000, None]

    def test_rule_fields(self) -> None:
        rules = normalize_config(load_native_config(), DEVICES, "mgm-uid").rulebases[0].rules

        deny_rule = rules[rule_uid("RuleColl1", "rule1")]
        assert deny_rule.rule_action == RuleAction.DROP
        assert deny_rule.rule_src_refs == "1.2.3.4"
        assert deny_rule.rule_dst_refs == "2.3.4.5"
        assert deny_rule.rule_svc_refs == "TCP_443|TCP_80"
        assert deny_rule.rule_installon == DEVICE.name

        group_rule = rules[rule_uid("RuleColl1", "secondrule")]
        assert group_rule.rule_src == "ipGroup1"
        assert group_rule.rule_src_refs == IP_GROUP_UID
        assert group_rule.rule_comment == "uses an ip group"

        any_rule = rules[rule_uid("AllowColl", "allow-any")]
        assert any_rule.rule_action == RuleAction.ACCEPT
        assert any_rule.rule_src_refs == "any"
        assert any_rule.rule_dst_refs == "AzureCloud.WestEurope|example.com"
        assert any_rule.rule_svc_refs == "any"

    def test_all_rule_refs_resolve_to_objects(self) -> None:
        normalized = normalize_config(load_native_config(), DEVICES, "mgm-uid")

        for rule in normalized.rulebases[0].rules.values():
            for ref in (rule.rule_src_refs + "|" + rule.rule_dst_refs).split("|"):
                assert ref in normalized.network_objects
            for ref in rule.rule_svc_refs.split("|"):
                assert ref in normalized.service_objects
        assert normalized.network_objects["example.com"].obj_typ == "domain"

    def test_nat_and_application_objects_are_not_imported(self) -> None:
        normalized = normalize_config(load_native_config(), DEVICES, "mgm-uid")

        assert "20.170.21.73" not in normalized.network_objects
        assert "UDP_80" not in normalized.service_objects

    def test_gateways_link_policy_rulebase(self) -> None:
        second_device = AzureDevice(uid="azfw2-uid", name="AzureFirewall2", policy_name=POLICY_NAME)

        normalized = normalize_config(load_native_config(), [DEVICE, second_device], "mgm-uid")

        assert [gw.Uid for gw in normalized.gateways] == ["azfw1-uid", "azfw2-uid"]
        assert all(gw.RulebaseLinks[0].to_rulebase_uid == POLICY_NAME for gw in normalized.gateways)
        assert all(gw.RulebaseLinks[0].is_initial for gw in normalized.gateways)
        rule = next(iter(normalized.rulebases[0].rules.values()))
        assert rule.rule_installon == "AzureFirewall1|AzureFirewall2"

    def test_missing_policy_fails(self) -> None:
        device = AzureDevice(uid="x", name="x", policy_name="unknown-policy")
        with pytest.raises(FwoImporterError, match="unknown-policy"):
            normalize_config(load_native_config(), [device], "mgm-uid")

    def test_rule_without_source_fails_with_rule_uid(self) -> None:
        native_config = load_native_config()
        rule = native_config.firewall_policies[0].rule_collection_groups[1].properties.rule_collections[0].rules[0]
        rule.source_addresses = []

        with pytest.raises(FwoImporterError, match=r"rules/rule1.*no addresses"):
            normalize_config(native_config, DEVICES, "mgm-uid")


def add_api_2026_rule(native_config: AzureConfig, rule_fields: dict[str, Any]) -> str:
    """Append a network rule using fields introduced with api version 2026-01-01 to the first filter collection."""
    rule_collection = native_config.firewall_policies[0].rule_collection_groups[1].properties.rule_collections[0]
    rule_collection.rules.append(
        AzureRule.model_validate(
            {
                "ruleType": "NetworkRule",
                "name": "rule-2026",
                "ipProtocols": ["TCP"],
                "destinationAddresses": ["10.9.9.9"],
                "destinationPorts": ["443"],
                **rule_fields,
            }
        )
    )
    return rule_uid("RuleColl1", "rule-2026")


class TestApiVersion2026:
    def test_legacy_native_config_without_api_version_is_parsed(self) -> None:
        native_config = load_native_config()

        assert native_config.api_version is None
        assert (
            native_config.firewall_policies[0]
            .rule_collection_groups[1]
            .properties.rule_collections[0]
            .rules[0]
            .source_kube_selector_groups
            == []
        )

    def test_api_version_is_kept_in_native_config(self) -> None:
        native_config = AzureConfig.model_validate({"apiVersion": "2026-01-01"})

        assert native_config.model_dump(by_alias=True)["apiVersion"] == "2026-01-01"

    def test_kube_selector_groups_are_dynamic_sources(self) -> None:
        native_config = load_native_config()
        uid = add_api_2026_rule(
            native_config,
            {"sourceAddresses": ["10.1.0.0/16"], "sourceKubeSelectorGroups": [KUBE_SELECTOR_GROUP_ID.upper()]},
        )

        normalized = normalize_config(native_config, DEVICES, "mgm-uid")

        rule = normalized.rulebases[0].rules[uid]
        assert rule.rule_src == "10.1.0.0/16|FRONTEND-PODS"
        assert rule.rule_src_refs == "10.1.0.0/16|" + KUBE_SELECTOR_GROUP_ID.lower()
        kube_obj = normalized.network_objects[KUBE_SELECTOR_GROUP_ID.lower()]
        assert kube_obj.obj_typ == "dynamic_net_obj"
        assert kube_obj.obj_ip is None

    def test_kube_selector_group_alone_is_a_valid_source(self) -> None:
        native_config = load_native_config()
        uid = add_api_2026_rule(native_config, {"sourceKubeSelectorGroups": [KUBE_SELECTOR_GROUP_ID]})

        normalized = normalize_config(native_config, DEVICES, "mgm-uid")

        assert normalized.rulebases[0].rules[uid].rule_src == "frontend-pods"

    @pytest.mark.parametrize("geo_field", ["sourceGeoLocations", "destinationGeoLocations"])
    def test_rules_with_geo_location_filters_are_skipped_with_warning(
        self, geo_field: str, mocker: MockerFixture
    ) -> None:
        warning = mocker.patch.object(FWOLogger, "warning")
        native_config = load_native_config()
        uid = add_api_2026_rule(native_config, {"sourceAddresses": ["*"], geo_field: COUNTRY_CODES})

        normalized = normalize_config(native_config, DEVICES, "mgm-uid")

        rules = normalized.rulebases[0].rules
        assert uid not in rules
        assert len(rules) == 4
        # objects of the skipped rule are not created
        assert "10.9.9.9" not in normalized.network_objects
        warning.assert_called_once()
        assert uid in warning.call_args.args[0]
        assert "geo-location" in warning.call_args.args[0]

    @pytest.mark.parametrize("geo_field", ["sourceGeoLocations", "destinationGeoLocations"])
    def test_rules_with_geo_location_filters_fail_if_skipping_is_disabled(self, geo_field: str) -> None:
        native_config = load_native_config()
        add_api_2026_rule(native_config, {"sourceAddresses": ["*"], geo_field: COUNTRY_CODES})

        with pytest.raises(FwoImporterError, match=r"rules/rule-2026.*geo-location.*importer settings"):
            normalize_config(native_config, DEVICES, "mgm-uid", skip_unsupported_rules=False)

    def test_supported_rules_are_unaffected_if_skipping_is_disabled(self) -> None:
        normalized = normalize_config(load_native_config(), DEVICES, "mgm-uid", skip_unsupported_rules=False)

        assert len(normalized.rulebases[0].rules) == 4

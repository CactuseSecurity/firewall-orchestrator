import fwo_const
from fw_modules.guardicoremanagementREST import gc_const
from fw_modules.guardicoremanagementREST.gc_models import (
    GcAndLabels,
    GcLabelRef,
    GcObjectRef,
    GcRuleSide,
    GuardicoreConfig,
)
from fw_modules.guardicoremanagementREST.gc_network import (
    GcNetworkObjectCollector,
    build_cidr_object,
    get_label_name,
    parse_ip_set,
)
from netaddr import IPNetwork, IPSet


def build_config() -> GuardicoreConfig:
    return GuardicoreConfig.model_validate(
        {
            "labels": [
                {
                    "id": "l-web",
                    "key": "AppRole",
                    "value": "Web",
                    "dynamic_criteria": [
                        {"field": "numeric_ip_addresses", "op": "SUBNET", "argument": "10.0.0.0/24"},
                        {"field": "name", "op": "STARTSWITH", "argument": "web"},
                    ],
                },
                {
                    "id": "l-prod",
                    "key": "Stage",
                    "value": "Prod",
                    "dynamic_criteria": [
                        {"field": "numeric_ip_addresses", "op": "RANGE", "argument": "10.0.0.128-10.0.1.255"}
                    ],
                },
                {"id": "l-asset", "key": "AppRole", "value": "Asset"},
            ],
            "label_groups": [
                {
                    "id": "lg-1",
                    "name": "Group: 1",
                    "include_labels": {"or_labels": [{"and_labels": ["l-web"]}]},
                    "exclude_labels": {"or_labels": [{"and_labels": [{"id": "l-prod"}]}]},
                },
                {"id": "lg-2", "key": "Group", "value": "2"},
            ],
            "assets": [
                {
                    "id": "a-1",
                    "name": "srv-1",
                    "nics": [{"ip_addresses": ["192.0.2.1", "2001:db8::1"]}],
                    "labels": [{"id": "l-asset"}, {"id": "l-web"}],
                }
            ],
        }
    )


class TestHelpers:
    def test_parse_ip_set_variants(self) -> None:
        assert parse_ip_set("10.0.0.1") == IPSet(["10.0.0.1/32"])
        assert parse_ip_set("10.0.0.0/30") == IPSet(["10.0.0.0/30"])
        assert parse_ip_set("10.0.0.1 - 10.0.0.2") == IPSet(["10.0.0.1/32", "10.0.0.2/32"])
        assert parse_ip_set("no-ip") == IPSet()
        assert parse_ip_set(None) == IPSet()

    def test_build_cidr_object_host_and_network(self) -> None:
        host = build_cidr_object(IPNetwork("10.0.0.1/32"))
        assert (host.obj_uid, host.obj_name, host.obj_typ) == ("10.0.0.1/32", "10.0.0.1", "host")
        network = build_cidr_object(IPNetwork("10.0.0.0/24"))
        assert (network.obj_uid, network.obj_name, network.obj_typ) == ("10.0.0.0/24", "10.0.0.0/24", "network")
        assert str(network.obj_ip) == "10.0.0.0/32"
        assert str(network.obj_ip_end) == "10.0.0.255/32"
        ipv6 = build_cidr_object(IPNetwork("2001:db8::/126"))
        assert str(ipv6.obj_ip) == "2001:db8::/128"
        assert str(ipv6.obj_ip_end) == "2001:db8::3/128"

    def test_get_label_name_falls_back_to_reference_and_id(self) -> None:
        assert get_label_name(GcLabelRef(id="x", key="K", value="V"), {}) == "K: V"
        assert get_label_name(GcLabelRef(id="x"), {}) == "x"


class TestCollector:
    def test_label_ip_sets_combine_ip_criteria_and_assets(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        assert collector.label_ip_sets["l-web"] == IPSet(["10.0.0.0/24", "192.0.2.1/32", "2001:db8::1/128"])
        assert collector.label_ip_sets["l-prod"] == IPSet(["10.0.0.128/25", "10.0.1.0/24"])
        assert collector.label_ip_sets["l-asset"] == IPSet(["192.0.2.1/32", "2001:db8::1/128"])

    def test_add_label_groups_creates_group_per_label(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        collector.add_label_groups()
        group = collector.network_objects["l-prod"]
        assert group.obj_typ == "group"
        assert group.obj_name == "Stage: Prod"
        assert group.obj_member_refs == fwo_const.LIST_DELIMITER.join(["10.0.0.128/25", "10.0.1.0/24"])
        assert collector.network_objects["10.0.1.0/24"].obj_typ == "network"

    def test_unknown_label_becomes_empty_group(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        uid = collector.add_label("missing")
        assert collector.network_objects[uid].obj_member_refs is None
        assert collector.network_objects[uid].obj_name == "missing"

    def test_and_labels_become_intersection_group(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        and_labels = GcAndLabels.model_validate({"and_labels": [{"id": "l-web"}, {"id": "l-prod"}]})
        uid = collector.add_and_labels(and_labels)
        assert uid == "l-prod&l-web"
        group = collector.network_objects["l-prod&l-web"]
        assert group.obj_name == "AppRole: Web & Stage: Prod"
        assert group.obj_member_refs == "10.0.0.128/25"
        assert collector.add_and_labels(GcAndLabels()) is None

    def test_label_group_subtracts_excluded_labels(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        uid = collector.add_label_group(GcObjectRef(id="lg-1"))
        refs = collector.network_objects[uid].obj_member_refs
        assert refs is not None
        assert "10.0.0.0/25" in refs.split(fwo_const.LIST_DELIMITER)
        assert "10.0.0.128/25" not in refs.split(fwo_const.LIST_DELIMITER)
        assert collector.network_objects[collector.add_label_group(GcObjectRef(id="lg-2"))].obj_name == "Group: 2"
        unknown_uid = collector.add_label_group(GcObjectRef(id="lg-x", name="X"))
        assert collector.network_objects[unknown_uid].obj_name == "X"

    def test_assets_subnets_and_classifications(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        asset_uid = collector.add_asset("a-1")
        assert asset_uid == gc_const.GC_ASSET_UID_PREFIX + "a-1"
        assert collector.network_objects[asset_uid].obj_name == "srv-1"
        assert collector.network_objects[collector.add_asset("a-x", "unknown")].obj_name == "unknown"
        assert collector.add_subnet("198.51.100.7") == "198.51.100.7/32"
        assert collector.add_subnet("garbage") is None
        range_uid = collector.add_subnet("198.51.100.1-198.51.100.2")
        assert range_uid is not None
        assert collector.network_objects[range_uid].obj_typ == "group"
        private_uid = collector.add_address_classification("Private")
        assert private_uid is not None
        assert collector.network_objects[private_uid].obj_member_refs == "10.0.0.0/8|172.16.0.0/12|192.168.0.0/16"
        internet_uid = collector.add_address_classification("Internet")
        assert internet_uid is not None
        internet_refs = collector.network_objects[internet_uid].obj_member_refs
        assert internet_refs is not None
        assert "10.0.0.0/8" not in internet_refs.split(fwo_const.LIST_DELIMITER)
        assert collector.add_address_classification("Moon") is None

    def test_resolve_rule_side(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        side = GcRuleSide.model_validate(
            {
                "labels": {"or_labels": [{"and_labels": [{"id": "l-web"}]}, {"and_labels": []}]},
                "label_group_ids": ["lg-2"],
                "assets": ["a-1"],
                "asset_ids": ["a-1"],
                "subnets": ["198.51.100.0/24"],
                "processes": ["nginx"],
                "domains": [],
            }
        )
        assert side.get_unsupported_fields() == ["processes"]
        uids = collector.resolve_rule_side(side, "r1")
        assert uids == ["l-web", "lg-2", "asset:a-1", "198.51.100.0/24"]
        assert collector.get_names(uids)[0] == "AppRole: Web"

    def test_empty_rule_side_is_any(self) -> None:
        collector = GcNetworkObjectCollector(build_config())
        assert collector.resolve_rule_side(GcRuleSide(), "r1") == [gc_const.GC_ANY_OBJECT_UID]
        any_obj = collector.network_objects[gc_const.GC_ANY_OBJECT_UID]
        assert str(any_obj.obj_ip) == fwo_const.ANY_IP_START
        assert str(any_obj.obj_ip_end) == fwo_const.ANY_IP_END
        assert collector.add_any() == gc_const.GC_ANY_OBJECT_UID

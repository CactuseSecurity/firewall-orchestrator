from typing import TYPE_CHECKING

import pytest
from fw_modules.guardicoremanagementREST.gc_models import GcRule, GcRuleSide, GuardicoreConfig
from fw_modules.guardicoremanagementREST.gc_network import GcNetworkObjectCollector
from fw_modules.guardicoremanagementREST.gc_rule import (
    build_rulebase_links,
    convert_last_hit,
    get_rule_action,
    get_rule_installon,
    get_rule_track,
    get_rulebase_name,
    get_side_labels,
    normalize_rules,
)
from fwo_exceptions import FwoImporterError
from model_controllers.management_controller import ManagementController
from models.rule import RuleAction, RuleTrack
from models.rulebase import Rulebase

if TYPE_CHECKING:
    from models.serviceobject import ServiceObject


def build_rule(**fields: object) -> GcRule:
    return GcRule.model_validate({"id": "r1", "action": "ALLOW", "section_position": "ALLOW", **fields})


def build_label_side(and_groups: list[list[str]]) -> GcRuleSide:
    or_labels = [{"and_labels": [{"id": label_id} for label_id in and_group]} for and_group in and_groups]
    return GcRuleSide.model_validate({"labels": {"or_labels": or_labels}})


@pytest.fixture
def label_collector() -> GcNetworkObjectCollector:
    labels = [
        {"id": "l-web", "key": "AppRole", "value": "AR-web"},
        {"id": "l-db", "key": "AppRole", "value": "AR-db"},
        {"id": "l-prod", "key": "Stage", "value": "Prod"},
        {"id": "l-test", "key": "Stage", "value": "Test"},
    ]
    return GcNetworkObjectCollector(GuardicoreConfig.model_validate({"labels": labels}))


class TestRuleAttributes:
    def test_rulebase_name_orders_sections_and_adds_ruleset(self) -> None:
        assert get_rulebase_name(build_rule(section_position="OVERRIDE_ALLOW")) == "01 Override Allow"
        assert get_rulebase_name(build_rule(section_position="OVERRIDE_ALERT")) == "02 Override Alert"
        assert get_rulebase_name(build_rule(section_position="OVERRIDE_BLOCK", ruleset_name="")) == "03 Override Block"
        assert get_rulebase_name(build_rule(ruleset_name="APP-2")) == "04 Allow APP-2"
        assert get_rulebase_name(build_rule(section_position="ALERT")) == "05 Alert"
        assert get_rulebase_name(build_rule(section_position="block", ruleset_name=" APP-1 ")) == "06 Block APP-1"
        with pytest.raises(FwoImporterError):
            get_rulebase_name(build_rule(section_position="SOMEWHERE"))

    def test_action_and_track(self) -> None:
        assert get_rule_action(build_rule(action="ALLOW_AND_ENCRYPT")) == RuleAction.ACCEPT
        assert get_rule_action(build_rule(action="ALERT")) == RuleAction.INFORM
        assert get_rule_action(build_rule(action="block_and_alert")) == RuleAction.DROP
        with pytest.raises(FwoImporterError):
            get_rule_action(build_rule(action="MAYBE"))
        assert get_rule_track(build_rule(action="BLOCK_AND_ALERT")) == RuleTrack.ALERT
        assert get_rule_track(build_rule(action="BLOCK")) == RuleTrack.LOG

    def test_last_hit_is_converted_from_epoch_milliseconds(self) -> None:
        assert convert_last_hit(None) is None
        assert convert_last_hit(1772087969150) == "2026-02-26T06:39:29+00:00"

    def test_rule_installon(self, management_controller: ManagementController) -> None:
        assert get_rule_installon(management_controller) == "Mock Management"
        management_controller.devices = [{"name": "gw-1"}]
        assert get_rule_installon(management_controller) == "gw-1"
        management_controller.devices = []
        management_controller.name = ""
        assert get_rule_installon(management_controller) == "mock.example.com"
        management_controller.hostname = ""
        with pytest.raises(FwoImporterError):
            get_rule_installon(management_controller)


class TestNormalizeRules:
    def test_rules_are_split_into_sorted_rulebases(self, management_controller: ManagementController) -> None:
        native_config = GuardicoreConfig.model_validate(
            {
                "rules": [
                    {"id": "a1", "action": "ALLOW", "section_position": "ALLOW", "ruleset_name": "B"},
                    {"id": "o1", "action": "BLOCK", "section_position": "OVERRIDE_BLOCK", "ip_protocols": ["TCP"]},
                    {"id": "a2", "action": "ALLOW", "section_position": "ALLOW", "ruleset_name": "A"},
                    {"id": "a3", "action": "ALLOW", "section_position": "ALLOW", "ruleset_name": "B"},
                    {"id": "x1", "action": "ALLOW", "section_position": "ALLOW", "ip_protocols": ["GRE"]},
                ]
            }
        )
        services: dict[str, ServiceObject] = {}
        rulebases = normalize_rules(
            native_config, GcNetworkObjectCollector(native_config), management_controller, services
        )
        assert [rulebase.name for rulebase in rulebases] == ["03 Override Block", "04 Allow A", "04 Allow B"]
        assert list(rulebases[2].rules) == ["a1", "a3"]
        assert set(services) == {"any", "tcp"}
        rule = rulebases[0].rules["o1"]
        assert (rule.rule_src, rule.rule_dst, rule.rule_svc) == ("Any", "Any", "tcp")
        assert rule.rule_action == RuleAction.DROP
        assert rule.rule_name is None

    def test_side_labels_keep_a_single_and_group(self, label_collector: GcNetworkObjectCollector) -> None:
        side = build_label_side([["l-web", "l-prod"]])

        assert get_side_labels(side, label_collector) == [{"AppRole": "AR-web", "Stage": "Prod"}]

    def test_side_labels_keep_ored_and_groups_apart(self, label_collector: GcNetworkObjectCollector) -> None:
        side = build_label_side([["l-web", "l-prod"], ["l-db", "l-test"]])

        assert get_side_labels(side, label_collector) == [
            {"AppRole": "AR-db", "Stage": "Test"},
            {"AppRole": "AR-web", "Stage": "Prod"},
        ]

    def test_side_labels_keep_a_label_used_in_two_groups(self, label_collector: GcNetworkObjectCollector) -> None:
        side = build_label_side([["l-web", "l-prod"], ["l-web", "l-test"]])

        assert get_side_labels(side, label_collector) == [
            {"AppRole": "AR-web", "Stage": "Prod"},
            {"AppRole": "AR-web", "Stage": "Test"},
        ]

    def test_side_labels_list_several_values_of_a_key_within_a_group(
        self, label_collector: GcNetworkObjectCollector
    ) -> None:
        side = build_label_side([["l-web", "l-db", "l-web"]])

        assert get_side_labels(side, label_collector) == [{"AppRole": ["AR-db", "AR-web"]}]

    def test_side_labels_ignore_group_order_and_duplicates(self, label_collector: GcNetworkObjectCollector) -> None:
        side = build_label_side([["l-web", "l-prod"], ["l-db", "l-test"], ["l-prod", "l-web"]])
        reordered_side = build_label_side([["l-test", "l-db"], ["l-prod", "l-web"]])

        assert get_side_labels(side, label_collector) == get_side_labels(reordered_side, label_collector)

    def test_side_labels_use_label_references_without_full_label(
        self, label_collector: GcNetworkObjectCollector
    ) -> None:
        side = GcRuleSide.model_validate(
            {"labels": {"or_labels": [{"and_labels": [{"id": "x", "key": "AppRole", "value": "AR-ext"}]}]}}
        )

        assert get_side_labels(side, label_collector) == [{"AppRole": "AR-ext"}]

    def test_side_labels_drop_groups_with_unknown_labels(self, label_collector: GcNetworkObjectCollector) -> None:
        side = build_label_side([["l-web", "broken"], ["l-db"], []])

        assert get_side_labels(side, label_collector) == [{"AppRole": "AR-db"}]
        assert get_side_labels(build_label_side([["broken"]]), label_collector) is None

    def test_side_labels_leave_out_label_groups(self, label_collector: GcNetworkObjectCollector) -> None:
        side = GcRuleSide.model_validate(
            {
                "labels": {"or_labels": [{"and_labels": [{"id": "l-web"}]}]},
                "label_groups": [{"id": "lg-1"}],
                "label_group_ids": ["lg-2"],
            }
        )

        assert get_side_labels(side, label_collector) == [{"AppRole": "AR-web"}]
        assert get_side_labels(GcRuleSide.model_validate({"label_group_ids": ["lg-2"]}), label_collector) is None

    def test_side_labels_are_none_without_labels(self, label_collector: GcNetworkObjectCollector) -> None:
        assert get_side_labels(GcRuleSide(), label_collector) is None
        assert get_side_labels(GcRuleSide.model_validate({"subnets": ["10.0.0.0/8"]}), label_collector) is None
        assert get_side_labels(build_label_side([]), label_collector) is None

    def test_rulebase_links_chain_rulebases(self) -> None:
        rulebases = [Rulebase(uid=uid, name=uid, mgm_uid="m") for uid in ["01 Override Allow", "04 Allow"]]
        links = build_rulebase_links(rulebases)
        assert [(link.from_rulebase_uid, link.to_rulebase_uid, link.is_initial) for link in links] == [
            (None, "01 Override Allow", True),
            ("01 Override Allow", "04 Allow", False),
        ]
        assert all(link.link_type == "ordered" for link in links)
        assert build_rulebase_links([]) == []

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

    def test_side_labels_map_keys_to_values(self) -> None:
        native_config = GuardicoreConfig.model_validate(
            {"labels": [{"id": "l1", "key": "AppRole", "value": "AR2"}, {"id": "l2", "key": "Stage", "value": "Prod"}]}
        )
        collector = GcNetworkObjectCollector(native_config)
        side = GcRuleSide.model_validate(
            {
                "labels": {
                    "or_labels": [
                        {"and_labels": [{"id": "l1"}, {"id": "l2"}]},
                        {"and_labels": [{"id": "x", "key": "AppRole", "value": "AR1"}, {"id": "l1"}]},
                        {"and_labels": [{"id": "broken"}]},
                    ]
                }
            }
        )

        assert get_side_labels(side, collector) == {"AppRole": ["AR1", "AR2"], "Stage": "Prod"}
        assert get_side_labels(GcRuleSide(), collector) is None
        assert get_side_labels(GcRuleSide.model_validate({"subnets": ["10.0.0.0/8"]}), collector) is None

    def test_rulebase_links_chain_rulebases(self) -> None:
        rulebases = [Rulebase(uid=uid, name=uid, mgm_uid="m") for uid in ["01 Override Allow", "04 Allow"]]
        links = build_rulebase_links(rulebases)
        assert [(link.from_rulebase_uid, link.to_rulebase_uid, link.is_initial) for link in links] == [
            (None, "01 Override Allow", True),
            ("01 Override Allow", "04 Allow", False),
        ]
        assert all(link.link_type == "ordered" for link in links)
        assert build_rulebase_links([]) == []

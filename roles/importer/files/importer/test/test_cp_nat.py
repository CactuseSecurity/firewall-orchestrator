from typing import Any

from fw_modules.checkpointR8x.cp_nat import (
    filter_nat_rulebases_for_gateway,
    get_initial_nat_rulebase_link,
    insert_parent_nat_rulebase,
    insert_rulebase_link,
    normalize_nat_rules,
    parse_nat_rule,
    parse_nat_rule_chunk,
    parse_nat_rule_transform,
    parse_nat_rulebase,
    parse_native_nat_rulebases,
)
from model_controllers.import_state_controller import ImportStateController
from models.rulebase import Rulebase


def _make_nat_rule(uid: str = "rule-uid-1") -> dict[str, Any]:
    return {
        "uid": uid,
        "original-source": {"uid": "src-uid", "type": "host", "name": "OrigSrc"},
        "original-destination": {"uid": "dst-uid", "type": "host", "name": "OrigDst"},
        "original-service": {"uid": "svc-uid", "type": "simple", "name": "OrigSvc"},
        "translated-source": {"uid": "t-src-uid", "type": "host", "name": "TransSrc"},
        "translated-destination": {"uid": "t-dst-uid", "type": "host", "name": "TransDst"},
        "translated-service": {"uid": "t-svc-uid", "type": "simple", "name": "TransSvc"},
        "install-on": [{"uid": "gw-uid", "name": "gw"}],
        "time": {"uid": "time-uid", "name": "Any"},
        "enabled": True,
        "comments": "a test rule",
    }


class TestParseNatRuleTransform:
    def test_returns_tuple_of_two(self):
        result = parse_nat_rule_transform(_make_nat_rule())
        assert len(result) == 2

    def test_in_rule_maps_original_fields(self):
        nat_rule = _make_nat_rule("r1")
        in_rule, _ = parse_nat_rule_transform(nat_rule)

        assert in_rule["uid"] == "r1"
        assert in_rule["source"] == [nat_rule["original-source"]]
        assert in_rule["destination"] == [nat_rule["original-destination"]]
        assert in_rule["service"] == [nat_rule["original-service"]]
        assert in_rule["type"] == "nat"
        assert in_rule["nat_rule"] is True
        assert in_rule["access_rule"] is False

    def test_out_rule_maps_translated_fields(self):
        nat_rule = _make_nat_rule("r2")
        _, out_rule = parse_nat_rule_transform(nat_rule)

        assert out_rule["uid"] == "r2_translated"
        assert out_rule["source"] == [nat_rule["translated-source"]]
        assert out_rule["destination"] == [nat_rule["translated-destination"]]
        assert out_rule["service"] == [nat_rule["translated-service"]]
        assert out_rule["nat_rule"] is True
        assert out_rule["access_rule"] is False

    def test_xlate_rule_uid_links_in_and_out(self):
        in_rule, out_rule = parse_nat_rule_transform(_make_nat_rule("r3"))
        assert in_rule["xlate_rule_uid"] == out_rule["uid"]

    def test_enabled_and_comments_propagated_to_in_rule(self):
        nat_rule = _make_nat_rule()
        nat_rule["enabled"] = False
        nat_rule["comments"] = "disabled rule"
        in_rule, _ = parse_nat_rule_transform(nat_rule)

        assert in_rule["enabled"] is False
        assert in_rule["comments"] == "disabled rule"

    def test_out_rule_always_enabled(self):
        nat_rule = _make_nat_rule()
        nat_rule["enabled"] = False
        _, out_rule = parse_nat_rule_transform(nat_rule)

        assert out_rule["enabled"] is True

    def test_in_rule_rule_number_is_zero(self):
        in_rule, out_rule = parse_nat_rule_transform(_make_nat_rule())
        assert in_rule["rule-number"] == 0
        assert out_rule["rule-number"] == 0

    def test_missing_time_field_defaults_to_none(self):
        nat_rule = _make_nat_rule()
        del nat_rule["time"]
        in_rule, _ = parse_nat_rule_transform(nat_rule)

        assert in_rule["time"] is None


class TestInsertRulebaseLink:
    def _make_gateway(self) -> dict[str, Any]:
        return {"RulebaseLinks": []}

    def test_adds_new_link(self):
        gateway = self._make_gateway()
        insert_rulebase_link("from-rb", "to-rb", "nat", gateway)

        assert len(gateway["RulebaseLinks"]) == 1
        link = gateway["RulebaseLinks"][0]
        assert link["from_rulebase_uid"] == "from-rb"
        assert link["to_rulebase_uid"] == "to-rb"
        assert link["link_type"] == "nat"
        assert link["is_initial"] is False
        assert link["is_global"] is False
        assert link["is_section"] is False

    def test_does_not_add_duplicate_link(self):
        gateway = self._make_gateway()
        insert_rulebase_link("from-rb", "to-rb", "nat", gateway)
        insert_rulebase_link("from-rb", "to-rb", "nat", gateway)

        assert len(gateway["RulebaseLinks"]) == 1

    def test_adds_different_link_type_separately(self):
        gateway = self._make_gateway()
        insert_rulebase_link("from-rb", "to-rb", "nat", gateway)
        insert_rulebase_link("from-rb", "to-rb", "ordered", gateway)

        assert len(gateway["RulebaseLinks"]) == 2

    def test_adds_different_from_rulebase_separately(self):
        gateway = self._make_gateway()
        insert_rulebase_link("from-rb-1", "to-rb", "nat", gateway)
        insert_rulebase_link("from-rb-2", "to-rb", "nat", gateway)

        assert len(gateway["RulebaseLinks"]) == 2


class TestInsertParentNatRulebase:
    def test_creates_nat_rulebase_when_missing(self, import_state_controller: ImportStateController):
        normalized_config: dict[str, Any] = {"policies": []}
        gateway = {"uid": "gw-1"}

        result = insert_parent_nat_rulebase(gateway, import_state_controller.state, normalized_config)

        assert result.uid == "nat-rulebase-gw-1"
        assert result.name == "NAT"
        assert len(normalized_config["policies"]) == 1

    def test_returns_existing_nat_rulebase_without_duplicate(self, import_state_controller: ImportStateController):
        existing = Rulebase(uid="nat-rulebase-gw-2", name="NAT", mgm_uid="mgm-uid-1")
        normalized_config = {"policies": [existing]}
        gateway = {"uid": "gw-2"}

        result = insert_parent_nat_rulebase(gateway, import_state_controller.state, normalized_config)

        assert result is existing
        assert len(normalized_config["policies"]) == 1


class TestGetInitialNatRulebaseLink:
    def _make_normalized_config_with_gateway(
        self, gateway_uid: str, rulebase_links: list[dict[str, Any]]
    ) -> dict[str, Any]:
        return {
            "gateways": [
                {
                    "Uid": gateway_uid,
                    "RulebaseLinks": rulebase_links,
                }
            ]
        }

    def test_returns_initial_policy_link(self):
        gateway = {"uid": "gw-1"}
        normalized_config = self._make_normalized_config_with_gateway(
            "gw-1",
            [
                {"is_initial": True, "link_type": "policy", "to_rulebase_uid": "rb-access"},
                {"is_initial": False, "link_type": "nat", "to_rulebase_uid": "rb-nat"},
            ],
        )

        result = get_initial_nat_rulebase_link(gateway, normalized_config)

        assert result is not None
        assert result["to_rulebase_uid"] == "rb-access"

    def test_returns_none_when_gateway_not_found(self):
        gateway = {"uid": "unknown-gw"}
        normalized_config = self._make_normalized_config_with_gateway("gw-1", [])

        result = get_initial_nat_rulebase_link(gateway, normalized_config)

        assert result is None

    def test_returns_none_when_no_initial_policy_link(self):
        gateway = {"uid": "gw-1"}
        normalized_config = self._make_normalized_config_with_gateway(
            "gw-1",
            [
                {"is_initial": False, "link_type": "policy", "to_rulebase_uid": "rb-1"},
                {"is_initial": True, "link_type": "nat", "to_rulebase_uid": "rb-nat"},
            ],
        )

        result = get_initial_nat_rulebase_link(gateway, normalized_config)

        assert result is None


class TestParseNatRule:
    def _make_rulebase(self) -> Rulebase:
        return Rulebase(uid="rb-1", mgm_uid="mgm-uid-1", name="Section", rules={})

    def test_inserts_both_in_and_out_rule(self):
        rulebase = self._make_rulebase()
        nat_rule = _make_nat_rule("rule-1")
        gateway = {"uid": "gw-1"}

        parse_nat_rule(nat_rule, rulebase, gateway, {"policies": []})

        assert set(rulebase.rules.keys()) == {"rule-1", "rule-1_translated"}

    def test_in_rule_uses_original_objects(self):
        rulebase = self._make_rulebase()
        nat_rule = _make_nat_rule("rule-2")
        gateway = {"uid": "gw-1"}

        parse_nat_rule(nat_rule, rulebase, gateway, {"policies": []})

        in_rule = rulebase.rules["rule-2"]
        assert in_rule.rule_src == "OrigSrc"
        assert in_rule.rule_dst == "OrigDst"
        assert in_rule.nat_rule is True

    def test_out_rule_uses_translated_objects(self):
        rulebase = self._make_rulebase()
        nat_rule = _make_nat_rule("rule-3")
        gateway = {"uid": "gw-1"}

        parse_nat_rule(nat_rule, rulebase, gateway, {"policies": []})

        out_rule = rulebase.rules["rule-3_translated"]
        assert out_rule.rule_src == "TransSrc"
        assert out_rule.rule_dst == "TransDst"
        assert out_rule.nat_rule is True


class TestParseNatRuleChunk:
    def _make_rulebase(self, uid: str = "nat-rb-1") -> Rulebase:
        return Rulebase(uid=uid, mgm_uid="mgm-uid-1", name="NAT", rules={})

    def _make_normalized_gateway(self) -> dict[str, Any]:
        return {"Uid": "gw-1", "RulebaseLinks": []}

    def test_returns_early_when_no_rulebase_key(self):
        rulebase = self._make_rulebase()
        gateway = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = {"policies": []}
        normalized_gateway = self._make_normalized_gateway()

        parse_nat_rule_chunk({}, rulebase, gateway, {"policies": []}, None, normalized_config, normalized_gateway)  # type: ignore[arg-type]

        assert rulebase.rules == {}
        assert normalized_config["policies"] == []

    def test_single_rule_entry_adds_rules_directly(self):
        rulebase = self._make_rulebase()
        gateway = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = {"policies": []}
        normalized_gateway = self._make_normalized_gateway()
        nat_rule = _make_nat_rule("rule-4")
        nat_rule["rule-number"] = 1
        chunk = {"rulebase": [nat_rule]}

        parse_nat_rule_chunk(chunk, rulebase, gateway, {"policies": []}, None, normalized_config, normalized_gateway)  # type: ignore[arg-type]

        assert set(rulebase.rules.keys()) == {"rule-4", "rule-4_translated"}
        assert normalized_config["policies"] == []

    def test_section_entry_creates_section_rulebase(self, import_state_controller: ImportStateController):
        rulebase = self._make_rulebase()
        gateway = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = {"policies": []}
        normalized_gateway = self._make_normalized_gateway()
        nat_rule = _make_nat_rule("rule-5")
        section = {"uid": "section-uid", "name": "Section1", "rulebase": [nat_rule]}
        chunk = {"rulebase": [section]}

        parse_nat_rule_chunk(
            chunk,
            rulebase,
            gateway,
            {"policies": []},
            import_state_controller.state,
            normalized_config,
            normalized_gateway,
        )

        assert len(normalized_config["policies"]) == 1
        section_rulebase = normalized_config["policies"][0]
        assert section_rulebase.uid == "section-uid"
        assert set(section_rulebase.rules.keys()) == {"rule-5", "rule-5_translated"}


class TestParseNatRulebase:
    def _make_normalized_gateway(self) -> dict[str, Any]:
        return {"Uid": "gw-1", "RulebaseLinks": []}

    def test_appends_new_section_rulebase_to_policies(self, import_state_controller: ImportStateController):
        normalized_nat_rulebase = Rulebase(uid="nat-rb-1", mgm_uid="mgm-uid-1", name="NAT", rules={})
        normalized_config: dict[str, Any] = {"policies": []}
        normalized_gateway = self._make_normalized_gateway()
        src_rulebase: dict[str, Any] = {"uid": "section-uid", "name": "Section1", "rulebase": []}

        parse_nat_rulebase(
            src_rulebase,
            normalized_nat_rulebase,
            {"uid": "gw-1"},
            {"policies": []},
            import_state_controller.state,
            normalized_config,
            normalized_gateway,
        )

        assert len(normalized_config["policies"]) == 1
        assert normalized_config["policies"][0].uid == "section-uid"
        assert normalized_config["policies"][0].name == "Section1"

    def test_does_not_duplicate_existing_section_rulebase(self, import_state_controller: ImportStateController):
        normalized_nat_rulebase = Rulebase(uid="nat-rb-1", mgm_uid="mgm-uid-1", name="NAT", rules={})
        existing_section = Rulebase(uid="section-uid", mgm_uid="mgm-uid-1", name="Section1", rules={})
        normalized_config: dict[str, Any] = {"policies": [existing_section]}
        normalized_gateway = self._make_normalized_gateway()
        src_rulebase: dict[str, Any] = {"uid": "section-uid", "name": "Section1", "rulebase": []}

        parse_nat_rulebase(
            src_rulebase,
            normalized_nat_rulebase,
            {"uid": "gw-1"},
            {"policies": []},
            import_state_controller.state,
            normalized_config,
            normalized_gateway,
        )

        assert len(normalized_config["policies"]) == 1
        assert normalized_config["policies"][0] is existing_section

    def test_creates_link_from_nat_rulebase_to_section(self, import_state_controller: ImportStateController):
        normalized_nat_rulebase = Rulebase(uid="nat-rb-1", mgm_uid="mgm-uid-1", name="NAT", rules={})
        normalized_config: dict[str, Any] = {"policies": []}
        normalized_gateway = self._make_normalized_gateway()
        src_rulebase: dict[str, Any] = {"uid": "section-uid", "name": "Section1", "rulebase": []}

        parse_nat_rulebase(
            src_rulebase,
            normalized_nat_rulebase,
            {"uid": "gw-1"},
            {"policies": []},
            import_state_controller.state,
            normalized_config,
            normalized_gateway,
        )

        assert len(normalized_gateway["RulebaseLinks"]) == 1
        link = normalized_gateway["RulebaseLinks"][0]
        assert link["from_rulebase_uid"] == "nat-rb-1"
        assert link["to_rulebase_uid"] == "section-uid"
        assert link["link_type"] == "nat"

    def test_parses_rules_from_src_rulebase(self, import_state_controller: ImportStateController):
        normalized_nat_rulebase = Rulebase(uid="nat-rb-1", mgm_uid="mgm-uid-1", name="NAT", rules={})
        normalized_config: dict[str, Any] = {"policies": []}
        normalized_gateway = self._make_normalized_gateway()
        nat_rule = _make_nat_rule("rule-6")
        src_rulebase: dict[str, Any] = {"uid": "section-uid", "name": "Section1", "rulebase": [nat_rule]}

        parse_nat_rulebase(
            src_rulebase,
            normalized_nat_rulebase,
            {"uid": "gw-1"},
            {"policies": []},
            import_state_controller.state,
            normalized_config,
            normalized_gateway,
        )

        section_rulebase = normalized_config["policies"][0]
        assert set(section_rulebase.rules.keys()) == {"rule-6", "rule-6_translated"}


class TestParseNativeNatRulebases:
    def _make_normalized_config(self, initial_link: dict[str, Any] | None) -> dict[str, Any]:
        rulebase_links = [initial_link] if initial_link is not None else []
        return {
            "policies": [],
            "gateways": [
                {
                    "Uid": "gw-1",
                    "RulebaseLinks": rulebase_links,
                }
            ],
        }

    def test_skips_nat_rulebase_without_chunks(self, import_state_controller: ImportStateController):
        gateway: dict[str, Any] = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = self._make_normalized_config(
            {"is_initial": True, "link_type": "policy", "to_rulebase_uid": "rb-access"}
        )

        parse_native_nat_rulebases(
            gateway, [{"not_a_chunk_field": True}], import_state_controller.state, normalized_config, {"policies": []}
        )

        assert normalized_config["policies"] == []

    def test_skips_when_normalized_gateway_missing(self, import_state_controller: ImportStateController):
        gateway: dict[str, Any] = {"uid": "unknown-gw"}
        normalized_config: dict[str, Any] = self._make_normalized_config(
            {"is_initial": True, "link_type": "policy", "to_rulebase_uid": "rb-access"}
        )
        nat_rulebases: list[dict[str, Any]] = [{"nat_rule_chunks": [{"rulebase": []}]}]

        parse_native_nat_rulebases(
            gateway, nat_rulebases, import_state_controller.state, normalized_config, {"policies": []}
        )

        assert normalized_config["policies"] == []  # no orphan NAT rulebase for an unknown gateway
        assert normalized_config["gateways"][0]["RulebaseLinks"] == [
            {"is_initial": True, "link_type": "policy", "to_rulebase_uid": "rb-access"}
        ]

    def test_skips_when_no_initial_policy_link(self, import_state_controller: ImportStateController):
        gateway: dict[str, Any] = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = self._make_normalized_config(None)
        nat_rulebases: list[dict[str, Any]] = [{"nat_rule_chunks": [{"rulebase": []}]}]

        parse_native_nat_rulebases(
            gateway, nat_rulebases, import_state_controller.state, normalized_config, {"policies": []}
        )

        assert normalized_config["gateways"][0]["RulebaseLinks"] == []

    def test_skips_when_initial_link_missing_to_rulebase_uid(self, import_state_controller: ImportStateController):
        gateway: dict[str, Any] = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = self._make_normalized_config({"is_initial": True, "link_type": "policy"})
        nat_rulebases: list[dict[str, Any]] = [{"nat_rule_chunks": [{"rulebase": []}]}]

        parse_native_nat_rulebases(
            gateway, nat_rulebases, import_state_controller.state, normalized_config, {"policies": []}
        )

        assert len(normalized_config["gateways"][0]["RulebaseLinks"]) == 1  # unchanged, no nat link added

    def test_happy_path_links_and_parses_rules(self, import_state_controller: ImportStateController):
        gateway: dict[str, Any] = {"uid": "gw-1"}
        normalized_config: dict[str, Any] = self._make_normalized_config(
            {"is_initial": True, "link_type": "policy", "to_rulebase_uid": "rb-access"}
        )
        nat_rule: dict[str, Any] = _make_nat_rule("rule-7")
        nat_rule["rule-number"] = 1
        nat_rulebases: list[dict[str, Any]] = [{"nat_rule_chunks": [{"rulebase": [nat_rule]}]}]

        parse_native_nat_rulebases(
            gateway, nat_rulebases, import_state_controller.state, normalized_config, {"policies": [], "gateways": []}
        )

        assert len(normalized_config["policies"]) == 1
        nat_rulebase = normalized_config["policies"][0]
        assert nat_rulebase.uid == "nat-rulebase-gw-1"
        assert set(nat_rulebase.rules.keys()) == {"rule-7", "rule-7_translated"}

        links = normalized_config["gateways"][0]["RulebaseLinks"]
        assert any(
            link.get("from_rulebase_uid") == "rb-access"
            and link.get("to_rulebase_uid") == "nat-rulebase-gw-1"
            and link.get("link_type") == "nat"
            for link in links
        )


class TestNormalizeNatRules:
    def test_returns_early_when_nat_rulebases_key_missing(self, import_state_controller: ImportStateController):
        native_config: dict[str, Any] = {"gateways": [{"uid": "gw-1"}]}
        normalized_config: dict[str, Any] = {"policies": [], "gateways": []}

        normalize_nat_rules(native_config, import_state_controller.state, normalized_config)

        assert normalized_config["policies"] == []

    def test_returns_early_when_nat_rulebases_empty(self, import_state_controller: ImportStateController):
        native_config: dict[str, Any] = {"nat_rulebases": [], "gateways": [{"uid": "gw-1"}]}
        normalized_config: dict[str, Any] = {"policies": [], "gateways": []}

        normalize_nat_rules(native_config, import_state_controller.state, normalized_config)

        assert normalized_config["policies"] == []

    def test_processes_each_gateway(self, import_state_controller: ImportStateController):
        nat_rule = _make_nat_rule("rule-8")
        nat_rule["rule-number"] = 1
        native_config: dict[str, Any] = {
            "nat_rulebases": [{"nat_rule_chunks": [{"rulebase": [nat_rule]}], "policy_uid": "rb-access"}],
            "gateways": [{"uid": "gw-1"}],
            "policies": [],
        }
        normalized_config: dict[str, Any] = {
            "policies": [],
            "gateways": [
                {
                    "Uid": "gw-1",
                    "RulebaseLinks": [{"is_initial": True, "link_type": "policy", "to_rulebase_uid": "rb-access"}],
                }
            ],
        }

        normalize_nat_rules(native_config, import_state_controller.state, normalized_config)

        assert len(normalized_config["policies"]) == 1
        nat_rulebase = normalized_config["policies"][0]
        assert set(nat_rulebase.rules.keys()) == {"rule-8", "rule-8_translated"}

    def test_does_not_leak_nat_rules_between_gateways_on_different_policies(
        self, import_state_controller: ImportStateController
    ):
        rule_gw1 = _make_nat_rule("rule-gw1")
        rule_gw1["rule-number"] = 1
        rule_gw2 = _make_nat_rule("rule-gw2")
        rule_gw2["rule-number"] = 1
        native_config: dict[str, Any] = {
            "nat_rulebases": [
                {"nat_rule_chunks": [{"rulebase": [rule_gw1]}], "policy_uid": "policy-1"},
                {"nat_rule_chunks": [{"rulebase": [rule_gw2]}], "policy_uid": "policy-2"},
            ],
            "gateways": [{"uid": "gw-1"}, {"uid": "gw-2"}],
            "policies": [],
        }
        normalized_config: dict[str, Any] = {
            "policies": [],
            "gateways": [
                {
                    "Uid": "gw-1",
                    "RulebaseLinks": [{"is_initial": True, "link_type": "policy", "to_rulebase_uid": "policy-1"}],
                },
                {
                    "Uid": "gw-2",
                    "RulebaseLinks": [{"is_initial": True, "link_type": "policy", "to_rulebase_uid": "policy-2"}],
                },
            ],
        }

        normalize_nat_rules(native_config, import_state_controller.state, normalized_config)

        nat_rulebase_gw1 = next(rb for rb in normalized_config["policies"] if rb.uid == "nat-rulebase-gw-1")
        nat_rulebase_gw2 = next(rb for rb in normalized_config["policies"] if rb.uid == "nat-rulebase-gw-2")

        assert set(nat_rulebase_gw1.rules.keys()) == {"rule-gw1", "rule-gw1_translated"}
        assert set(nat_rulebase_gw2.rules.keys()) == {"rule-gw2", "rule-gw2_translated"}


class TestFilterNatRulebasesForGateway:
    def _make_normalized_config_with_gateway(
        self, gateway_uid: str, rulebase_links: list[dict[str, Any]]
    ) -> dict[str, Any]:
        return {
            "gateways": [
                {
                    "Uid": gateway_uid,
                    "RulebaseLinks": rulebase_links,
                }
            ]
        }

    def test_only_returns_rulebases_matching_gateway_policy(self):
        gateway = {"uid": "gw-1"}
        normalized_config = self._make_normalized_config_with_gateway(
            "gw-1",
            [{"is_initial": True, "link_type": "policy", "to_rulebase_uid": "policy-1"}],
        )
        native_nat_rulebases: list[dict[str, Any]] = [
            {"nat_rule_chunks": [], "policy_uid": "policy-1"},
            {"nat_rule_chunks": [], "policy_uid": "policy-2"},
        ]

        result = filter_nat_rulebases_for_gateway(gateway, native_nat_rulebases, normalized_config)

        assert result == [{"nat_rule_chunks": [], "policy_uid": "policy-1"}]

    def test_returns_empty_list_when_no_initial_link(self):
        gateway = {"uid": "gw-1"}
        normalized_config = self._make_normalized_config_with_gateway("gw-1", [])
        native_nat_rulebases: list[dict[str, Any]] = [{"nat_rule_chunks": [], "policy_uid": "policy-1"}]

        result = filter_nat_rulebases_for_gateway(gateway, native_nat_rulebases, normalized_config)

        assert result == []

    def test_returns_empty_list_when_gateway_not_found(self):
        gateway = {"uid": "unknown-gw"}
        normalized_config = self._make_normalized_config_with_gateway(
            "gw-1", [{"is_initial": True, "link_type": "policy", "to_rulebase_uid": "policy-1"}]
        )
        native_nat_rulebases: list[dict[str, Any]] = [{"nat_rule_chunks": [], "policy_uid": "policy-1"}]

        result = filter_nat_rulebases_for_gateway(gateway, native_nat_rulebases, normalized_config)

        assert result == []

import unittest.mock

from fw_modules.checkpointR8x.cp_nat import (
    get_initial_nat_rulebase_link,
    insert_parent_nat_rulebase,
    insert_rulebase_link,
    parse_nat_rule_transform,
)
from models.rulebase import Rulebase


def _make_nat_rule(uid: str = "rule-uid-1") -> dict:
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

    def test_missing_time_field_defaults_to_empty_string(self):
        nat_rule = _make_nat_rule()
        del nat_rule["time"]
        in_rule, _ = parse_nat_rule_transform(nat_rule)

        assert in_rule["time"] == ""


class TestInsertRulebaseLink:
    def _make_gateway(self) -> dict:
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
    def _make_import_state(self, mgm_uid: str = "mgm-uid-1") -> object:
        mgm_details = unittest.mock.MagicMock()
        mgm_details.uid = mgm_uid
        import_state = unittest.mock.MagicMock()
        import_state.mgm_details = mgm_details
        return import_state

    def test_creates_nat_rulebase_when_missing(self):
        import_state = self._make_import_state()
        normalized_config = {"policies": []}
        gateway = {"uid": "gw-1"}

        result = insert_parent_nat_rulebase(gateway, import_state, normalized_config)

        assert result.uid == "nat-rulebase-gw-1"
        assert result.name == "NAT"
        assert len(normalized_config["policies"]) == 1

    def test_returns_existing_nat_rulebase_without_duplicate(self):
        import_state = self._make_import_state()
        existing = Rulebase(uid="nat-rulebase-gw-2", name="NAT", mgm_uid="mgm-uid-1")
        normalized_config = {"policies": [existing]}
        gateway = {"uid": "gw-2"}

        result = insert_parent_nat_rulebase(gateway, import_state, normalized_config)

        assert result is existing
        assert len(normalized_config["policies"]) == 1


class TestGetInitialNatRulebaseLink:
    def _make_normalized_config_with_gateway(self, gateway_uid: str, rulebase_links: list) -> dict:
        return {
            "gateways": [
                {
                    "Uid": gateway_uid,
                    "RulebaseLinks": rulebase_links,
                }
            ]
        }

    def test_returns_initial_ordered_link(self):
        gateway = {"uid": "gw-1"}
        normalized_config = self._make_normalized_config_with_gateway(
            "gw-1",
            [
                {"is_initial": True, "link_type": "ordered", "to_rulebase_uid": "rb-access"},
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

    def test_returns_none_when_no_initial_ordered_link(self):
        gateway = {"uid": "gw-1"}
        normalized_config = self._make_normalized_config_with_gateway(
            "gw-1",
            [
                {"is_initial": False, "link_type": "ordered", "to_rulebase_uid": "rb-1"},
                {"is_initial": True, "link_type": "nat", "to_rulebase_uid": "rb-nat"},
            ],
        )

        result = get_initial_nat_rulebase_link(gateway, normalized_config)

        assert result is None

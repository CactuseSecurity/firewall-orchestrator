from model_controllers.fwconfig_import_rule import FwConfigImportRule
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from pytest_mock import MockerFixture
from test.utils.test_utils import mock_get_graphql_code


def build_normalized_rule(rule_uid: str, *, rule_src_zone: str | None, rule_dst_zone: str | None) -> RuleNormalized:
    return RuleNormalized(
        rule_num_numeric=1.0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src="src",
        rule_src_refs="src",
        rule_dst_neg=False,
        rule_dst="dst",
        rule_dst_refs="dst",
        rule_svc_neg=False,
        rule_svc="svc",
        rule_svc_refs="svc",
        rule_src_zone=rule_src_zone,
        rule_dst_zone=rule_dst_zone,
        rule_time="time",
        rule_action=RuleAction.ACCEPT,
        rule_track=RuleTrack.NONE,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        rule_uid=rule_uid,
    )


class TestFwConfigImportRule:
    def test_is_change_security_relevant_detects_zone_change(
        self,
        fwconfig_import_rule: FwConfigImportRule,
    ):
        # A change that only differs in the src/dst zone text must stay security-relevant
        # (zones were made security-relevant in 9.1.10).
        old_rule = build_normalized_rule("rule-uid", rule_src_zone="zoneA", rule_dst_zone="zoneB")
        new_rule = build_normalized_rule("rule-uid", rule_src_zone="zoneC", rule_dst_zone="zoneB")

        assert fwconfig_import_rule.is_change_security_relevant(old_rule, new_rule) is True

    def test_prepare_rule_for_import_populates_zone_text(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
    ):
        # The retained rule_src_zone/rule_dst_zone text columns must keep being written on import.
        fwconfig_import_rule.uid2id_mapper = mocker.Mock()
        fwconfig_import_rule.uid2id_mapper.get_rulebase_id.return_value = 1
        fwconfig_import_rule.import_details = mocker.Mock()
        fwconfig_import_rule.import_details.state.mgm_details.current_mgm_id = 1
        fwconfig_import_rule.import_details.state.import_id = 1
        fwconfig_import_rule.import_details.state.lookup_action.return_value = 1
        fwconfig_import_rule.import_details.state.lookup_track.return_value = 1

        normalized_rule = build_normalized_rule("rule-uid", rule_src_zone="src_zone", rule_dst_zone="dst_zone")

        prepared = fwconfig_import_rule.prepare_rule_for_import(normalized_rule, "rulebase-uid")

        assert prepared.rule_src_zone == "src_zone"
        assert prepared.rule_dst_zone == "dst_zone"

    def test_write_changelog_rules_changed_rule_writes_new_and_old_ids(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_get_graphql_code(mocker, "mutation { dummy }")

        rule_uid = "changed-rule-uid"
        old_rule_id = 101
        new_rule_id = 202

        def get_rule_id_side_effect(_uid: str, before_update: bool = False) -> int:
            return old_rule_id if before_update else new_rule_id

        fwconfig_import_rule.uid2id_mapper.get_rule_id = mocker.Mock(side_effect=get_rule_id_side_effect)
        fwconfig_import_rule.is_change_security_relevant = mocker.Mock(return_value=True)
        fwconfig_import_rule.import_details.api_call.call = mocker.Mock(return_value={"data": {}})

        old_rule = mocker.Mock(rule_uid=rule_uid)
        new_rule = mocker.Mock(rule_uid=rule_uid)

        # Act
        fwconfig_import_rule.write_changelog_rules(
            added_rules=[],
            removed_rules=[],
            changed_rules=[(old_rule, new_rule)],
        )

        # Assert
        fwconfig_import_rule.import_details.api_call.call.assert_called_once()
        query_variables = fwconfig_import_rule.import_details.api_call.call.call_args.kwargs["query_variables"]
        rule_changes = query_variables["rule_changes"]

        assert len(rule_changes) == 1
        assert rule_changes[0]["change_action"] == "C"
        assert rule_changes[0]["new_rule_id"] == new_rule_id
        assert rule_changes[0]["old_rule_id"] == old_rule_id

    def test_write_changelog_rules_uses_current_mgm_id_for_sub_management(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_get_graphql_code(mocker, "mutation { dummy }")

        fwconfig_import_rule.import_details.state.mgm_details.mgm_id = 3
        fwconfig_import_rule.import_details.state.mgm_details.current_mgm_id = 7
        fwconfig_import_rule.uid2id_mapper.get_rule_id = mocker.Mock(return_value=202)
        fwconfig_import_rule.import_details.api_call.call = mocker.Mock(return_value={"data": {}})

        added_rule = mocker.Mock(rule_uid="added-rule-uid")

        # Act
        fwconfig_import_rule.write_changelog_rules(
            added_rules=[added_rule],
            removed_rules=[],
            changed_rules=[],
        )

        # Assert
        fwconfig_import_rule.import_details.api_call.call.assert_called_once()
        query_variables = fwconfig_import_rule.import_details.api_call.call.call_args.kwargs["query_variables"]
        rule_changes = query_variables["rule_changes"]

        assert len(rule_changes) == 1
        assert rule_changes[0]["change_action"] == "I"
        assert rule_changes[0]["mgm_id"] == fwconfig_import_rule.import_details.state.mgm_details.current_mgm_id
        assert rule_changes[0]["mgm_id"] != fwconfig_import_rule.import_details.state.mgm_details.mgm_id

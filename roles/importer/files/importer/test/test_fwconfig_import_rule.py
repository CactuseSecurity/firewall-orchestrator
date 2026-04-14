from model_controllers.fwconfig_import_rule import FwConfigImportRule
from pytest_mock import MockerFixture
from states.import_state import ImportState
from states.management_state import ManagementState
from test.utils.test_utils import mock_get_graphql_code


class TestFwConfigImportRule:
    def test_write_changelog_rules_changed_rule_writes_new_and_old_ids(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
        import_state: ImportState,
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
        import_state.fwo_api.call = mocker.Mock(return_value={"data": {}})

        old_rule = mocker.Mock(rule_uid=rule_uid)
        new_rule = mocker.Mock(rule_uid=rule_uid)

        # Act
        fwconfig_import_rule.write_changelog_rules(
            added_rules=[],
            removed_rules=[],
            changed_rules=[(old_rule, new_rule)],
        )

        # Assert
        import_state.fwo_api.call.assert_called_once()
        query_variables = import_state.fwo_api.call.call_args.kwargs["query_variables"]
        rule_changes = query_variables["rule_changes"]

        assert len(rule_changes) == 1
        assert rule_changes[0]["change_action"] == "C"
        assert rule_changes[0]["new_rule_id"] == new_rule_id
        assert rule_changes[0]["old_rule_id"] == old_rule_id

    def test_write_changelog_rules_uses_current_mgm_id_for_sub_management(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        mock_get_graphql_code(mocker, "mutation { dummy }")

        import_state.mgm_details.mgm_id = 3
        management_state.mgm_id = 7
        fwconfig_import_rule.uid2id_mapper.get_rule_id = mocker.Mock(return_value=202)
        import_state.fwo_api.call = mocker.Mock(return_value={"data": {}})

        added_rule = mocker.Mock(rule_uid="added-rule-uid")

        # Act
        fwconfig_import_rule.write_changelog_rules(
            added_rules=[added_rule],
            removed_rules=[],
            changed_rules=[],
        )

        # Assert
        import_state.fwo_api.call.assert_called_once()
        query_variables = import_state.fwo_api.call.call_args.kwargs["query_variables"]
        rule_changes = query_variables["rule_changes"]

        assert len(rule_changes) == 1
        assert rule_changes[0]["change_action"] == "I"
        assert rule_changes[0]["mgm_id"] != management_state.mgm_id
        assert rule_changes[0]["mgm_id"] == import_state.mgm_details.mgm_id

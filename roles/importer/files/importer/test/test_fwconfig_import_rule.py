from model_controllers.fwconfig_import_rule import FwConfigImportRule
from pytest_mock import MockerFixture
from test.utils.test_utils import mock_get_graphql_code


class TestFwConfigImportRule:
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

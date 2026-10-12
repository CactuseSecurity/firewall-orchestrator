from typing import Any

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture
from test.utils.test_utils import mock_get_graphql_code


def unlock_and_get_query_variables(
    import_state_controller: ImportStateController,
    api_connection: FwoApi,
    mocker: MockerFixture,
) -> dict[str, Any]:
    mock_get_graphql_code(mocker, "mutation { dummy }")
    api_connection.call = mocker.Mock(return_value={"data": {"update_import_control": {"affected_rows": 1}}})

    FwoApiCall(api_connection).unlock_import(import_state_controller.state, success=True)

    return api_connection.call.call_args.kwargs["query_variables"]


class TestUnlockImport:
    def test_documentation_only_rule_change_sets_policy_changes_found_without_security_relevant_count(
        self,
        import_state_controller: ImportStateController,
        api_connection: FwoApi,
        mocker: MockerFixture,
    ) -> None:
        # policy_changes_found has to cover every new rule version (the rule_owner prefilter of the
        # variance analysis relies on it), while security_relevant_changes_counter drives the rule
        # change notification and must stay 0 for documentation-only changes.
        import_state_controller.state.stats.increment_rule_change_count()

        query_variables = unlock_and_get_query_variables(import_state_controller, api_connection, mocker)

        assert query_variables["changesFound"] is True
        assert query_variables["policyChangesFound"] is True
        assert query_variables["changeNumber"] == 0

    def test_security_relevant_rule_change_is_counted(
        self,
        import_state_controller: ImportStateController,
        api_connection: FwoApi,
        mocker: MockerFixture,
    ) -> None:
        import_state_controller.state.stats.increment_rule_change_count(2)
        import_state_controller.state.stats.increment_rule_change_count_security_relevant()

        query_variables = unlock_and_get_query_variables(import_state_controller, api_connection, mocker)

        assert query_variables["policyChangesFound"] is True
        assert query_variables["changeNumber"] == 1

    def test_object_only_change_sets_no_policy_changes(
        self,
        import_state_controller: ImportStateController,
        api_connection: FwoApi,
        mocker: MockerFixture,
    ) -> None:
        import_state_controller.state.stats.statistics.network_object_change_count = 1

        query_variables = unlock_and_get_query_variables(import_state_controller, api_connection, mocker)

        assert query_variables["changesFound"] is True
        assert query_variables["policyChangesFound"] is False
        assert query_variables["changeNumber"] == 0

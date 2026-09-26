from __future__ import annotations

import json
import time
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiFailedLockImportError, FwoImporterError
from models.import_state import ImportState
from test.utils.test_utils import mock_get_graphql_code

if TYPE_CHECKING:
    from model_controllers.management_controller import ManagementController
    from pytest_mock import MockerFixture

GRAPHQL_CODE = "query stub { stub }"
MGM_ID = 3
IMPORT_ID = 42
IMPORT_LOCK_ALERT_CODE = 15
IMPORT_ERROR_ALERT_CODE = 14


@pytest.fixture
def api() -> MagicMock:
    return MagicMock(spec=FwoApi)


@pytest.fixture
def fwo_api_call(api: MagicMock, mocker: MockerFixture) -> FwoApiCall:
    mock_get_graphql_code(mocker, GRAPHQL_CODE)
    return FwoApiCall(api)


@pytest.fixture
def import_state(management_controller: ManagementController) -> ImportState:
    state = ImportState()
    state.mgm_details = management_controller
    state.import_id = IMPORT_ID
    return state


def _passed_variables(api: MagicMock, call_index: int = -1) -> dict[str, Any]:
    return api.call.call_args_list[call_index].kwargs["query_variables"]


class TestManagementAndConfigQueries:
    def test_get_mgm_ids_returns_ids(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"management": [{"id": 1}, {"id": 5}]}}

        assert fwo_api_call.get_mgm_ids() == [1, 5]
        assert _passed_variables(api) == {}

    def test_get_mgm_ids_returns_empty_list_without_data(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"errors": []}

        assert fwo_api_call.get_mgm_ids({"x": [1]}) == []

    def test_get_config_value_returns_first_value(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"config": [{"config_value": "True"}]}}

        assert fwo_api_call.get_config_value("importCheckCertificates") == "True"
        assert _passed_variables(api) == {"key": "importCheckCertificates"}

    def test_get_config_value_returns_none_without_config_value(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"config": [{"config_key": "x"}]}}

        assert fwo_api_call.get_config_value() is None

    def test_get_config_value_returns_none_without_data(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {}

        assert fwo_api_call.get_config_value() is None

    def test_get_config_value_returns_none_on_api_error(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.side_effect = FwoImporterError("down")

        assert fwo_api_call.get_config_value() is None

    def test_get_config_values_returns_key_value_map(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {
            "data": {"config": [{"config_key": "a", "config_value": "1"}, {"config_key": "b", "config_value": "2"}]}
        }

        assert fwo_api_call.get_config_values("import") == {"a": "1", "b": "2"}
        assert _passed_variables(api) == {"keyFilter": "import%"}

    def test_get_config_values_returns_none_without_data(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {}

        assert fwo_api_call.get_config_values() is None

    def test_get_config_values_returns_none_on_api_error(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.side_effect = FwoImporterError("down")

        assert fwo_api_call.get_config_values() is None

    def test_log_import_attempt_passes_management_and_success(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {}}

        assert fwo_api_call.log_import_attempt(MGM_ID, successful=True) == {"data": {}}
        variables = _passed_variables(api)
        assert variables["mgmId"] == MGM_ID
        assert variables["success"] is True
        assert "timeStamp" in variables

    def test_call_delegates_to_api(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"x": 1}}

        assert fwo_api_call.call("q", {"a": 1}, analyze_payload=True) == {"data": {"x": 1}}
        api.call.assert_called_once_with("q", {"a": 1}, True)


class TestImportLock:
    def test_set_import_lock_returns_new_import_id(
        self, fwo_api_call: FwoApiCall, api: MagicMock, management_controller: ManagementController
    ) -> None:
        api.call.return_value = {"data": {"insert_import_control": {"returning": [{"control_id": IMPORT_ID}]}}}

        assert fwo_api_call.set_import_lock(management_controller, is_initial_import=0) == IMPORT_ID
        assert _passed_variables(api) == {"mgmId": MGM_ID, "importTypeId": 1, "isInitialImport": 0}

    def test_set_import_lock_keeps_default_for_empty_control_id(
        self, fwo_api_call: FwoApiCall, api: MagicMock, management_controller: ManagementController
    ) -> None:
        api.call.return_value = {"data": {"insert_import_control": {"returning": [{"control_id": 0}]}}}

        assert fwo_api_call.set_import_lock(management_controller, is_initial_import=1) == -1

    def test_set_import_lock_failure_creates_issue_and_alert(
        self,
        fwo_api_call: FwoApiCall,
        api: MagicMock,
        management_controller: ManagementController,
        mocker: MockerFixture,
    ) -> None:
        api.call.return_value = {"errors": [{"message": "locked"}]}
        mock_issue = mocker.patch.object(fwo_api_call, "create_data_issue")
        mock_alert = mocker.patch.object(fwo_api_call, "set_alert")

        with pytest.raises(FwoApiFailedLockImportError):
            fwo_api_call.set_import_lock(management_controller, is_initial_import=0)

        mock_issue.assert_called_once()
        assert mock_alert.call_args.kwargs["alert_code"] == IMPORT_LOCK_ALERT_CODE
        assert mock_alert.call_args.kwargs["mgm_details"] is management_controller

    def test_unlock_import_sends_statistics(
        self, fwo_api_call: FwoApiCall, api: MagicMock, import_state: ImportState
    ) -> None:
        import_state.stats.statistics.rule_add_count = 2
        api.call.return_value = {"data": {"update_import_control": {"affected_rows": 1}}}

        fwo_api_call.unlock_import(import_state, success=True)

        variables = _passed_variables(api)
        assert variables["importId"] == IMPORT_ID
        assert variables["success"] is True
        assert variables["changesFound"] is True
        assert variables["policyChangesFound"] is True
        assert variables["changeNumber"] == 2

    def test_unlock_import_logs_api_errors(
        self, fwo_api_call: FwoApiCall, api: MagicMock, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mock_exception = mocker.patch("fwo_api_call.FWOLogger.exception")
        api.call.return_value = {"errors": [{"message": "no"}]}

        fwo_api_call.unlock_import(import_state, success=False)

        assert "failed to unlock import" in mock_exception.call_args.args[0]


class TestChangeCounting:
    def test_count_rule_changes_per_import(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"changelog_rule_aggregate": {"aggregate": {"count": "7"}}}}

        assert fwo_api_call.count_rule_changes_per_import(IMPORT_ID) == 7

    def test_count_rule_changes_per_import_returns_zero_on_error(
        self, fwo_api_call: FwoApiCall, api: MagicMock
    ) -> None:
        api.call.return_value = {}

        assert fwo_api_call.count_rule_changes_per_import(IMPORT_ID) == 0

    def test_count_any_changes_per_import_sums_all_changelogs(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {
            "data": {
                "changelog_object_aggregate": {"aggregate": {"count": 1}},
                "changelog_service_aggregate": {"aggregate": {"count": 2}},
                "changelog_user_aggregate": {"aggregate": {"count": 3}},
                "changelog_rule_aggregate": {"aggregate": {"count": 4}},
            }
        }

        assert fwo_api_call.count_any_changes_per_import(IMPORT_ID) == 10

    def test_count_any_changes_per_import_returns_zero_on_error(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.side_effect = FwoImporterError("down")

        assert fwo_api_call.count_any_changes_per_import(IMPORT_ID) == 0


class TestImportConfigTable:
    def test_import_json_config_passes_config(
        self, fwo_api_call: FwoApiCall, api: MagicMock, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mock_exception = mocker.patch("fwo_api_call.FWOLogger.exception")
        api.call.return_value = {"data": {"insert_import_config": {"affected_rows": 1}}}
        config = MagicMock()

        fwo_api_call.import_json_config(import_state, config, start_import=True)

        variables = _passed_variables(api)
        assert variables["config"] is config
        assert variables["mgmId"] == MGM_ID
        assert variables["importId"] == IMPORT_ID
        assert variables["start_import_flag"] is True
        mock_exception.assert_not_called()

    def test_import_json_config_logs_graphql_errors(
        self, fwo_api_call: FwoApiCall, api: MagicMock, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mock_exception = mocker.patch("fwo_api_call.FWOLogger.exception")
        api.call.return_value = {"errors": ["invalid"]}

        fwo_api_call.import_json_config(import_state, MagicMock(), start_import=False)

        assert "error while writing importable config" in mock_exception.call_args.args[0]

    def test_import_json_config_logs_exceptions(
        self, fwo_api_call: FwoApiCall, api: MagicMock, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mock_exception = mocker.patch("fwo_api_call.FWOLogger.exception")
        api.call.side_effect = FwoImporterError("down")

        fwo_api_call.import_json_config(import_state, MagicMock(), start_import=False)

        assert "failed to write normalized config" in mock_exception.call_args.args[0]

    def test_delete_json_config_in_import_table(
        self, fwo_api_call: FwoApiCall, api: MagicMock, mocker: MockerFixture
    ) -> None:
        mock_exception = mocker.patch("fwo_api_call.FWOLogger.exception")
        api.call.return_value = {"data": {"delete_import_config": {"affected_rows": 1}}}

        fwo_api_call.delete_json_config_in_import_table({"importId": IMPORT_ID})

        mock_exception.assert_not_called()

    def test_delete_json_config_in_import_table_logs_errors(
        self, fwo_api_call: FwoApiCall, api: MagicMock, mocker: MockerFixture
    ) -> None:
        mock_exception = mocker.patch("fwo_api_call.FWOLogger.exception")
        api.call.return_value = {}

        fwo_api_call.delete_json_config_in_import_table({"importId": IMPORT_ID})

        mock_exception.assert_called_once()

    def test_get_error_string_from_imp_control(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"import_control": [{"import_errors": "err"}]}}

        assert fwo_api_call.get_error_string_from_imp_control(MagicMock(), {"importId": IMPORT_ID}) == [
            {"import_errors": "err"}
        ]


class TestDataIssues:
    @pytest.mark.parametrize("obj_name", ["all", "Original"])
    def test_create_data_issue_ignores_enriched_objects(
        self, obj_name: str, fwo_api_call: FwoApiCall, api: MagicMock
    ) -> None:
        fwo_api_call.create_data_issue(obj_name=obj_name)

        api.call.assert_not_called()

    def test_create_data_issue_passes_all_given_fields(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"insert_log_data_issue": {"returning": [{"id": 1}]}}}

        fwo_api_call.create_data_issue(
            obj_name="obj",
            mgm_id=MGM_ID,
            dev_id=4,
            severity=2,
            rule_uid="rule-uid",
            object_type="network",
            description="broken ref",
        )

        assert _passed_variables(api) == {
            "source": "import",
            "severity": 2,
            "devId": 4,
            "mgmId": MGM_ID,
            "objectName": "obj",
            "objectType": "network",
            "ruleUid": "rule-uid",
            "description": "broken ref",
        }

    def test_create_data_issue_warns_on_unexpected_result(
        self, fwo_api_call: FwoApiCall, api: MagicMock, mocker: MockerFixture
    ) -> None:
        mock_warning = mocker.patch("fwo_api_call.FWOLogger.warning")
        api.call.return_value = {"data": {"insert_log_data_issue": {"returning": []}}}

        fwo_api_call.create_data_issue(description="x")

        mock_warning.assert_called_once()

    def test_create_data_issue_logs_api_errors(
        self, fwo_api_call: FwoApiCall, api: MagicMock, mocker: MockerFixture
    ) -> None:
        mock_error = mocker.patch("fwo_api_call.FWOLogger.error")
        api.call.side_effect = FwoImporterError("down")

        fwo_api_call.create_data_issue(description="x")

        assert "failed to create log_data_issue" in mock_error.call_args.args[0]


class TestAlerts:
    def test_set_alert_without_alert_code_does_not_acknowledge(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"insert_alert": {"returning": [{"newIdLong": 9}]}}}

        fwo_api_call.set_alert(title="t", dev_id=1, user_id=2, ref_alert="r", description="d", json_data={"k": "v"})

        api.call.assert_called_once()
        variables = _passed_variables(api)
        assert variables["devId"] == 1
        assert variables["userId"] == 2
        assert variables["refAlert"] == "r"
        assert json.loads(variables["jsonData"]) == {"k": "v", "severity": 1}

    def test_set_alert_acknowledges_older_alerts(
        self, fwo_api_call: FwoApiCall, api: MagicMock, management_controller: ManagementController
    ) -> None:
        api.call.side_effect = [
            {"data": {"insert_alert": {"returning": [{"newIdLong": 9}]}}},
            {"data": {"alert": [{"alert_id": 5}, {"other": 1}]}},
            {"data": {}},
        ]

        fwo_api_call.set_alert(
            import_id=IMPORT_ID,
            mgm_id=MGM_ID,
            alert_code=IMPORT_ERROR_ALERT_CODE,
            severity=None,
            mgm_details=management_controller,
        )

        assert api.call.call_count == 3
        add_variables = _passed_variables(api, 0)
        assert json.loads(add_variables["jsonData"]) == {"import_id": IMPORT_ID, "mgm_name": management_controller.name}
        assert _passed_variables(api, 1) == {"mgmId": MGM_ID, "alertCode": IMPORT_ERROR_ALERT_CODE, "currentAlertId": 9}
        assert _passed_variables(api, 2)["alertId"] == 5

    def test_set_alert_stops_without_existing_alert_data(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.side_effect = [{"data": {"insert_alert": {"returning": [{"newIdLong": 9}]}}}, {"errors": []}]

        fwo_api_call.set_alert(mgm_id=MGM_ID, alert_code=IMPORT_ERROR_ALERT_CODE)

        assert api.call.call_count == 2

    def test_set_alert_logs_and_reraises_api_errors(
        self, fwo_api_call: FwoApiCall, api: MagicMock, mocker: MockerFixture
    ) -> None:
        mock_error = mocker.patch("fwo_api_call.FWOLogger.error")
        api.call.return_value = {}

        with pytest.raises(KeyError):
            fwo_api_call.set_alert(title="t")

        assert "failed to create alert entry" in mock_error.call_args.args[0]


class TestCompleteImport:
    def test_complete_import_skips_when_not_responsible(
        self, fwo_api_call: FwoApiCall, api: MagicMock, import_state: ImportState
    ) -> None:
        import_state.responsible_for_importing = False

        fwo_api_call.complete_import(import_state)

        api.call.assert_not_called()

    def test_complete_import_success_logs_change_details(
        self, fwo_api_call: FwoApiCall, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mock_attempt = mocker.patch.object(fwo_api_call, "log_import_attempt")
        mock_unlock = mocker.patch.object(fwo_api_call, "unlock_import")
        mock_alert = mocker.patch.object(fwo_api_call, "set_alert")
        mock_info = mocker.patch("fwo_api_call.FWOLogger.info")
        mocker.patch.object(import_state.stats, "get_change_details", return_value={"rules": 1})
        import_state.start_time = int(time.time())

        fwo_api_call.complete_import(import_state)

        mock_attempt.assert_called_once_with(MGM_ID, successful=True)
        mock_unlock.assert_called_once_with(import_state, success=True)
        mock_alert.assert_not_called()
        message = mock_info.call_args.args[0]
        assert " successful," in message
        assert "change details" in message

    def test_complete_import_with_exception_creates_issue_and_alert(
        self, fwo_api_call: FwoApiCall, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mocker.patch.object(fwo_api_call, "log_import_attempt", side_effect=FwoImporterError("log failed"))
        mock_unlock = mocker.patch.object(fwo_api_call, "unlock_import")
        mock_issue = mocker.patch.object(fwo_api_call, "create_data_issue")
        mock_alert = mocker.patch.object(fwo_api_call, "set_alert")
        mock_info = mocker.patch("fwo_api_call.FWOLogger.info")

        fwo_api_call.complete_import(import_state, FwoImporterError("import failed"))

        mock_unlock.assert_called_once_with(import_state, success=False)
        mock_issue.assert_called_once_with(severity=1, description="import failed")
        assert mock_alert.call_args.kwargs["alert_code"] == IMPORT_ERROR_ALERT_CODE
        assert "threw errors" in mock_info.call_args.args[0]
        assert "ERRORS: import failed" in mock_info.call_args.args[0]

    def test_complete_import_uses_str_for_exceptions_without_message(
        self, fwo_api_call: FwoApiCall, import_state: ImportState, mocker: MockerFixture
    ) -> None:
        mocker.patch.object(fwo_api_call, "log_import_attempt")
        mocker.patch.object(fwo_api_call, "unlock_import")
        mock_issue = mocker.patch.object(fwo_api_call, "create_data_issue")
        mocker.patch.object(fwo_api_call, "set_alert")

        fwo_api_call.complete_import(import_state, ValueError("plain error"))

        mock_issue.assert_called_once_with(severity=1, description="plain error")


class TestLastCompleteImport:
    def test_get_last_complete_import_returns_id_and_date(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"import_control": [{"control_id": IMPORT_ID, "start_time": "2026-01-01"}]}}

        assert fwo_api_call.get_last_complete_import({"mgmId": MGM_ID}) == (IMPORT_ID, "2026-01-01")

    def test_get_last_complete_import_without_imports(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {"data": {"import_control": []}}

        assert fwo_api_call.get_last_complete_import({"mgmId": MGM_ID}) == (0, "")

    def test_get_last_complete_import_reraises_errors(self, fwo_api_call: FwoApiCall, api: MagicMock) -> None:
        api.call.return_value = {}

        with pytest.raises(KeyError):
            fwo_api_call.get_last_complete_import({"mgmId": MGM_ID})

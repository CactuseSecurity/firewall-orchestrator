from typing import Protocol, cast
from unittest.mock import MagicMock

import common
import fwo_globals
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import_rollback import FwConfigImportRollback
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture


class MockAssertions(Protocol):
    def assert_not_called(self) -> None: ...

    def assert_called_once(self) -> None: ...


class TestRollBackExceptionHandler:
    def test_genuine_failure_after_data_changes_keeps_import_record(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.rollback_required = True
        mocker.patch.object(fwo_globals, "shutdown_requested", new=False)
        mock_rollback = mocker.patch("common.FwConfigImportRollback")

        # Act
        common.roll_back_exception_handler(import_state_controller, config_importer=MagicMock(), exc=Exception("boom"))

        # Assert
        cast("MockAssertions", mock_rollback.return_value.rollback_current_import).assert_called_once()
        cast("MockAssertions", import_state_controller.delete_import).assert_not_called()

    def test_failure_before_data_changes_deletes_import_record(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.rollback_required = False
        mocker.patch.object(fwo_globals, "shutdown_requested", new=False)
        mocker.patch("common.FwConfigImportRollback")

        # Act
        common.roll_back_exception_handler(import_state_controller, config_importer=MagicMock(), exc=Exception("boom"))

        # Assert
        cast("MockAssertions", import_state_controller.delete_import).assert_called_once()

    def test_shutdown_after_data_changes_deletes_import_record(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.rollback_required = True
        mocker.patch.object(fwo_globals, "shutdown_requested", new=True)
        mocker.patch("common.FwConfigImportRollback")

        # Act
        common.roll_back_exception_handler(import_state_controller, config_importer=MagicMock(), exc=Exception("boom"))

        # Assert
        cast("MockAssertions", import_state_controller.delete_import).assert_called_once()


class TestRollbackCurrentImport:
    def test_uses_data_only_mutation_with_import_id_list(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.import_id = 99
        get_code = mocker.patch.object(FwoApi, "get_graphql_code", return_value="mutation")
        fwo_api_call = cast("FwoApiCall", MagicMock(spec=FwoApiCall))
        fwo_api_call.call = MagicMock(return_value={"data": {}})

        # Act
        FwConfigImportRollback().rollback_current_import(
            import_state=import_state_controller.state, fwo_api_call=fwo_api_call
        )

        # Assert
        requested_files = get_code.call_args.args[0]
        assert requested_files[0].endswith("import/rollbackImportData.graphql")
        fwo_api_call.call.assert_called_once()
        assert fwo_api_call.call.call_args.kwargs["query_variables"] == {"importIds": [99]}


class TestUnlockImportPersistsErrors:
    def test_import_errors_forwarded_to_mutation_variables(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.import_id = 42
        mocker.patch.object(FwoApi, "get_graphql_code", return_value="mutation")
        api = MagicMock(spec=FwoApi)
        api.call = MagicMock(return_value={"data": {"update_import_control": {"affected_rows": 1}}})
        fwo_api_call = FwoApiCall(api)

        # Act
        fwo_api_call.unlock_import(import_state_controller.state, success=False, import_errors="boom")

        # Assert
        query_variables = api.call.call_args.kwargs["query_variables"]
        assert query_variables["importErrors"] == "boom"
        assert query_variables["success"] is False


class TestCompleteImportForwardsErrors:
    def test_exception_message_is_written_as_import_errors(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        api = MagicMock(spec=FwoApi)
        fwo_api_call = FwoApiCall(api)
        unlock = mocker.patch.object(fwo_api_call, "unlock_import")
        mocker.patch.object(fwo_api_call, "log_import_attempt")
        mocker.patch.object(fwo_api_call, "create_data_issue")
        mocker.patch.object(fwo_api_call, "set_alert")

        # Act
        fwo_api_call.complete_import(import_state_controller.state, exception=Exception("boom"))

        # Assert
        unlock.assert_called_once()
        assert unlock.call_args.kwargs["success"] is False
        assert unlock.call_args.kwargs["import_errors"] == "boom"

    def test_successful_import_writes_no_import_errors(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        api = MagicMock(spec=FwoApi)
        fwo_api_call = FwoApiCall(api)
        unlock = mocker.patch.object(fwo_api_call, "unlock_import")
        mocker.patch.object(fwo_api_call, "log_import_attempt")

        # Act
        fwo_api_call.complete_import(import_state_controller.state, exception=None)

        # Assert
        unlock.assert_called_once()
        assert unlock.call_args.kwargs["success"] is True
        assert unlock.call_args.kwargs["import_errors"] is None

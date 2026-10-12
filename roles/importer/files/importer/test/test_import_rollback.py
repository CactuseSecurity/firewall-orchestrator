from typing import Protocol, cast
from unittest.mock import MagicMock

import common
import fwo_const
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
        # the data statements live in a shared fragment, which has to travel with the operation
        assert requested_files[1].endswith("import/fragments/rollbackImportDataFields.graphql")
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

    def test_empty_message_attribute_falls_back_to_str(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        api = MagicMock(spec=FwoApi)
        fwo_api_call = FwoApiCall(api)
        unlock = mocker.patch.object(fwo_api_call, "unlock_import")
        mocker.patch.object(fwo_api_call, "log_import_attempt")
        mocker.patch.object(fwo_api_call, "create_data_issue")
        mocker.patch.object(fwo_api_call, "set_alert")

        class EmptyMessageError(Exception):
            message = None

        # Act
        fwo_api_call.complete_import(import_state_controller.state, exception=EmptyMessageError("boom"))

        # Assert
        # an exception with an empty/None .message must still forward the reason via str(exception)
        unlock.assert_called_once()
        assert unlock.call_args.kwargs["success"] is False
        assert unlock.call_args.kwargs["import_errors"] == "boom"


class TestUnlockImportReportsLockRelease:
    """
    A kept import_control row whose stop_time was never stamped holds the per management import
    lock, so unlock_import has to report whether it really stamped the row.
    """

    @staticmethod
    def _build_api_call(mocker: MockerFixture, call_result: object) -> FwoApiCall:
        mocker.patch.object(FwoApi, "get_graphql_code", return_value="mutation")
        api = MagicMock(spec=FwoApi)
        if isinstance(call_result, Exception):
            api.call = MagicMock(side_effect=call_result)
        else:
            api.call = MagicMock(return_value=call_result)
        return FwoApiCall(api)

    def test_returns_true_when_row_was_stamped(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        fwo_api_call = self._build_api_call(mocker, {"data": {"update_import_control": {"affected_rows": 1}}})

        assert fwo_api_call.unlock_import(import_state_controller.state, success=False) is True

    def test_returns_false_when_no_row_was_stamped(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        fwo_api_call = self._build_api_call(mocker, {"data": {"update_import_control": {"affected_rows": 0}}})

        assert fwo_api_call.unlock_import(import_state_controller.state, success=False) is False

    def test_returns_false_when_api_is_unreachable(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        fwo_api_call = self._build_api_call(mocker, ConnectionError("api unreachable"))

        assert fwo_api_call.unlock_import(import_state_controller.state, success=False) is False

    def test_complete_import_propagates_failed_unlock(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        api = MagicMock(spec=FwoApi)
        fwo_api_call = FwoApiCall(api)
        mocker.patch.object(fwo_api_call, "unlock_import", return_value=False)
        mocker.patch.object(fwo_api_call, "log_import_attempt")
        mocker.patch.object(fwo_api_call, "create_data_issue")
        mocker.patch.object(fwo_api_call, "set_alert")

        # Act / Assert - the caller needs this to drop the row and release the import lock
        assert fwo_api_call.complete_import(import_state_controller.state, exception=Exception("boom")) is False

    def test_complete_import_reports_success_when_unlocked(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        api = MagicMock(spec=FwoApi)
        fwo_api_call = FwoApiCall(api)
        mocker.patch.object(fwo_api_call, "unlock_import", return_value=True)
        mocker.patch.object(fwo_api_call, "log_import_attempt")

        # Act / Assert
        assert fwo_api_call.complete_import(import_state_controller.state, exception=None) is True


class TestTruncateImportError:
    def test_keeps_short_error_unchanged(self) -> None:
        assert FwoApiCall.truncate_import_error("short") == "short"

    def test_keeps_none(self) -> None:
        assert FwoApiCall.truncate_import_error(None) is None

    def test_truncates_long_traceback(self) -> None:
        long_error = "x" * (fwo_const.MAX_IMPORT_ERROR_LENGTH + 500)

        truncated = FwoApiCall.truncate_import_error(long_error)

        assert truncated is not None
        assert truncated.endswith(" [truncated]")
        assert len(truncated) == fwo_const.MAX_IMPORT_ERROR_LENGTH + len(" [truncated]")

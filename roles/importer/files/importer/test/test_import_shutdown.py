from typing import Protocol, cast

from common import handle_shutdown_exception
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from pytest_mock import MockerFixture
from states.global_state import GlobalState
from states.import_state import ImportState


class MockAssertions(Protocol):
    def assert_not_called(self) -> None: ...

    def assert_called_once(self) -> None: ...


class TestHandleShutdownException:
    def test_shutdown_before_import_lock_skips_cleanup(
        self, mocker: MockerFixture, import_state: ImportState, global_state: GlobalState
    ) -> None:
        # Arrange
        import_state.import_id = -1
        import_state.rollback_required = False
        mock_delete_import = cast("MockAssertions", mocker.patch.object(import_state, "delete_import"))
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act
        handle_shutdown_exception(global_state, import_state, config_importer=None, exc=None)

        # Assert
        mock_delete_import.assert_not_called()
        mock_rollback.assert_not_called()

    def test_shutdown_before_import_data_changes_deletes_import_lock(
        self, mocker: MockerFixture, import_state: ImportState, global_state: GlobalState
    ) -> None:
        # Arrange
        import_state.import_id = 7
        import_state.rollback_required = False
        mock_delete_import = cast("MockAssertions", mocker.patch.object(import_state, "delete_import"))
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act
        handle_shutdown_exception(global_state, import_state, config_importer=None, exc=None)

        # Assert
        mock_delete_import.assert_called_once()
        mock_rollback.assert_not_called()

    def test_shutdown_after_import_data_changes_rolls_back(
        self, mocker: MockerFixture, import_state: ImportState, global_state: GlobalState
    ) -> None:
        # Arrange
        import_state.import_id = 7
        import_state.rollback_required = True
        mock_delete_import = cast("MockAssertions", mocker.patch.object(import_state, "delete_import"))
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act
        handle_shutdown_exception(global_state, import_state, config_importer=None, exc=None)

        # Assert
        mock_rollback.assert_called_once()
        mock_delete_import.assert_not_called()


class TestRollbackRequiredTracking:
    def test_import_state_does_not_require_rollback_by_default(self, import_state: ImportState) -> None:
        assert not import_state.rollback_required

    def test_import_management_set_marks_rollback_required(
        self, import_state: ImportState, global_state: GlobalState
    ) -> None:
        # Arrange
        config_importer = FwConfigImport()
        empty_config = FwConfigManagerListController.generate_empty_config()

        # Act
        config_importer.import_management_set(global_state, import_state, empty_config)

        # Assert
        assert import_state.rollback_required

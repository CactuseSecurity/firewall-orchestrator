from typing import Protocol, cast

from common import handle_shutdown_exception
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture
from services.service_provider import ServiceProvider


class MockAssertions(Protocol):
    def assert_not_called(self) -> None: ...

    def assert_called_once(self) -> None: ...


class TestHandleShutdownException:
    def test_shutdown_before_import_lock_skips_cleanup(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.import_id = -1
        import_state_controller.state.rollback_required = False
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act
        handle_shutdown_exception(import_state_controller)

        # Assert
        cast("MockAssertions", import_state_controller.delete_import).assert_not_called()
        mock_rollback.assert_not_called()

    def test_shutdown_before_import_data_changes_deletes_import_lock(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.import_id = 7
        import_state_controller.state.rollback_required = False
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act
        handle_shutdown_exception(import_state_controller)

        # Assert
        cast("MockAssertions", import_state_controller.delete_import).assert_called_once()
        mock_rollback.assert_not_called()

    def test_shutdown_after_import_data_changes_rolls_back(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        import_state_controller.state.import_id = 7
        import_state_controller.state.rollback_required = True
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act
        handle_shutdown_exception(import_state_controller)

        # Assert
        mock_rollback.assert_called_once()
        cast("MockAssertions", import_state_controller.delete_import).assert_not_called()


class TestRollbackRequiredTracking:
    def test_import_state_does_not_require_rollback_by_default(
        self, import_state_controller: ImportStateController
    ) -> None:
        assert not import_state_controller.state.rollback_required

    def test_import_management_set_marks_rollback_required(
        self, service_provider: ServiceProvider, import_state_controller: ImportStateController
    ) -> None:
        # Arrange
        config_importer = FwConfigImport()
        empty_config = FwConfigManagerListController.generate_empty_config()

        # Act
        config_importer.import_management_set(service_provider, empty_config)

        # Assert
        assert import_state_controller.state.rollback_required

from common import handle_shutdown_exception
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController


class TestHandleShutdownException:
    def test_shutdown_before_import_lock_skips_cleanup(self, mocker, import_state_controller):
        # Arrange
        import_state_controller.state.import_id = -1
        import_state_controller.state.rollback_required = False
        mock_rollback = mocker.patch("common.roll_back_exception_handler")

        # Act
        handle_shutdown_exception(import_state_controller)

        # Assert
        import_state_controller.delete_import.assert_not_called()
        mock_rollback.assert_not_called()

    def test_shutdown_before_import_data_changes_deletes_import_lock(self, mocker, import_state_controller):
        # Arrange
        import_state_controller.state.import_id = 7
        import_state_controller.state.rollback_required = False
        mock_rollback = mocker.patch("common.roll_back_exception_handler")

        # Act
        handle_shutdown_exception(import_state_controller)

        # Assert
        import_state_controller.delete_import.assert_called_once()
        mock_rollback.assert_not_called()

    def test_shutdown_after_import_data_changes_rolls_back(self, mocker, import_state_controller):
        # Arrange
        import_state_controller.state.import_id = 7
        import_state_controller.state.rollback_required = True
        mock_rollback = mocker.patch("common.roll_back_exception_handler")

        # Act
        handle_shutdown_exception(import_state_controller)

        # Assert
        mock_rollback.assert_called_once()
        import_state_controller.delete_import.assert_not_called()


class TestRollbackRequiredTracking:
    def test_import_state_does_not_require_rollback_by_default(self, import_state_controller):
        assert not import_state_controller.state.rollback_required

    def test_import_management_set_marks_rollback_required(self, service_provider, import_state_controller):
        # Arrange
        config_importer = FwConfigImport()
        empty_config = FwConfigManagerListController.generate_empty_config()

        # Act
        config_importer.import_management_set(service_provider, empty_config)

        # Assert
        assert import_state_controller.state.rollback_required

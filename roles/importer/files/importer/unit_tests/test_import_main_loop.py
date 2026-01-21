import fwo_globals
import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiLoginFailedError
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture
from unit_tests.utils.test_utils import mock_login

from importer.import_main_loop import get_fwo_jwt, wait_with_shutdown_check


class TestGetFwoJwt:
    def test_get_fwo_jwt_success(
        self,
        mocker: MockerFixture,
    ):
        # Arrange
        expected_value = "mocked_value"
        mock_login(mocker, return_value=expected_value)

        # Act
        jwt_token = get_fwo_jwt("mocked_username", "", "mocked_mgm_api")

        # Assert
        assert jwt_token == expected_value

    def test_get_fwo_jwt_failure(
        self,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")
        side_effect = FwoApiLoginFailedError("Login failed")
        mock_login(mocker, side_effect=side_effect)

        # Act
        jwt_token = get_fwo_jwt("mocked_username", "", "mocked_mgm_api")

        # Assert
        assert jwt_token is None
        call_args = mock_logger.call_args[0][0]
        assert call_args == "Login failed"

    def test_get_fwo_jwt_unexpected_exception(
        self,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")
        mock_login(mocker, side_effect=Exception("Unexpected error"))

        # Act & Assert
        jwt_token = get_fwo_jwt("mocked_username", "", "mocked_mgm_api")

        # Assert
        assert jwt_token is None
        logged_error_message = mock_logger.call_args[0][0]
        assert logged_error_message.startswith(
            "import_main_loop - unspecified error during FWO API login - skipping: Traceback"
        )


class TestWaitWithShutdownCheck:
    def test_wait_completes_without_shutdown(
        self,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_sleep = mocker.patch("import_main_loop.time.sleep")
        mock_logger = mocker.patch("fwo_log.FWOLogger")
        fwo_globals.shutdown_requested = False

        sleep_duration = 3

        # Execute
        wait_with_shutdown_check(sleep_duration)

        # Assert
        assert mock_sleep.call_count == sleep_duration
        mock_logger.info.assert_not_called()

    def test_shutdown_requested_immediately(
        self,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_sleep = mocker.patch("importer.import_main_loop.time.sleep")
        mock_logger = mocker.patch("importer.import_main_loop.FWOLogger")
        fwo_globals.shutdown_requested = True

        # Act
        with pytest.raises(SystemExit) as excinfo:
            wait_with_shutdown_check(5)

        # Assert
        assert "shutdown requested" in str(excinfo.value)
        mock_sleep.assert_not_called()
        mock_logger.info.assert_called_once()

    def test_shutdown_requested_during_loop(
        self,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_sleep = mocker.patch("importer.import_main_loop.time.sleep")
        mock_logger = mocker.patch("importer.import_main_loop.FWOLogger")
        fwo_globals.shutdown_requested = False

        def side_effect_simulating_external_shutdown(_: int):
            if mock_sleep.call_count >= 2:
                fwo_globals.shutdown_requested = True

        mock_sleep.side_effect = side_effect_simulating_external_shutdown

        # Act
        with pytest.raises(SystemExit) as excinfo:
            wait_with_shutdown_check(10)  # Requesting 10 seconds

        # Assert
        assert "shutdown requested" in str(excinfo.value)
        assert mock_sleep.call_count == 2
        mock_logger.info.assert_called_once()


class TestImportSingleManagement:
    def test_import_single_management_calls_wait_with_shutdown_check(
        self,
        mocker: MockerFixture,
        import_state_controller: ImportStateController,
        api_call: FwoApiCall,
        api_connection: FwoApi,
    ):
        # Arrange
        mock_wait = mocker.patch("importer.import_main_loop.wait_with_shutdown_check")
        mock_initialize_import = mocker.patch.object(
            ImportStateController,
            "initialize_import",
            return_value=import_state_controller,
        )
        mock_register_global_state = mocker.patch("importer.import_main_loop.register_global_state")
        # mock_get_mgm_details = mocker.patch.object(
        #    import_state_controller.mgm_details,
        #    "get_mgm_details",
        # return_value=import_state_controller.mgm_details,
        ##)

        # Act
        from importer.import_main_loop import import_single_management

        import_single_management(
            mgm_id=1,
            fwo_api_call=api_call,
            verify_certificates=True,
            api_fetch_limit=100,
            clear=False,
            suppress_certificate_warnings=False,
            jwt="mocked_jwt",
            force=False,
            fwo_major_version=9,
            sleep_timer=0,
            is_full_import=True,
            fwo_api=api_connection,
        )

        # Assert
        mock_wait.assert_called_once_with(0)
        mock_initialize_import.assert_called_once_with(1, "mocked_jwt", False, True, False, 9, False, True)
        mock_register_global_state.assert_called_once_with(import_state_controller)
        # mock_get_mgm_details.assert_called_once_with(api_connection, 1)

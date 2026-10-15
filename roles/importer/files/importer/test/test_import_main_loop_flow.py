from __future__ import annotations

import runpy
import sys
from pathlib import Path
from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import fwo_globals
import fwo_log
import import_main_loop
import pytest
from fwo_exceptions import FwLoginFailedError, FwoApiFailedLockImportError, FwoApiLoginFailedError, FwoImporterError
from import_main_loop import import_single_management, main, main_loop
from models.fwo_config import FwoConfig

if TYPE_CHECKING:
    from pytest_mock import MockerFixture

MGM_ID = 7
SUPPORTED_DEVICE_TYPE_ID = 9
UNSUPPORTED_DEVICE_TYPE_ID = 1
SLEEP_TIMER = 5
API_FETCH_LIMIT = 150
MAIN_LOOP_SCRIPT = Path(import_main_loop.__file__)


@pytest.fixture
def global_state_mock() -> MagicMock:
    global_state = MagicMock()
    global_state.fwo_config_controller.fwo_config = FwoConfig(
        fwo_api_url="https://api/graphql",
        fwo_user_mgmt_api_uri="https://middleware/",
        api_fetch_size=API_FETCH_LIMIT,
        sleep_timer=SLEEP_TIMER,
    )
    global_state.fwo_api_call.get_mgm_ids.return_value = [1, 2]
    global_state.fwo_api_call.get_config_value.return_value = None
    return global_state


@pytest.fixture
def single_mgm_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    return {
        "wait": mocker.patch("import_main_loop.wait_with_shutdown_check"),
        "mgm_details": mocker.patch(
            "import_main_loop.ManagementController.get_mgm_details",
            return_value={"id": MGM_ID, "deviceType": {"id": SUPPORTED_DEVICE_TYPE_ID}},
        ),
        "import_management": mocker.patch("import_main_loop.import_management"),
        "info": mocker.patch("import_main_loop.FWOLogger.info"),
        "error": mocker.patch("import_main_loop.FWOLogger.error"),
    }


class TestImportSingleManagement:
    def test_supported_management_is_imported(
        self, global_state_mock: MagicMock, single_mgm_mocks: dict[str, MagicMock]
    ) -> None:
        import_state = MagicMock()

        import_single_management(global_state_mock, import_state, MGM_ID)

        single_mgm_mocks["import_management"].assert_called_once_with(
            global_state=global_state_mock, import_state=import_state
        )

    def test_unsupported_device_type_is_skipped(
        self, global_state_mock: MagicMock, single_mgm_mocks: dict[str, MagicMock]
    ) -> None:
        single_mgm_mocks["mgm_details"].return_value = {"id": MGM_ID, "deviceType": {"id": UNSUPPORTED_DEVICE_TYPE_ID}}

        import_single_management(global_state_mock, MagicMock(), MGM_ID)

        single_mgm_mocks["import_management"].assert_not_called()

    def test_management_details_error_waits_and_skips(
        self, global_state_mock: MagicMock, single_mgm_mocks: dict[str, MagicMock]
    ) -> None:
        single_mgm_mocks["mgm_details"].side_effect = FwoImporterError("no details")

        import_single_management(global_state_mock, MagicMock(), MGM_ID)

        single_mgm_mocks["import_management"].assert_not_called()
        single_mgm_mocks["wait"].assert_called_with(SLEEP_TIMER)
        assert "error while getting FW management details" in single_mgm_mocks["error"].call_args.args[0]

    @pytest.mark.parametrize("error", [FwoApiFailedLockImportError("locked"), FwLoginFailedError("login")])
    def test_minor_errors_are_logged_as_info(
        self, error: Exception, global_state_mock: MagicMock, single_mgm_mocks: dict[str, MagicMock]
    ) -> None:
        single_mgm_mocks["import_management"].side_effect = error

        import_single_management(global_state_mock, MagicMock(), MGM_ID)

        assert "minor error while importing" in single_mgm_mocks["info"].call_args.args[0]
        single_mgm_mocks["error"].assert_not_called()

    def test_other_errors_are_logged_as_error(
        self, global_state_mock: MagicMock, single_mgm_mocks: dict[str, MagicMock]
    ) -> None:
        single_mgm_mocks["import_management"].side_effect = ValueError("boom")

        import_single_management(global_state_mock, MagicMock(), MGM_ID)

        assert "unspecific error while importing" in single_mgm_mocks["error"].call_args.args[0]


@pytest.fixture
def main_loop_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    mocker.patch("import_main_loop.FWOLogger.error")
    mocker.patch("import_main_loop.urllib3.disable_warnings")
    return {
        "wait": mocker.patch("import_main_loop.wait_with_shutdown_check"),
        "import_state": mocker.patch("import_main_loop.ImportState"),
        "single": mocker.patch("import_main_loop.import_single_management"),
    }


class TestMainLoop:
    def test_logs_in_imports_all_managements_and_sleeps(
        self, global_state_mock: MagicMock, main_loop_mocks: dict[str, MagicMock]
    ) -> None:
        main_loop(global_state_mock)

        global_state_mock.login_to_api.assert_called_once()
        assert main_loop_mocks["import_state"].call_count == 2
        assert [call.args[2] for call in main_loop_mocks["single"].call_args_list] == [1, 2]
        main_loop_mocks["wait"].assert_called_with(SLEEP_TIMER)

    def test_config_values_override_defaults(
        self, global_state_mock: MagicMock, main_loop_mocks: dict[str, MagicMock]
    ) -> None:
        config_values = {"fwApiElementsPerFetch": "500", "importSleepTime": "30"}

        def get_config_value(key: str) -> str | None:
            return config_values.get(key)

        global_state_mock.fwo_api_call.get_config_value.side_effect = get_config_value

        main_loop(global_state_mock)

        global_state_mock.fwo_config_controller.update_settings.assert_called_once_with(
            sleep_timer=30, api_fetch_size=500
        )
        main_loop_mocks["wait"].assert_called_with(30)

    def test_failed_login_waits_and_returns(
        self, global_state_mock: MagicMock, main_loop_mocks: dict[str, MagicMock]
    ) -> None:
        global_state_mock.login_to_api.side_effect = FwoApiLoginFailedError("fwo_api: JWT could not be parsed")

        main_loop(global_state_mock)

        main_loop_mocks["single"].assert_not_called()
        main_loop_mocks["wait"].assert_called_with(SLEEP_TIMER)

    def test_management_id_error_waits_and_returns(
        self, global_state_mock: MagicMock, main_loop_mocks: dict[str, MagicMock]
    ) -> None:
        global_state_mock.fwo_api_call.get_mgm_ids.side_effect = FwoImporterError("no ids")

        main_loop(global_state_mock)

        main_loop_mocks["single"].assert_not_called()
        main_loop_mocks["wait"].assert_called_with(SLEEP_TIMER)


@pytest.fixture
def isolated_globals(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", fwo_globals.verify_certs)
    monkeypatch.setattr(fwo_globals, "suppress_cert_warnings", fwo_globals.suppress_cert_warnings)
    monkeypatch.setattr(fwo_globals, "cli_verify_certs", fwo_globals.cli_verify_certs)
    monkeypatch.setattr(sys, "path", list(sys.path))


@pytest.mark.usefixtures("isolated_globals")
class TestMain:
    def test_main_runs_single_cycle_when_clearing(self, mocker: MockerFixture) -> None:
        mocker.patch("import_main_loop.FWOLogger")
        mock_global_state = mocker.patch("import_main_loop.GlobalState")
        mock_disable_warnings = mocker.patch("import_main_loop.urllib3.disable_warnings")
        mock_main_loop = mocker.patch("import_main_loop.main_loop")

        main(debug_level=0, verify_certificates_default=True, suppress_certificate_warnings=True, clear=True)

        mock_disable_warnings.assert_called_once()
        mock_main_loop.assert_called_once_with(global_state=mock_global_state.return_value)
        assert mock_global_state.call_args.kwargs["clear"] is True
        assert fwo_globals.verify_certs is True
        # the command line can only force certificate checking on
        assert fwo_globals.cli_verify_certs is True

    def test_main_loops_until_interrupted(self, mocker: MockerFixture) -> None:
        mocker.patch("import_main_loop.FWOLogger")
        mocker.patch("import_main_loop.GlobalState")
        mock_main_loop = mocker.patch("import_main_loop.main_loop", side_effect=[None, SystemExit("stop")])

        with pytest.raises(SystemExit):
            main(debug_level=0)

        assert mock_main_loop.call_count == 2

    def test_script_entry_point_parses_arguments(self, mocker: MockerFixture, monkeypatch: pytest.MonkeyPatch) -> None:
        # the script runs in a fresh namespace, so its dependencies are patched at their source modules
        mock_logger = mocker.patch.object(fwo_log, "FWOLogger")
        mocker.patch("states.global_state.GlobalState.__init__", side_effect=FwoImporterError("no config"))
        mock_set_global_values = mocker.patch.object(fwo_globals, "set_global_values")
        mock_disable_warnings = mocker.patch("urllib3.disable_warnings")
        monkeypatch.setattr(sys, "argv", [str(MAIN_LOOP_SCRIPT), "-d", "3", "-s", "-c"])

        # building the global state is the first step after argument handling: stop there
        with pytest.raises(FwoImporterError, match="no config"):
            runpy.run_path(str(MAIN_LOOP_SCRIPT), run_name="__main__")

        mock_logger.assert_called_once_with(3)
        # -v not given, -s given
        mock_set_global_values.assert_called_once_with(False, True)
        mock_disable_warnings.assert_called_once()

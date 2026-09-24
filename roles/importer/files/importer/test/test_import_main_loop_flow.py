from __future__ import annotations

import json
import runpy
import sys
from pathlib import Path
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import fwo_base
import fwo_const
import fwo_globals
import fwo_log
import import_main_loop
import pytest
from fwo_exceptions import FwLoginFailedError, FwoApiFailedLockImportError, FwoImporterError
from import_main_loop import import_single_management, main, main_loop

if TYPE_CHECKING:
    from pytest_mock import MockerFixture

MGM_ID = 7
SUPPORTED_DEVICE_TYPE_ID = 9
UNSUPPORTED_DEVICE_TYPE_ID = 1
SLEEP_TIMER = 5
API_FETCH_LIMIT = 150
FWO_MAJOR_VERSION = 9
TEST_JWT = "jwt-token"
IMPORTER_USER = "importer"
USER_MANAGEMENT_URL = "https://middleware/"
FWO_API_URL = "https://api/graphql"
MAIN_LOOP_SCRIPT = Path(import_main_loop.__file__)


def _run_single_management(api_call: MagicMock) -> None:
    import_single_management(
        MGM_ID,
        api_call,
        verify_certificates=False,
        api_fetch_limit=API_FETCH_LIMIT,
        clear=False,
        suppress_certificate_warnings=True,
        force=False,
        fwo_major_version=FWO_MAJOR_VERSION,
        sleep_timer=SLEEP_TIMER,
    )


def _run_main_loop(pwd_file: Path, sleep_timer: int = SLEEP_TIMER) -> None:
    main_loop(
        str(pwd_file),
        IMPORTER_USER,
        USER_MANAGEMENT_URL,
        FWO_API_URL,
        FWO_MAJOR_VERSION,
        API_FETCH_LIMIT,
        sleep_timer,
        clear=False,
        force=False,
    )


@pytest.fixture
def single_mgm_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    mocker.patch("import_main_loop.ImportStateController.initialize_import")
    mocker.patch("import_main_loop.register_global_state")
    return {
        "wait": mocker.patch("import_main_loop.wait_with_shutdown_check"),
        "mgm_details": mocker.patch(
            "import_main_loop.ManagementController.get_mgm_details",
            return_value={"deviceType": {"id": SUPPORTED_DEVICE_TYPE_ID}},
        ),
        "import_management": mocker.patch("import_main_loop.import_management"),
        "info": mocker.patch("import_main_loop.FWOLogger.info"),
        "error": mocker.patch("import_main_loop.FWOLogger.error"),
    }


class TestImportSingleManagement:
    def test_supported_management_is_imported(self, single_mgm_mocks: dict[str, MagicMock]) -> None:
        api_call = MagicMock()

        _run_single_management(api_call)

        single_mgm_mocks["import_management"].assert_called_once_with(
            MGM_ID, api_call, False, API_FETCH_LIMIT, False, True, suppress_consistency_check=False
        )

    def test_unsupported_device_type_is_skipped(self, single_mgm_mocks: dict[str, MagicMock]) -> None:
        single_mgm_mocks["mgm_details"].return_value = {"deviceType": {"id": UNSUPPORTED_DEVICE_TYPE_ID}}

        _run_single_management(MagicMock())

        single_mgm_mocks["import_management"].assert_not_called()

    def test_management_details_error_waits_and_skips(self, single_mgm_mocks: dict[str, MagicMock]) -> None:
        single_mgm_mocks["mgm_details"].side_effect = FwoImporterError("no details")

        _run_single_management(MagicMock())

        single_mgm_mocks["import_management"].assert_not_called()
        single_mgm_mocks["wait"].assert_called_with(SLEEP_TIMER)
        assert "error while getting FW management details" in single_mgm_mocks["error"].call_args.args[0]

    @pytest.mark.parametrize("error", [FwoApiFailedLockImportError("locked"), FwLoginFailedError("login")])
    def test_minor_errors_are_logged_as_info(self, error: Exception, single_mgm_mocks: dict[str, MagicMock]) -> None:
        single_mgm_mocks["import_management"].side_effect = error

        _run_single_management(MagicMock())

        assert "minor error while importing" in single_mgm_mocks["info"].call_args.args[0]
        single_mgm_mocks["error"].assert_not_called()

    def test_other_errors_are_logged_as_error(self, single_mgm_mocks: dict[str, MagicMock]) -> None:
        single_mgm_mocks["import_management"].side_effect = ValueError("boom")

        _run_single_management(MagicMock())

        assert "unspecific error while importing" in single_mgm_mocks["error"].call_args.args[0]


@pytest.fixture
def pwd_file(tmp_path: Path) -> Path:
    file = tmp_path / "importer_pwd"
    file.write_text("secret\n", encoding="utf-8")
    return file


@pytest.fixture
def main_loop_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    api_call = mocker.patch("import_main_loop.FwoApiCall").return_value
    api_call.get_mgm_ids.return_value = [1, 2]
    api_call.get_config_value.return_value = None
    mocker.patch("import_main_loop.FWOLogger.error")
    mocker.patch("import_main_loop.urllib3.disable_warnings")
    return {
        "api_call": api_call,
        "wait": mocker.patch("import_main_loop.wait_with_shutdown_check"),
        "jwt": mocker.patch("import_main_loop.get_fwo_jwt", return_value=json.dumps({"AccessToken": TEST_JWT})),
        "fwo_api": mocker.patch("import_main_loop.FwoApi"),
        "init_service_provider": mocker.patch("import_main_loop.init_service_provider"),
        "single": mocker.patch("import_main_loop.import_single_management"),
        "reset_warnings": mocker.patch("import_main_loop.warnings.resetwarnings"),
    }


class TestMainLoop:
    def test_imports_all_managements_and_sleeps(
        self, pwd_file: Path, main_loop_mocks: dict[str, MagicMock], mocker: MockerFixture
    ) -> None:
        mock_reset = mocker.patch("import_main_loop.ServiceProvider")

        _run_main_loop(pwd_file)

        main_loop_mocks["jwt"].assert_called_once_with(IMPORTER_USER, "secret", USER_MANAGEMENT_URL)
        main_loop_mocks["fwo_api"].assert_called_once_with(FWO_API_URL, TEST_JWT)
        assert main_loop_mocks["single"].call_count == 2
        assert main_loop_mocks["init_service_provider"].call_count == 2
        assert mock_reset.return_value.reset.call_count == 2
        main_loop_mocks["reset_warnings"].assert_called_once()
        main_loop_mocks["wait"].assert_called_with(SLEEP_TIMER)

    def test_config_values_override_defaults(self, pwd_file: Path, main_loop_mocks: dict[str, MagicMock]) -> None:
        config_values = {
            "importCheckCertificates": "True",
            "importSuppressCertificateWarnings": "True",
            "fwApiElementsPerFetch": "500",
            "importSleepTime": "30",
        }

        def get_config_value(key: str) -> str:
            return config_values[key]

        main_loop_mocks["api_call"].get_config_value.side_effect = get_config_value
        main_loop_mocks["api_call"].get_mgm_ids.return_value = [1]

        _run_main_loop(pwd_file)

        call_args = main_loop_mocks["single"].call_args.args
        assert call_args[2] is True  # verify_certificates
        assert call_args[3] == 500
        assert call_args[5] is True  # suppress_certificate_warnings
        assert call_args[8] == 30
        main_loop_mocks["reset_warnings"].assert_not_called()
        main_loop_mocks["wait"].assert_called_with(30)

    def test_missing_password_file_is_raised(self, tmp_path: Path, main_loop_mocks: dict[str, MagicMock]) -> None:
        with pytest.raises(FileNotFoundError):
            _run_main_loop(tmp_path / "missing")

        main_loop_mocks["jwt"].assert_not_called()

    @pytest.mark.parametrize("jwt_response", [None, "not json", json.dumps({"RefreshToken": "x"})])
    def test_failed_login_waits_and_returns(
        self, jwt_response: str | None, pwd_file: Path, main_loop_mocks: dict[str, MagicMock]
    ) -> None:
        main_loop_mocks["jwt"].return_value = jwt_response

        _run_main_loop(pwd_file)

        main_loop_mocks["fwo_api"].assert_not_called()
        main_loop_mocks["wait"].assert_called_with(SLEEP_TIMER)

    def test_management_id_error_waits_and_returns(self, pwd_file: Path, main_loop_mocks: dict[str, MagicMock]) -> None:
        main_loop_mocks["api_call"].get_mgm_ids.side_effect = FwoImporterError("no ids")

        _run_main_loop(pwd_file)

        main_loop_mocks["single"].assert_not_called()
        main_loop_mocks["wait"].assert_called_with(SLEEP_TIMER)


@pytest.fixture
def isolated_globals(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", fwo_globals.verify_certs)
    monkeypatch.setattr(fwo_globals, "suppress_cert_warnings", fwo_globals.suppress_cert_warnings)
    monkeypatch.setattr(sys, "path", list(sys.path))


def _fake_service_provider() -> MagicMock:
    service_provider = MagicMock()
    service_provider.get_fwo_config.return_value = {
        "fwo_api_base_url": FWO_API_URL,
        "fwo_major_version": FWO_MAJOR_VERSION,
        "user_management_api_base_url": USER_MANAGEMENT_URL,
    }
    return service_provider


@pytest.mark.usefixtures("isolated_globals")
class TestMain:
    def test_main_runs_single_cycle_when_clearing(self, mocker: MockerFixture) -> None:
        mocker.patch("import_main_loop.FWOLogger")
        mocker.patch("import_main_loop.init_service_provider", return_value=_fake_service_provider())
        mock_disable_warnings = mocker.patch("import_main_loop.urllib3.disable_warnings")
        mock_main_loop = mocker.patch("import_main_loop.main_loop")

        main(debug_level=0, verify_certificates=True, suppress_certificate_warnings=True, clear=True)

        mock_disable_warnings.assert_called_once()
        mock_main_loop.assert_called_once()
        call_args: tuple[Any, ...] = mock_main_loop.call_args.args
        assert call_args[1:5] == (IMPORTER_USER, USER_MANAGEMENT_URL, FWO_API_URL, FWO_MAJOR_VERSION)
        assert call_args[0].endswith("/etc/secrets/importer_pwd")
        assert fwo_globals.verify_certs is True

    def test_main_loops_until_interrupted(self, mocker: MockerFixture) -> None:
        mocker.patch("import_main_loop.FWOLogger")
        mocker.patch("import_main_loop.init_service_provider", return_value=_fake_service_provider())
        mock_main_loop = mocker.patch("import_main_loop.main_loop", side_effect=[None, SystemExit("stop")])

        with pytest.raises(SystemExit):
            main(debug_level=0)

        assert mock_main_loop.call_count == 2

    def test_script_entry_point_parses_arguments(
        self, mocker: MockerFixture, monkeypatch: pytest.MonkeyPatch, tmp_path: Path
    ) -> None:
        # the script runs in a fresh namespace, so its dependencies are patched at their source modules
        mocker.patch.object(fwo_log, "FWOLogger")
        mocker.patch.object(fwo_base, "init_service_provider", return_value=_fake_service_provider())
        mocker.patch("urllib3.disable_warnings")
        monkeypatch.setattr(fwo_const, "BASE_DIR", str(tmp_path))
        monkeypatch.setattr(sys, "argv", [str(MAIN_LOOP_SCRIPT), "-d", "0", "-s", "-c"])

        # no importer password file below the patched base dir: the single clearing cycle stops there
        with pytest.raises(FileNotFoundError):
            runpy.run_path(str(MAIN_LOOP_SCRIPT), run_name="__main__")

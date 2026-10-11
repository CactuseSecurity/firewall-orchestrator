from __future__ import annotations

import json
from datetime import datetime, timedelta, timezone
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import fwo_globals
import pytest
from fwo_exceptions import FwoImporterError
from models.fwo_config_controller import FwoConfigController, read_tls_identity
from states import global_state as global_state_module
from states.global_state import GlobalState
from states.import_state import ImportState

if TYPE_CHECKING:
    from pathlib import Path

    from pytest_mock import MockerFixture

MGM_ID = 4
MANAGEMENT_MAP_RESPONSE: dict[str, Any] = {
    "data": {
        "management": [
            {
                "mgm_id": MGM_ID,
                "mgm_uid": "super-uid",
                "sub_managers": [{"mgm_id": 5, "mgm_uid": "sub-uid"}],
            }
        ]
    }
}


def build_api_call(last_import_date: str = "", retention_days: str | None = "30") -> MagicMock:
    api_call = MagicMock()
    api_call.get_last_complete_import.return_value = (12, last_import_date)
    api_call.get_config_value.return_value = retention_days
    api_call.call.return_value = MANAGEMENT_MAP_RESPONSE
    return api_call


@pytest.fixture
def management_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    mgm_details = MagicMock(mgm_id=MGM_ID)
    return {
        "get_mgm_details": mocker.patch("states.import_state.ManagementController.get_mgm_details", return_value={}),
        "from_json": mocker.patch("states.import_state.ManagementController.from_json", return_value=mgm_details),
    }


@pytest.mark.usefixtures("management_mocks")
class TestImportStateInit:
    def test_initial_import_without_previous_import(self) -> None:
        import_state = ImportState(MGM_ID, MagicMock(), build_api_call(), input_file="config.json")

        assert import_state.is_initial_import is True
        assert import_state.days_since_last_full_import == 0
        assert import_state.data_retention_days == 30
        assert import_state.input_file == "config.json"

    def test_days_since_last_full_import_are_counted(self) -> None:
        last_import = (datetime.now(timezone.utc) - timedelta(days=3, hours=1)).isoformat()

        import_state = ImportState(MGM_ID, MagicMock(), build_api_call(last_import, retention_days="7"))

        assert import_state.is_initial_import is False
        assert import_state.days_since_last_full_import == 3
        assert import_state.data_retention_days == 7
        assert import_state.last_successful_import == last_import

    def test_naive_import_date_is_treated_as_utc(self) -> None:
        last_import = (datetime.now(timezone.utc) - timedelta(days=2, hours=1)).replace(tzinfo=None).isoformat()

        import_state = ImportState(MGM_ID, MagicMock(), build_api_call(last_import, retention_days=None))

        assert import_state.days_since_last_full_import == 2
        assert import_state.data_retention_days == 30

    def test_management_map_includes_sub_managers(self) -> None:
        import_state = ImportState(MGM_ID, MagicMock(), build_api_call())

        assert import_state.lookup_management_id("super-uid") == MGM_ID
        assert import_state.lookup_management_id("sub-uid") == 5
        assert import_state.lookup_management_id("unknown") is None

    def test_management_details_error_is_raised(self, management_mocks: dict[str, MagicMock]) -> None:
        management_mocks["get_mgm_details"].side_effect = FwoImporterError("no details")

        with pytest.raises(FwoImporterError, match="no details"):
            ImportState(MGM_ID, MagicMock(), build_api_call())

    def test_last_import_error_is_raised(self) -> None:
        api_call = build_api_call()
        api_call.get_last_complete_import.side_effect = FwoImporterError("no import data")

        with pytest.raises(FwoImporterError, match="no import data"):
            ImportState(MGM_ID, MagicMock(), api_call)

    def test_past_import_info_error_is_wrapped(self) -> None:
        api_call = build_api_call()
        api_call.get_config_value.side_effect = ValueError("bad config")

        with pytest.raises(FwoImporterError, match="past import details"):
            ImportState(MGM_ID, MagicMock(), api_call)

    def test_management_map_error_is_wrapped(self) -> None:
        api_call = build_api_call()
        api_call.call.side_effect = ValueError("api down")

        with pytest.raises(FwoImporterError, match="Error while getting managements"):
            ImportState(MGM_ID, MagicMock(), api_call)


@pytest.mark.usefixtures("management_mocks")
class TestImportStateHelpers:
    def test_setters(self) -> None:
        import_state = ImportState(MGM_ID, MagicMock(), build_api_call())

        import_state.set_import_id(99)
        import_state.set_import_file_name("file.json")

        assert import_state.import_id == 99
        assert import_state.import_file_name == "file.json"

    def test_delete_import_removes_import_control(self) -> None:
        api_call = build_api_call()
        import_state = ImportState(MGM_ID, MagicMock(), api_call)
        import_state.set_import_id(99)
        api_call.call.return_value = {"data": {"delete_import_control": {"affected_rows": 1}}}

        import_state.delete_import(api_call)

        assert api_call.call.call_args.kwargs["query_variables"] == {"importId": 99}

    def test_delete_import_logs_errors(self, mocker: MockerFixture) -> None:
        api_call = build_api_call()
        import_state = ImportState(MGM_ID, MagicMock(), api_call)
        api_call.call.side_effect = FwoImporterError("api down")
        mock_exception = mocker.patch("states.import_state.FWOLogger.exception")

        import_state.delete_import(api_call)

        mock_exception.assert_called_once()


def write_fwo_config(tmp_path: Path, overrides: dict[str, Any] | None = None) -> str:
    config: dict[str, Any] = {
        "middleware_uri": "https://middleware/",
        "api_uri": "https://api/graphql",
        "product_version": "9.2.4",
        "importer_user_name": "importer-user",
        "fwApiElementsPerFetch": 500,
        "importSleepTime": 30,
        "tls_client_certificate": "/certs/client.crt",
        "tls_client_private_key": "/certs/client.key",
        "tls_ca_certificate": "/certs/ca.crt",
    }
    config.update(overrides or {})
    config_file = tmp_path / "fworch.json"
    config_file.write_text(json.dumps(config), encoding="utf-8")
    return str(config_file)


@pytest.fixture
def importer_password(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    password_file = tmp_path / "importer_pwd"
    password_file.write_text("secret\n", encoding="utf-8")
    monkeypatch.setattr("models.fwo_config_controller.IMPORTER_PWD_FILE", str(password_file))
    read_tls_identity.cache_clear()


@pytest.mark.usefixtures("importer_password")
class TestFwoConfigController:
    def test_reads_config_file(self, tmp_path: Path) -> None:
        controller = FwoConfigController(write_fwo_config(tmp_path), force=True, clear=True, debug_level=2)

        assert controller.as_dict() == {
            "fwo_api_base_url": "https://api/graphql",
            "user_management_api_base_url": "https://middleware/",
            "api_fetch_size": 500,
            "importer_password": "secret",
            "importer_user_name": "importer-user",
            "fwo_major_version": 9,
            "sleep_timer": 30,
        }
        assert controller.fwo_config.force is True
        assert controller.fwo_config.clear is True
        assert controller.fwo_config.debug_level == 2

    def test_update_settings_changes_only_given_values(self, tmp_path: Path) -> None:
        controller = FwoConfigController(write_fwo_config(tmp_path))

        controller.update_settings(
            fwo_api_url="https://other-api",
            fwo_user_mgmt_api_uri="https://other-middleware/",
            api_fetch_size=10,
            major_version=10,
            importer_password="other",  # noqa: S106
            importer_user_name="other-user",
            sleep_timer=5,
            ssl_verification=False,
            suppress_certificate_warnings=True,
            suppress_consistency_check=True,
        )

        fwo_config = controller.fwo_config
        assert (fwo_config.fwo_api_url, fwo_config.fwo_user_mgmt_api_uri) == (
            "https://other-api",
            "https://other-middleware/",
        )
        assert (fwo_config.api_fetch_size, fwo_config.major_version, fwo_config.sleep_timer) == (10, 10, 5)
        assert (fwo_config.importer_password, fwo_config.importer_user_name) == ("other", "other-user")
        assert fwo_config.ssl_verification is False
        assert fwo_config.suppress_certificate_warnings is True
        assert fwo_config.suppress_consistency_check is True

    def test_missing_key_exits(self, tmp_path: Path) -> None:
        config_file = write_fwo_config(tmp_path)
        config = json.loads(open(config_file).read())  # noqa: SIM115
        del config["api_uri"]
        (tmp_path / "fworch.json").write_text(json.dumps(config), encoding="utf-8")

        with pytest.raises(SystemExit):
            FwoConfigController(config_file)

    def test_missing_file_exits(self, tmp_path: Path) -> None:
        with pytest.raises(SystemExit):
            FwoConfigController(str(tmp_path / "missing.json"))

    def test_invalid_version_exits(self, tmp_path: Path) -> None:
        with pytest.raises(SystemExit):
            FwoConfigController(write_fwo_config(tmp_path, {"product_version": "not-a-version"}))

    def test_invalid_tls_identity_json_is_reported(self, tmp_path: Path) -> None:
        config_file = tmp_path / "broken.json"
        config_file.write_text("{not json", encoding="utf-8")

        with pytest.raises(FwoImporterError, match="not valid json"):
            read_tls_identity(str(config_file))


@pytest.fixture
def restore_certificate_globals(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(fwo_globals, "cli_verify_certs", fwo_globals.cli_verify_certs)


@pytest.mark.usefixtures("importer_password", "restore_certificate_globals")
class TestGlobalState:
    @staticmethod
    def build(
        tmp_path: Path, mocker: MockerFixture, config_values: dict[str, str]
    ) -> tuple[GlobalState, dict[str, MagicMock]]:
        mocks = {
            "fwo_api": mocker.patch.object(global_state_module, "FwoApi"),
            "fwo_api_call": mocker.patch.object(global_state_module, "FwoApiCall"),
            "stm_mapper": mocker.patch.object(global_state_module, "StmMapper"),
            "reset_warnings": mocker.patch.object(global_state_module.warnings, "resetwarnings"),
        }

        def get_config_value(key: str) -> str | None:
            return config_values.get(key)

        mocks["fwo_api_call"].return_value.get_config_value.side_effect = get_config_value
        global_state = GlobalState(write_fwo_config(tmp_path), force=False, clear=False, debug_level=0)
        return global_state, mocks

    def test_logs_in_with_config_values(self, tmp_path: Path, mocker: MockerFixture) -> None:
        global_state, mocks = self.build(tmp_path, mocker, {"importCheckCertificates": "True"})

        mocks["fwo_api"].assert_called_once_with(
            "https://api/graphql",
            importer_user_name="importer-user",
            importer_password="secret",  # noqa: S106
            importer_mgm_uri="https://middleware/",
            fwo_user_mgmt_api_uri="https://middleware/",
        )
        mocks["stm_mapper"].assert_called_once_with(global_state.fwo_api_call)
        assert global_state.importer_version == 9
        assert global_state.fwo_config_controller.fwo_config.ssl_verification is True
        assert global_state.fwo_config_controller.fwo_config.suppress_certificate_warnings is False
        mocks["reset_warnings"].assert_called_once()

    def test_command_line_forces_certificate_checking(self, tmp_path: Path, mocker: MockerFixture) -> None:
        fwo_globals.set_cli_verify_certs(cli_verify_certs_in=True)

        global_state, mocks = self.build(
            tmp_path, mocker, {"importCheckCertificates": "False", "importSuppressCertificateWarnings": "True"}
        )

        assert global_state.fwo_config_controller.fwo_config.ssl_verification is True
        assert global_state.fwo_config_controller.fwo_config.suppress_certificate_warnings is True
        mocks["reset_warnings"].assert_not_called()

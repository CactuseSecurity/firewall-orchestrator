# pyright: reportPrivateUsage=false
from __future__ import annotations

from socket import gethostname
from typing import TYPE_CHECKING, Any, cast
from unittest.mock import MagicMock

import common
import fwo_globals
import pytest
from common import (
    get_config_from_api,
    get_config_top_level,
    get_module_package_name,
    handle_unexpected_exception,
    import_from_file,
    import_management,
    roll_back_exception_handler,
    set_filename,
)
from fwo_exceptions import (
    FwLoginFailedError,
    FwoApiWriteError,
    FwoImporterError,
    FwoImporterErrorInconsistenciesError,
    ImportInterruptionError,
    ImportRecursionLimitReachedError,
    ShutdownRequestedError,
)
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController

if TYPE_CHECKING:
    from pathlib import Path

    from fwo_api_call import FwoApiCall
    from model_controllers.import_state_controller import ImportStateController
    from pytest_mock import MockerFixture

MGM_ID = 3
IMPORT_ID = 17


def _mock(value: Any) -> MagicMock:
    return cast("MagicMock", value)


@pytest.fixture
def import_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    mocker.patch("common.fwo_signalling.register_signalling_handlers")
    return {
        "inner": mocker.patch("common._import_management"),
        "rollback": mocker.patch("common.roll_back_exception_handler"),
        "shutdown": mocker.patch("common.handle_shutdown_exception"),
        "unexpected": mocker.patch("common.handle_unexpected_exception"),
        "config_importer": mocker.patch("common.FwConfigImport"),
    }


def _run_import_management(api_call: FwoApiCall) -> None:
    import_management(
        MGM_ID,
        api_call,
        ssl_verification=False,
        limit=100,
        clear_management_data=False,
        suppress_cert_warnings=True,
    )


class TestImportManagementExceptionHandling:
    def test_successful_import_completes_import(
        self,
        import_mocks: dict[str, MagicMock],
        api_call: FwoApiCall,
        import_state_controller: ImportStateController,
    ) -> None:
        _run_import_management(api_call)

        import_mocks["inner"].assert_called_once()
        _mock(api_call.complete_import).assert_called_once_with(import_state_controller.state, None)
        import_mocks["rollback"].assert_not_called()

    def test_login_failure_deletes_import_and_rolls_back(
        self,
        import_mocks: dict[str, MagicMock],
        api_call: FwoApiCall,
        import_state_controller: ImportStateController,
    ) -> None:
        error = FwLoginFailedError("login failed")
        import_mocks["inner"].side_effect = error

        _run_import_management(api_call)

        _mock(import_state_controller.delete_import).assert_called_once()
        import_mocks["rollback"].assert_called_once()
        _mock(api_call.complete_import).assert_called_once_with(import_state_controller.state, error)

    @pytest.mark.parametrize(
        "error", [ImportRecursionLimitReachedError("deep"), FwoImporterErrorInconsistenciesError("inconsistent")]
    )
    def test_consistency_errors_delete_import_without_rollback(
        self,
        error: Exception,
        import_mocks: dict[str, MagicMock],
        api_call: FwoApiCall,
        import_state_controller: ImportStateController,
    ) -> None:
        import_mocks["inner"].side_effect = error

        _run_import_management(api_call)

        _mock(import_state_controller.delete_import).assert_called_once()
        import_mocks["rollback"].assert_not_called()
        _mock(api_call.complete_import).assert_called_once_with(import_state_controller.state, error)

    @pytest.mark.parametrize("error", [KeyboardInterrupt(), ShutdownRequestedError("stop")])
    def test_shutdown_is_handled_and_reraised_without_completing(
        self, error: BaseException, import_mocks: dict[str, MagicMock], api_call: FwoApiCall
    ) -> None:
        import_mocks["inner"].side_effect = error

        with pytest.raises(type(error)):
            _run_import_management(api_call)

        import_mocks["shutdown"].assert_called_once()
        _mock(api_call.complete_import).assert_not_called()

    def test_interruption_during_shutdown_is_handled_as_shutdown(
        self, import_mocks: dict[str, MagicMock], api_call: FwoApiCall, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(fwo_globals, "shutdown_requested", True)
        import_mocks["inner"].side_effect = ImportInterruptionError("interrupted")

        with pytest.raises(ImportInterruptionError):
            _run_import_management(api_call)

        import_mocks["shutdown"].assert_called_once()
        import_mocks["rollback"].assert_not_called()
        _mock(api_call.complete_import).assert_not_called()

    def test_interruption_without_shutdown_rolls_back_and_completes(
        self, import_mocks: dict[str, MagicMock], api_call: FwoApiCall, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(fwo_globals, "shutdown_requested", False)
        import_mocks["inner"].side_effect = ImportInterruptionError("interrupted")

        with pytest.raises(ImportInterruptionError):
            _run_import_management(api_call)

        import_mocks["shutdown"].assert_not_called()
        import_mocks["rollback"].assert_called_once()
        _mock(api_call.complete_import).assert_called_once()

    @pytest.mark.parametrize("error", [FwoApiWriteError("write"), FwoImporterError("import")])
    def test_importer_errors_roll_back(
        self, error: Exception, import_mocks: dict[str, MagicMock], api_call: FwoApiCall
    ) -> None:
        import_mocks["inner"].side_effect = error

        _run_import_management(api_call)

        import_mocks["rollback"].assert_called_once()
        import_mocks["unexpected"].assert_not_called()

    def test_unexpected_errors_are_handled(self, import_mocks: dict[str, MagicMock], api_call: FwoApiCall) -> None:
        import_mocks["inner"].side_effect = ValueError("unexpected")

        _run_import_management(api_call)

        import_mocks["unexpected"].assert_called_once()

    def test_errors_during_completion_are_logged(
        self, import_mocks: dict[str, MagicMock], api_call: FwoApiCall, mocker: MockerFixture
    ) -> None:
        mock_error = mocker.patch("common.FWOLogger.error")
        _mock(api_call.complete_import).side_effect = FwoImporterError("complete failed")

        _run_import_management(api_call)

        assert "Error during import completion" in mock_error.call_args.args[0]
        assert import_mocks["inner"].called


class TestImportManagementInner:
    @pytest.fixture
    def inner_mocks(
        self, mocker: MockerFixture, import_state_controller: ImportStateController, tmp_path: Path
    ) -> dict[str, MagicMock]:
        state = import_state_controller.state
        state.mgm_details.importer_hostname = gethostname()
        state.force_import = False
        state.is_initial_import = False
        state.days_since_last_full_import = 0
        _mock(import_state_controller.api_call.set_import_lock).return_value = IMPORT_ID
        mocker.patch("common.IMPORT_TMP_PATH", str(tmp_path / "import"))
        mocker.patch("common.ManagementController.build_gateway_list", return_value=[])
        return {
            "config_importer": mocker.patch("common.FwConfigImport").return_value,
            "get_config": mocker.patch("common.get_config_top_level"),
            "consistency": mocker.patch("common.FwConfigImportCheckConsistency"),
        }

    @staticmethod
    def _run(clear_management_data: bool = False, suppress_consistency_check: bool = False) -> None:
        common._import_management(
            MGM_ID,
            ssl_verification=False,
            file=None,
            limit=100,
            clear_management_data=clear_management_data,
            suppress_cert_warnings=True,
            suppress_consistency_check=suppress_consistency_check,
        )

    def test_skips_disabled_management(
        self, inner_mocks: dict[str, MagicMock], import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.mgm_details.import_disabled = True

        self._run()

        _mock(import_state_controller.api_call.set_import_lock).assert_not_called()
        inner_mocks["get_config"].assert_not_called()

    def test_skips_management_of_other_importer_host(
        self, inner_mocks: dict[str, MagicMock], import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.mgm_details.importer_hostname = "other-importer-host"

        self._run()

        assert import_state_controller.state.responsible_for_importing is False
        inner_mocks["get_config"].assert_not_called()

    def test_imports_changed_config_and_checks_consistency(
        self, inner_mocks: dict[str, MagicMock], import_state_controller: ImportStateController, tmp_path: Path
    ) -> None:
        config = MagicMock()
        inner_mocks["get_config"].return_value = (True, config)

        self._run()

        assert import_state_controller.state.import_id == IMPORT_ID
        assert (tmp_path / "import").is_dir()
        config.store_full_normalized_config_to_file.assert_called_once_with(import_state_controller.state)
        inner_mocks["consistency"].return_value.check_fwconfig_managerlist_consistency.assert_called_once_with(config)
        inner_mocks["config_importer"].import_management_set.assert_called_once()
        inner_mocks["config_importer"].delete_old_imports.assert_not_called()

    def test_unchanged_config_is_not_imported_but_old_imports_are_deleted(
        self, inner_mocks: dict[str, MagicMock], import_state_controller: ImportStateController
    ) -> None:
        inner_mocks["get_config"].return_value = (False, MagicMock())
        import_state_controller.state.days_since_last_full_import = (
            import_state_controller.state.data_retention_days + 1
        )

        self._run()

        inner_mocks["config_importer"].import_management_set.assert_not_called()
        inner_mocks["config_importer"].delete_old_imports.assert_called_once()

    def test_clearing_import_skips_consistency_check_on_request(
        self, inner_mocks: dict[str, MagicMock], import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.days_since_last_full_import = (
            import_state_controller.state.data_retention_days + 1
        )

        self._run(clear_management_data=True, suppress_consistency_check=True)

        inner_mocks["config_importer"].clear_management.assert_called_once()
        inner_mocks["get_config"].assert_not_called()
        inner_mocks["consistency"].assert_not_called()
        inner_mocks["config_importer"].import_management_set.assert_called_once()
        inner_mocks["config_importer"].delete_old_imports.assert_not_called()


class TestRollbackHandlers:
    def test_handle_unexpected_exception_rolls_back_with_importer(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        mock_rollback = mocker.patch("common.roll_back_exception_handler")
        error = ValueError("x")

        handle_unexpected_exception(import_state_controller, MagicMock(), error)

        mock_rollback.assert_called_once()

    def test_handle_unexpected_exception_without_state_does_nothing(self, mocker: MockerFixture) -> None:
        mock_rollback = mocker.patch("common.roll_back_exception_handler")

        handle_unexpected_exception()

        mock_rollback.assert_not_called()

    @pytest.mark.parametrize(
        ("shutdown_requested", "error_text", "exc", "expected_log"),
        [
            (True, "", None, "Shutdown requested."),
            (False, "custom", None, "Exception: custom"),
            (False, "", ValueError("x"), "Exception: ValueError"),
            (False, "", None, "Exception: no exception provided"),
        ],
    )
    def test_roll_back_exception_handler_logs_reason_and_rolls_back(
        self,
        shutdown_requested: bool,
        error_text: str,
        exc: BaseException | None,
        expected_log: str,
        mocker: MockerFixture,
        monkeypatch: pytest.MonkeyPatch,
        import_state_controller: ImportStateController,
    ) -> None:
        monkeypatch.setattr(fwo_globals, "shutdown_requested", shutdown_requested)
        mock_rollback = mocker.patch("common.FwConfigImportRollback")
        mock_error = mocker.patch("common.FWOLogger.error")
        mock_warning = mocker.patch("common.FWOLogger.warning")

        roll_back_exception_handler(import_state_controller, MagicMock(), exc, error_text)

        logged = [*mock_error.call_args_list, *mock_warning.call_args_list]
        assert logged[0].args[0] == expected_log
        mock_rollback.return_value.rollback_current_import.assert_called_once_with(
            import_state=import_state_controller.state, fwo_api_call=import_state_controller.api_call
        )
        _mock(import_state_controller.delete_import).assert_called_once()

    def test_roll_back_exception_handler_without_importer_skips_rollback(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        mock_rollback = mocker.patch("common.FwConfigImportRollback")

        roll_back_exception_handler(import_state_controller, None, ValueError("x"))

        mock_rollback.assert_not_called()
        _mock(import_state_controller.delete_import).assert_called_once()

    def test_roll_back_exception_handler_logs_rollback_errors(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        mocker.patch("common.FwConfigImportRollback", side_effect=FwoImporterError("rollback failed"))
        mock_error = mocker.patch("common.FWOLogger.error")

        roll_back_exception_handler(import_state_controller, MagicMock(), ValueError("x"))

        assert "Error during rollback" in mock_error.call_args.args[0]


class TestConfigRetrieval:
    def test_get_config_top_level_from_file_with_normalized_config(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        file_config = FwConfigManagerListController.generate_empty_config()
        mocker.patch("common.import_from_file", return_value=(True, file_config))
        mock_api = mocker.patch("common.get_config_from_api")

        assert get_config_top_level(import_state_controller, "config.json") == (True, file_config)
        mock_api.assert_not_called()

    def test_get_config_top_level_feeds_native_file_config_to_api(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        file_config = FwConfigManagerListController.generate_empty_config()
        file_config.native_config = {"native": True}
        mocker.patch("common.import_from_file", return_value=(True, file_config))
        mock_api = mocker.patch("common.get_config_from_api", return_value=(False, file_config))

        assert get_config_top_level(import_state_controller, "config.json") == (False, file_config)
        mock_api.assert_called_once_with(import_state_controller, file_config)

    def test_get_config_top_level_without_file_uses_api(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.mgm_details.hostname = "fw.example.com"
        import_state_controller.state.mgm_details.domain_name = None
        mock_file = mocker.patch("common.import_from_file")
        mock_api = mocker.patch("common.get_config_from_api", return_value=(True, MagicMock()))

        get_config_top_level(import_state_controller)

        mock_file.assert_not_called()
        mock_api.assert_called_once()

    def test_import_from_file_sets_file_name_and_reads_config(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        config = MagicMock()
        mock_read = mocker.patch("common.fwo_file_import.read_json_config_from_file", return_value=config)

        assert import_from_file(import_state_controller, "config.json") == (True, config)
        _mock(import_state_controller.set_import_file_name).assert_called_once_with("config.json")
        mock_read.assert_called_once_with(import_state_controller.api_call, import_state_controller.state)

    def test_set_filename_without_uri_keeps_file_name_unset(
        self, import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.mgm_details.hostname = "fw.example.com"
        import_state_controller.state.mgm_details.domain_name = None

        set_filename(import_state_controller)

        _mock(import_state_controller.set_import_file_name).assert_not_called()

    def test_get_config_from_api_fetches_changed_config(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.force_import = False
        config_in = FwConfigManagerListController.generate_empty_config()
        native_config = MagicMock()
        fw_module = mocker.patch("common.get_module").return_value
        fw_module.has_config_changed.return_value = True
        fw_module.get_config.return_value = (0, native_config)
        mock_write = mocker.patch("common.write_native_config_to_file")

        assert get_config_from_api(import_state_controller, config_in) == (True, native_config)
        fw_module.get_config.assert_called_once_with(config_in, import_state_controller)
        mock_write.assert_called_once_with(import_state_controller.state, config_in.native_config)

    def test_get_config_from_api_without_changes_returns_empty_config(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.force_import = False
        fw_module = mocker.patch("common.get_module").return_value
        fw_module.has_config_changed.return_value = False
        mocker.patch("common.write_native_config_to_file")

        changed, config = get_config_from_api(
            import_state_controller, FwConfigManagerListController.generate_empty_config()
        )

        assert changed is False
        assert config.native_config_is_empty()
        fw_module.get_config.assert_not_called()

    def test_get_config_from_api_rejects_missing_native_config(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        import_state_controller.state.force_import = True
        config_in = FwConfigManagerListController.generate_empty_config()
        config_in.native_config = None
        fw_module = mocker.patch("common.get_module").return_value
        fw_module.has_config_changed.return_value = False
        fw_module.get_config.return_value = (0, MagicMock())

        with pytest.raises(FwoImporterError, match="get_config returned no config"):
            get_config_from_api(import_state_controller, config_in)

    def test_get_config_from_api_reraises_module_errors(
        self, mocker: MockerFixture, import_state_controller: ImportStateController
    ) -> None:
        mocker.patch("common.get_module", side_effect=FwoImporterError("no module"))
        mock_exception = mocker.patch("common.FWOLogger.exception")

        with pytest.raises(FwoImporterError, match="no module"):
            get_config_from_api(import_state_controller, FwConfigManagerListController.generate_empty_config())

        mock_exception.assert_called_once()


class TestModulePackageName:
    @pytest.mark.parametrize(
        ("type_name", "type_version", "expected"),
        [
            ("Check Point", "R8x", "checkpointR8x"),
            ("Check Point", "MDS R8x", "checkpointR8x"),
            ("FortiManager", "5ff", "fortiadom5ff"),
            ("Cisco Asa on FirePower", "9", "ciscoasa9"),
            ("OPNsense Standalone", "25ff", "opnsensestandalone25ff"),
        ],
    )
    def test_get_module_package_name(
        self, type_name: str, type_version: str, expected: str, import_state_controller: ImportStateController
    ) -> None:
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.device_type_name = type_name
        mgm_details.device_type_version = type_version

        assert get_module_package_name(import_state_controller.state) == expected

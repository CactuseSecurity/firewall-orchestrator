import importlib
import sys
from pathlib import Path

import pytest
from common import get_config_uri, get_module, set_filename
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.import_state import ImportState


@pytest.fixture
def import_state_controller(
    management_controller: ManagementController,
    api_call: FwoApiCall,
    api_connection: FwoApi,
) -> ImportStateController:
    import_state = ImportState()
    import_state.mgm_details = management_controller
    controller = ImportStateController(state=import_state, api_call=api_call)
    controller.state = import_state
    controller.api_call = api_call
    controller.api_connection = api_connection
    return controller


class TestCommonConfigUri:
    def test_get_config_uri_prefers_hostname_uri(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.mgm_details.hostname = "https://example.com/config.json"
        import_state.state.mgm_details.domain_name = "https://example.com/ignored.json"

        assert get_config_uri(import_state) == "https://example.com/config.json"

    def test_get_config_uri_falls_back_to_config_path_uri(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.mgm_details.hostname = "fw.example.com"
        import_state.state.mgm_details.domain_name = "file:///tmp/config.json"

        assert get_config_uri(import_state) == "file:///tmp/config.json"

    def test_set_filename_uses_config_uri_when_present(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.mgm_details.hostname = "fw.example.com"
        import_state.state.mgm_details.domain_name = "https://example.com/config.json"

        set_filename(import_state)

        assert import_state.state.import_file_name == "https://example.com/config.json"


class TestCommonModuleSelection:
    @pytest.mark.parametrize(
        ("package_name", "module_attribute"),
        [
            ("ciscoasa9", "CiscoAsa9Common"),
            ("fortiadom5ff", "FortiAdom5ffCommon"),
            ("checkpointR8x", "CheckpointR8xCommon"),
            ("fortiosmanagementREST", "FortiosManagementRESTCommon"),
            ("genericfirewallmanagement1.0", "GenericFirewallCommon"),
            ("azure2022ff", "Azure2022ffCommon"),
        ],
    )
    def test_get_module_selects_importer_for_known_package(
        self,
        package_name: str,
        module_attribute: str,
        monkeypatch: pytest.MonkeyPatch,
    ):
        selected_module = object()

        def fake_get_module_package_name(_import_state: ImportState) -> str:
            return package_name

        monkeypatch.setattr("common.get_module_package_name", fake_get_module_package_name)
        monkeypatch.setattr(f"common.{module_attribute}", lambda: selected_module)

        assert get_module(ImportState()) is selected_module

    def test_get_module_rejects_unknown_package(self, monkeypatch: pytest.MonkeyPatch):
        def fake_get_module_package_name(_import_state: ImportState) -> str:
            return "unsupported"

        monkeypatch.setattr("common.get_module_package_name", fake_get_module_package_name)

        with pytest.raises(FwoImporterError, match="unsupported"):
            get_module(ImportState())


@pytest.mark.parametrize(
    "module_name",
    [
        "fw_modules.checkpointR8x.cp_user",
        "fw_modules.fortiadom5ff.fmgr_base",
        "fw_modules.fortiadom5ff.fmgr_gw_networking",
        "import_mgm",
        "model_controllers.interface_controller",
        "model_controllers.route_controller",
    ],
)
def test_importer_modules_load_with_deferred_annotations(
    module_name: str,
    monkeypatch: pytest.MonkeyPatch,
):
    if module_name == "fw_modules.fortiadom5ff.fmgr_gw_networking":
        module_dir = str(Path(__file__).resolve().parents[1] / "fw_modules" / "fortiadom5ff")
        monkeypatch.setattr(sys, "path", [module_dir, *sys.path])

    assert importlib.import_module(module_name) is not None

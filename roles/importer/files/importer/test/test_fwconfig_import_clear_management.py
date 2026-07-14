from fwo_base import init_service_provider
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.import_state_controller import ImportStateController


class TestFwconfigImportClearManagement:
    def test_clear_management_uses_management_uid(
        self,
        import_state_controller: ImportStateController,
    ):
        service_provider = init_service_provider()
        import_state = import_state_controller
        service_provider.get_global_state().import_state = import_state

        importer = FwConfigImport()
        config = importer.clear_management()

        assert config.ManagerSet[0].manager_uid == import_state.state.mgm_details.uid

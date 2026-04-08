from model_controllers.fwconfig_import import FwConfigImport
from states.import_state import ImportState


class TestFwconfigImportClearManagement:
    def test_clear_management_uses_management_uid(
        self,
        import_state: ImportState,
    ):

        import_state.mgm_details.uid = "mock-mgm-uid"
        import_state.mgm_details.name = "Mock Manager"
        import_state.mgm_details.is_super_manager = False
        import_state.mgm_details.sub_manager_ids = []
        import_state.mgm_details.domain_name = "Mock Domain"
        import_state.mgm_details.domain_uid = "mock-domain-uid"

        importer = FwConfigImport()
        config = importer.clear_management(import_state)

        assert config.ManagerSet[0].manager_uid == import_state.mgm_details.uid

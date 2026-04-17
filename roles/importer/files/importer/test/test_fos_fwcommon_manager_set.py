from fw_modules.fortiosmanagementREST import fwcommon
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from states.import_state import ImportState


class TestEnsureManagerSet:
    def test_ensure_manager_set_adds_manager(self, import_state: ImportState):
        config_in = FwConfigManagerListController()

        import_state.mgm_details.uid = "mock-mgm-uid"
        import_state.mgm_details.name = "Mock Manager"
        import_state.mgm_details.is_super_manager = False
        import_state.mgm_details.sub_manager_ids = []
        import_state.mgm_details.domain_name = "Mock Domain"
        import_state.mgm_details.domain_uid = "mock-domain-uid"

        fwcommon.ensure_manager_set(config_in, import_state)

        assert len(config_in.ManagerSet) == 1
        assert config_in.ManagerSet[0].manager_uid == import_state.mgm_details.uid

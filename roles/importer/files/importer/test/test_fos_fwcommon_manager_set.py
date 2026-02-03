from fw_modules.fortiosmanagementREST import fwcommon
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController


class TestEnsureManagerSet:
    def test_ensure_manager_set_adds_manager(self, import_state_controller: ImportStateController):
        import_state = import_state_controller
        config_in = FwConfigManagerListController()

        fwcommon.ensure_manager_set(config_in, import_state)

        assert len(config_in.ManagerSet) == 1
        assert config_in.ManagerSet[0].manager_uid == import_state.state.mgm_details.uid

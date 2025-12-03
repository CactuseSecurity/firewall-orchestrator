from models.fw_common import FwCommon
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController


class FortiosManagementRESTCommon(FwCommon):
    def get_config(self, config_in: FwConfigManagerListController, import_state: ImportStateController) -> tuple[int, FwConfigManagerListController]:
        raise NotImplementedError("Fortios Management REST is not supported yet in the new python importer.")
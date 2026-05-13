from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fw_common import FwCommon
from states.global_state import GlobalState
from states.import_state import ImportState


class CiscoFirepowerDomain7ffCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportState, global_state: GlobalState
    ) -> tuple[int, FwConfigManagerListController]:
        raise NotImplementedError("Cisco Firepower Domain 7ff is not supported yet in the new python importer.")

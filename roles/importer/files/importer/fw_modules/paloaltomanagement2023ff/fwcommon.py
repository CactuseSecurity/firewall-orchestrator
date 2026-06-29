from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fw_common import FwCommon
from states.global_state import GlobalState
from states.import_state import ImportState


class PaloAltoManagement2023ffCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportState, global_state: GlobalState
    ) -> tuple[int, FwConfigManagerListController]:
        raise NotImplementedError("Palo Alto Management 2023 ff is not supported yet in the new python importer.")

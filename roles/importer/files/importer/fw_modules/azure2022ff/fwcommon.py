from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon


class Azure2022ffCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        raise NotImplementedError("Azure 2022 ff is not supported yet in the new python importer.")

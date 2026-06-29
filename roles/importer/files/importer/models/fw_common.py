from abc import ABC, abstractmethod

from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from states.global_state import GlobalState
from states.import_state import ImportState


class FwCommon(ABC):
    def has_config_changed(
        self, _full_config: FwConfigManagerListController, _import_state: ImportState, _force: bool = False
    ) -> bool:
        return True

    @abstractmethod
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportState, global_state: GlobalState
    ) -> tuple[int, FwConfigManagerListController]:
        raise NotImplementedError("Please Implement this method")

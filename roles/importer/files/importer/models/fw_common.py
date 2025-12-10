from abc import ABC, abstractmethod

from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController


class FwCommon(ABC):
    def has_config_changed(
        self, _full_config: FwConfigManagerListController, _import_state: ImportStateController, _force: bool = False
    ) -> bool:
        return True

    @abstractmethod
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        raise NotImplementedError("Please Implement this method")

from fwo_const import FWO_CONFIG_FILENAME
from models.fwo_config_controller import FwoConfigController


class GlobalState:
    fwo_config_controller: FwoConfigController

    def __init__(self):
        self.fwo_config_controller = FwoConfigController(FWO_CONFIG_FILENAME)

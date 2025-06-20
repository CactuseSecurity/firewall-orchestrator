from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized


class GlobalState:
    
    import_state: ImportStateController
    previous_config: FwConfigNormalized
    normalized_config: FwConfigNormalized

    def __init__(self):
        self.import_state = None
        self.previous_config = None
        self.normalized_config = None


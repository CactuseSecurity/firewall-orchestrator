from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized


class GlobalState:
    
    import_state: ImportStateController|None
    previous_config: FwConfigNormalized|None
    previous_global_config: FwConfigNormalized|None
    normalized_config: FwConfigNormalized|None
    global_normalized_config: FwConfigNormalized|None

    def __init__(self):
        self.import_state = None
        self.previous_config = None
        self.previous_global_config = None
        self.normalized_config = None
        self.global_normalized_config = None


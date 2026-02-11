from models.fwconfig_normalized import FwConfigNormalized
from states.global_state import GlobalState
from states.management_state import ManagementState


class ImportState:
    import_id: int
    global_state: GlobalState
    global_config: FwConfigNormalized | None
    previous_global_config: FwConfigNormalized | None
    management_state: ManagementState
    mgm_map: dict[int, dict[str, int]]
    gateway_map: dict[int, dict[str, int]]
    rulebase_map: dict[str, int]

    def __init__(self, global_state: GlobalState):
        self.import_id = 0
        self.global_state = global_state
        self.global_config = None
        self.previous_global_config = None
        self.management_state = ManagementState()
        self.mgm_map = {}
        self.gateway_map = {}
        self.rulebase_map = {}

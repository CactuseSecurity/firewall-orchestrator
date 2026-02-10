from models.fwo_config_controller import FwoConfigController
from states.management_state import ManagementState


class ImportState:
    import_id: int
    global_config: FwoConfigController
    management_state: ManagementState
    mgm_map: dict[int, dict[str, int]]
    gateway_map: dict[int, dict[str, int]]
    rulebase_map: dict[str, int]

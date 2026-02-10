from models.fwconfig_normalized import FwConfigNormalized
from services.group_flats_mapper import GroupFlatsMapper
from services.uid2id_mapper import Uid2IdMapper


class ManagementState:
    normalized_config: FwConfigNormalized | None
    previous_config: FwConfigNormalized | None
    uid2id_mapper: Uid2IdMapper | None
    group_flats_mapper: GroupFlatsMapper | None

    def __init__(self, global_state):
        self.global_state = global_state
        self.normalized_config = None
        self.previous_config = None
        self.uid2id_mapper = None
        self.group_flats_mapper = None

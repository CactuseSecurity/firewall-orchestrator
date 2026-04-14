from copy import deepcopy

from models.fwconfig_normalized import FwConfigNormalized
from services.group_flats_mapper import GroupFlatsMapper
from services.uid2id_mapper import Uid2IdMapper
from states.import_state import ImportState


class ManagementState:
    previous_config: FwConfigNormalized | None
    normalized_config: FwConfigNormalized | None
    uid2id_mapper: Uid2IdMapper
    group_flats_mapper: GroupFlatsMapper
    prev_group_flats_mapper: GroupFlatsMapper

    mgm_id: int
    name: str
    uid: str
    is_super_manager: bool

    def __init__(
        self, import_state: ImportState, mgm_id: int, normalized_config: FwConfigNormalized, is_super_manager: bool
    ):
        self.is_super_manager = is_super_manager
        self.mgm_id = mgm_id
        self.normalized_config = normalized_config
        self.previous_config = None
        self.uid2id_mapper = (
            Uid2IdMapper(import_state=import_state)
            if import_state.super_uid2id_mapper is None
            else deepcopy(import_state.super_uid2id_mapper)
        )
        self.group_flats_mapper = GroupFlatsMapper()
        self.prev_group_flats_mapper = GroupFlatsMapper()

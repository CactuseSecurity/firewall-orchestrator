from fwo_api import FwoApi
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
        self,
        import_state: ImportState,
        fwo_api: FwoApi,
        mgm_id: int,
        normalized_config: FwConfigNormalized,
        is_super_manager: bool,
    ):
        self.is_super_manager = is_super_manager
        self.mgm_id = mgm_id
        self.normalized_config = normalized_config
        self.previous_config = None
        self.group_flats_mapper = GroupFlatsMapper()
        self.prev_group_flats_mapper = GroupFlatsMapper()
        self.uid2id_mapper = Uid2IdMapper(fwo_api)
        if import_state.super_uid2id_mapper is not None:
            self.uid2id_mapper.copy_maps_from(import_state.super_uid2id_mapper)

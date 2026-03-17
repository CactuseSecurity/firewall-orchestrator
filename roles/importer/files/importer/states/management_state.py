from typing import Any

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
    hostname: str
    import_disabled: bool
    devices: list[dict[str, Any]]
    importer_hostname: str
    device_type_name: str
    device_type_version: str
    port: int
    import_user: str
    secret: str
    sub_manager_ids: list[int]
    current_mgm_id: int
    current_mgm_is_super_manager: bool
    domain_name: str | None
    domain_uid: str | None
    sub_managers: list["ManagementState"]
    cloud_client_id: str | None = None
    cloud_client_secret: str | None = None

    def __init__(self, import_state: ImportState, mgm_id: int):
        self.mgm_id = mgm_id
        self.normalized_config = None
        self.previous_config = None
        self.uid2id_mapper = Uid2IdMapper(import_state=import_state)
        self.group_flats_mapper = GroupFlatsMapper()

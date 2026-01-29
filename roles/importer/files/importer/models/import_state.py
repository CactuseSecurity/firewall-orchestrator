import time

from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.management_controller import ManagementController

"""Used for storing state during import process per management"""


class ImportState:
    debug_level: int
    verify_certs: bool = False
    config_changed_since_last_import: bool
    fwo_config: FworchConfigController
    mgm_details: ManagementController
    import_id: int = -1
    import_file_name: str = ""
    force_import: bool
    import_version: int
    data_retention_days: int
    days_since_last_full_import: int
    last_full_import_id: int
    last_full_import_date: str | None = None
    last_successful_import: str | None = None
    is_full_import: bool
    is_initial_import: bool = False
    responsible_for_importing: bool = True
    is_clearing_import: bool = False

    def __init__(self) -> None:
        self.stats: ImportStatisticsController = ImportStatisticsController()
        self.start_time: int = int(time.time())
        self.actions: dict[str, int] = {}
        self.tracks: dict[str, int] = {}
        self.link_types: dict[str, int] = {}
        self.gateway_map: dict[int, dict[str, int]] = {}
        self.rulebase_map: dict[str, int] = {}
        self.management_map: dict[str, int] = {}
        self.color_map: dict[str, int] = {}
        self.network_obj_type_map: dict[str, int] = {}
        self.service_obj_type_map: dict[str, int] = {}
        self.user_obj_type_map: dict[str, int] = {}
        self.protocol_map: dict[str, int] = {}
        self.rulebase_to_gateway_map: dict[int, list[int]] = {}
        self.data_retention_days: int = 30

    def lookup_action(self, action_str: str) -> int:
        action_id = self.actions.get(action_str.lower(), None)
        if action_id is None:
            FWOLogger.error(f"Action {action_str} not found")
            raise FwoImporterError(f"Action {action_str} not found")
        return action_id

    def lookup_track(self, track_str: str) -> int:
        track_id = self.tracks.get(track_str.lower(), None)
        if track_id is None:
            FWOLogger.error(f"Track {track_str} not found")
            raise FwoImporterError(f"Track {track_str} not found")
        return track_id

    def lookup_link_type(self, link_uid: str) -> int:
        link_type_id = self.link_types.get(link_uid, None)
        if not link_type_id:
            FWOLogger.error(f"Link type {link_uid} not found")
            raise FwoImporterError(f"Link type {link_uid} not found")
        return link_type_id

    def lookup_color_id(self, color_str: str) -> int:
        return self.color_map.get(color_str, 1)  # 1 = forground color black

    def lookup_network_obj_type_id(self, obj_type_str: str) -> int:
        obj_type_id = self.network_obj_type_map.get(obj_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"Network object type {obj_type_str} not found")
            raise FwoImporterError(f"Network object type {obj_type_str} not found")
        return obj_type_id

    def lookup_service_obj_type_id(self, svc_type_str: str) -> int:
        obj_type_id = self.service_obj_type_map.get(svc_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"Service object type {svc_type_str} not found")
            raise FwoImporterError(f"Service object type {svc_type_str} not found")
        return obj_type_id

    def lookup_user_obj_type_id(self, usr_type_str: str) -> int:
        obj_type_id = self.user_obj_type_map.get(usr_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"User object type {usr_type_str} not found")
            raise FwoImporterError(f"User object type {usr_type_str} not found")
        return obj_type_id

    def lookup_protocol_id(self, proto_str: str) -> int:
        proto_id = self.protocol_map.get(proto_str.lower(), None)
        if proto_id is None:
            FWOLogger.error(f"Protocol {proto_str} not found")
            raise FwoImporterError(f"Protocol {proto_str} not found")
        return proto_id

    def lookup_gateway_id(self, gw_uid: str) -> int:
        mgm_id = self.mgm_details.current_mgm_id
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        gw_id = gws_for_mgm.get(gw_uid, None)
        if gw_id is None:
            FWOLogger.error(
                f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm"
            )
            raise FwoImporterError(
                f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm"
            )
        return gw_id

    def lookup_all_gateway_ids(self) -> list[int]:
        mgm_id = self.mgm_details.current_mgm_id
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        return list(gws_for_mgm.values())

    def lookup_management_id(self, mgm_uid: str) -> int | None:
        if not self.management_map.get(mgm_uid, None):
            FWOLogger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgm_uid}'")
        return self.management_map.get(mgm_uid, None)

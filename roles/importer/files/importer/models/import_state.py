import time
from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_controller import ManagementController
from fwo_log import FWOLogger
from fwo_exceptions import FwoImporterError

"""Used for storing state during import process per management"""
class ImportState():
    stats: ImportStatisticsController = ImportStatisticsController()
    start_time: int = int(time.time())
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
    actions: dict[str, int]
    tracks: dict[str, int]
    link_types: dict[str, int]
    gateway_map: dict[int, dict[str, int]] # mgm_id -> ( key = gateway.uid and value = gateway.id )
    rulebase_map: dict[str, int]
    rule_map: dict[str, int]
    responsible_for_importing: bool = True
    management_map: dict[str, int]  # maps management uid to management id
    color_map: dict[str, int] = {}
    rulebase_to_gateway_map: dict[int, list[int]] = {}
    is_clearing_import: bool = False
    removed_rules_map: dict[str, int] = {}  # rule_id -> rule dict
    data_retention_days: int = 30  # Default data retention days


    def lookup_rule(self, rule_uid: str) -> int | None:
        return self.rule_map.get(rule_uid, None)

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

    def lookup_rulebase_id(self, rulebase_uid: str) -> int:
        rulebase_id = self.rulebase_map.get(rulebase_uid, None)
        if rulebase_id is None:
            FWOLogger.error(f"Rulebase {rulebase_uid} not found in {len(self.rulebase_map)} known rulebases")
            raise FwoImporterError(f"Rulebase {rulebase_uid} not found in {len(self.rulebase_map)} known rulebases")
        return rulebase_id

    def lookup_link_type(self, link_uid: str) -> int:
        return self.link_types.get(link_uid, -1)

    def lookup_gateway_id(self, gw_uid: str) -> int | None:
        mgm_id = self.mgm_details.current_mgm_id
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        gw_id = gws_for_mgm.get(gw_uid, None)
        if gw_id is None:
            FWOLogger.error(f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm")
            raise FwoImporterError(f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm")
        return gw_id
    
    def lookup_all_gateway_ids(self) -> list[int]:
        mgm_id = self.mgm_details.current_mgm_id
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        gw_ids = list(gws_for_mgm.values())
        return gw_ids

    def lookup_management_id(self, mgm_uid: str) -> int | None:
        if not self.management_map.get(mgm_uid, None):
            FWOLogger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgm_uid}'")
        return self.management_map.get(mgm_uid, None)


    def lookupColorId(self, color_str: str) -> int:
        return self.color_map.get(color_str, 1)  # 1 = forground color black

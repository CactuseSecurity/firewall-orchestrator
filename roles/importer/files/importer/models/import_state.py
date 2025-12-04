from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_controller import ManagementController

"""Used for storing state during import process per management"""
class ImportState():
    stats: ImportStatisticsController = ImportStatisticsController()
    start_time: int
    debug_level: int
    verify_certs: bool = False
    config_changed_since_last_import: bool
    fwo_config: FworchConfigController
    mgm_details: ManagementController
    import_id: int
    import_file_name: str
    force_import: bool
    import_version: int
    data_retention_days: int
    days_since_last_full_import: int
    last_full_import_id: int
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

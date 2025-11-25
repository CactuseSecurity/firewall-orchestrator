from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_controller import ManagementController

"""Used for storing state during import process per management"""
class ImportState():
    Stats: ImportStatisticsController = ImportStatisticsController()
    StartTime: int
    DebugLevel: int
    VerifyCerts: bool = False
    ConfigChangedSinceLastImport: bool
    FwoConfig: FworchConfigController
    MgmDetails: ManagementController
    ImportId: int
    import_file_name: str
    ForceImport: bool
    ImportVersion: int
    DataRetentionDays: int
    DaysSinceLastFullImport: int
    LastFullImportId: int
    LastSuccessfulImport: str|None = None
    IsFullImport: bool
    IsInitialImport: bool = False
    Actions: dict[str, int]
    Tracks: dict[str, int]
    LinkTypes: dict[str, int]
    GatewayMap: dict[int, dict[str, int]] # mgm_id -> ( key = gateway.uid and value = gateway.id )
    RulebaseMap: dict[str, int]
    RuleMap: dict[str, int]
    responsible_for_importing: bool = True

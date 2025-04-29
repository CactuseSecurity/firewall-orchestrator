from typing import Dict, Optional

from models.import_statistics import ImportStatistics
from model_controllers.import_statistics_controller import ImportStatisticsController
from fwo_api_oo import FwoApi
from models.action import Action
from models.track import Track
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_details_controller import ManagementDetailsController

"""Used for storing state during import process per management"""
class ImportState():
    Stats: ImportStatisticsController = ImportStatisticsController()
    StartTime: int
    DebugLevel: int
    VerifyCerts: bool = False
    ConfigChangedSinceLastImport: bool
    FwoConfig: FworchConfigController
    MgmDetails: ManagementDetailsController
    ImportId: int
    ImportFileName: str
    ForceImport: str
    ImportVersion: int
    DataRetentionDays: int
    DaysSinceLastFullImport: int
    LastFullImportId: int
    LastSuccessfulImport: Optional[str] = None
    IsFullImport: bool
    IsInitialImport: bool = False
    Actions: Dict[str, Action]
    Tracks: Dict[str, Track]
    LinkTypes: Dict[str, int]
    RulebaseMap: Dict[str, int]
    RuleMap: Dict[str, int]
    
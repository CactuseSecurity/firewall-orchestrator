import time
from datetime import datetime
from typing import List, Dict
import requests, requests.packages

from fwo_api_oo import FwoApi
from fwo_log import getFwoLogger
from fwo_config import readConfig
from fwo_const import fwo_config_filename, importer_user_name
import fwo_api
from fwo_exception import FwoApiLoginFailed
import fwo_globals
from models.action import Action
from models.track import Track
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_details_controller import ManagementDetailsController

"""Used for storing state during import process per management"""
class ImportState(FwoApi):
    ErrorCount: int
    ChangeCount: int
    ErrorString: str
    StartTime: int
    DebugLevel: int
    ConfigChangedSinceLastImport: bool
    FwoConfig: FworchConfigController
    MgmDetails: ManagementDetailsController
    FullMgmDetails: dict
    ImportId: int
    ImportFileName: str
    ForceImport: str
    ImportVersion: int
    DataRetentionDays: int
    DaysSinceLastFullImport: int
    LastFullImportId: int
    IsFullImport: bool
    Actions: Dict[str, Action]
    Tracks: Dict[str, Track]
    LinkTypes: Dict[str, int]
    RulebaseMap: Dict[str, int]
    RuleMap: Dict[str, int]

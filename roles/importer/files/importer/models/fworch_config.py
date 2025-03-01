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

"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FworchConfig():
    FwoApiUri: str
    FwoUserMgmtApiUri: str
    ApiFetchSize: int
    ImporterPassword: str

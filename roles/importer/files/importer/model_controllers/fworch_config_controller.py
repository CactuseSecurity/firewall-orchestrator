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
from models.fworch_config import FworchConfig

"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FworchConfigController(FworchConfig):

    def __init__(self, fwoApiUri, fwoUserMgmtApiUri, importerPwd, apiFetchSize=500):
        if fwoApiUri is not None:
            self.FwoApiUri = fwoApiUri
        else:
            self.FwoApiUFwoUserMgmtApiri = None
        if fwoUserMgmtApiUri is not None:
            self.FwoUserMgmtApiUri = fwoUserMgmtApiUri
        else:
            self.FwoUserMgmtApiUri = None
        self.ImporterPassword = importerPwd
        self.ApiFetchSize = apiFetchSize

    @classmethod
    def fromJson(cls, json_dict):
        fwoApiUri = json_dict['fwo_api_base_url']
        fwoUserMgmtApiUri = json_dict['user_management_api_base_url']
        if 'importerPassword' in json_dict:
            fwoImporterPwd = json_dict['importerPassword']
        else:
            fwoImporterPwd = None
        
        return cls(fwoApiUri, fwoUserMgmtApiUri, fwoImporterPwd)

    def __str__(self):
        return f"{self.FwoApiUri}, {self.FwoUserMgmtApi}, {self.ApiFetchSize}"

    def setImporterPwd(self, importerPassword):
        self.ImporterPassword = importerPassword        

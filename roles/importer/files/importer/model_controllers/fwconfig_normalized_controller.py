from typing import List
import json
import time
import traceback

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from models.gateway import Gateway
from fwo_base import ConfFormat

from fwo_base import deserializeClassToDictRecursively
from fwo_const import import_tmp_path
import fwo_globals
from models.fwconfig_normalized import FwConfigNormalized


class FwConfigNormalizedController():

    NormalizedConfig: FwConfigNormalized

    def __init__(self, ConfigFormat: ConfFormat, fwConfig: FwConfigNormalized):

        self.NormalizedConfig = fwConfig

    @staticmethod
    def convertListToDict(listIn: List, idField: str) -> dict:
        logger = getFwoLogger()
        result = {}
        for item in listIn:
            if idField in item:
                key = item[idField]
                result[key] = item
            else:
                logger.error(f"dict {str(item)} does not contain id field {idField}")
        return result # { listIn[idField]: listIn for listIn in listIn }

    def __str__(self):
        return f"{self.action}({str(self.network_objects)})"

    def stripUnusedElements(self):
        for policyName in self.rules:
            deleteDictElements(self.rules[policyName].Rules, ['control_id', 'rulebase_name'])

        FwConfigNormalized.deleteControlIdFromDictList(self.network_objects)
        FwConfigNormalized.deleteControlIdFromDictList(self.service_objects)
        FwConfigNormalized.deleteControlIdFromDictList(self.users)
        FwConfigNormalized.deleteControlIdFromDictList(self.zone_objects)

    @staticmethod
    def deleteControlIdFromDictList(dictListInOut: dict):
        if isinstance(dictListInOut, List): 
            deleteListDictElements(dictListInOut, ['control_id'])
        elif isinstance(dictListInOut, dict): 
            deleteDictElements(dictListInOut, ['control_id'])
        return dictListInOut
    
    def split(self):
        return [self]   # for now not implemented

    @staticmethod
    def join(configList):
        resultingConfig = FwConfigNormalized()
        for conf in configList:
            resultingConfig.addElements(conf)
        return resultingConfig

    def addElements(self, config):
        self.network_objects += config.Networks
        self.service_objects += config.Services
        self.users += config.Users
        self.zone_objects += config.Zones
        self.rules += config.Policies
        self.gateways += config.Gateways

    def fillGateways(self, importState: ImportState, gateways:List[Gateway]):      
        self.gateways = gateways
        # for dev in importState.MgmDetails.Devices:
        #     gw = Gateway(f"{dev['name']}_{dev['local_rulebase_name']}",
        #                  dev['name'],
        #                  [],    # TODO: routing
        #                  [],    # TODO: interfaces
        #                  [dev['local_rulebase_name']],
        #                  [dev['package_name']],
        #                  None  # TODO: global policy UID
        #                  )

    def writeNormalizedConfigToFile(self, importState):
        if not self == {}:
            logger = getFwoLogger()
            debug_start_time = int(time.time())
            try:
                if fwo_globals.debug_level>5:
                    normalized_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_normalized.json"
                    with open(normalized_config_filename, "w") as json_data:
                        if importState.ImportVersion>8:
                            json_data.write(self.toJsonString(prettyPrint=True))
                        else:
                            json_data.write(self.toJsonStringLegacy(prettyPrint=True))
            except:
                logger.error(f"import_management - unspecified error while dumping normalized config to json file: {str(traceback.format_exc())}")
                raise

            time_write_debug_json = int(time.time()) - debug_start_time
            logger.debug(f"import_management - writing normalized config json files duration {str(time_write_debug_json)}s")


class FwConfigManager():
    def __init__(self, ManagerUid: str, IsGlobal: bool=False, DependantManagerUids: List[str]=[], Configs: List[FwConfigNormalized]=[]):
        """
            mandatory parameter: ManagerUid, 
        """
        self.ManagerUid = ManagerUid
        self.IsGlobal = IsGlobal
        self.DependantManagerUids = DependantManagerUids
        self.Configs = Configs

    @classmethod
    def fromJson(cls, jsonDict):
        ManagerUid = jsonDict['manager_uid']
        IsGlobal = jsonDict['is_global']
        DependantManagerUids = jsonDict['dependant_manager_uids']
        Configs = jsonDict['configs']
        return cls(ManagerUid, IsGlobal, DependantManagerUids, Configs)

    def __str__(self):
        return f"{self.ManagerUid}({str(self.Configs)})"

def deleteDictElements(dictIn: dict, attrNames: List[str]):
    logger = getFwoLogger()
    for dName in dictIn:
        for attr in attrNames:
            try:
                del dictIn[dName][attr]
            except KeyError as e:
                logger.warning(f"trying to remove element from dict that does not exist: {e}")

def deleteListDictElements(listIn: dict, attrNames: List[str]):
    logger = getFwoLogger()
    for element in listIn:
        for attr in attrNames:
            try:
                del element[attr]
            except KeyError as e:
                logger.warning(f"trying to remove element from dict that does not exist: {e}")

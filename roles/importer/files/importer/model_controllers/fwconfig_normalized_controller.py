from typing import List
import json
import time
import traceback

from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
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
        self.rulebases += config.Policies
        self.gateways += config.Gateways

    def fillGateways(self, importState: ImportStateController, gateways:List[Gateway]):      
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

    
    def merge(self, config: FwConfigNormalized):
        """
        Merges the given config into this config.
        """
        self.NormalizedConfig.action = config.action
        self.NormalizedConfig.network_objects.update(config.network_objects)
        self.NormalizedConfig.service_objects.update(config.service_objects)
        self.NormalizedConfig.users.update(config.users)
        self.NormalizedConfig.zone_objects.update(config.zone_objects)
        self.NormalizedConfig.rulebases.extend(config.rulebases)
        self.NormalizedConfig.gateways.extend(config.gateways)
        
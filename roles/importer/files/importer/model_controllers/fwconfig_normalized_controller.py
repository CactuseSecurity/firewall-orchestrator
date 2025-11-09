from typing import Any
from fwo_log import getFwoLogger
from fwo_base import ConfFormat
from models.fwconfig_normalized import FwConfigNormalized


class FwConfigNormalizedController():

    NormalizedConfig: FwConfigNormalized

    def __init__(self, ConfigFormat: ConfFormat, fwConfig: FwConfigNormalized):

        self.NormalizedConfig = fwConfig

    @staticmethod
    def convertListToDict(listIn: list[Any], idField: str) -> dict[Any, Any]:
        logger = getFwoLogger()
        result: dict[Any, Any] = {}
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
    def deleteControlIdFromDictList(dictListInOut: dict[Any, Any] | list[Any]) -> dict[Any, Any] | list[Any]:
        if isinstance(dictListInOut, list): 
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
        
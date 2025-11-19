from typing import Any
from fwo_log import get_fwo_logger
from fwo_base import ConfFormat
from models.fwconfig_normalized import FwConfigNormalized


class FwConfigNormalizedController():

    NormalizedConfig: FwConfigNormalized

    def __init__(self, ConfigFormat: ConfFormat, fwConfig: FwConfigNormalized):

        self.NormalizedConfig = fwConfig

    @staticmethod
    def convertListToDict(listIn: list[Any], idField: str) -> dict[Any, Any]:
        logger = get_fwo_logger()
        result: dict[Any, Any] = {}
        for item in listIn:
            if idField in item:
                key = item[idField]
                result[key] = item
            else:
                logger.error(f"dict {str(item)} does not contain id field {idField}")
        return result # { listIn[idField]: listIn for listIn in listIn }

    def __str__(self):
        return f"{self.action}({str(self.network_objects)})" # TODO self.action not defined? # type: ignore

    @staticmethod
    def deleteControlIdFromDictList(dictListInOut: dict[Any, Any] | list[Any]) -> dict[Any, Any] | list[Any]:
        if isinstance(dictListInOut, list): 
            deleteListDictElements(dictListInOut, ['control_id']) # TODO deleteListDictElements not defined
        elif isinstance(dictListInOut, dict): 
            deleteDictElements(dictListInOut, ['control_id']) # TODO deleteListDictElements not defined
        return dictListInOut
    
    def split(self):
        return [self]   # for now not implemented

    @staticmethod
    def join(configList: list[FwConfigNormalized]):
        resultingConfig = FwConfigNormalized() 
        for conf in configList:
            resultingConfig.addElements(conf) # TODO addElements not defined
        return resultingConfig

    def addElements(self, config: FwConfigNormalized):
        self.network_objects += config.Networks # TODO: all members are not defined
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
        
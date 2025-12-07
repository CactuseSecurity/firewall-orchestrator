from typing import Any
from fwo_log import FWOLogger
from fwo_base import ConfFormat
from models.fwconfig_normalized import FwConfigNormalized


class FwConfigNormalizedController():

    normalized_config: FwConfigNormalized

    def __init__(self, config_format: ConfFormat, fw_config: FwConfigNormalized):
        self.normalized_config = fw_config


    @staticmethod
    def convert_list_to_dict(list_in: list[Any], id_field: str) -> dict[Any, Any]:
        result: dict[Any, Any] = {}
        for item in list_in:
            if id_field in item:
                key = item[id_field]
                result[key] = item
            else:
                FWOLogger.error(f"dict {str(item)} does not contain id field {id_field}")
        return result # { listIn[idField]: listIn for listIn in listIn }

    def __str__(self):
        return f"{self.action}({str(self.network_objects)})" # TODO self.action not defined? # type: ignore

    @staticmethod
    def delete_control_id_from_dict_list(dict_list_in_out: dict[Any, Any] | list[Any]) -> dict[Any, Any] | list[Any]:
        if isinstance(dict_list_in_out, list): 
            deleteListDictElements(dict_list_in_out, ['control_id']) # TODO deleteListDictElements not defined
        elif isinstance(dict_list_in_out, dict): 
            deleteDictElements(dict_list_in_out, ['control_id']) # TODO deleteListDictElements not defined
        return dict_list_in_out
        
    def split(self):
        return [self]   # for now not implemented

    @staticmethod
    def join(config_list: list[FwConfigNormalized]):
        resulting_config = FwConfigNormalized() 
        for conf in config_list:
            resulting_config.add_elements(conf) # TODO addElements not defined
        return resulting_config

    def add_elements(self, config: FwConfigNormalized):
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
        self.normalized_config.action = config.action
        self.normalized_config.network_objects.update(config.network_objects)
        self.normalized_config.service_objects.update(config.service_objects)
        self.normalized_config.users.update(config.users)
        self.normalized_config.zone_objects.update(config.zone_objects)
        self.normalized_config.rulebases.extend(config.rulebases)
        self.normalized_config.gateways.extend(config.gateways)
        
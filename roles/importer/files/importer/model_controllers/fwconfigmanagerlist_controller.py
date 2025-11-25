import json
import time
import traceback
from copy import deepcopy
from typing import Any

import fwo_globals
from fwo_log import FWOLogger
from fwo_base import serialize_dict_to_class_rec, deserialize_class_to_dict_rec
from fwo_const import IMPORT_TMP_PATH

from model_controllers.import_state_controller import ImportStateController
from models.fwconfigmanagerlist import FwConfigManagerList
from models.fwconfigmanager import FwConfigManager
from model_controllers.fwconfig_controller import FwoEncoder
from fwo_base import ConfFormat

"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""
class FwConfigManagerListController(FwConfigManagerList):
    def __str__(self):
        return f"{str(self.ManagerSet)})"

    def to_json(self):
        return deserialize_class_to_dict_rec(self)

    def to_json_string(self, pretty_print: bool=False):
        json_dict = self.to_json()
        if pretty_print:
            return json.dumps(json_dict, indent=2, cls=FwoEncoder)
        else:
            return json.dumps(json_dict)
        
    def mergeConfigs(self, conf2: 'FwConfigManagerListController'):
        if self.ConfigFormat==conf2.ConfigFormat:
            self.ManagerSet.extend(conf2.ManagerSet)

    @staticmethod
    def generate_empty_config(is_super_manager: bool=False):
        """
        Generates an empty FwConfigManagerListController with a single empty FwConfigManager.
        """
        empty_config = FwConfigManagerListController()
        empty_config.ConfigFormat = ConfFormat.NORMALIZED
        empty_manager = FwConfigManager(manager_uid="",
                                        is_super_manager=is_super_manager,
                                        sub_manager_ids=[],
                                        configs=[],
                                        domain_name="",
                                        domain_uid="",
                                        manager_name=""
                                        )
        empty_config.add_manager(empty_manager)
        empty_config.native_config = {}
        return empty_config

# to be re-written:
    def to_json_legacy(self):
        return deserialize_class_to_dict_rec(self)

# to be re-written:
    def to_json_string_legacy(self, pretty_print: bool=False):
        json_dict = self.to_json()
        if pretty_print:
            return json.dumps(json_dict, indent=2, cls=FwoEncoder)
        else:
            return json.dumps(json_dict, cls=FwoEncoder)


    def get_all_zone_names(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all zone UIDs in the configuration.
        """
        all_zone_names: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid==mgr_uid:
                for single_config in mgr.configs:
                    all_zone_names.extend(single_config.zone_objects.keys())
        return set(all_zone_names)
    

    def get_all_network_object_uids(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all network objects in the configuration.
        """
        all_network_objects: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid==mgr_uid:
                for single_config in mgr.configs:
                    all_network_objects.extend(single_config.network_objects.keys())
        return set(all_network_objects)
    

    def get_all_service_object_uids(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all service objects in the configuration.
        """
        all_service_objects: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid==mgr_uid:
                for single_config in mgr.configs:
                    all_service_objects.extend(single_config.service_objects.keys())
        return set(all_service_objects)
    

    def get_all_user_object_uids(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all user objects in the configuration.
        """
        all_user_objects: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid==mgr_uid:
                for single_config in mgr.configs:
                    all_user_objects.extend(single_config.users.keys())
        return set(all_user_objects)
    

    def add_manager(self, manager: FwConfigManager):
        self.ManagerSet.append(manager)

    def get_first_manager(self):
        if len(self.ManagerSet)>0:
            return self.ManagerSet[0]
        else:
            return None

    @staticmethod
    def get_device_uid_from_rulebase_name(rb_name: str) -> str:
        return rb_name

    @staticmethod
    def get_policy_uid_from_rulebase_name(rb_name: str) -> str:
        return rb_name
    
    @classmethod
    def from_json(cls, json_in: dict[str, Any]) -> 'FwConfigManagerListController':
        return serialize_dict_to_class_rec(json_in, cls)


    def store_full_normalized_config_to_file(self, import_state: ImportStateController):
        if fwo_globals.debug_level>5:
            debug_start_time = int(time.time())
            try:
                normalized_config_filename = f"{IMPORT_TMP_PATH}/mgm_id_{str(import_state.mgm_details.id)}_config_normalized.json"

                config_copy_without_native= deepcopy(self)
                config_copy_without_native.native_config = {}

                with open(normalized_config_filename, "w") as json_data:
                    json_data.write(config_copy_without_native.to_json_string(pretty_print=True))
                time_write_debug_json = int(time.time()) - debug_start_time
                FWOLogger.debug(f"storeFullNormalizedConfigToFile - writing normalized config json files duration {str(time_write_debug_json)}s")
                
                return normalized_config_filename
            
            except Exception:
                FWOLogger.error(f"import_management - unspecified error while dumping normalized config to json file: {str(traceback.format_exc())}")
                raise
    

    def is_native(self) -> bool:
        return self.native_config is not None


    def is_native_non_empty(self) -> bool:
        return self.native_config is not None and self.native_config != {}


    def contains_only_native(self) -> bool:
        return self.is_native() and (
            len(self.ManagerSet)==0 or
            len(self.ManagerSet)==1 and len(self.ManagerSet[0].configs)==0
        ) 


    def native_config_is_empty(self) -> bool:
        return (self.native_config is None or self.native_config == {})


    def normalized_config_is_empty(self) -> bool:
        return len(self.ManagerSet)==1 and len(self.ManagerSet[0].configs)==0


    def is_normalized(self) -> bool:
        return not self.is_native()


    def is_legacy(self) -> bool:
        return self.ConfigFormat==ConfFormat.IsLegacyConfigFormat


    def has_empty_config(self) -> bool:
        return self.native_config_is_empty() and self.normalized_config_is_empty() 

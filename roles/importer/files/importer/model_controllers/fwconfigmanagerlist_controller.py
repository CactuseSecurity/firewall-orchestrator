import json
import jsonpickle
import time
import traceback
from copy import deepcopy

import fwo_globals
from fwo_log import getFwoLogger
from fwo_exceptions import FwoImporterError
from model_controllers.interface_controller import InterfaceSerializable
from model_controllers.route_controller import RouteSerializable
from fwo_base import split_list, serializeDictToClassRecursively, deserializeClassToDictRecursively
from fwo_const import max_objs_per_chunk, import_tmp_path

from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import Management
from models.fwconfig_normalized import FwConfig, FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManagerList
from models.fwconfigmanager import FwConfigManager
from model_controllers.fwconfig_controller import FwoEncoder
from model_controllers.management_controller import ManagementController
from fwo_base import ConfFormat

"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""
class FwConfigManagerListController(FwConfigManagerList):
    def __str__(self):
        return f"{str(self.ManagerSet)})"

    def toJson(self):
        return deserializeClassToDictRecursively(self)

    def toJsonString(self, prettyPrint=False):
        jsonDict = self.toJson()
        if prettyPrint:
            return json.dumps(jsonDict, indent=2, cls=FwoEncoder)
        else:
            return json.dumps(jsonDict)
        
    def mergeConfigs(self, conf2: 'FwConfigManagerListController'):
        if self.ConfigFormat==conf2.ConfigFormat:
            self.ManagerSet.extend(conf2.ManagerSet)

    @staticmethod
    def generate_empty_config(is_super_manager=False):
        """
        Generates an empty FwConfigManagerListController with a single empty FwConfigManager.
        """
        empty_config = FwConfigManagerListController()
        empty_config.ConfigFormat = ConfFormat.NORMALIZED
        empty_manager = FwConfigManager(ManagerUid="",
                                        IsSuperManager=is_super_manager,
                                        SubManagerIds=[],
                                        Configs=[],
                                        DomainName="",
                                        DomainUid="",
                                        ManagerName=""
                                        )
        empty_config.addManager(empty_manager)
        empty_config.native_config = {}
        return empty_config

# to be re-written:
    def toJsonLegacy(self):
        return deserializeClassToDictRecursively(self)

# to be re-written:
    def toJsonStringLegacy(self, prettyPrint=False):
        jsonDict = self.toJson()
        if prettyPrint:
            return json.dumps(jsonDict, indent=2, cls=FwoEncoder)
        else:
            return json.dumps(jsonDict, cls=FwoEncoder)


    def get_all_zone_names(self, mgr_uid):
        """
        Returns a list of all zone UIDs in the configuration.
        """
        all_zone_names = []
        for mgr in self.ManagerSet:
            if mgr.IsSuperManager or mgr.ManagerUid==mgr_uid:
                for single_config in mgr.Configs:
                    all_zone_names.extend(single_config.zone_objects.keys())
        return set(all_zone_names)
    

    def get_all_network_object_uids(self, mgr_uid):
        """
        Returns a list of all network objects in the configuration.
        """
        all_network_objects = []
        for mgr in self.ManagerSet:
            if mgr.IsSuperManager or mgr.ManagerUid==mgr_uid:
                for single_config in mgr.Configs:
                    all_network_objects.extend(single_config.network_objects.keys())
        return set(all_network_objects)
    

    def get_all_service_object_uids(self, mgr_uid):
        """
        Returns a list of all service objects in the configuration.
        """
        all_service_objects = []
        for mgr in self.ManagerSet:
            if mgr.IsSuperManager or mgr.ManagerUid==mgr_uid:
                for single_config in mgr.Configs:
                    all_service_objects.extend(single_config.service_objects.keys())
        return set(all_service_objects)
    

    def get_all_user_object_uids(self, mgr_uid):
        """
        Returns a list of all user objects in the configuration.
        """
        all_user_objects = []
        for mgr in self.ManagerSet:
            if mgr.IsSuperManager or mgr.ManagerUid==mgr_uid:
                for single_config in mgr.Configs:
                    all_user_objects.extend(single_config.users.keys())
        return set(all_user_objects)
    

    def addManager(self, manager):
        self.ManagerSet.append(manager)

    def getFirstManager(self):
        if len(self.ManagerSet)>0:
            return self.ManagerSet[0]
        else:
            return None

    @staticmethod
    def getDevUidFromRulebaseName(rb_name: str) -> str:
        return rb_name

    @staticmethod
    def getPolicyUidFromRulebaseName(rb_name: str) -> str:
        return rb_name
    
    @classmethod
    def FromJson(cls, jsonIn):
        return serializeDictToClassRecursively(jsonIn, cls)


    def storeFullNormalizedConfigToFile(self, importState: ImportStateController):
        if fwo_globals.debug_level>5:
            logger = getFwoLogger()
            debug_start_time = int(time.time())
            try:
                normalized_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_normalized.json"

                config_copy_without_native= deepcopy(self)
                config_copy_without_native.native_config = {}

                with open(normalized_config_filename, "w") as json_data:
                    json_data.write(config_copy_without_native.toJsonString(prettyPrint=True))
                time_write_debug_json = int(time.time()) - debug_start_time
                logger.debug(f"storeFullNormalizedConfigToFile - writing normalized config json files duration {str(time_write_debug_json)}s")
            except Exception:
                logger.error(f"import_management - unspecified error while dumping normalized config to json file: {str(traceback.format_exc())}")
                raise
    

    def is_native(self) -> bool:
        return self.native_config is not None


    def is_native_non_empty(self) -> bool:
        return self.native_config is not None and self.native_config != {}


    def contains_only_native(self) -> bool:
        return self.is_native() and (
            len(self.ManagerSet)==0 or
            len(self.ManagerSet)==1 and len(self.ManagerSet[0].Configs)==0
        ) 


    def native_config_is_empty(self) -> bool:
        return (self.native_config is None or self.native_config == {})


    def normalized_config_is_empty(self) -> bool:
        return len(self.ManagerSet)==1 and len(self.ManagerSet[0].Configs)==0


    def is_normalized(self) -> bool:
        return not self.is_native()


    def is_legacy(self) -> bool:
        return self.ConfigFormat==ConfFormat.IsLegacyConfigFormat


    def has_empty_config(self) -> bool:
        return self.native_config_is_empty() and self.normalized_config_is_empty() 

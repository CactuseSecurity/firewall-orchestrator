import json
import jsonpickle
from typing import List
import time
import traceback

import fwo_globals
from fwo_log import getFwoLogger
from fwo_data_networking import InterfaceSerializable, RouteSerializable
from fwo_base import split_list, calcManagerUidHash
from fwo_const import max_objs_per_chunk, import_tmp_path

from fwoBaseImport import ImportState, ManagementDetails
from fwconfig_base import EnumEncoder, ConfigAction, ConfFormat, NetworkObject, Gateway, Policy
from fwconfig_normalized import FwConfig, FwConfigNormalized
from fwo_api_import import importLatestConfig, deleteLatestConfig

class FwConfigManager():
    ManagerUid: str
    ManagerName: str
    IsGlobal: bool
    DependantManagerUids: List[str]
    Configs: List[dict]


    def __init__(self, ManagerUid: str, ManagerName: str, IsGlobal: bool=False, DependantManagerUids: List[str]=[], Configs: List[FwConfigNormalized]=[]):
        """
            mandatory parameter: ManagerUid, 
        """
        self.ManagerUid = ManagerUid
        self.ManagerName = ManagerName
        self.IsGlobal = IsGlobal
        self.DependantManagerUids = DependantManagerUids
        self.Configs = Configs

    @classmethod
    def fromJson(cls, jsonDict):
        ManagerUid = jsonDict['manager_uid']
        ManagerName = jsonDict['mgm_name']
        IsGlobal = jsonDict['is_global']
        DependantManagerUids = jsonDict['dependant_manager_uids']
        Configs = jsonDict['configs']
        return cls(ManagerUid, ManagerName, IsGlobal, DependantManagerUids, Configs)

    def __str__(self):
        return f"{self.ManagerUid}({str(self.Configs)})"


"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""
class FwConfigManagerList():

    ConfigFormat: ConfFormat
    ManagerSet: List[FwConfigManager]

    def __init__(self, ConfigFormat=ConfFormat.NORMALIZED, ManagerSet: List[FwConfigManager]=[]):
        self.ManagerSet = ManagerSet
        self.ConfigFormat=ConfigFormat

    def __str__(self):
        return f"{str(self.ManagerSet)})"
    
    def toJsonStringFull(self):
        return jsonpickle.encode(self, indent=2)

    def toJsonString(self, prettyPrint=False):
        if prettyPrint:
            return json.dumps(self.toJson(), indent=2, cls=EnumEncoder)
        else:
            return json.dumps(self.toJson(), cls=EnumEncoder)

    def toJson(self):
        mgrSet = []
        for mgr in self.ManagerSet:
            configsJson =  {}
            for config in mgr.Configs:
                configsJson.update(config.toJson())
            mgrSet.append( { 
                'mgmUid': mgr.ManagerUid,
                'mgmName': mgr.ManagerName,
                'IsGlobal': mgr.IsGlobal,
                'Configs': configsJson,
                'DependantManagerUids': mgr.DependantManagerUids
            })

        return {
            'ConfigFormat': self.ConfigFormat,
            'managers': mgrSet
        } 

    def toJsonStringLegacy(self):
        configOut = {}
        for mgr in self.ManagerSet:
            for config in mgr.Configs:
                configOut.update(config.toJsonLegacy(withAction=False))
        return json.dumps(configOut, indent=2)

    def addManager(self, manager):
        self.ManagerSet.append(manager)

    def getFirstManager(self):
        if len(self.ManagerSet)>0:
            return self.ManagerSet[0]
        else:
            return None

    def getDevUidFromRulebaseName(rb_name: str) -> str:
        return rb_name

    def getPolicyUidFromRulebaseName(rb_name: str) -> str:
        return rb_name

    def convertFromLegacyNormalizedConfig(self, legacyConfig: dict, mgmDetails: ManagementDetails) -> None:
        if 'networkobjects' in legacyConfig:
            mgr = FwConfigManager(ManagerUid=calcManagerUidHash(mgmDetails.FullMgmDetails),
                                  IsGlobal=False,
                                  DependantManagerUids = [],
                                  Configs=[])
            convertedConfig = FwConfigNormalized(action=ConfigAction.INSERT,
                                                 networks=legacyConfig['network_objects'],
                                                 users=legacyConfig['users'],
                                                 services=legacyConfig['network_services'])
            policies = {}
            rulebase_names = []

            # now we need to convert rulebases, routing and interfaces to match the device structure
            for rule in legacyConfig['rules']:
                rb_name = rule['rulebase_name']
                policyUid = self.getPolicyUidFromRulebaseName(rb_name)
                if rb_name not in rulebase_names:   # add new policy
                    rulebase_names.append(rb_name)
                    policy = Policy(Uid=rb_name, Name=rb_name, EnforcingGatewayUids=[self.getDevUidFromRulebaseName()], Rules=[])
                    policies.update( { policyUid: policy } )
                policies[policyUid].Rules.append(rule)
            mgr.Configs.append(convertedConfig)
            self.addManager(mgr)
        else:
            logger = getFwoLogger()
            logger.error(f"found malformed legacy config: {str(legacyConfig)}")
        pass

    def convertLegacyConfig(self, legacyConfig: dict, mgmDetails: ManagementDetails):
        if 'networkobjects' in legacyConfig:
            mgr = FwConfigManager(ManagerUid=calcManagerUidHash(mgmDetails.FullMgmDetails),
                                  IsGlobal=False,
                                  DependantManagerUids = [],
                                  Configs=[])
            convertedConfig = FwConfig()
            mgr.Configs.append(convertedConfig)
            self.addManager(mgr)
        else:
            logger = getFwoLogger()
            logger.error(f"found malformed legacy config: {str(legacyConfig)}")
        pass

    def storeConfig(self, importState):
        self.writeNormalizedConfigToFile(importState)
        errorsFound = deleteLatestConfig(importState)
        if errorsFound:
            getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {importState.ImportId}")
        errorsFound = importLatestConfig(importState, self.toJsonString(prettyPrint=False))
        if errorsFound:
            getFwoLogger().warning(f"error while writing latest config for mgm_id: {importState.ImportId}")

    def writeNormalizedConfigToFile(self, importState):
        if not self == {}:
            logger = getFwoLogger()
            debug_start_time = int(time.time())
            try:
                if fwo_globals.debug_level>5:
                    normalized_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_normalized.json"
                    with open(normalized_config_filename, "w") as json_data:
                        json_data.write(self.toJsonString(prettyPrint=True))
            except:
                logger.error(f"import_management - unspecified error while dumping normalized config to json file: {str(traceback.format_exc())}")
                raise

            time_write_debug_json = int(time.time()) - debug_start_time
            logger.debug(f"import_management - writing native config json files duration {str(time_write_debug_json)}s")


# split the config into chunks of max size "max_objs_per_chunk" to avoid 
# timeout of import while writing data to import table
# each object table to import is handled here 
def split_config(importState: ImportState, config2import: FwConfigManagerList):
    # temp disable chunking of imports
    # config_split_with_metadata = [{
    #     "config": config2import,
    #     "start_import_flag": False,
    #     "importId": int(importState.ImportId), 
    #     "mgmId": int(importState.MgmDetails.Id) 
    # }]
    return config2import

    conf_split_dict_of_lists = {}
    max_number_of_chunks = 0
    logger = getFwoLogger()
    object_lists = ["network_objects", "service_objects", "user_objects", "rules", "zone_objects", "interfaces", "routing"]
    confMgr: FwConfigManager

    # split config into conf_split_dict_of_lists and calculate the (max) number of chunks
    for confMgr in config2import.ManagerSet:
        for conf in confMgr.Configs:
            for attr in dir(conf):
                if attr in object_lists:
                    if attr == 'interfaces' or attr == 'routing':
                        obj_list = getattr(conf, attr)
                        obj_list_ser = []
                        for el in obj_list:
                            if attr == 'interfaces':
                                obj_list_ser.append(InterfaceSerializable(el))
                            elif attr == 'routing':
                                obj_list_ser.append(RouteSerializable(el))
                        if attr == 'interfaces':
                            conf.Interfaces = json.loads(jsonpickle.encode(obj_list_ser, unpicklable=False))
                        elif attr == 'routing':
                            conf.Routing = json.loads(jsonpickle.encode(obj_list_ser, unpicklable=False))
                
                    split_list_tmp = split_list(getattr(conf, attr), max_objs_per_chunk)
                    conf_split_dict_of_lists.update({attr: split_list_tmp})
                    max_number_of_chunks = max(len(split_list_tmp),max_number_of_chunks)

    #   
    conf_split = []
    current_chunk = 0
    for confMgr in config2import.ManagerSet:
        for conf in confMgr.Configs:
            for attr in dir(conf):
                if attr in object_lists:
                    single_chunk = {}
                    single_chunk[attr] = []
                    for chunk in conf_split_dict_of_lists.attr:
                        single_chunk[attr] = chunk
            conf_split.append(single_chunk)

    # now adding meta data around (start_import_flag used as trigger)
    config_split_with_metadata = []
    for conf_chunk in conf_split:
        config_split_with_metadata.append({
            "config": conf_chunk,
            "start_import_flag": False,
            "importId": int(importState.ImportId), 
            "mgmId": int(importState.MgmDetails.Id) 
        })
    # setting the trigger in the last chunk:
    if len(config_split_with_metadata)>0:
        config_split_with_metadata[len(config_split_with_metadata)-1]["start_import_flag"] = True
    else:
        logger.warning('got empty config (no chunks at all)')
    if fwo_globals.debug_level>0 and len(config_split_with_metadata)>0:
        config_split_with_metadata[len(config_split_with_metadata)-1]["debug_mode"] = True
    return config_split_with_metadata


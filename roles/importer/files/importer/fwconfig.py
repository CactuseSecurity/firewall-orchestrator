import json
import jsonpickle
from typing import List
from enum import Enum, auto

import fwo_globals
from fwo_log import getFwoLogger
from fwo_data_networking import InterfaceSerializable, RouteSerializable
from fwo_base import split_list, calcManagerUidHash
from fwo_const import max_objs_per_chunk
from fwoBaseImport import ImportState, ManagementDetails


class ConfigAction(Enum):
    INSERT = auto()
    UPDATE = auto()
    DELETE = auto()


class ConfFormat(Enum):
    NORMALIZED = auto()
    
    CHECKPOINT = auto()
    FORTINET = auto()
    PALOALTO = auto()
    CISCOFIREPOWER = auto()

    NORMALIZED_LEGACY = auto()

    CHECKPOINT_LEGACY = auto()
    FORTINET_LEGACY = auto()
    PALOALTO_LEGACY = auto()
    CISCOFIREPOWER_LEGACY = auto()


"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""
class FwConfig():
    ConfigFormat: ConfFormat
    Config: dict

    def __init__(self, configFormat: ConfFormat=ConfFormat.NORMALIZED, config={}):
        self.ConfigFormat = configFormat
        self.Config = config

    @classmethod
    def fromJson(cls, jsonDict):
        ConfigFormat = jsonDict['config-format']
        Config = jsonDict['config']
        return cls(ConfigFormat, Config)

    def __str__(self):
        return f"{self.ConfigType}({str(self.Config)})"


class NetworkObject():

    def __init__(self, Uid: str, Name: str, Ip: str, IpEnd: str, Color: str = 'black'):
        self.Uid = Uid
        self.Name = Name
        self.Ip = Ip
        self.IpEnd = IpEnd
        self.Color = Color
 
    @classmethod
    def fromJson(cls, jsonDict):
        return cls(jsonDict['uid'], 
                   jsonDict['name'], 
                   jsonDict['ip'],
                   jsonDict['ip_end'],
                   jsonDict['color'])

# 'policy':
#     {
#         'policy_name': 'pol1',
#         'policy_uid': 'a32bc348234-23432a',
#         'enforcing_gateway_uids':  [ ... ], // how to define order of policies? rule_order_num?
#         'rules': [ { ... }, { ... }, ... ]
#     }
class Policy():
    Uid: str
    Name: str
    EnforcingGatewayUids: List[str]
    Rules: List[dict]

    def __init__(self, Uid: str, Name: str, EnforcingGatewayUids: str=[], Rules: str=[]):
        self.Name = Uid
        self.Uid = Name
        self.EnforcingGatewayUids = EnforcingGatewayUids
        self.Rules = Rules
 
"""
    the normalized configuraton of a firewall management to import
    this applies to a single management which might be either a global or a stand-alone management

    FwConfigNormalized:
    {
        'action': 'INSERT|UPDATE|DELETE',
        'network_objects': [ ... ],
        'service_objects': [ ... ],
        'users': [...],
        'zone_objects': [ ... ],
        'policies': [
            {
                'policy_name': 'pol1',
                'policy_uid': 'a32bc348234-23432a',
                'enforcing_gateway_uids':  [ ... ], // how to define order of policies? rule_order_num?
                'rules': [ { ... }, { ... }, ... ]
            }
        ]
    }
"""
class FwConfigNormalized(FwConfig):
    Action: ConfigAction
    # Networks: List[NetworkObject]
    Networks: List[dict]
    Services: List[dict]
    Users: List[dict]
    Zones: List[dict]
    Policies: dict
    Routing: List[dict]
    Interfaces: List[dict]

    def __init__(self, action: ConfigAction, networks, services, users, zones, policies, routing=[], interfaces=[], format=ConfFormat.NORMALIZED_LEGACY):
        super().__init__(format)
        self.IsSuperManagerConfig = False
        self.Action = action
        self.Networks = networks
        self.Services = services
        self.Users = users
        self.Zones = zones
        self.Policies = policies
        self.Routing = routing
        self.Interfaces = interfaces

    @classmethod
    def fromJson(cls, jsonDict):
        if 'routing' not in jsonDict:
            jsonDict.update({'routing': []})
        if 'interfaces' not in jsonDict:
            jsonDict.update({'interfaces': []})
        # default action (backward compatibility) is INSERT
        if 'action' not in jsonDict:
            jsonDict.update({'action': ConfigAction.INSERT})
        return cls(jsonDict['action'], 
                   jsonDict['network_objects'], 
                   jsonDict['service_objects'],
                   jsonDict['users'],
                   jsonDict['zone_objects'], 
                   jsonDict['policies'],
                   jsonDict['routing'],
                   jsonDict['interfaces']
                   )

    def __str__(self):
        return f"{self.Action}({str(self.Networks)})"

    def toJsonLegacy(self, withAction=True):
        config = {
            'network_objects': self.Networks,
            'service_objects': self.Services,
            'users': self.Users,
            'zone_objects': self.Zones,
            'rules': self.Policies,
            'routing': self.Routing,
            'interfaces': self.Interfaces
        }

        if withAction:
            config.update({'action': self.Action})

        return config
    
    def toJson(self):
        # json.load(self.serialize(withAction=True))
        return self.toJsonLegacy(withAction=False)

"""
    the normalized configuration of a firewall management to import
    this set of configs contains the full configuration of either a single manager or a super manager

    FwConfigManager:
    {
        'manager_uid': '33478afe-399378acd', // each manager only occurs once
        'is_global': False, // if True: rest of config exists in all dependant_managers (objects) and enforcing_gateways (rules) underneath
        'dependant_manager_uids': [ ... ], // empty if not global, all of them should be contained in the same import
        'configs': [ <FWConfigNormalized> ]
    }
"""
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

    def toJsonStringLegacy(self):
        configOut = {}
        for mgr in self.ManagerSet:
            for config in mgr.Configs:
                configOut.update(config.toJsonLegacy(withAction=False))
        return json.dumps(configOut, indent=2)

    def addManager(self, manager):
        self.ManagerSet.append(manager)

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


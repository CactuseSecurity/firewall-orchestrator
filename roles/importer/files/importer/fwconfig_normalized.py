from typing import List
import json
import time
import traceback

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState, ManagementDetails
from fwconfig_base import Gateway
from fwo_base import ConfigAction, ConfFormat

from fwo_base import deserializeClassToDictRecursively
from fwo_const import import_tmp_path
import fwo_globals
from fwconfig_base import FwoEncoder


class FwConfig():
    ConfigFormat: ConfFormat
    # Config: dict

    def __init__(self, configFormat: ConfFormat=ConfFormat.NORMALIZED, config={}):
        self.ConfigFormat = configFormat
       # self.Config = config

    # #@classmethod
    # def fromJson(cls, jsonDict):
    #     configFormatString = jsonDict['ConfigFormat']
    #     if configFormatString == 'NORMALIZED':
    #         # serialize everything into config
    #         Config = jsonDict['ManagerSet']
    #     else:
    #         Config = jsonDict['config']
    #     return cls(stringToEnum(ConfFormat, configFormatString), Config)

    # def __str__(self):
    #     return f"{self.ConfigType}({str(self.Config)})"

# Function to convert a string to an Enum
def stringToEnum(enum_class, string_value):
    try:
        return enum_class(string_value)
    except ValueError:
        raise ValueError(f"'{string_value}' is not a valid value for {enum_class.__name__}")


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
                'rules': [ { ... }, { ... }, ... ]
            }
        ],
        'gateways': # this is also a change, so these mappings are only listed once for insertion
        {
            'gw-uid-1': {
                'name': 'gw1',
                'global_policy_uid': 'pol-global-1',
                'policies': ['policy_uid_1', 'policy_uid_2']        # here order is the order of policies on the gateway
            }
        }

    }

    write methods to 
        a) split a config into < X MB chunks
        b) combine configs to a single config

"""
class FwConfigNormalized(FwConfig):
    action: ConfigAction
    # Networks: List[NetworkObject]
    network_objects: dict
    service_objects: dict
    users: dict
    zone_objects: dict
    rules: dict
    gateways: List[dict]
    # gateways: List[Gateway]

    def __init__(self, 
                 action: ConfigAction, 
                 network_objects = {}, 
                 service_objects = {}, 
                 users = {},
                 zone_objects = {}, 
                 rules = {}, 
                 gateways = [], 
                 ConfigFormat=ConfFormat.NORMALIZED_LEGACY, 
                 stripFields=True,
                 IsSuperManagerConfig=False
                 ):
        super().__init__(ConfigFormat)

        # do not want Config in this class
        # del self.Config

        self.IsSuperManagerConfig = IsSuperManagerConfig
        self.action = action
        formatOld = False

        if ConfigFormat==ConfFormat.NORMALIZED: # already latest format, just copy the objects 
            self.network_objects = network_objects
            self.service_objects = service_objects
            self.users = users
            self.zone_objects = zone_objects
            self.rules = rules
            self.gateways = gateways            
        else:
            for policyElement in rules:
                if isinstance(policyElement, dict):    # old format
                    formatOld = True
                    break
                elif isinstance(policyElement, str):   # found new format
                    rules[policyElement].Rules = FwConfigNormalized.convertListToDict(rules[policyElement].Rules, 'rule_uid')
                else:
                    logger = getFwoLogger()
                    logger.warning("found unknown policy format")

            if formatOld:                 # found old config format, do not adjust config
                self.network_objects = network_objects
                self.service_objects = service_objects
                self.users = users
                self.zone_objects = zone_objects
                self.rules = rules
                self.gateways = gateways
            else:
                self.network_objects = FwConfigNormalized.convertListToDict(FwConfigNormalized.deleteControlIdFromDictList(network_objects), 'obj_uid')
                self.service_objects = FwConfigNormalized.convertListToDict(FwConfigNormalized.deleteControlIdFromDictList(service_objects), 'svc_uid')
                self.users = FwConfigNormalized.convertListToDict(FwConfigNormalized.deleteControlIdFromDictList(users), 'user_uid')
                self.zone_objects = FwConfigNormalized.convertListToDict(FwConfigNormalized.deleteControlIdFromDictList(zone_objects), 'zone_name')
                self.rules = rules
                self.gateways = gateways
                if stripFields:
                    self.stripUnusedElements()
                self.ConfigFormat = ConfFormat.NORMALIZED


    # @classmethod
    # def fromJson(cls, jsonDict):
    #     if 'routing' not in jsonDict:
    #         jsonDict.update({'routing': []})
    #     if 'interfaces' not in jsonDict:
    #         jsonDict.update({'interfaces': []})
    #     # default action (backward compatibility) is INSERT
    #     if 'action' not in jsonDict:
    #         jsonDict.update({'action': ConfigAction.INSERT})
    #     return cls(jsonDict['action'], 
    #                FwConfigNormalized.convertListToDict(jsonDict['network_objects'], 'obj_uid'), 
    #                FwConfigNormalized.convertListToDict(jsonDict['service_objects'], 'svc_uid'),
    #                FwConfigNormalized.convertListToDict(jsonDict['users'], 'user_id'),
    #                FwConfigNormalized.convertListToDict(jsonDict['zone_objects'], 'zone_name'), 
    #                jsonDict['policies'],
    #                jsonDict['gateways']
    #                )

    @classmethod
    def convertListToDict(cls, listIn: List, idField: str) -> dict:
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

    def toJsonLegacy(self, withAction=True):
        rules = []
        gws = []
        if self.ConfigFormat == ConfFormat.NORMALIZED:
            for policyUid in self.rules:
                rules += self.rules[policyUid].toJsonLegacy()
            for gw in self.gateways:
                gws.append(gw.toJson())
        elif self.ConfigFormat == ConfFormat.NORMALIZED_LEGACY:
            rules = self.rules
        else:
            logger = getFwoLogger()
            logger.error("found no suitable config format")
            return {}
        
        config = {
            'network_objects': self.network_objects,
            'service_objects': self.service_objects,
            'users': self.users,
            'zone_objects': self.zone_objects,
            'rules': rules
        }
            # ,
            # 'gateways': gws

        if withAction:
            config.update({'action': self.action})

        return config

    def toJson(self, withAction=False):
        return deserializeClassToDictRecursively(self)

    def toJsonString(self, prettyPrint=False):
        jsonDict = self.toJson()
        if prettyPrint:
            return json.dumps(jsonDict, indent=2, cls=FwoEncoder)
        else:
            return json.dumps(jsonDict, cls=FwoEncoder)

    def stripUnusedElements(self):
        for policyName in self.rules:
            deleteDictElements(self.rules[policyName].Rules, ['control_id', 'rulebase_name'])

        FwConfigNormalized.deleteControlIdFromDictList(self.network_objects)
        FwConfigNormalized.deleteControlIdFromDictList(self.service_objects)
        FwConfigNormalized.deleteControlIdFromDictList(self.users)
        FwConfigNormalized.deleteControlIdFromDictList(self.zone_objects)

    @classmethod
    def deleteControlIdFromDictList(cls, dictListInOut: dict):
        if isinstance(dictListInOut, List): 
            deleteListDictElements(dictListInOut, ['control_id'])
        elif isinstance(dictListInOut, dict): 
            deleteDictElements(dictListInOut, ['control_id'])
        return dictListInOut


    # def toJson(self, prettyPrint=False, withAction=False):
    #     gws = []
    #     logger = getFwoLogger()

    #     policiesJson = {} 

    #     if self.ConfigFormat == ConfFormat.NORMALIZED:

    #         for gw in self.gateways:
    #             gws.append(gw.toJson())

    #         for polName in self.rules: 
    #             if isinstance(self.rules[polName], Policy):
    #                 policiesJson.update ( { polName: self.rules[polName].toJson() } )
    #             else:
    #                 logger.warning("should never occur")
    #                 policiesJson.update ( { polName: self.rules[polName] } )

    #     # elif self.ConfigFormat == ConfFormat.NORMALIZED:
    #     #     for policyUid in self.Policies:
    #     #         policiesJson += self.Policies[policyUid].toJsonLegacy()
    #     #     for gw in self.Gateways:
    #     #         gws.append(gw.toJson())
    #     elif self.ConfigFormat == ConfFormat.NORMALIZED_LEGACY:
    #         policiesJson = self.rules
    #     else:
    #         logger.error("found no suitable config format")
    #         return {}
    #     config = {
    #         'action': self.action,
    #         'network_objects': self.network_objects,
    #         'service_objects': self.service_objects,
    #         'users': self.users,
    #         'zone_objects': self.zone_objects,
    #         'policies': policiesJson,
    #         'gateways': gws
    #     }
    #     return config
    
    def split(self):
        return [self]   # for now not implemented

    @classmethod
    def join(cls, configList):
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

    def fillGateways(self, importState: ImportState):      
        for dev in importState.MgmDetails.Devices:
            gw = Gateway(f"{dev['name']}_{dev['local_rulebase_name']}",
                         dev['name'],
                         [],    # TODO: routing
                         [],    # TODO: interfaces
                         [dev['local_rulebase_name']],
                         [dev['package_name']],
                         None  # TODO: global policy UID
                         )
            self.gateways.append(gw)

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

# helpers

# def deleteAttributes(objList: List[object], attrNames: List[str]):
#     logger = getFwoLogger()
#     for obj in objList:
#         for attr in attrNames:
#             try:
#                 delattr(obj, attr)
#             except AttributeError as e:
#                 logger.warning(f"trying to remote attributes that does not exist: {e}")


# dictIn has the form:
# {
#    "x_uid": {
#         "field1": "value1"
#     },
#    "y_uid": {
#         "field1": "value1"
#     }
# }
#
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

from typing import List
import json

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState, ManagementDetails
from fwconfig_base import ConfigAction, ConfFormat, NetworkObject, Gateway, Policy


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
    Action: ConfigAction
    # Networks: List[NetworkObject]
    Networks: List[dict]
    Services: List[dict]
    Users: List[dict]
    Zones: List[dict]
    Policies: dict
    Gateways: List[Gateway]

    # def __init__(self, action: ConfigAction, networks, services, users, zones, policies, gateways, format=ConfFormat.NORMALIZED_LEGACY):
    def __init__(self, action: ConfigAction, networks, services, users, zones, policies, gateways, format=ConfFormat.NORMALIZED_LEGACY):
        super().__init__(format)
        self.IsSuperManagerConfig = False
        self.Action = action
        self.Networks = networks
        self.Services = services
        self.Users = users
        self.Zones = zones
        self.Policies = policies
        self.Gateways = gateways

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
                   jsonDict['gateways']
                   )

    def __str__(self):
        return f"{self.Action}({str(self.Networks)})"

    def toJsonLegacy(self, withAction=True):
        rules = []
        gws = []
        for policyUid in self.Policies:
            rules += self.Policies[policyUid].toJsonLegacy()
        for gw in self.Gateways:
            gws.append(gw.toJson())
            
        config = {
            'network_objects': self.Networks,
            'service_objects': self.Services,
            'users': self.Users,
            'zone_objects': self.Zones,
            'rules': rules,
            'gateways': gws
        }

        if withAction:
            config.update({'action': self.Action})

        return config
    
    def toJsonString(self, prettyPrint=False):
        json.dumps(self.toJson(prettyPrint=prettyPrint))

    def toJson(self, prettyPrint=False):
        gws = []
        for gw in self.Gateways:
            gws.append(gw.toJson())

        policies = []
        for policyName in self.Policies:
            deleteDictElements(self.Policies[policyName].Rules, ['control_id', 'rulebase_name'])
            policies.append(self.Policies[policyName].toJson())
        
        deleteDictElements(self.Networks, ['control_id'])
        deleteDictElements(self.Services, ['control_id'])
        deleteDictElements(self.Users, ['control_id'])
        deleteDictElements(self.Zones, ['control_id'])

        config = {
            'action': self.Action,
            'network_objects': self.Networks,
            'service_objects': self.Services,
            'users': self.Users,
            'zone_objects': self.Zones,
            'policies': policies,
            'gateways': gws
        }
        return config
    
    def split(self):
        return [self]   # for now not implemented

    @classmethod
    def join(cls, configList):
        resultingConfig = FwConfigNormalized()
        for conf in configList:
            resultingConfig.addElements(conf)
        return resultingConfig

    def addElements(self, config):
        self.Networks += config.Networks
        self.Services += config.Services
        self.Users += config.Users
        self.Zones += config.Zones
        self.Policies += config.Policies
        self.Gateways += config.Gateways

    def importConfig(self, importState: ImportState) -> int:
        self.fillGateways(importState)
        # do import
        return 0

    def saveLastConfig(self):
        pass

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
            self.Gateways.append(gw)

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

def deleteDictElements(dictList: List[object], attrNames: List[str]):
    logger = getFwoLogger()
    for d in dictList:
        for attr in attrNames:
            try:
                del d[attr]
            except KeyError as e:
                logger.warning(f"trying to remove element from dict that does not exist: {e}")

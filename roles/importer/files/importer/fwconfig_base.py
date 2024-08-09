import json
from typing import List
from enum import Enum, auto

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState, ManagementDetails
from fwo_base import calcManagerUidHash


class EnumEncoder(json.JSONEncoder):
    def default(self, obj):
        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name 
        return json.JSONEncoder.default(self, obj)

class ConfigAction(Enum):
    INSERT = auto()
    UPDATE = auto()
    DELETE = auto()

    # def toJson(self):
    #     json.dumps(self, cls=ConfigActionEncoder)

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

"""
Gateway
 {
    'gw-uid-1': {
        'name': 'gw1',
        'global_policy_uid': 'pol-global-1',
        'policies': ['policy_uid_1', 'policy_uid_2']        # here order is the order of policies on the gateway
        'nat-policies': ['nat_policy_uid_1']        # is always only a single policy?
    }
}
"""
class Gateway():
    Uid: str
    Name: str
    Routing: List[dict]
    Interfaces: List[dict]
    GlobalPolicyUid: str
    EnforcedPolicyUids: List[str]
    EnforcedNatPolicyUids: List[str]

    def __init__(self, Uid: str, Name: str, Interfaces: List[dict] = [], Routing: List[dict] = [], 
                 EnforcedPolicyUids: List[str]=[], EnforcedNatPolicyUids: List[str]=[], GlobalPolicyUid: str=None):
        self.Name = Name
        self.Uid = Uid
        self.Interfaces = Interfaces
        self.Routing = Routing
        self.EnforcedPolicyUids = EnforcedPolicyUids
        self.EnforcedNatPolicyUids = EnforcedNatPolicyUids
        self.GlobalPolicyUid = GlobalPolicyUid

    def toJson(self, prettyPrint=False):
        return {
            'uid': self.Uid,
            'name': self.Name,
            'routing': self.Routing,
            'interfaces': self.Interfaces,
            'GlobalPolicyUid': self.GlobalPolicyUid,
            'EnforcedPolicyUids': self.EnforcedPolicyUids,
            'EnforcedNatPolicyUids': self.EnforcedNatPolicyUids
        }
    
    @classmethod
    def buildGatewayList(cls, mgmDetails: dict) -> List['Gateway']:
        gws = []
        for gw in mgmDetails['devices']:
            gws.append(Gateway(gw['name'], f"{gw['name']}/{calcManagerUidHash(mgmDetails)}"))
        return gws

"""
'policy':
    {
        'policy_name': 'pol1',
        'policy_uid': 'a32bc348234-23432a',
        'rules': [ { ... }, { ... }, ... ]
    }
"""
class Policy():
    Uid: str
    Name: str
    EnforcingGatewayUids: List[str]
    Rules: List[dict]

    def __init__(self, Uid: str, Name: str, Rules: str=[]):
        self.Name = Uid
        self.Uid = Name
        self.Rules = Rules

    def toJson(self):
        return {
            'name': self.Name,
            'uid': self.Uid,
            'Rules': self.Rules
        }
                
    def toJsonLegacy(self):
        rules = []
        for ruleUid in self.Rules:
            rules.append(self.Rules[ruleUid])
        return {
            'name': self.Name,
            'uid': self.Uid,
            'Rules': rules
        }

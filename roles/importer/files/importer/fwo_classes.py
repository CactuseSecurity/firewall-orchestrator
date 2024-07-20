import time
from typing import List, Dict
from enum import Enum, auto

"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FwoConfig():
    FwoApiUri: str
    FwoUserMgmtApiUri: str
    ApiFetchSize: int
    ImporterPassword: str
    
    def __init__(self, fwoApiUri, fwoUserMgmtApiUri, apiFetchSize=500):
        if fwoApiUri is not None:
            self.FwoApiUri = fwoApiUri
        else:
            self.FwoApiUFwoUserMgmtApiri = None
        if fwoUserMgmtApiUri is not None:
            self.FwoUserMgmtApiUri = fwoUserMgmtApiUri
        else:
            self.FwoUserMgmtApiUri = None

        self.ImporterPassword = None
        self.ApiFetchSize = apiFetchSize

    @classmethod
    def from_json(cls, json_dict):
        fwoApiUri = json_dict['fwo_api_base_url']
        fwoUserMgmtApiUri = json_dict['user_management_api_base_url']
        
        return cls(fwoApiUri, fwoUserMgmtApiUri)

    def __str__(self):
        return f"{self.FwoApiUri}, {self.FwoUserMgmtApi}, {self.ApiFetchSize}"

    def setImporterPwd(self, importerPassword):
        self.ImporterPassword = importerPassword        

class ManagementDetails():
    Id: int
    Name: str
    Hostname: str
    ImportDisabled: bool
    Devices: dict
    ImporterHostname: str
    DeviceTypeName: str
    DeviceTypeVersion: str

    def __init__(self, hostname: str, id: int, importDisabled: bool, devices: Dict, 
                 importerHostname: str, name: str, deviceTypeName: str, deviceTypeVersion: str):
        self.Hostname = hostname
        self.Id = id
        self.ImportDisabled = importDisabled
        self.Devices = devices
        self.ImporterHostname = importerHostname
        self.Name = name
        self.DeviceTypeName = deviceTypeName
        self.DeviceTypeVersion = deviceTypeVersion

    @classmethod
    def from_json(cls, json_dict: Dict):
        Hostname = json_dict['hostname']
        Id = json_dict['id']
        ImportDisabled = json_dict['importDisabled']
        Devices = json_dict['devices']
        ImporterHostname = json_dict['importerHostname']
        Name = json_dict['name']
        DeviceTypeName = json_dict['deviceType']['name']
        DeviceTypeVersion = json_dict['deviceType']['version']
        return cls(Hostname, Id, ImportDisabled, Devices, ImporterHostname, Name, DeviceTypeName, DeviceTypeVersion)

    def __str__(self):
        return f"{self.Hostname}({self.Id})"


"""Used for storing state during import process per management"""
class ImportState():
    ErrorCount: int
    ChangeCount: int
    ErrorString: str
    StartTime: int
    DebugLevel: int
    Config2import: dict
    ConfigChangedSinceLastImport: bool
    FwoConfig: dict
    MgmDetails: dict
    FullMgmDetails: dict
    ImportId: int
    Jwt: str
    ImportFileName: str
    ForceImport: str


    def __init__(self, debugLevel, configChangedSinceLastImport, fwoConfig, mgmDetails, jwt, force):
        self.ErrorCount = 0
        self.ChangeCount = 0
        self.ErrorString = ''
        self.StartTime = int(time.time())
        self.DebugLevel = debugLevel
        self.Config2import = { "network_objects": [], "service_objects": [], "user_objects": [], "zone_objects": [], "rules": [] }
        self.ConfigChangedSinceLastImport = configChangedSinceLastImport
        self.FwoConfig = fwoConfig
        self.MgmDetails = ManagementDetails.from_json(mgmDetails)
        self.FullMgmDetails = mgmDetails
        self.ImportId = None
        self.Jwt = jwt
        self.ImportFileName = None
        self.ForceImport = force

    def __str__(self):
        return f"{str(self.ManagementDetails)}({self.age})"
    
    def setImportFileName(self, importFileName):
        self.ImportFileName = importFileName

    def setImportId(self, importId):
        self.ImportId = importId

    def setChangeCounter(self, changeNo):
        self.ChangeCount = changeNo

    def setErrorCounter(self, errorNo):
        self.ErrorCount = errorNo

    def setErrorString(self, errorStr):
        self.ErrorString = errorStr


"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""
class FwConfig():
    ConfigFormat: str
    Config: dict

    def __init__(self, configFormat=None, config=None):
        if configFormat is not None:
            self.ConfigFormat = configFormat
        else:
            self.ConfigFormat = None
        if config is not None:
            self.Config = config
        else:
            self.Config = {}

    @classmethod
    def from_json(cls, json_dict):
        ConfigFormat = json_dict['config-type']
        Config = json_dict['config']
        return cls(ConfigFormat, Config)

    def __str__(self):
        return f"{self.ConfigType}({str(self.Config)})"




class ConfigAction(Enum):
    INSERT = auto()
    UPDATE = auto()
    DELETE = auto()


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
    the normalized configuraton of a firewall management to import
    this applies to a single management which might be either a global or a stand-alone management

    FwConfigNormalized:
    {
        'action': 'INSERT|UPDATE|DELETE',
        'network_objects': [ ... ],
        'service_objects': [ ... ],
        'users': [...],
        'zones': [ ... ],
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
    Networks: List[NetworkObject]
    Services: List[Dict]
    Users: List[Dict]
    Zones: List[Dict]
    Policies: List[Dict]

    def __init__(self, action: ConfigAction, networks, services, users, zones, policies):
        self.IsSuperManagerConfig = False
        self.Action = action
        self.Networks = networks
        self.Services = services
        self.Users = users
        self.Zones = zones
        self.Policies = policies

    @classmethod
    def fromJson(cls, jsonDict):
        return cls(jsonDict['action'], 
                   jsonDict['network_objects'], 
                   jsonDict['service_objects'],
                   jsonDict['users'],
                   jsonDict['zones'], 
                   jsonDict['policies'])

    def __str__(self):
        return f"{self.Action}({str(self.Networks)})"


"""
    the normalized configuraton of a firewall management to import
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

    def __init__(self, ManagerUid: str, IsGlobal: bool, DependantManagerUids: List[str], Configs: List[FwConfigNormalized]):
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
    FwConfigManagerSet: [ FwConfigManager ]
"""
class FwConfigManagerSet():

    def __init__(self, ManagerSet: List[FwConfigManager]):
        self.ManagerSet = ManagerSet

    def __str__(self):
        return f"{str(self.ManagerSet)})"

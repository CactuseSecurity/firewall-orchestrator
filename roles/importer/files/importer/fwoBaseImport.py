import time
from typing import List, Dict

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
    def fromJson(cls, json_dict):
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
    def fromJson(cls, json_dict: Dict):
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
        self.MgmDetails = ManagementDetails.fromJson(mgmDetails)
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


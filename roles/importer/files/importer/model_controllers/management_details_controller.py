from typing import List, Dict
from models.management_details import ManagementDetails
import fwo_exceptions

class ManagementDetailsController(ManagementDetails):

    def __init__(self, hostname: str, id: int, uid: str, importDisabled: bool, devices: Dict, 
                 importerHostname: str, name: str, deviceTypeName: str, deviceTypeVersion: str, 
                 port: int = 443, secret: str = '', importUser: str = '', isSuperManager: bool = False, 
                 subManagerIds: List[int] = [], subManagers: List['ManagementDetails'] = [],
                 domainName: str = '', domainUid: str = ''):
        self.Hostname = hostname
        self.Id = id
        self.Uid = uid
        self.ImportDisabled = importDisabled
        self.Devices = devices
        self.ImporterHostname = importerHostname
        self.Name = name
        self.DeviceTypeName = deviceTypeName
        self.DeviceTypeVersion = deviceTypeVersion
        self.Port = port
        self.ImportUser = importUser
        self.Secret = secret
        self.IsSuperManager = isSuperManager
        self.SubManagerIds = subManagerIds
        self.SubManagers = subManagers
        self.DomainName = domainName
        self.DomainUid = domainUid

    @classmethod
    def fromJson(cls, json_dict: Dict):
        Hostname = json_dict['hostname']
        Id = json_dict['id']
        Uid = json_dict['uid']
        ImportDisabled = json_dict['importDisabled']
        Devices = json_dict['devices']
        ImporterHostname = json_dict['importerHostname']
        Name = json_dict['name']
        DeviceTypeName = json_dict['deviceType']['name']
        DeviceTypeVersion = json_dict['deviceType']['version']
        Port = json_dict['port']
        ImportUser = json_dict['import_credential']['user']
        Secret = json_dict['import_credential']['secret']
        IsSuperManager = json_dict["isSuperManager"]
        SubManagerIds = [subManager["id"] for subManager in json_dict["subManagers"]]
        SubManagers = [cls.fromJson(subManager) for subManager in json_dict["subManagers"]]
        domainName = json_dict['configPath']
        domainUid = json_dict['domainUid']

        return cls(Hostname, Id, Uid, ImportDisabled, Devices, ImporterHostname, Name, DeviceTypeName, DeviceTypeVersion,
                    port=Port, importUser=ImportUser, secret=Secret, isSuperManager = IsSuperManager, subManagerIds = SubManagerIds, 
                    subManagers = SubManagers, domainName = domainName, domainUid = domainUid)

    def __str__(self):
        return f"{self.Hostname}({self.Id})"
    

    # TODO: fix device type URIs
    def buildFwApiString(self):
        if self.DeviceTypeName == 'Check Point':
            return f"https://{self.Hostname}:{str(self.Port)}/web_api/"
        elif self.DeviceTypeName == 'CiscoFMC':
            return f"https://{self.Hostname}:{str(self.Port)}/api/fmc_platform/v1/"
        elif self.DeviceTypeName == 'Fortinet':
            return f"https://{self.Hostname}:{str(self.Port)}/api/v2/"
        elif self.DeviceTypeName == 'PaloAlto':
            return f"https://{self.Hostname}:{str(self.Port)}/restapi/v10.0/"
        elif self.DeviceTypeName == 'PaloAltoLegacy':
            return f"https://{self.Hostname}:{str(self.Port)}/restapi/v10.0/"
        else:
            raise fwo_exceptions.FwLoginFailed(f"Unsupported device type: {self.DeviceTypeName}")


    def getDomainString(self):
        return self.DomainUid if self.DomainUid != None else self.DomainName

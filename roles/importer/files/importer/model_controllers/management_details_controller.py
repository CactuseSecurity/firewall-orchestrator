from typing import List, Dict
from models.management_details import ManagementDetails

class ManagementDetailsController(ManagementDetails):

    def __init__(self, hostname: str, id: int, uid: str, importDisabled: bool, devices: Dict, 
                 importerHostname: str, name: str, deviceTypeName: str, deviceTypeVersion: str, 
                 port: int = 443, secret: str = '', importUser: str = '', isSuperManager: bool = False, SubManager: List[int] = []):
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
        self.Secret = secret
        self.ImportUser = importUser
        self.IsSuperManager = isSuperManager
        self.SubManager = SubManager

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

        return cls(Hostname, Id, Uid, ImportDisabled, Devices, ImporterHostname, Name, DeviceTypeName, DeviceTypeVersion, port=Port, importUser=ImportUser, secret=Secret)

    def __str__(self):
        return f"{self.Hostname}({self.Id})"

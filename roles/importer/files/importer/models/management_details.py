from typing import List
from pydantic import Field

class ManagementDetails():
    Id: int
    Name: str
    Uid: str
    Hostname: str
    ImportDisabled: bool
    Devices: dict
    ImporterHostname: str
    DeviceTypeName: str
    DeviceTypeVersion: str
    Port: int
    ImportUser: str
    Secret: str
    IsSuperManager: bool
    SubManagerIds: List[int] = []
    DomainName: str = Field(alias='configPath', default='')
    DomainUid: str = Field(alias='domainUid', default='')
    SubManagers: List['ManagementDetails'] = Field(default=[], alias='subManager')

from pydantic import Field

class Management():
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
    SubManagerIds: list[int] = []
    DomainName: str = Field(alias='configPath', default='')
    DomainUid: str = Field(alias='domainUid', default='')
    SubManagers: list['Management'] = Field(default=[], alias='subManager')

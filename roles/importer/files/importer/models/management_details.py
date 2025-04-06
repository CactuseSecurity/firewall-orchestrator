from typing import List
# from pydantic import BaseModel

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
    SubManagerIds: List[int]

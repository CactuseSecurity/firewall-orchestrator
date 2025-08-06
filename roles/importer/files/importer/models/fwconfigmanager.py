from pydantic import BaseModel
from models.fwconfig_normalized import FwConfigNormalized

class FwConfigManager(BaseModel):
    ManagerUid: str
    ManagerName: str
    IsSuperManager: bool = False
    DomainUid: str
    DomainName: str
    SubManagerIds: list[int] = []
    Configs: list[FwConfigNormalized] = []

    class Config:
        arbitrary_types_allowed = True

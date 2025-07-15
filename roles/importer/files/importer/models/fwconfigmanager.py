from pydantic import BaseModel
from typing import List
from models.fwconfig_normalized import FwConfigNormalized

class FwConfigManager(BaseModel):
    ManagerUid: str
    ManagerName: str
    IsSuperManager: bool = False
    DomainUid: str
    DomainName: str
    SubManagerIds: List[int] = []
    Configs: List[FwConfigNormalized] = []

    class Config:
        arbitrary_types_allowed = True

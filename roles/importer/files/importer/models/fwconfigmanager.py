from pydantic import BaseModel
from typing import List
from models.fwconfig_normalized import FwConfigNormalized

class FwConfigManager(BaseModel):
    ManagerUid: str
    ManagerName: str
    IsGlobal: bool = False
    DependantManagerUids: List[str] = []
    Configs: List[FwConfigNormalized] = []

    class Config:
        arbitrary_types_allowed = True

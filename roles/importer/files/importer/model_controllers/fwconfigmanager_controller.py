from typing import Any
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager

class FwConfigManagerController(FwConfigManager):
    ManagerUid: str
    ManagerName: str
    IsGlobal: bool = False
    DependantManagerUids: list[str] = []
    Configs: list[FwConfigNormalized] = []


    model_config = {
        "arbitrary_types_allowed": True
    }
    
    @classmethod
    def fromJson(cls, jsonDict: dict[str, Any]) -> 'FwConfigManagerController':
        ManagerUid: str = jsonDict['manager_uid']
        ManagerName: str = jsonDict['mgm_name']
        IsGlobal: bool = jsonDict['is_global']
        DependantManagerUids: list[str] = jsonDict['dependant_manager_uids']
        Configs: list[FwConfigNormalized] = jsonDict['configs']
        return cls(ManagerUid, ManagerName, IsGlobal, DependantManagerUids, Configs)#type: ignore # TODO: this class does not have a Constructor!

    def __str__(self):
        return f"{self.ManagerUid}({str(self.Configs)})"

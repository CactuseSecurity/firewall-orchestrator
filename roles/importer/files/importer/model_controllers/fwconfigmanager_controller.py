from typing import List
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager

class FwConfigManagerController(FwConfigManager):
    ManagerUid: str
    ManagerName: str
    IsGlobal: bool = False
    DependantManagerUids: List[str] = []
    Configs: List[FwConfigNormalized] = []

    class Config:
        arbitrary_types_allowed = True


    # def __init__(self, ManagerUid: str, ManagerName: str, IsGlobal: bool=False, DependantManagerUids: List[str]=[], Configs: List[FwConfigNormalized]=[]):
    # def __init__(self, ManagerUid: str, ManagerName: str, IsGlobal: bool=False, DependantManagerUids: List[str]=[], Configs: List[dict]=[]):
    #     """
    #         mandatory parameter: ManagerUid, 
    #     """
    #     self.ManagerUid = ManagerUid
    #     self.ManagerName = ManagerName
    #     self.IsGlobal = IsGlobal
    #     self.DependantManagerUids = DependantManagerUids
    #     self.Configs = Configs

    @classmethod
    def fromJson(cls, jsonDict):
        ManagerUid = jsonDict['manager_uid']
        ManagerName = jsonDict['mgm_name']
        IsGlobal = jsonDict['is_global']
        DependantManagerUids = jsonDict['dependant_manager_uids']
        Configs = jsonDict['configs']
        return cls(ManagerUid, ManagerName, IsGlobal, DependantManagerUids, Configs)

    def __str__(self):
        return f"{self.ManagerUid}({str(self.Configs)})"

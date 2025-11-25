from typing import Any
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager

class FwConfigManagerController(FwConfigManager):
    manager_uid: str
    manager_name: str
    is_global: bool = False
    dependant_manager_uids: list[str] = []
    configs: list[FwConfigNormalized] = []
    model_config: dict[str, Any] = {
        "arbitrary_types_allowed": True
    }

    def __init__(self, manager_uid: str, manager_name: str, is_global: bool, dependant_manager_uids: list[str], configs: list[FwConfigNormalized]):
        self.manager_uid = manager_uid
        self.manager_name = manager_name
        self.is_global = is_global
        self.dependant_manager_uids = dependant_manager_uids
        self.configs = configs
    
    @classmethod
    def fromJson(cls, jsonDict: dict[str, Any]) -> 'FwConfigManagerController':
        manager_uid: str = jsonDict['manager_uid']
        manager_name: str = jsonDict['mgm_name']
        is_global: bool = jsonDict['is_global']
        dependant_manager_uids: list[str] = jsonDict['dependant_manager_uids']
        configs: list[FwConfigNormalized] = jsonDict['configs']
        return cls(manager_uid, manager_name, is_global, dependant_manager_uids, configs)

    def __str__(self):
        return f"{self.manager_uid}({str(self.configs)})"

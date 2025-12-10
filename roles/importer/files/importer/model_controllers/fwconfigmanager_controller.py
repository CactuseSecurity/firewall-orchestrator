from typing import Any

from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager


class FwConfigManagerController(FwConfigManager):
    manager_uid: str
    manager_name: str
    is_global: bool = False
    dependant_manager_uids: list[str]
    configs: list[FwConfigNormalized]
    model_config = {"arbitrary_types_allowed": True}  # noqa: RUF012

    def __init__(
        self,
        manager_uid: str,
        manager_name: str,
        is_global: bool,
        dependant_manager_uids: list[str],
        configs: list[FwConfigNormalized],
    ):
        self.manager_uid = manager_uid
        self.manager_name = manager_name
        self.is_global = is_global
        self.dependant_manager_uids = dependant_manager_uids
        self.configs = configs

    @classmethod
    def from_json(cls, json_dict: dict[str, Any]) -> "FwConfigManagerController":
        manager_uid: str = json_dict["manager_uid"]
        manager_name: str = json_dict["mgm_name"]
        is_global: bool = json_dict["is_global"]
        dependant_manager_uids: list[str] = json_dict["dependant_manager_uids"]
        configs: list[FwConfigNormalized] = json_dict["configs"]
        return cls(manager_uid, manager_name, is_global, dependant_manager_uids, configs)

    def __str__(self):
        return f"{self.manager_uid}({self.configs!s})"

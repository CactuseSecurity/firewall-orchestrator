from typing import Any

from fwconfig_base import ConfFormat
from models.fwconfigmanager import FwConfigManager
from pydantic import BaseModel

"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""


class FwConfigManagerList(BaseModel):
    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED
    ManagerSet: list[FwConfigManager] = []
    native_config: (
        dict[str, Any] | None
    ) = {}  # native config as dict, if available # TODO: change inital value to None?

    model_config = {"arbitrary_types_allowed": True}

    def __str__(self):
        return f"{self.ManagerSet!s})"

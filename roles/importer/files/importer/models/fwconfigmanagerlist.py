from pydantic import BaseModel

from fwconfig_base import ConfFormat
from models.fwconfigmanager import FwConfigManager

"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""
class FwConfigManagerList(BaseModel):

    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED
    ManagerSet: list[FwConfigManager] = []
    native_config: dict = {}  # native config as dict, if available

    model_config = {
        "arbitrary_types_allowed": True
    }

    def __str__(self):
        return f"{str(self.ManagerSet)})"

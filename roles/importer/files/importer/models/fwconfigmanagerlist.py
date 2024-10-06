from pydantic import BaseModel
from typing import List
from fwconfig_base import ConfFormat
from models.fwconfigmanager import FwConfigManager

"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""
class FwConfigManagerList(BaseModel):

    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED
    ManagerSet: List[FwConfigManager] = []

    class Config:
        arbitrary_types_allowed = True

    def __str__(self):
        return f"{str(self.ManagerSet)})"

    # def toJson(self):
    #     return deserializeClassToDictRecursively(self)

    # def toJsonString(self, prettyPrint=False):
    #     jsonDict = self.toJson()
    #     if prettyPrint:
    #         return json.dumps(jsonDict, indent=2, cls=FwoEncoder)
    #     else:
    #         return json.dumps(jsonDict)

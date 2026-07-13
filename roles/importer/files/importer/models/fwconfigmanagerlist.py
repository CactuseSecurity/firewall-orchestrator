from __future__ import annotations

from importlib import import_module
from typing import TYPE_CHECKING, Any

from fwo_enums import ConfFormat
from pydantic import BaseModel

if TYPE_CHECKING:
    from models.fwconfigmanager import FwConfigManager

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


FwConfigManagerList.model_rebuild(
    _types_namespace={"FwConfigManager": import_module("models.fwconfigmanager").FwConfigManager}
)

from typing import Any

from fwo_base import ConfFormat
from pydantic import BaseModel

"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""


class FwConfig(BaseModel):
    ConfigFormat: ConfFormat
    FwConf: dict[str, Any] = {}

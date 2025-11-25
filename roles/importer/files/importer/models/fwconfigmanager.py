from pydantic import BaseModel
from models.fwconfig_normalized import FwConfigNormalized

class FwConfigManager(BaseModel):
    manager_uid: str
    manager_name: str
    is_super_manager: bool = False
    domain_uid: str
    domain_name: str
    sub_manager_ids: list[int] = []
    configs: list[FwConfigNormalized] = []


    model_config = {
        "arbitrary_types_allowed": True
    }
    
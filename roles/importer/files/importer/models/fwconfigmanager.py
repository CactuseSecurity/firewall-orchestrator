from typing import Any
from pydantic import BaseModel, ConfigDict, Field
from models.fwconfig_normalized import FwConfigNormalized

class FwConfigManager(BaseModel):
    manager_uid: str = Field(description="Unique identifier string of the management", alias="ManagerUid")
    manager_name: str = Field(description="Name of the management", alias="ManagerName")
    is_super_manager: bool = Field(description="Indicates if the management is a super manager", alias="IsSuperManager", default=False)
    domain_uid: str = Field(description="Domain UID", alias="DomainUid")
    domain_name: str = Field(description="Domain name", alias="DomainName")
    sub_manager_ids: list[int] = Field(default=[], description="List of sub-manager IDs", alias="SubManagerIds")
    configs: list[FwConfigNormalized] = Field(default=[], description="List of normalized firewall configurations", alias="Configs")

    model_config = ConfigDict(populate_by_name=True)


    def model_dump(self, **kwargs: Any) -> dict[str, Any]:
        kwargs.setdefault('by_alias', True)
        return super().model_dump(**kwargs)

    def model_dump_json(self, **kwargs: Any) -> str:
        kwargs.setdefault('by_alias', True)
        return super().model_dump_json(**kwargs)
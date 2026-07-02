from __future__ import annotations

from importlib import import_module
from typing import TYPE_CHECKING, Any

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_pascal

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized


class FwConfigManager(BaseModel):
    model_config = ConfigDict(alias_generator=to_pascal, validate_by_name=True)

    manager_uid: str = Field(description="Unique identifier string of the management")
    manager_name: str = Field(description="Name of the management")
    is_super_manager: bool = Field(description="Indicates if the management is a super manager", default=False)
    domain_uid: str | None = Field(default=None, description="Domain UID")
    domain_name: str | None = Field(default=None, description="Domain name")
    sub_manager_ids: list[int] = Field(default=[], description="List of sub-manager IDs")
    configs: list[FwConfigNormalized] = Field(description="List of normalized firewall configurations")

    def model_dump(self, **kwargs: Any) -> dict[str, Any]:
        kwargs.setdefault("by_alias", True)
        return super().model_dump(**kwargs)

    def model_dump_json(self, **kwargs: Any) -> str:
        kwargs.setdefault("by_alias", True)
        return super().model_dump_json(**kwargs)


FwConfigManager.model_rebuild(
    _types_namespace={"FwConfigNormalized": import_module("models.fwconfig_normalized").FwConfigNormalized}
)

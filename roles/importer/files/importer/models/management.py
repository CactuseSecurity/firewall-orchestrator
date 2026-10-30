from __future__ import annotations

from typing import Any

from models.credential import AdditionalCredential  # noqa: TC002 - Pydantic resolves this type at runtime
from pydantic import BaseModel


class Management(BaseModel):
    mgm_id: int
    name: str
    uid: str
    is_super_manager: bool
    hostname: str
    import_disabled: bool
    devices: list[dict[str, Any]]
    importer_hostname: str
    device_type_name: str
    device_type_version: str
    port: int
    import_user: str
    secret: str
    sub_manager_ids: list[int]
    current_mgm_id: int
    current_mgm_is_super_manager: bool
    domain_name: str | None
    domain_uid: str | None
    sub_managers: list[Management]
    additional_credential: AdditionalCredential | None = None
    cloud_tenant_id: str | None = None
    cloud_subscription_id: str | None = None

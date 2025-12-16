from typing import Any
from pydantic import BaseModel, ConfigDict

class Management(BaseModel):
    model_config = ConfigDict(arbitrary_types_allowed=True)
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
    domain_name: str
    domain_uid: str
    sub_managers: list['Management']
    cloud_client_id: str | None = None
    cloud_client_secret: str | None = None

# Resolve forward reference to self (`sub_managers`) for pydantic v2
Management.model_rebuild()  # type: ignore[attr-defined]

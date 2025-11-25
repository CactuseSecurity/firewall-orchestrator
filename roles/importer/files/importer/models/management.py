from typing import Any
from pydantic import BaseModel, ConfigDict, Field

class Management(BaseModel):
    id: int = Field(description="Unique identifier for the (super-)management", alias="Id")
    name: str = Field(description="Name of the management", alias="Name")
    uid: str = Field(description="Unique identifier string of the management", alias="Uid")
    is_super_manager: bool = Field(description="Indicates if the management is a super manager", alias="IsSuperManager")
    hostname: str = Field(description="Hostname of the management server", alias="Hostname")
    import_disabled: bool = Field(description="Indicates if import is disabled for the management", alias="ImportDisabled")
    devices: list[dict[str, Any]] = Field(description="Dictionary of devices managed by this entity", alias="Devices")
    importer_hostname: str = Field(description="Hostname of the machine running the importer", alias="ImporterHostname")
    device_type_name: str = Field(description="Name of the device type", alias="DeviceTypeName")
    device_type_version: str = Field(description="Version of the device type", alias="DeviceTypeVersion")
    port: int = Field(description="Port used for management communication", alias="Port")
    import_user: str = Field(description="Username used for import operations", alias="ImportUser")
    secret: str = Field(description="Secret or password for import operations", alias="Secret")
    sub_manager_ids: list[int] = Field(default=[], description="List of sub-manager IDs", alias="SubManagerIds")
    current_mgm_id: int = Field(description="Tracks the current management in multi-management imports", alias="CurrentMgmId")
    current_mgm_is_super_manager: bool = Field(description="Indicates if the current management is a super manager", alias="CurrentMgmIsSuperManager")
    domain_name: str = Field(alias='configPath', default='', description="Domain name")
    domain_uid: str = Field(alias='domainUid', default='', description="Domain UID")
    sub_managers: list['Management'] = Field(default=[], alias='subManager', description="List of sub-manager entities")

    model_config = ConfigDict(populate_by_name=True)

    # Override model_dump (returns a dictionary)
    def model_dump(self, **kwargs: Any) -> dict[str, Any]:
        # Set by_alias to True if not explicitly provided
        kwargs.setdefault('by_alias', True)
        return super().model_dump(**kwargs)

    # Override model_dump_json (returns a JSON string)
    def model_dump_json(self, **kwargs: Any) -> str:
        # Set by_alias to True if not explicitly provided
        kwargs.setdefault('by_alias', True)
        return super().model_dump_json(**kwargs)

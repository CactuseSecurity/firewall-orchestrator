from typing import Any

from netaddr import AddrFormatError, IPNetwork
from pydantic import BaseModel, field_serializer, field_validator


class NetworkObject(BaseModel):
    obj_uid: str
    obj_name: str
    obj_ip: IPNetwork | None = None
    obj_ip_end: IPNetwork | None = None
    obj_color: str
    obj_typ: str
    obj_member_refs: str | None = None
    obj_member_names: str | None = None
    obj_comment: str | None = None

    @field_validator("obj_ip", "obj_ip_end", mode="before")
    def convert_strings_to_ip_objects(self, value: object, info: Any) -> IPNetwork | None:
        """
        Convert string values to IPNetwork objects, treating 'None' or empty as None.
        """
        if value is None:
            return None
        if isinstance(value, IPNetwork):
            return value

        s = str(value).strip()
        if s.lower() == "none" or s == "":
            return None

        try:
            return IPNetwork(s)
        except AddrFormatError as e:
            raise ValueError(f"Invalid {info.field_name} network format: {value}") from e

    @field_serializer("obj_ip", "obj_ip_end")
    def serialize_ipnetwork(self, value: IPNetwork | None, _info: Any) -> str | None:
        """
        Serialize IPNetwork objects to strings, keeping None as None.
        """
        return None if value is None else str(value)

    model_config = {"arbitrary_types_allowed": True}


class NetworkObjectForImport:
    obj_uid: str
    obj_name: str
    obj_ip: str | None
    obj_ip_end: str | None
    obj_color_id: int | None
    obj_member_refs: str | None
    obj_member_names: str | None
    obj_comment: str | None
    mgm_id: int
    obj_create: int
    obj_last_seen: int
    obj_typ_id: int

    def __init__(self, nw_object: NetworkObject, mgm_id: int, import_id: int, color_id: int, typ_id: int):
        self.obj_uid = nw_object.obj_uid
        self.obj_name = nw_object.obj_name
        self.obj_ip = str(nw_object.obj_ip)
        self.obj_ip_end = str(nw_object.obj_ip_end)
        self.obj_color_id = color_id
        self.obj_member_refs = nw_object.obj_member_refs
        self.obj_member_names = nw_object.obj_member_names
        self.obj_comment = nw_object.obj_comment
        self.mgm_id = mgm_id
        self.obj_create = import_id
        self.obj_last_seen = import_id
        self.obj_typ_id = typ_id

    def to_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {
            "obj_uid": self.obj_uid,
            "obj_name": self.obj_name,
            "obj_color_id": self.obj_color_id,
            "obj_member_refs": self.obj_member_refs,
            "obj_member_names": self.obj_member_names,
            "obj_comment": self.obj_comment,
            "mgm_id": self.mgm_id,
            "obj_create": self.obj_create,
            "obj_last_seen": self.obj_last_seen,
            "obj_typ_id": self.obj_typ_id,
        }

        if self.obj_ip is not None and self.obj_ip != "None":
            result.update({"obj_ip": self.obj_ip})
        if self.obj_ip_end is not None and self.obj_ip_end != "None":
            result.update({"obj_ip_end": self.obj_ip_end})
        return result

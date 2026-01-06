from typing import Any

from pydantic import BaseModel


class ServiceObject(BaseModel):
    svc_uid: str
    svc_name: str
    svc_port: int | None = None
    svc_port_end: int | None = None
    svc_color: str
    svc_typ: str  # TODO: ENUM
    ip_proto: int | None = None
    svc_member_refs: str | None = None
    svc_member_names: str | None = None
    svc_comment: str | None = None
    svc_timeout: int | None = None
    rpc_nr: str | None = None


class ServiceObjectForImport:
    svc_uid: str
    svc_name: str
    svc_port: int | None
    svc_port_end: int | None
    svc_color_id: int
    svc_typ: str  # TODO: ENUM
    ip_proto_id: int | None
    svc_member_refs: str | None
    svc_member_names: str | None
    svc_comment: str | None
    svc_timeout: int | None
    svc_rpcnr: str | None
    mgm_id: int
    svc_create: int
    svc_last_seen: int
    svc_typ_id: int

    def __init__(self, svc_object: ServiceObject, mgm_id: int, import_id: int, color_id: int, typ_id: int):
        self.svc_uid = svc_object.svc_uid
        self.svc_name = svc_object.svc_name
        self.svc_port = svc_object.svc_port
        self.svc_port_end = svc_object.svc_port_end
        self.svc_color_id = color_id
        self.svc_typ_id = typ_id
        self.ip_proto_id = svc_object.ip_proto
        self.svc_member_refs = svc_object.svc_member_refs
        self.svc_member_names = svc_object.svc_member_names
        self.svc_comment = svc_object.svc_comment
        self.svc_timeout = svc_object.svc_timeout
        self.svc_rpcnr = svc_object.rpc_nr
        self.mgm_id = mgm_id
        self.svc_create = import_id
        self.svc_last_seen = import_id

    def to_dict(self) -> dict[str, Any]:
        return {
            "svc_uid": self.svc_uid,
            "svc_name": self.svc_name,
            "svc_port": self.svc_port,
            "svc_port_end": self.svc_port_end,
            "ip_proto_id": self.ip_proto_id,
            "svc_color_id": self.svc_color_id,
            "svc_member_refs": self.svc_member_refs,
            "svc_member_names": self.svc_member_names,
            "svc_comment": self.svc_comment,
            "svc_create": self.svc_create,
            "svc_last_seen": self.svc_last_seen,
            "svc_typ_id": self.svc_typ_id,
            "svc_rpcnr": self.svc_rpcnr,
            "svc_timeout": self.svc_timeout,
            "mgm_id": self.mgm_id,
        }

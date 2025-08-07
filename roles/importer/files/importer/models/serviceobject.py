from pydantic import BaseModel
import json


class ServiceObject(BaseModel):
    svc_uid: str
    svc_name: str
    svc_port: int|None = None
    svc_port_end: int|None = None
    svc_color: str
    svc_typ: str        # TODO: ENUM
    ip_proto: int|None = None
    svc_member_refs: str|None = None
    svc_member_names: str|None = None
    svc_comment: str|None = None
    svc_timeout: int|None = None
    rpc_nr: str|None = None


class ServiceObjectForImport():
    svc_uid: str
    svc_name: str
    svc_port: int|None = None
    svc_port_end: int|None = None
    svc_color_id: int|None = None
    svc_typ: str # TODO: ENUM
    ip_proto_id: int|None = None
    svc_member_refs: str|None = None
    svc_member_names: str|None = None
    svc_comment: str|None = None
    svc_timeout: int|None = None
    svc_rpcnr: str|None = None
    mgm_id: int
    svc_create: int
    svc_last_seen: int
    svc_removed: int|None = None
    svc_typ_id: int


    def __init__(self, svcObject: ServiceObject, mgmId: int, importId: int, colorId: int, typId: int):
        self.svc_uid = svcObject.svc_uid
        self.svc_name = svcObject.svc_name
        self.svc_port = svcObject.svc_port
        self.svc_port_end = svcObject.svc_port_end
        self.ip_proto_id = svcObject.ip_proto
        self.svc_color_id = colorId
        self.svc_member_refs = svcObject.svc_member_refs
        self.svc_member_names = svcObject.svc_member_names
        self.svc_comment = svcObject.svc_comment
        self.mgm_id = mgmId
        self.svc_create = importId
        self.svc_last_seen = importId
        self.svc_typ_id = typId
        self.svc_rpcnr = svcObject.rpc_nr


    def toDict (self):
        return  {
            'svc_uid': self.svc_uid,
            'svc_name': self.svc_name,
            'svc_port': self.svc_port,
            'svc_port_end': self.svc_port_end,
            'ip_proto_id': self.ip_proto_id,
            'svc_color_id': self.svc_color_id,
            'svc_member_refs': self.svc_member_refs,
            'svc_member_names': self.svc_member_names,
            'svc_comment': self.svc_comment,
            'svc_create': self.svc_create,
            'svc_last_seen': self.svc_last_seen,
            'svc_typ_id': self.svc_typ_id,
            'svc_rpcnr': self.svc_rpcnr,
            'mgm_id': self.mgm_id
        }

    # def toJson (self):
    #     return json.dumps(self.toDict())

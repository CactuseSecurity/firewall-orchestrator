from pydantic import BaseModel, field_validator, field_serializer
from netaddr import IPNetwork, AddrFormatError

class NetworkObject(BaseModel):
    obj_uid: str
    obj_name: str
    obj_ip: IPNetwork | None = None
    obj_ip_end: IPNetwork | None = None
    obj_color: str
    obj_typ: str # host ->   | network ->  | ip_range -> | group ->
    obj_member_refs: str | None = None
    obj_member_names: str | None = None
    obj_comment: str | None = None


    @field_validator('obj_ip', 'obj_ip_end', mode='before')
    def convert_strings_to_ip_objects(cls, value, info):
        """
        Convert string values to IPNetwork objects, treating 'None' or empty as None.
        """
        if value is None:
            return None
        if isinstance(value, IPNetwork):
            return value

        s = str(value).strip()
        if s.lower() == 'none' or s == '':
            return None

        try:
            return IPNetwork(s)
        except AddrFormatError as e:
            raise ValueError(f"Invalid {info.field_name} network format: {value}") from e

    @field_serializer('obj_ip', 'obj_ip_end')
    def serialize_ipnetwork(self, value: IPNetwork | None, _info):
        """
        Serialize IPNetwork objects to strings, keeping None as None.
        """
        return None if value is None else str(value)

    model_config = {
        "arbitrary_types_allowed": True
    }



class NetworkObjectForImport():
    obj_uid: str
    obj_name: str
    obj_ip: str|None
    obj_ip_end: str|None
    obj_color_id: int|None
    obj_member_refs: str|None
    obj_member_names: str|None
    obj_comment: str|None
    mgm_id: int
    obj_create: int
    obj_last_seen: int
    obj_typ_id: int

    def __init__(self, nwObject: NetworkObject, mgmId: int, importId: int, colorId: int, typId: int):
        self.obj_uid = nwObject.obj_uid
        self.obj_name = nwObject.obj_name
        self.obj_ip = str(nwObject.obj_ip)
        self.obj_ip_end = str(nwObject.obj_ip_end)
        self.obj_color_id = colorId
        self.obj_member_refs = nwObject.obj_member_refs
        self.obj_member_names = nwObject.obj_member_names
        self.obj_comment = nwObject.obj_comment
        self.mgm_id = mgmId
        self.obj_create = importId
        self.obj_last_seen = importId
        self.obj_typ_id = typId

    def toDict (self):
        result = {
            'obj_uid': self.obj_uid,
            'obj_name': self.obj_name,
            'obj_color_id': self.obj_color_id,
            'obj_member_refs': self.obj_member_refs,
            'obj_member_names': self.obj_member_names,
            'obj_comment': self.obj_comment,
            'mgm_id': self.mgm_id,
            'obj_create': self.obj_create,
            'obj_last_seen': self.obj_last_seen,
            'obj_typ_id': self.obj_typ_id
        }

        if self.obj_ip is not None and self.obj_ip != 'None':
            result.update({'obj_ip': self.obj_ip})
        if self.obj_ip_end is not None and self.obj_ip_end != 'None':
            result.update({'obj_ip_end': self.obj_ip_end})
        return result

from pydantic import BaseModel, validator, root_validator
from netaddr import IPAddress, IPNetwork, AddrFormatError
from typing import List, Optional
import json

class NetworkObject(BaseModel):
    obj_uid: str
    obj_name: str
    obj_ip: Optional[IPNetwork] = None
    obj_ip_end: Optional[IPNetwork] = None
    obj_color: str
    obj_typ: str
    obj_member_refs: Optional[str] = None
    obj_member_names: Optional[str] = None
    obj_comment: Optional[str] = None


    @validator('obj_ip', 'obj_ip_end', pre=True)
    def convert_strings_to_ip_objects(cls, value, field):
        """
        Convert string values to IPNetwork or IPAddress objects depending on the field type.
        """
        if isinstance(value, str):
            # Determine the type of the field and convert appropriately
            if field.name == 'obj_ip' or field.name == 'obj_ip_end':
                try:
                    return IPNetwork(value)
                except AddrFormatError as e:
                    raise ValueError(f"Invalid network format: {value}") from e
        return value

    # @root_validator(pre=True)
    # def validate_ip_addresses(cls, values):

    #     obj_ip = values.get("obj_ip")
    #     obj_ip_end = values.get("obj_ip_end")

    #     # for groups we ignore ip addresses
    #     if values.get('obj_typ') == 'group':
    #         return values
    #     else:
    #         try:
    #             if IPNetwork(obj_ip) and IPNetwork(obj_ip_end):
    #                 return values
    #         except AddrFormatError as e:
    #             raise ValueError(f"Invalid network address format: {obj_ip}") from e

    #     return values


    class Config:
        arbitrary_types_allowed = True


class NetworkObjectForImport():
    obj_uid: str
    obj_name: str
    obj_ip: Optional[IPNetwork] = None
    obj_ip_end: Optional[IPNetwork] = None
    obj_color_id: Optional[int]
    obj_member_refs: Optional[str] = None
    obj_member_names: Optional[str] = None
    obj_comment: Optional[str] = None
    mgm_id: int
    obj_create: int
    obj_last_seen: int
    obj_removed: Optional[int]
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

    # def toJson (self):
    #     nwObjDict = self.toDict()
    #     return nwObjDict
    #     # result = json.dumps(nwObjDict)
        # return result
        # return json.dumps(self.toDict())

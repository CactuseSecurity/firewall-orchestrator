from pydantic import BaseModel, validator, root_validator
from netaddr import IPAddress, IPNetwork, AddrFormatError
import json

class NetworkObject(BaseModel):
    obj_uid: str
    obj_name: str
    obj_ip: IPNetwork|None = None
    obj_ip_end: IPNetwork|None = None
    obj_color: str
    obj_typ: str
    obj_member_refs: str|None = None
    obj_member_names: str|None = None
    obj_comment: str|None = None

    # convert IPNetworks to strings
    def json(self, **kwargs):
        # Call the default json method from BaseModel
        data = super().json(**kwargs)
        # Replace the IPNetwork objects with their string representations
        data = data.replace(str(self.obj_ip), str(self.obj_ip))
        data = data.replace(str(self.obj_ip_end), str(self.obj_ip_end))
        return data

    def dict(self, **kwargs):
        # Create a dictionary representation and convert IPNetwork to string
        original_dict = super().dict(**kwargs)
        original_dict['obj_ip'] = str(self.obj_ip)  # Convert to string
        original_dict['obj_ip_end'] = str(self.obj_ip_end)  # Convert to string
        return original_dict
    
    @validator('obj_ip', 'obj_ip_end', pre=True)
    def convert_strings_to_ip_objects(cls, value, field):
        """
        Convert string values to IPNetwork or IPAddress objects depending on the field type.
        """
        if isinstance(value, str):
            if value == 'None':
                return None
            # Determine the type of the field and convert appropriately
            if field.name == 'obj_ip' or field.name == 'obj_ip_end':
                try:
                    return IPNetwork(value)
                except AddrFormatError as e:
                    if value == 'None':   # undefined ip addresses are okay (for groups)
                        return None
                    else:
                        raise ValueError(f"Invalid network format: {value}") from e
        return value
    

    class Config:
        arbitrary_types_allowed = True


class NetworkObjectForImport():
    obj_uid: str
    obj_name: str
    obj_ip: IPNetwork|None = None
    obj_ip_end: IPNetwork|None = None
    obj_color_id: int|None
    obj_member_refs: str|None = None
    obj_member_names: str|None = None
    obj_comment: str|None = None
    mgm_id: int
    obj_create: int
    obj_last_seen: int
    obj_removed: int
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

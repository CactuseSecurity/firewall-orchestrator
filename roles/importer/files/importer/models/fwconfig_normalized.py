from typing import List, Dict
from pydantic import BaseModel

from fwo_base import ConfigAction, ConfFormat
from models.policy import Policy
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject


class FwConfig(BaseModel):
    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED

"""
    the normalized configuraton of a firewall management to import
    this applies to a single management which might be either a global or a stand-alone management

    FwConfigNormalized:
    {
        'action': 'INSERT|UPDATE|DELETE',
        'network_objects': [ ... ],
        'service_objects': [ ... ],
        'users': [...],
        'zone_objects': [ ... ],
        'policies': [
            {
                'policy_name': 'pol1',
                'policy_uid': 'a32bc348234-23432a',
                'rules': [ { ... }, { ... }, ... ]
            }
        ],
        'gateways': # this is also a change, so these mappings are only listed once for insertion
        {
            'gw-uid-1': {
                'name': 'gw1',
                'global_policy_uid': 'pol-global-1',
                'policies': ['policy_uid_1', 'policy_uid_2']        # here order is the order of policies on the gateway
            }
        }

    }

    write methods to 
        a) split a config into < X MB chunks
        b) combine configs to a single config

"""
class FwConfigNormalized(FwConfig):
    action: ConfigAction
    # Networks: List[NetworkObject]
    network_objects: Dict[str, NetworkObject] = {}
    service_objects: Dict[str, ServiceObject] = {}
    users: dict = {}
    zone_objects: dict = {}
    rules: List[Policy] = []
    gateways: List[dict] = []
    # gateways: List[Gateway]
    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED_LEGACY

    class Config:
        arbitrary_types_allowed = True

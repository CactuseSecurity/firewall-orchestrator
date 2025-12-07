from typing import Any
from pydantic import BaseModel

from fwo_base import ConfigAction, ConfFormat
from models.rulebase import Rulebase
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from models.gateway import Gateway


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
    action: ConfigAction = ConfigAction.INSERT
    network_objects: dict[str, NetworkObject] = {}
    service_objects: dict[str, ServiceObject] = {}
    users: dict[str, Any] = {}
    zone_objects: dict[str, Any] = {}
    rulebases: list[Rulebase] = []
    gateways: list[Gateway] = []
    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED_LEGACY


    model_config = {
        "arbitrary_types_allowed": True
    }


    def get_rulebase(self, rulebase_uid: str) -> Rulebase:
        """
        get the policy with a specific uid  
        :param policyUid: The UID of the relevant policy.
        :return: Returns the policy with a specific uid, otherwise returns None.
        """
        rulebase = self.get_rulebase_or_none(rulebase_uid)
        if rulebase is not None:
            return rulebase

        raise KeyError(f"Rulebase with UID {rulebase_uid} not found.")

    def get_rulebase_or_none(self, rulebase_uid: str) -> Rulebase | None:
        """
        get the policy with a specific uid  
        :param policyUid: The UID of the relevant policy.
        :return: Returns the policy with a specific uid, otherwise returns None.
        """
        for rb in self.rulebases:
            if rb.uid == rulebase_uid:
                return rb
        return None

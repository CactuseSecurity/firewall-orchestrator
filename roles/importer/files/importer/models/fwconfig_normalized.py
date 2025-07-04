from typing import List, Dict
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
    action: ConfigAction
    network_objects: Dict[str, NetworkObject] = {}
    service_objects: Dict[str, ServiceObject] = {}
    users: dict = {}
    zone_objects: dict = {}
    rulebases: List[Rulebase] = []
    gateways: List[Gateway] = []
    ConfigFormat: ConfFormat = ConfFormat.NORMALIZED_LEGACY

    class Config:
        arbitrary_types_allowed = True


    def getRulebase(self, rulebaseUid: str) -> Rulebase:
        """
        get the policy with a specific uid  
        :param policyUid: The UID of the relevant policy.
        :return: Returns the policy with a specific uid, otherwise returns empty policy.
        """
        for rb in self.rulebases:
            if rb.uid == rulebaseUid:
                return rb
        return Rulebase(uid='', name='', mgm_uid='')


    def getOrderedRuleList(self, policyUid: str) -> List[dict]:
        """
        get the policy with a specific uid as an ordered list (ordered by rule_num)
        :param policyUid: The UID of the relevant policy.
        :return: Returns the policy with a specific uid as an ordered list [ruleUid, rule_num].
        """
        ruleList = [{'Uid': rule_uid, 'rule_num': details['rule_num']} for rule_uid, details in self.getRulebase(policyUid).items()]
        return sorted(ruleList, key=lambda x: x['rule_num'])

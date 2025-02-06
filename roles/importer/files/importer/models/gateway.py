from typing import List, Optional
from pydantic import BaseModel
from models.rulebase_link import RulebaseLinkUidBased

"""
Gateway
 {
    'gw-uid-1': {
        'name': 'gw1',
        'global_policy_uid': 'pol-global-1',
        'policies': ['policy_uid_1', 'policy_uid_2']        # here order is the order of policies on the gateway
        'nat-policies': ['nat_policy_uid_1']        # is always only a single policy?
    }
}
"""
class Gateway(BaseModel):
    Uid: str
    Name: str
    Routing: List[dict] = []
    Interfaces: List[dict]  = []
    RulebaseLinks: List[RulebaseLinkUidBased] = []
    GlobalPolicyUid: Optional[str] = None
    EnforcedPolicyUids: Optional[List[str]] = []
    EnforcedNatPolicyUids: Optional[List[str]] = []


    def __eq__(self, other):
        if isinstance(other, Gateway):
            return self.Uid == other.Uid \
                and self.Name == other.Name \
                and self.Routing == other.Routing \
                and self.Interfaces == other.Interfaces \
                and self.RulebaseLinks == other.RulebaseLinks \
                and self.GlobalPolicyUid == other.GlobalPolicyUid \
                and self.EnforcedPolicyUids == other.EnforcedPolicyUids \
                and self.EnforcedNatPolicyUids == other.EnforcedNatPolicyUids
        return False

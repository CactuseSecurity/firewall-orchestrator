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
    Routing: list[dict] = []
    Interfaces: list[dict]  = []
    RulebaseLinks: list[RulebaseLinkUidBased] = []
    GlobalPolicyUid: str|None = None
    EnforcedPolicyUids: list[str]|None = []
    EnforcedNatPolicyUids: list[str]|None = []
    ImportDisabled: bool|None = False
    ShowInUI: bool|None = True

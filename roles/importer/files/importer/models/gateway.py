from __future__ import annotations

from importlib import import_module
from typing import TYPE_CHECKING, Any

from pydantic import BaseModel

if TYPE_CHECKING:
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
    Uid: str | None = None
    Name: str | None = None
    Routing: list[dict[str, Any]] = []
    Interfaces: list[dict[str, Any]] = []
    RulebaseLinks: list[RulebaseLinkUidBased] = []
    GlobalPolicyUid: str | None = None
    EnforcedPolicyUids: list[str] | None = []
    EnforcedNatPolicyUids: list[str] | None = []
    ImportDisabled: bool = False
    ShowInUI: bool = True


Gateway.model_rebuild(
    _types_namespace={"RulebaseLinkUidBased": import_module("models.rulebase_link").RulebaseLinkUidBased}
)

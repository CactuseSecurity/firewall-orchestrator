from typing import Optional
from pydantic import BaseModel
from model_controllers.import_state_controller import ImportStateController


# RulebaseLinkUidBased is the model for a rulebase_link (containing no DB IDs)
class RulebaseLinkUidBased(BaseModel, ImportStateController):
    from_rulebase_uid: Optional[str] = None
    from_rule_uid: Optional[str] = None
    to_rulebase_uid: str
    link_type: str = "section"
    is_initial: bool
    is_global: bool

class RulebaseLink(BaseModel):
    id: Optional[int] = None    # will be created during db import
    gw_id: int
    from_rule_id: Optional[int] = None  # null for initial rulebase
    from_rulebase_id: Optional[int] = None  # either from_rule_id or from_rulebase_id must be set
    to_rulebase_id: int
    link_type: int = 1
    is_initial: bool
    is_global: bool
    created: int
    removed: Optional[int] = None


    def toDict(self):
        return {
            "gw_id": self.gw_id,
            "from_rule_id": self.from_rule_id,
            "from_rulebase_id": self.from_rulebase_id,
            "to_rulebase_id": self.to_rulebase_id,
            "link_type": self.link_type,
            "is_initial": self.is_initial,
            "is_global": self.is_global,
            "created": self.created,
            "removed": self.removed
        }
    

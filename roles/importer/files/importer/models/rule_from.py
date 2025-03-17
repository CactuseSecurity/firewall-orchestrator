from typing import Optional
from pydantic import BaseModel


# RuleFrom is the model for a normalized rule (containing DB IDs)
# does not contain the rule_from_id primary key as this one is set by the database
class RuleFrom(BaseModel):
    active: bool = True
    rule_id: int
    obj_id: int
    rf_create: int
    rf_last_seen: int
    removed: Optional[int] = None
    user_id: Optional[int] = None
    negated: bool = False

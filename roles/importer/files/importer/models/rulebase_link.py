from typing import Optional
from pydantic import BaseModel
from fwoBaseImport import ImportState


# RulebaseLinkUidBased is the model for a rulebase_link (containing no DB IDs)
class RulebaseLinkUidBased(BaseModel, ImportState):
    from_rulebase_uid: Optional[str] = None
    from_rule_uid: Optional[str] = None
    to_rulebase_uid: str
    link_type: str = "section"

# RulebaseLink is the model for a rulebase_link
# Create table IF NOT EXISTS "rulebase_link"
# (
# 	"id" SERIAL primary key,
# 	"gw_id" Integer,
# 	"from_rule_id" Integer,
# 	"to_rulebase_id" Integer NOT NULL,
# 	"link_type" Integer,
# 	"created" BIGINT,
# 	"removed" BIGINT
# );
class RulebaseLink(BaseModel):
    id: Optional[int] = None    # will be created during db import
    gw_id: int
    from_rule_id: Optional[int] = None
    to_rulebase_id: int
    link_type: int = 0
    created: int
    removed: Optional[int] = None

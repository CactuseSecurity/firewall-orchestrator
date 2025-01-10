from typing import List, Optional, Dict
from models.rule import Rule, RuleForImport
from pydantic import BaseModel
from fwoBaseImport import ImportState

# Rulebase is the model for a rulebase (containing no DB IDs)
class Rulebase(BaseModel):
    id: Optional[int] = None
    uid: str
    name: str
    mgm_uid: str
    is_global: bool = False
    Rules: dict[str, Rule] = {}


# RulebaseForImport is the model for a rule to be imported into the DB (containing IDs)
"""
    based on public.rulebase:

	# "id" SERIAL primary key,
	# "name" Varchar NOT NULL,
	# "uid" Varchar NOT NULL,
	# "mgm_id" Integer NOT NULL,
	# "is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	# "created" BIGINT,
	# "removed" BIGINT
"""
class RulebaseForImport(BaseModel):
    id: Optional[int] = None
    name: str
    uid: str
    mgm_id: int
    is_global: bool = False
    created: int
    removed: Optional[int] = None
    # rules: Dict[RuleForImport] = {}
    rules: Dict[str, List[RuleForImport]] = {}

    def dict(self, **kwargs):
        return super().dict(by_alias=True, exclude_unset=True, **kwargs)

from models.rule import RuleNormalized, Rule
from pydantic import BaseModel

# Rulebase is the model for a rulebase (containing no DB IDs)
class Rulebase(BaseModel):
    uid: str
    name: str
    mgm_uid: str
    is_global: bool = False
    rules: dict[str, RuleNormalized] = {}


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
    id: int|None = None
    name: str
    uid: str
    mgm_id: int
    is_global: bool = False
    created: int
    removed: int|None = None
    rules: list[Rule] = []

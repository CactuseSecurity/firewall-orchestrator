from models.rule import RuleNormalized
from pydantic import BaseModel


# Rulebase is the model for a rulebase (containing no DB IDs)
class Rulebase(BaseModel):
    uid: str
    name: str
    mgm_uid: str
    is_global: bool = False
    rules: dict[str, RuleNormalized] = {}

    def to_json(self) -> dict[str, object]:
        return {
            "uid": self.uid,
            "name": self.name,
            "mgm_uid": self.mgm_uid,
            "is_global": self.is_global,
            "rules": {uid: rule.model_dump() for uid, rule in self.rules.items()},
        }


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
    name: str
    uid: str
    mgm_id: int
    is_global: bool = False
    created: int
    removed: int | None = None

    @classmethod
    def from_rulebase(cls, rulebase: Rulebase, mgm_id: int, created: int) -> "RulebaseForImport":
        return cls(
            name=rulebase.name,
            uid=rulebase.uid,
            mgm_id=mgm_id,
            is_global=rulebase.is_global,
            created=created,
        )

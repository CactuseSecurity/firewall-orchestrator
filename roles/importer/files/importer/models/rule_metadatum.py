from pydantic import BaseModel


# Rule is the model for a normalized rule_metadata
class RuleMetadatum(BaseModel):
    rule_uid: str
    mgm_id: int
    rule_created: str | None = None
    rule_last_modified: str | None = None
    rule_last_hit: str | None = None


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(RuleMetadatum):
    rule_metadata_id: int | None = None

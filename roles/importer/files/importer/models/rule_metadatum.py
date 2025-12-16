from pydantic import BaseModel

# Rule is the model for a normalized rule_metadata
class RuleMetadatum(BaseModel):
    rule_uid: str
    mgm_id: int
    rule_created: int
    rule_last_modified: int
    rule_first_hit: str|None = None
    rule_last_hit: str|None = None
    rule_hit_counter: int|None = None


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(RuleMetadatum):
    rule_metadata_id: int|None = None

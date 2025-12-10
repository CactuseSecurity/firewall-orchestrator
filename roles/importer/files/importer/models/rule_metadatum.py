from pydantic import BaseModel


# Rule is the model for a normalized rule_metadata
class RuleMetadatum(BaseModel):
    rule_uid: str
    mgm_id: int
    rule_created: str | None = None
    rule_last_modified: str | None = None
    rule_first_hit: str | None = None
    rule_last_hit: str | None = None
    rule_hit_counter: int | None = None
    rule_last_certified: str | None = None
    rule_last_certifier: str | None = None
    rule_last_certifier_dn: str | None = None
    rule_owner: int | None = None
    rule_owner_dn: str | None = None
    rule_to_be_removed: bool = False
    last_change_admin: str | None = None
    rule_decert_date: int | None = None
    rule_recertification_comment: str | None = None


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(RuleMetadatum):
    rule_metadata_id: int | None = None

from typing import Any, Dict
from pydantic import BaseModel

# Create table "rule_metadata"
# (
# 	"rule_metadata_id" BIGSERIAL,
# delete: 	"dev_id" Integer NOT NULL,
# delete:	"rulebase_id" Integer NOT NULL,
# 	"rule_uid" Text NOT NULL,
# 	"rule_created" Timestamp NOT NULL Default now(),
# 	"rule_last_modified" Timestamp NOT NULL Default now(),
# 	"rule_first_hit" Timestamp,
# 	"rule_last_hit" Timestamp,
# 	"rule_hit_counter" BIGINT,
# 	"rule_last_certified" Timestamp,
# 	"rule_last_certifier" Integer,
# 	"rule_last_certifier_dn" VARCHAR,
# 	"rule_owner" Integer, -- points to a uiuser (not an owner)
# 	"rule_owner_dn" Varchar, -- distinguished name pointing to ldap group, path or user
# 	"rule_to_be_removed" Boolean NOT NULL Default FALSE,
# 	"last_change_admin" Integer,
# 	"rule_decert_date" Timestamp,
# 	"rule_recertification_comment" Varchar,
#  primary key ("rule_metadata_id")
# );

# Rule is the model for a normalized rule_metadata
class RuleMetadatum(BaseModel):
    rule_uid: str
    mgm_id: int
    rule_created: int
    rule_last_modified: int
    rule_first_hit: str|None = None
    rule_last_hit: str|None = None
    rule_hit_counter: int|None = None
    # compatibility helper for both pydantic v1 (.dict) and v2 (.model_dump)
    def model_dump(self, *args: Any, **kwargs: Any) -> Dict[str, Any]:  # type: ignore[override]
        try:
            return super().model_dump(*args, **kwargs)  # type: ignore[attr-defined]
        except AttributeError:
            return self.dict(*args, **kwargs)  # type: ignore[call-arg]


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(RuleMetadatum):
    rule_metadata_id: int|None = None

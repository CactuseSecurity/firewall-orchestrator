from typing import Optional
from pydantic import BaseModel
from models.caseinsensitiveenum import CaseInsensitiveEnum

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
    rule_created: Optional[str] = None
    rule_last_modified: Optional[str] = None
    rule_first_hit: Optional[str] = None
    rule_last_hit: Optional[str] = None
    rule_hit_counter: Optional[int] = None
    rule_last_certified: Optional[str] = None
    rule_last_certifier: Optional[str] = None
    rule_last_certifier_dn: Optional[str] = None
    rule_owner: Optional[int] = None
    rule_owner_dn: Optional[str] = None
    rule_to_be_removed: bool = False
    last_change_admin: Optional[str] = None
    rule_decert_date: Optional[int] = None
    rule_recertification_comment: Optional[str] = None


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(BaseModel):
    rule_metadata_id: Optional[int] = None
    rule_uid: str
    rule_created: Optional[str] = None
    rule_last_modified: Optional[str] = None
    rule_first_hit: Optional[str] = None
    rule_last_hit: Optional[str] = None
    rule_hit_counter: Optional[int] = None
    rule_last_certified: Optional[str] = None
    rule_last_certifier: Optional[str] = None
    rule_last_certifier_dn: Optional[str] = None
    rule_owner: Optional[int] = None
    rule_owner_dn: Optional[str] = None
    rule_to_be_removed: bool = False
    last_change_admin: Optional[str] = None
    rule_decert_date: Optional[int] = None
    rule_recertification_comment: Optional[str] = None

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
    rule_created: str|None = None
    rule_last_modified: str|None = None
    rule_first_hit: str|None = None
    rule_last_hit: str|None = None
    rule_hit_counter: int|None = None
    rule_last_certified: str|None = None
    rule_last_certifier: str|None = None
    rule_last_certifier_dn: str|None = None
    rule_owner: int|None = None
    rule_owner_dn: str|None = None
    rule_to_be_removed: bool = False
    last_change_admin: str|None = None
    rule_decert_date: int|None = None
    rule_recertification_comment: str|None = None


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(BaseModel):
    rule_metadata_id: int|None = None
    rule_uid: str
    rule_created: str|None = None
    rule_last_modified: str|None = None
    rule_first_hit: str|None = None
    rule_last_hit: str|None = None
    rule_hit_counter: int|None = None
    rule_last_certified: str|None = None
    rule_last_certifier: str|None = None
    rule_last_certifier_dn: str|None = None
    rule_owner: int|None = None
    rule_owner_dn: str|None = None
    rule_to_be_removed: bool = False
    last_change_admin: str|None = None
    rule_decert_date: int|None = None
    rule_recertification_comment: str|None = None

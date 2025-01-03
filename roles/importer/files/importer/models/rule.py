from typing import List, Optional
import datetime
import time
from enum import Enum, auto
from fwo_api import call
from fwoBaseImport import ImportState
from pydantic import BaseModel

class CaseInsensitiveEnum(str, Enum):
    @classmethod
    def from_str(cls, value: str):
        # Iterate through enum members and perform case-insensitive comparison
        for item in cls:
            if item.value.lower() == value.lower():
                return item
        raise ValueError(f"'{value}' is not a valid {cls.__name__}")

    @classmethod
    def __get_validators__(cls):
        yield cls.validate

    @classmethod
    def validate(cls, value):
        # This is used by Pydantic to validate the input during model creation
        if isinstance(value, str):
            return cls.from_str(value)
        raise TypeError(f"'{value}' is not a valid {cls.__name__}")

class RuleType(CaseInsensitiveEnum):
    ACCESS = 'access'
    NAT = 'nat'
    ACCESSANDNAT = 'accessandnat'
    SECTIONHEADER = 'sectionheader'

class RuleAction(CaseInsensitiveEnum):
    ACCEPT = 'accept'
    DROP = 'drop'
    REJECT = 'reject'
    CLIENTAUTH = 'client auth'
    INNERLAYER = 'inner layer'
    INFORM = 'inform'

class RuleTrack(CaseInsensitiveEnum):
    NONE = 'none'
    LOG = 'log'
    ALERT = 'alert'

# Rule is the model for a normalized rule (containing no DB IDs)
class Rule(BaseModel):
    rule_num: int
    rule_disabled: bool
    rule_src_neg: bool
    rule_src: str
    rule_src_refs: str
    rule_dst_neg: bool
    rule_dst: str
    rule_dst_refs: str
    rule_svc_neg: bool
    rule_svc: str
    rule_svc_refs: str
    rule_action: RuleAction
    rule_track: RuleTrack
    rule_installon: str
    rule_time: str
    rule_name: Optional[str] = None
    rule_uid: str
    rule_custom_fields: Optional[str] = None
    rule_implied: bool
    rule_type: RuleType = RuleType.SECTIONHEADER
    rule_last_change_admin: Optional[str] = None
    parent_rule_uid: Optional[str] = None
    last_hit: Optional[str] = None
    rule_comment: Optional[str] = None
    rule_src_zone: Optional[str] = None
    rule_dst_zone: Optional[str] = None
    rule_head_text: Optional[str] = None


"""
    based on public.rule:

	"rule_id" BIGSERIAL,
	"last_change_admin" Integer,
	"rule_name" Varchar,
	"mgm_id" Integer NOT NULL,
	"parent_rule_id" BIGINT,
	"parent_rule_type" smallint,
	"active" Boolean NOT NULL Default TRUE,
	"removed" BIGINT,
	"rule_num" Integer NOT NULL,
	"rule_num_numeric" NUMERIC(16, 8),
	"rule_ruleid" Varchar,
	"rule_uid" Text,
	"rule_disabled" Boolean NOT NULL Default false,
	"rule_src_neg" Boolean NOT NULL Default false,
	"rule_dst_neg" Boolean NOT NULL Default false,
	"rule_svc_neg" Boolean NOT NULL Default false,
	"action_id" Integer NOT NULL,
	"track_id" Integer NOT NULL,
	"rule_src" Text NOT NULL,
	"rule_dst" Text NOT NULL,
	"rule_svc" Text NOT NULL,
	"rule_src_refs" Text,
	"rule_dst_refs" Text,
	"rule_svc_refs" Text,
	"rule_from_zone" Integer,
	"rule_to_zone" Integer,
	"rule_action" Text NOT NULL,
	"rule_track" Text NOT NULL,
	"rule_installon" Varchar,
	"rule_time" Varchar,
	"rule_comment" Text,
	"rule_head_text" Text,
	"rule_implied" Boolean NOT NULL Default FALSE,
	"rule_create" BIGINT NOT NULL,
	"rule_last_seen" BIGINT NOT NULL,
	"dev_id" Integer,
	"rule_custom_fields" jsonb,
	"access_rule" BOOLEAN Default TRUE,
	"nat_rule" BOOLEAN Default FALSE,
	"xlate_rule" BIGINT,
	"is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	"rulebase_id" Integer NOT NULL,
"""

# Rule is the model for a rule to be imported into the DB (containing IDs)
class RuleForImport(BaseModel):
    access_rule: bool = True
    action_id: int
    is_global: bool = False
    last_change_admin: Optional[int] = None
    # last_hit: Optional[str] = None
    mgm_id: int
    nat_rule: bool = False
    parent_rule_id: Optional[int] = None
    removed: Optional[int] = None
    rule_action: str
    rule_comment: Optional[str] = None
    rule_create: int
    rule_custom_fields: Optional[str] = None
    rule_disabled: bool
    rule_dst: str
    rule_dst_neg: bool
    rule_dst_refs: str
    rule_from_zone: Optional[str] = None
    rule_head_text: Optional[str] = None
    rule_implied: bool = False
    rule_installon: str
    rule_last_seen: int
    rule_name: Optional[str] = None
    rule_num: int
    rule_num_numeric: float
    rule_src: str
    rule_src_neg: bool
    rule_src_refs: str
    rule_svc: str
    rule_svc_neg: bool
    rule_svc_refs: str
    rule_time: str
    rule_to_zone: Optional[str] = None
    track_id: int
    xlate_rule: Optional[int] = None
    rule_track: str
    rule_uid: str
    rulebase_id: int

    # def __init__(self, rule: Rule, mgmId: int, importId: int, access_rule: bool, nat_rule: bool, rulebase_id: str):
    #     self.rule_uid = rule.rule_uid
    #     self.rule_name = rule.rule_name
    #     self.rule_comment = rule.rule_comment
    #     self.access_rule = access_rule
    #     self.nat_rule = nat_rule
    #     self.mgm_id = mgmId
    #     self.rule_create = importId
    #     self.rule_last_seen = importId
    #     self.rulebase_id = rulebase_id
    #     # self.rule_typ_id = typId

    # def toDict (self):
    #     result = {
    #         'rule_uid': self.rule_uid,
    #         'rule_name': self.rule_name,
    #         'rule_comment': self.rule_comment,
    #         'mgm_id': self.mgm_id,
    #         'rule_create': self.rule_create,
    #         'rule_last_seen': self.rule_last_seen,
    #         'rule_typ_id': self.rule_typ_id
    #     }
    #     return result
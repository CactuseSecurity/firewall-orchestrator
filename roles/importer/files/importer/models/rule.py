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
    # rule_custom_fields: Optional[dict] = None
    rule_implied: bool
    rule_type: RuleType = RuleType.SECTIONHEADER
    rule_last_change_admin: Optional[str] = None
    parent_rule_uid: Optional[str] = None
    last_hit: Optional[str] = None
    rule_comment: Optional[str] = None
    rule_src_zone: Optional[str] = None
    rule_dst_zone: Optional[str] = None

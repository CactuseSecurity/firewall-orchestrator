from __future__ import annotations

from models.time_object import validate_iso_timestamp_value
from pydantic import BaseModel, field_validator


class RuleHit(BaseModel):
    """
    Last hit of a rule on one gateway, as read by the rule hit import.

    The rule and the gateway may belong to different managements of a super management,
    e.g. a FortiManager global rule hit on a gateway of an ADOM.
    """

    rule_mgm_id: int
    rule_uid: str
    gateway_mgm_id: int
    gateway_uid: str
    last_hit: str

    @field_validator("last_hit")
    @classmethod
    def validate_last_hit_format(cls, value: str) -> str:
        normalized_value = validate_iso_timestamp_value("Rule hit last_hit", value)
        if normalized_value is None:
            raise ValueError("Rule hit last_hit must not be empty")
        return normalized_value

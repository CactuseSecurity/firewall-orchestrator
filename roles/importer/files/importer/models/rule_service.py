from __future__ import annotations

from pydantic import BaseModel


class RuleService(BaseModel):
    active: bool = True
    rule_id: int
    svc_id: int
    rs_create: int
    removed: int | None = None
    negated: bool = False

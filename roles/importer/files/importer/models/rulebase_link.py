from typing import Any

from pydantic import BaseModel, ConfigDict, TypeAdapter


# RulebaseLinkUidBased is the model for a rulebase_link (containing no DB IDs)
class RulebaseLinkUidBased(BaseModel):
    from_rulebase_uid: str | None = None
    from_rule_uid: str | None = None
    to_rulebase_uid: str
    link_type: str = "section"
    is_initial: bool
    is_global: bool
    is_section: bool

    def to_dict(self) -> dict[str, object | str | bool | None]:
        return {
            "from_rule_uid": self.from_rule_uid,
            "from_rulebase_uid": self.from_rulebase_uid,
            "to_rulebase_uid": self.to_rulebase_uid,
            "link_type": self.link_type,
            "is_initial": self.is_initial,
            "is_global": self.is_global,
            "is_section": self.is_section,
        }


class RulebaseLink(BaseModel):
    id: int | None = None  # will be created during db import
    gw_id: int
    from_rule_id: int | None = None  # null for initial rulebase
    from_rulebase_id: int | None = None  # either from_rule_id or from_rulebase_id must be set
    to_rulebase_id: int
    link_type: int = 1
    is_initial: bool
    is_global: bool
    is_section: bool
    created: int
    removed: int | None = None
    model_config = ConfigDict(populate_by_name=True)

    def to_dict(self) -> dict[str, Any]:
        return {
            "gw_id": self.gw_id,
            "from_rule_id": self.from_rule_id,
            "from_rulebase_id": self.from_rulebase_id,
            "to_rulebase_id": self.to_rulebase_id,
            "link_type": self.link_type,
            "is_initial": self.is_initial,
            "is_global": self.is_global,
            "is_section": self.is_section,
            "created": self.created,
            "removed": self.removed,
        }


def parse_rulebase_links(data: list[dict[str, Any]]) -> list[RulebaseLink]:
    adapter = TypeAdapter(list[RulebaseLink])
    return adapter.validate_python(data)

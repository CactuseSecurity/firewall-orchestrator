from typing import Any, Dict
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
    # compatibility helper for both pydantic v1 (.dict) and v2 (.model_dump)
    def model_dump(self, *args: Any, **kwargs: Any) -> Dict[str, Any]:  # type: ignore[override]
        try:
            return super().model_dump(*args, **kwargs)  # type: ignore[attr-defined]
        except AttributeError:
            return self.dict(*args, **kwargs)  # type: ignore[call-arg]


# RuleForImport is the model for a rule to be imported into the DB (containing IDs)
class RuleMetadatumForImport(RuleMetadatum):
    rule_metadata_id: int|None = None

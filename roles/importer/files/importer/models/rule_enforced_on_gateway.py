from typing import Optional
from pydantic import BaseModel
from model_controllers.import_state_controller import ImportStateController


# the model for a connection between a rule and a gateway
# each rule has to be linked to the gateway the rule is enforced on
# this is e.g. used for the "install on" feature in Check Point  
class RuleEnforcedOnGateway(BaseModel):
    # "rule_id" Integer NOT NULL,
	# "dev_id" Integer,  --  NULL if rule is available for all gateways of its management
	# "created" BIGINT,
	# "removed" BIGINT
    rule_id: int
    dev_id: int
    created: Optional[int]
    removed: Optional[int] 


    def to_dict(self):
        return {
            "rule_id": self.rule_id,
            "dev_id": self.dev_id,
            "created": self.created,
            "removed": self.removed
        }

# normalized config without db ids
class RuleEnforcedOnGatewayNormalized(BaseModel, ImportStateController):
    rule_uid: str
    dev_uid: str


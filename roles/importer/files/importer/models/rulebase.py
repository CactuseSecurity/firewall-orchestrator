from typing import List, Optional
from models.rule import Rule, RuleForImport
from pydantic import BaseModel
from fwoBaseImport import ImportState

# Rulebase is the model for a rulebase (containing no DB IDs)
class Rulebase(BaseModel):
    uid: str
    name: str
    mgm_uid: str
    is_global: bool = False
    Rules: dict[str, Rule] = {}

    def PrepareForImport(self, importDetails: ImportState) -> dict[str, RuleForImport]:
        prepared_rules = {}
        for uid, rule in self.Rules.items():

            rule_for_import = RuleForImport(
                mgm_id=importDetails.MgmDetails.Id,
                rule_num=rule.rule_num,
                rule_disabled=rule.rule_disabled,
                rule_src_neg=rule.rule_src_neg,
                rule_src=rule.rule_src,
                rule_src_refs=rule.rule_src_refs,
                rule_dst_neg=rule.rule_dst_neg,
                rule_dst=rule.rule_dst,
                rule_dst_refs=rule.rule_dst_refs,
                rule_svc_neg=rule.rule_svc_neg,
                rule_svc=rule.rule_svc,
                rule_svc_refs=rule.rule_svc_refs,
                rule_action=rule.rule_action,
                rule_track=rule.rule_track,
                rule_installon=rule.rule_installon,
                rule_time=rule.rule_time,
                rule_name=rule.rule_name,
                rule_uid=rule.rule_uid,
                rule_custom_fields=rule.rule_custom_fields,
                rule_implied=rule.rule_implied,
                # parent_rule_id=rule.parent_rule_id,
                rule_comment=rule.rule_comment,
                rule_from_zone=rule.rule_src_zone,
                rule_to_zone=rule.rule_dst_zone,
                access_rule=True,
                nat_rule=False,
                is_global=False,
                # rulebase_id=rule.rulebase_id,
                rule_create=importDetails.ImportId,
                rule_last_seen=importDetails.ImportId,
                rule_num_numeric=1,
                action_id = self.lookupAction(rule.rule_action),
                track_id = self.lookupTrack(rule.rule_track),
                rule_head_text=rule.rule_head_text
            )
            prepared_rules[uid] = rule_for_import
        return prepared_rules

# RulebaseForImport is the model for a rule to be imported into the DB (containing IDs)
"""
    based on public.rulebase:

	# "id" SERIAL primary key,
	# "name" Varchar NOT NULL,
	# "uid" Varchar NOT NULL,
	# "mgm_id" Integer NOT NULL,
	# "is_global" BOOLEAN DEFAULT FALSE NOT NULL,
	# "created" BIGINT,
	# "removed" BIGINT
"""
class RulebaseForImport(BaseModel):
    id: int
    name: str
    uid: str
    mgm_id: int
    is_global: bool = False
    created: int
    removed: Optional[int] = None
    rules: List[RuleForImport] = []

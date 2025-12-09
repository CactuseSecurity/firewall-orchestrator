from enum import Enum
import traceback
from difflib import ndiff
import json
from typing import Generator, Any

import fwo_const
import fwo_api_call as fwo_api_call
from fwo_exceptions import FwoApiWriteError, FwoImporterError
from models.rule import Rule
from models.rule_metadatum import RuleMetadatum
from models.rulebase import Rulebase, RulebaseForImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from fwo_log import ChangeLogger, FWOLogger
from datetime import datetime
from models.rule_from import RuleFrom
from models.rule_to import RuleTo
from models.rule_service import RuleService
from models.rule import RuleNormalized
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from services.global_state import GlobalState
from services.group_flats_mapper import GroupFlatsMapper
from services.uid2id_mapper import Uid2IdMapper
from services.service_provider import ServiceProvider
from fwo_api import FwoApi


class RefType(Enum):
    SRC = "rule_from"
    DST = "rule_to"
    SVC = "rule_service"
    NWOBJ_RESOLVED = "rule_nwobj_resolved"
    SVC_RESOLVED = "rule_svc_resolved"
    USER_RESOLVED = "rule_user_resolved"
    SRC_ZONE = "rule_from_zone"
    DST_ZONE = "rule_to_zone"

# this class is used for importing rules and rule refs into the FWO API
class FwConfigImportRule():

    _changed_rule_id_map: dict[int, int]
    global_state: GlobalState
    import_details: ImportStateController
    normalized_config: FwConfigNormalized | None = None
    uid2id_mapper: Uid2IdMapper
    group_flats_mapper: GroupFlatsMapper
    prev_group_flats_mapper: GroupFlatsMapper

    def __init__(self):
        self._changed_rule_id_map = {}

        service_provider = ServiceProvider()
        self.global_state = service_provider.get_global_state()
        self.import_details = self.global_state.import_state
        #TODO: why is there a state where this is initialized with normalized_config = None? - see #3154
        self.normalized_config = self.global_state.normalized_config # type: ignore
        self.uid2id_mapper = service_provider.get_uid2id_mapper(self.import_details.import_id)
        self.group_flats_mapper = service_provider.get_group_flats_mapper(self.import_details.import_id)
        self.prev_group_flats_mapper = service_provider.get_prev_group_flats_mapper(self.import_details.import_id)
        self.rule_order_service = service_provider.get_rule_order_service(self.import_details.import_id)

    def update_rulebase_diffs(self, prevConfig: FwConfigNormalized) -> list[int]:
        if self.normalized_config is None:
            raise FwoImporterError("cannot update rulebase diffs: normalized_config is None")

        # calculate rule diffs
        changed_rule_uids: dict[str, list[str]] = {} # rulebase_id -> list of rule_uids
        rule_uids_in_both: dict[str, list[str]] = {}
        previous_rulebase_uids: list[str] = []
        current_rulebase_uids: list[str] = []
        new_hit_information: list[dict[str, Any]] = []

        rule_order_diffs: dict[str, dict[str, list[str]]] = self.rule_order_service.update_rule_order_diffs()

        # collect rulebase UIDs of previous config
        for rulebase in prevConfig.rulebases:
            previous_rulebase_uids.append(rulebase.uid)

        # collect rulebase UIDs of current (just imported) config
        for rulebase in self.normalized_config.rulebases:
            current_rulebase_uids.append(rulebase.uid)

        for rulebase_uid in previous_rulebase_uids:
            current_rulebase = self.normalized_config.get_rulebase_or_none(rulebase_uid)
            if current_rulebase is None:
                FWOLogger.info(f"current rulebase has been deleted: {rulebase_uid}")
                continue
            if rulebase_uid in current_rulebase_uids:
                # deal with policies contained both in this and previous config
                previous_rulebase = prevConfig.get_rulebase(rulebase_uid)
                rule_uids_in_both.update({ rulebase_uid: list(current_rulebase.rules.keys() & previous_rulebase.rules.keys()) })
            else:
                FWOLogger.info(f"previous rulebase has been deleted: {current_rulebase.name} (id:{rulebase_uid})")

        # find changed rules
        for rulebase_uid in rule_uids_in_both:
            changed_rule_uids.update({ rulebase_uid: [] })
            current_rulebase = self.normalized_config.get_rulebase(rulebase_uid) # [pol for pol in self.NormalizedConfig.rulebases if pol.Uid == rulebaseId]
            previous_rulebase = prevConfig.get_rulebase(rulebase_uid)
            for ruleUid in rule_uids_in_both[rulebase_uid]:
                self.preserve_rule_num_numeric(current_rulebase, previous_rulebase, ruleUid)
                self.collect_changed_rules(ruleUid, current_rulebase, previous_rulebase, rulebase_uid, changed_rule_uids)

        # collect hit information for all rules with hit data
        self.collect_all_hit_information(prevConfig, new_hit_information)

        # add moved rules that are not in changed rules (e.g. move across rulebases)
        self._collect_uncaught_moves(rule_order_diffs["moved_rule_uids"], changed_rule_uids)

        # add full rule details first
        new_rulebases = self.get_rules(rule_order_diffs["new_rule_uids"])

        # update rule_metadata before adding rules
        _, _ = self.add_new_rule_metadata(new_rulebases)
        self.update_rule_metadata_last_hit(new_hit_information)

        # # now update the database with all rule diffs
        self.uid2id_mapper.update_rule_mapping()

        num_added_rules, new_rule_ids = self.add_new_rules(new_rulebases)
        num_changed_rules, old_rule_ids, updated_rule_ids = self.create_new_rule_version(changed_rule_uids)

        self.uid2id_mapper.add_rule_mappings(new_rule_ids + updated_rule_ids)
        _ = self.add_new_refs(prevConfig)

        num_deleted_rules, removed_rule_ids = self.mark_rules_removed(rule_order_diffs["deleted_rule_uids"])
        self.remove_outdated_refs(prevConfig)

        num_moved_rules, _ = self.verify_rules_moved(changed_rule_uids)

        new_rule_ids = [rule['rule_id'] for rule in new_rule_ids]  # extract rule_ids from the returned list of dicts
        self.write_changelog_rules(new_rule_ids, removed_rule_ids)

        self.import_details.stats.increment_rule_add_count(num_added_rules)
        self.import_details.stats.increment_rule_delete_count(num_deleted_rules)
        self.import_details.stats.increment_rule_move_count(num_moved_rules)
        self.import_details.stats.increment_rule_change_count(num_changed_rules)
        
        for removed_rules_by_rulebase in removed_rule_ids:
            old_rule_ids.append(removed_rules_by_rulebase)
 
        if len(old_rule_ids) > 0:
            self._create_removed_rules_map(old_rule_ids)
            
        # TODO: rule_nwobj_resolved fuellen (recert?)
        return new_rule_ids
    

    def _create_removed_rules_map(self, removed_rule_ids: list[int]):
        removed_rule_ids_set = set(removed_rule_ids)
        for rule_id in removed_rule_ids_set:
            rule_uid = next((k for k, v in self.import_details.rule_map.items() if v == rule_id), None)
            if rule_uid:
                self.import_details.removed_rules_map[rule_uid] = rule_id

    

    def _collect_uncaught_moves(self, movedRuleUids: dict[str, list[str]], changedRuleUids: dict[str, list[str]]):
        for rulebaseId in movedRuleUids:
            for ruleUid in movedRuleUids[rulebaseId]:
                if ruleUid not in changedRuleUids.get(rulebaseId, []):
                    if rulebaseId not in changedRuleUids:
                        changedRuleUids[rulebaseId] = []
                    changedRuleUids[rulebaseId].append(ruleUid)

    def collect_all_hit_information(self, prev_config: FwConfigNormalized, new_hit_information: list[dict[str, Any]]):
        """
        Consolidated hit information collection for ALL rules that need hit updates.

        Args:
            prev_config: Previous configuration for comparison
            new_hit_information: List to append hit update information to
        """
        processed_rules: set[str] = set()

        def add_hit_update(new_hit_information: list[dict[str, Any]], rule: RuleNormalized):
            """Add a hit information update entry for a rule."""
            new_hit_information.append({ 
                "where": { "rule_uid": { "_eq": rule.rule_uid }, "mgm_id": { "_eq": self.import_details.mgm_details.current_mgm_id } },
                "_set": { "rule_last_hit": rule.last_hit }
            })

        # check all rulebases in current config
        if self.normalized_config is None:
            raise FwoImporterError("cannot collect hit information: normalized_config is None")
        
        for current_rulebase in self.normalized_config.rulebases:
            previous_rulebase = prev_config.get_rulebase_or_none(current_rulebase.uid)

            for rule_uid in current_rulebase.rules:
                current_rule = current_rulebase.rules[rule_uid]
                previous_rule = previous_rulebase.rules.get(rule_uid) if previous_rulebase else None

                if current_rule.last_hit is None:
                    continue  # No hit information to update

                if previous_rule is None or \
                    (current_rule.last_hit != previous_rule.last_hit):
                    # rulebase or rule is new or hit information changed
                    add_hit_update(new_hit_information, current_rule)
                    processed_rules.add(rule_uid)

    def update_rule_metadata_last_hit(self, new_hit_information: list[dict[str, Any]]):
        """
        Updates rule_metadata.rule_last_hit for all rules with hit information changes.
        This method executes the actual database updates for hit information.

        Args:
            new_hit_information (list[dict]): The hit information to update.
        """

        if len(new_hit_information) > 0:
            update_last_hit_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule_metadata/updateLastHits.graphql"])
            query_variables = { 'hit_info': new_hit_information  }

            try:
                import_result = self.import_details.api_call.call(update_last_hit_mutation, query_variables=query_variables, analyze_payload=True)
                if 'errors' in import_result:
                    FWOLogger.exception(f"fwo_api:importNwObject - error in addNewRuleMetadata: {str(import_result['errors'])}")
                    # do not count last hit changes as changes here
            except Exception:
                raise FwoApiWriteError(f"failed to update RuleMetadata last hit info: {str(traceback.format_exc())}")

    @staticmethod
    def collect_changed_rules(rule_uid: str, current_rulebase: Rulebase, previous_rulebase: Rulebase, rulebase_id: str, changed_rule_uids: dict[str, list[str]]):
        if current_rulebase.rules[rule_uid] != previous_rulebase.rules[rule_uid]:
            changed_rule_uids[rulebase_id].append(rule_uid)


    @staticmethod
    def preserve_rule_num_numeric(current_rulebase: Rulebase, previous_rulebase: Rulebase, rule_uid: str):
        if current_rulebase.rules[rule_uid].rule_num_numeric == 0:
            current_rulebase.rules[rule_uid].rule_num_numeric = previous_rulebase.rules[rule_uid].rule_num_numeric


    def get_rule_refs(self, rule: RuleNormalized, is_prev: bool = False) -> dict[RefType, list[tuple[str, str | None]] | list[str]]:
        froms: list[tuple[str, str | None]] = []
        tos: list[tuple[str, str | None]] = []
        users: list[str] = []
        nwobj_resolveds = []
        svc_resolveds = []
        user_resolveds = []
        from_zones = []
        to_zones = []
        for src_ref in rule.rule_src_refs.split(fwo_const.LIST_DELIMITER):
            user_ref = None
            if fwo_const.USER_DELIMITER in src_ref:
                src_ref, user_ref = src_ref.split(fwo_const.USER_DELIMITER)
                users.append(user_ref)
            froms.append((src_ref, user_ref))
        for dst_ref in rule.rule_dst_refs.split(fwo_const.LIST_DELIMITER):
            user_ref = None
            if fwo_const.USER_DELIMITER in dst_ref:
                dst_ref, user_ref = dst_ref.split(fwo_const.USER_DELIMITER)
                users.append(user_ref)
            tos.append((dst_ref, user_ref))
        svcs = rule.rule_svc_refs.split(fwo_const.LIST_DELIMITER)
        if is_prev:
            nwobj_resolveds = self.prev_group_flats_mapper.get_network_object_flats([ref[0] for ref in froms + tos])
            svc_resolveds = self.prev_group_flats_mapper.get_service_object_flats(svcs)
            user_resolveds = self.prev_group_flats_mapper.get_user_flats(users)
        else:
            nwobj_resolveds = self.group_flats_mapper.get_network_object_flats([ref[0] for ref in froms + tos])
            svc_resolveds = self.group_flats_mapper.get_service_object_flats(svcs)
            user_resolveds = self.group_flats_mapper.get_user_flats(users)
        from_zones = rule.rule_src_zone.split(fwo_const.LIST_DELIMITER) if rule.rule_src_zone else []
        to_zones = rule.rule_dst_zone.split(fwo_const.LIST_DELIMITER) if rule.rule_dst_zone else []
        return {
            RefType.SRC: froms,
            RefType.DST: tos,
            RefType.SVC: svcs,
            RefType.NWOBJ_RESOLVED: nwobj_resolveds,
            RefType.SVC_RESOLVED: svc_resolveds,
            RefType.USER_RESOLVED: user_resolveds,
            RefType.SRC_ZONE: from_zones,
            RefType.DST_ZONE: to_zones
        }

    def get_ref_objs(self, ref_type: RefType, ref_uid: tuple[str, str | None] | str , prev_config: FwConfigNormalized) -> tuple[tuple[None | NetworkObject, None | Any], tuple[None | NetworkObject , None | Any]] | tuple[None | NetworkObject | ServiceObject, None | Any]: #TODO Any is user type but there is no user Type
        
        if self.normalized_config is None:
            raise FwoImporterError("cannot get ref objs: normalized_config is None")
        
        if ref_type == RefType.SRC or ref_type == RefType.DST:
            nwobj_uid, user_uid = ref_uid
            
            return (prev_config.network_objects.get(nwobj_uid, None), prev_config.users.get(user_uid, None) if user_uid else None), \
                     (self.normalized_config.network_objects.get(nwobj_uid, None), self.normalized_config.users.get(user_uid, None) if user_uid else None)
        elif ref_type == RefType.NWOBJ_RESOLVED:
            return prev_config.network_objects.get(ref_uid, None), self.normalized_config.network_objects.get(ref_uid, None) # type: ignore #TODO: change ref_uid to str only
        elif ref_type == RefType.SVC or ref_type == RefType.SVC_RESOLVED:
            return prev_config.service_objects.get(ref_uid, None), self.normalized_config.service_objects.get(ref_uid, None) # type: ignore
        elif ref_type == RefType.USER_RESOLVED:
            return prev_config.users.get(ref_uid, None), self.normalized_config.users.get(ref_uid, None) # type: ignore
        elif ref_type == RefType.SRC_ZONE or ref_type == RefType.DST_ZONE:
            return prev_config.zone_objects.get(ref_uid, None), self.normalized_config.zone_objects.get(ref_uid, None) # type: ignore
        else:
            raise FwoImporterError(f"unknown ref type: {ref_type}")
    
    def get_ref_remove_statement(self, ref_type: RefType, rule_uid: str, ref_uid: tuple[str, str | None] | str) -> dict[str, Any]:
        if ref_type == RefType.SRC or ref_type == RefType.DST:
            nwobj_uid, user_uid = ref_uid
            statement = {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"obj_id": {"_eq": self.uid2id_mapper.get_network_object_id(nwobj_uid, before_update=True)}}
                ]
            }
            if user_uid:
                statement["_and"].append({"user_id": {"_eq": self.uid2id_mapper.get_user_id(user_uid, before_update=True)}})
            else:
                statement["_and"].append({"user_id": {"_is_null": True}})
            return statement
        elif ref_type == RefType.SVC or ref_type == RefType.SVC_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"svc_id": {"_eq": self.uid2id_mapper.get_service_object_id(ref_uid, before_update=True)}} # type: ignore # ref_uid is str here
                ]
            }
        elif ref_type == RefType.NWOBJ_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"obj_id": {"_eq": self.uid2id_mapper.get_network_object_id(ref_uid, before_update=True)}} # type: ignore # ref_uid is str here
                ]
            }
        elif ref_type == RefType.USER_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"user_id": {"_eq": self.uid2id_mapper.get_user_id(ref_uid, before_update=True)}} # type: ignore # ref_uid is str here
                ]
            }
        elif ref_type == RefType.SRC_ZONE or ref_type == RefType.DST_ZONE:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"zone_id": {"_eq": self.uid2id_mapper.get_zone_object_id(ref_uid, before_update=True)}}# type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict
                ]
            }
        else:
            raise FwoImporterError(f"unknown ref type: {ref_type}")


    def get_outdated_refs_to_remove(self, prev_rule: RuleNormalized, rule: RuleNormalized | None, prev_config: FwConfigNormalized, remove_all: bool) -> dict[RefType, list[dict[str, Any]]]:
        """
        Get the references that need to be removed for a rule based on comparison with the previous rule.
        Args:
            prev_rule (RuleNormalized): The previous version of the rule.
            rule (RuleNormalized): The current version of the rule.
            prev_config (FwConfigNormalized): The previous configuration containing the rules.
            remove_all (bool): If True, all references will be removed. If False, it will check for changes in references that need to be removed.
        """
        ref_uids: dict[RefType, list[tuple[str, str | None]] | list[str]] = { ref_type: [] for ref_type in RefType }

        if rule is None:
            return {}

        if not remove_all:
            ref_uids = self.get_rule_refs(rule)
        prev_ref_uids = self.get_rule_refs(prev_rule, is_prev=True)
        refs_to_remove: dict[RefType, list[dict[str, Any]]] = {}
        for ref_type in RefType:
            refs_to_remove[ref_type] = []
            for prev_ref_uid in prev_ref_uids[ref_type]:
                if prev_ref_uid in ref_uids[ref_type]:
                    prev_ref_obj, ref_obj = self.get_ref_objs(ref_type, prev_ref_uid, prev_config)
                    if prev_ref_obj == ref_obj:
                        continue # ref not removed or changed
                # ref removed or changed
                if prev_rule.rule_uid is None:
                    raise FwoImporterError(f"previous reference UID is None: {prev_ref_uid} in rule {prev_rule.rule_uid}")
                refs_to_remove[ref_type].append(self.get_ref_remove_statement(ref_type, prev_rule.rule_uid, prev_ref_uid))
        return refs_to_remove
    
    def get_refs_to_remove(self, prev_config: FwConfigNormalized) -> dict[RefType, list[dict[str, Any]]]:
        all_refs_to_remove: dict[RefType, list[dict[str, Any]]] = {ref_type: [] for ref_type in RefType}
        for prev_rulebase in prev_config.rulebases:
            if self.normalized_config is None:
                raise FwoImporterError("cannot remove outdated refs: normalized_config is None")
            rules = next((rb.rules for rb in self.normalized_config.rulebases if rb.uid == prev_rulebase.uid), None)
            if rules is None:
                continue
            for prev_rule in prev_rulebase.rules.values():
                uid = prev_rule.rule_uid
                if uid is None:
                    raise FwoImporterError(f"rule UID is None: {prev_rule} in rulebase {prev_rulebase.name}")
                rule_removed_or_changed = uid not in rules or prev_rule != rules[uid] # rule removed or changed -> all refs need to be removed
                rule_refs_to_remove = self.get_outdated_refs_to_remove(prev_rule, rules.get(uid, None), prev_config, rule_removed_or_changed)
                for ref_type, ref_statements in rule_refs_to_remove.items():
                    all_refs_to_remove[ref_type].extend(ref_statements)
        return all_refs_to_remove

    def remove_outdated_refs(self, prev_config: FwConfigNormalized):
        all_refs_to_remove = self.get_refs_to_remove(prev_config)

        if not any(all_refs_to_remove.values()):
            return
        
        import_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/updateRuleRefs.graphql"])
        
        query_variables: dict[str, Any] = {
            'importId': self.import_details.import_id,
            'ruleFroms': all_refs_to_remove[RefType.SRC],
            'ruleTos': all_refs_to_remove[RefType.DST],
            'ruleServices': all_refs_to_remove[RefType.SVC],
            'ruleNwObjResolveds': all_refs_to_remove[RefType.NWOBJ_RESOLVED],
            'ruleSvcResolveds': all_refs_to_remove[RefType.SVC_RESOLVED],
            'ruleUserResolveds': all_refs_to_remove[RefType.USER_RESOLVED],
            'ruleFromZones': all_refs_to_remove[RefType.SRC_ZONE],
            'ruleToZones': all_refs_to_remove[RefType.DST_ZONE],
        }

        try:
            import_result = self.import_details.api_call.call(import_mutation, query_variables=query_variables, analyze_payload=True)
            if "errors" in import_result:
                FWOLogger.error(f"failed to remove outdated rule references: {str(import_result['errors'])}")
                raise FwoApiWriteError(f"failed to remove outdated rule references: {str(import_result['errors'])}")
            _ = sum((import_result['data'][f"update_{ref_type.value}"].get('affected_rows', 0) for ref_type in RefType))
        except Exception:
            raise FwoApiWriteError(f"failed to remove outdated rule references: {str(traceback.format_exc())}")
    

    def get_ref_add_statement(self, ref_type: RefType, rule: RuleNormalized, ref_uid: tuple[str, str | None] | str) -> dict[str, Any]:

        if rule.rule_uid is None:
            raise FwoImporterError(f"rule UID is None: {rule} in rulebase during get_ref_add_statement") # should not happen

        if ref_type == RefType.SRC:
            nwobj_uid, user_uid = ref_uid
            _ = self.uid2id_mapper.get_network_object_id(nwobj_uid) # check if nwobj exists
            new_ref_dict = RuleFrom(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid), 
                obj_id=self.uid2id_mapper.get_network_object_id(nwobj_uid),
                user_id=self.uid2id_mapper.get_user_id(user_uid) if user_uid else None,
                rf_create=self.import_details.import_id,
                rf_last_seen=self.import_details.import_id, #TODO: to be removed in the future
                negated=rule.rule_src_neg
            ).model_dump()
            return new_ref_dict
        elif ref_type == RefType.DST:
            nwobj_uid, user_uid = ref_uid
            new_ref_dict = RuleTo(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                obj_id=self.uid2id_mapper.get_network_object_id(nwobj_uid),
                user_id=self.uid2id_mapper.get_user_id(user_uid) if user_uid else None,
                rt_create=self.import_details.import_id,
                rt_last_seen=self.import_details.import_id, #TODO: to be removed in the future
                negated=rule.rule_dst_neg
            ).model_dump()
            return new_ref_dict
        elif ref_type == RefType.SVC:
            new_ref_dict = RuleService(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                svc_id=self.uid2id_mapper.get_service_object_id(ref_uid), # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict
                rs_create=self.import_details.import_id,
                rs_last_seen=self.import_details.import_id, #TODO: to be removed in the future
            ).model_dump()
            return new_ref_dict
        elif ref_type == RefType.NWOBJ_RESOLVED:
            return {
                "mgm_id": self.import_details.mgm_details.current_mgm_id,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "obj_id": self.uid2id_mapper.get_network_object_id(ref_uid), # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict
                "created": self.import_details.import_id,
            }
        elif ref_type == RefType.SVC_RESOLVED:
            return {
                "mgm_id": self.import_details.mgm_details.current_mgm_id,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "svc_id": self.uid2id_mapper.get_service_object_id(ref_uid), # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict
                "created": self.import_details.import_id,
            }
        elif ref_type == RefType.USER_RESOLVED:
            return {
                "mgm_id": self.import_details.mgm_details.current_mgm_id,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "user_id": self.uid2id_mapper.get_user_id(ref_uid), # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict
                "created": self.import_details.import_id,
            }
        elif ref_type == RefType.SRC_ZONE or ref_type == RefType.DST_ZONE:
            return {
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "zone_id": self.uid2id_mapper.get_zone_object_id(ref_uid), # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict
                "created": self.import_details.import_id,
            }


    def get_new_refs_to_add(self, rule: RuleNormalized, prev_rule: RuleNormalized | None, prev_config: FwConfigNormalized, add_all: bool) -> dict[RefType, list[dict[str, Any]]]:
        """
        Get the references that need to be added for a rule based on comparison with the previous rule.
        Args:
            rule (RuleNormalized): The current version of the rule.
            prev_rule (RuleNormalized): The previous version of the rule.
            prev_config (FwConfigNormalized): The previous configuration containing the rules.
            add_all (bool): If True, all references will be added. If False, it will check for changes in references that need to be added.
        """
        prev_ref_uids: dict[RefType, list[tuple[str, str | None]] | list[str]] = { ref_type: [] for ref_type in RefType }
        if not add_all and prev_rule is not None:
            prev_ref_uids = self.get_rule_refs(prev_rule, is_prev=True)
        ref_uids = self.get_rule_refs(rule)
        refs_to_add: dict[RefType, list[dict[str, Any]]] = {}
        for ref_type in RefType:
            refs_to_add[ref_type] = []
            for ref_uid in ref_uids[ref_type]:
                if ref_uid in prev_ref_uids[ref_type]:
                    prev_ref_obj, ref_obj = self.get_ref_objs(ref_type, ref_uid, prev_config)
                    if prev_ref_obj == ref_obj:
                        continue # ref not added or changed
                # ref added or changed
                refs_to_add[ref_type].append(self.get_ref_add_statement(ref_type, rule, ref_uid))
        return refs_to_add

    def add_new_refs(self, prev_config: FwConfigNormalized):
        all_refs_to_add: dict[RefType, list[dict[str, Any]]] = {ref_type: [] for ref_type in RefType}
        if self.normalized_config is None:
            raise FwoImporterError("cannot add new refs: normalized_config is None")
        for rulebase in self.normalized_config.rulebases:
            prev_rules:  dict[str, RuleNormalized] = {}
            prev_rules = next((rb.rules for rb in prev_config.rulebases if rb.uid == rulebase.uid), prev_rules)
            for rule in rulebase.rules.values():
                uid = rule.rule_uid
                if uid is None:
                    raise FwoImporterError(f"rule UID is None: {rule} in rulebase {rulebase.name}")
                rule_added_or_changed = uid not in prev_rules or rule != prev_rules[uid] # rule added or changed -> all refs need to be added
                rule_refs_to_add = self.get_new_refs_to_add(rule, prev_rules.get(uid, None), prev_config, rule_added_or_changed)
                for ref_type, ref_statements in rule_refs_to_add.items():
                    all_refs_to_add[ref_type].extend(ref_statements)

        if not any(all_refs_to_add.values()):
            return 0
        
        import_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/insertRuleRefs.graphql"])
        query_variables = {
            'ruleFroms': all_refs_to_add[RefType.SRC],
            'ruleTos': all_refs_to_add[RefType.DST],
            'ruleServices': all_refs_to_add[RefType.SVC],
            'ruleNwObjResolveds': all_refs_to_add[RefType.NWOBJ_RESOLVED],
            'ruleSvcResolveds': all_refs_to_add[RefType.SVC_RESOLVED],
            'ruleUserResolveds': all_refs_to_add[RefType.USER_RESOLVED],
            'ruleFromZones': all_refs_to_add[RefType.SRC_ZONE],
            'ruleToZones': all_refs_to_add[RefType.DST_ZONE]
        }

        try:
            import_result = self.import_details.api_call.call(import_mutation, query_variables=query_variables)
        except Exception:
            raise FwoApiWriteError(f"failed to add new rule references: {str(traceback.format_exc())}")
        if 'errors' in import_result:
            raise FwoApiWriteError(f"failed to add new rule references: {str(import_result['errors'])}")
        else:
            return sum((import_result['data'][f"insert_{ref_type.value}"].get('affected_rows', 0) for ref_type in RefType))

    
    def get_rules_by_id_with_ref_uids(self, rule_ids: list[int]) -> list[dict[str, Any]]: #TODO: change return type to list[Rule] and cast
        get_rule_uid_refs_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/getRulesByIdWithRefUids.graphql"])
        query_variables = { 'ruleIds': rule_ids }
        
        try:
            import_result = self.import_details.api_call.call(get_rule_uid_refs_query, query_variables=query_variables)
            if 'errors' in import_result:
                
                FWOLogger.exception(f"fwconfig_import_rule:getRulesByIdWithRefUids - error in addNewRules: {str(import_result['errors'])}")
                return []
            else:
                return import_result['data']['rule']
        except Exception:
            FWOLogger.exception(f"failed to get rules from API: {str(traceback.format_exc())}")
            raise


    def get_rules(self, rule_uids: dict[str, list[str]]) -> list[Rulebase]:
        #TODO: seems unnecessary, as the rulebases should already have been created this way in the normalized config
        rulebases: list[Rulebase] = []

        if self.normalized_config is None:
            raise FwoImporterError("cannot get rules: normalized_config is None")
        
        for rb in self.normalized_config.rulebases:
            if rb.uid in rule_uids:
                filtered_rules = {uid: rule for uid, rule in rb.rules.items() if uid in rule_uids[rb.uid]}
                rulebase = Rulebase(
                    name=rb.name,
                    uid=rb.uid,
                    mgm_uid=rb.mgm_uid,
                    is_global=rb.is_global,
                    rules=filtered_rules
                )
                rulebases.append(rulebase)
        return rulebases


    # assuming input of form:
    # {'rule-uid1': {'rule_num': 17', ... }, 'rule-uid2': {'rule_num': 8, ...}, ... }
    @staticmethod
    def rule_dict_to_ordered_list_of_rule_uids(rules: dict[str, dict[str, Any]]) -> list[str]:
        return sorted(rules, key=lambda x: rules[x]['rule_num'])

    @staticmethod
    def list_diff(oldRules: list[str], newRules: list[str]) -> list[tuple[str, str]]:
        diff = list(ndiff(oldRules, newRules))
        changes: list[tuple[str, str]] = []

        for change in diff:
            if change.startswith("- "):
                changes.append(('delete', change[2:]))
            elif change.startswith("+ "):
                changes.append(('insert', change[2:]))
            elif change.startswith("  "):
                changes.append(('unchanged', change[2:]))
        
        return changes

    def _find_following_rules(self, ruleUid: str, previousRulebase: dict[str, int], rulebaseId: str) -> Generator[str, None, None]:
        """
        Helper method to find the next rule in self that has an existing rule number.
        
        :param ruleUid: The ID of the current rule being processed.
        :param previousRulebase: Dictionary of existing rule IDs and their rule_number values.
        :return: Generator yielding rule IDs that appear after `current_rule_id` in self.new_rules.
        """
        found = False
        if self.normalized_config is None:
            raise FwoImporterError("cannot find following rules: normalized_config is None")
        current_rulebase = self.normalized_config.get_rulebase(rulebaseId)
        for currentUid in current_rulebase.rules:
            if currentUid == ruleUid:
                found = True
            elif found and ruleUid in previousRulebase:
                yield currentUid


    # adds new rule_metadatum to the database
    def add_new_rule_metadata(self, new_rules: list[Rulebase]) -> tuple[int, list[int]]:
        changes: int = 0
        new_rule_metadata_ids: list[int] = []
        new_rule_ids: list[int] = []

        add_new_rule_metadata_mutation = """mutation upsertRuleMetadata($ruleMetadata: [rule_metadata_insert_input!]!) {
             insert_rule_metadata(objects: $ruleMetadata, on_conflict: {constraint: rule_metadata_rule_uid_unique, update_columns: [rule_last_modified]}) {
                affected_rows
                returning {
                    rule_metadata_id
                }
            }
        }
        """

        add_new_rule_metadata: list[dict[str, Any]] = self.prepare_new_rule_metadata(new_rules)
        query_variables = { 'ruleMetadata': add_new_rule_metadata }
        
        FWOLogger.debug(json.dumps(query_variables), 10)    # just for debugging purposes

        try:
            import_result = self.import_details.api_call.call(add_new_rule_metadata_mutation, query_variables=query_variables, analyze_payload=True)
        except Exception:
            raise FwoApiWriteError(f"failed to write new RulesMetadata: {str(traceback.format_exc())}")
        if 'errors' in import_result:
            raise FwoApiWriteError(f"failed to write new RulesMetadata: {str(import_result['errors'])}")
        else:
            # reduce change number by number of rulebases
            changes = import_result['data']['insert_rule_metadata']['affected_rows']
            if changes > 0:
                for rule_metadata_id in import_result['data']['insert_rule_metadata']['returning']:
                    new_rule_metadata_ids.append(rule_metadata_id)
        
        return changes, new_rule_ids


    def add_rulebases_without_rules(self, new_rules: list[Rulebase]):
        changes: int = 0
        new_rulebase_ids: list[int] = []
        
        add_rulebases_without_rules_mutation = """mutation upsertRulebaseWithoutRules($rulebases: [rulebase_insert_input!]!) {
                insert_rulebase(
                    objects: $rulebases,
                    on_conflict: {
                        constraint: unique_rulebase_mgm_id_uid,
                        update_columns: []
                    }
                ) {
                    affected_rows
                    returning {
                        id
                        name
                        uid
                    }
                }
            }
        """

        new_rulebases_for_import: list[RulebaseForImport] = self.prepare_new_rulebases(new_rules)
        query_variables = { 'rulebases': [rb.model_dump(by_alias=True, exclude_unset=True) for rb in new_rulebases_for_import] }
        
        try:
            import_result = self.import_details.api_call.call(add_rulebases_without_rules_mutation, query_variables=query_variables)
        except Exception:
            FWOLogger.exception(f"fwo_api:importRules - error in addNewRules: {str(traceback.format_exc())}")
            raise FwoApiWriteError(f"failed to write new rulebases: {str(traceback.format_exc())}")
        if 'errors' in import_result:
            FWOLogger.exception(f"fwo_api:importRules - error in addNewRules: {str(import_result['errors'])}")
            raise FwoApiWriteError(f"failed to write new rulebases: {str(import_result['errors'])}")
        else:
            # reduce change number by number of rulebases
            changes = import_result['data']['insert_rulebase']['affected_rows']
            if changes>0:
                for rulebase in import_result['data']['insert_rulebase']['returning']:
                    new_rulebase_ids.append(rulebase['id'])
            # finally, add the new rulebases to the map for next step (adding rulebase with rules)
            self.import_details.SetRulebaseMap(self.import_details.api_call) 
            return changes, new_rulebase_ids
        
    # as we cannot add the rules for all rulebases in one go (using a constraint from the rule table), 
    # we need to add them per rulebase separately
    #TODO: separation because of constraint still needed?
    def add_rules_within_rulebases(self, rulebases: list[Rulebase]) -> tuple[int, list[dict[str, Any]]]:
        """
        Adds rules within the given rulebases to the database.

        Args:
            rulebases (list[Rulebase]): List of Rulebase objects containing rules to be added

        Returns:
            tuple[int, list[dict]]: A tuple containing the number of changes made and a list of dictionaries,
                each with 'rule_id' and 'rule_uid' for each newly added rule.
        """
        changes: int = 0
        new_rule_ids: list[dict[str, Any]] = []

        upsert_rulebase_with_rules = """mutation upsertRules($rules: [rule_insert_input!]!) {
                insert_rule(
                    objects: $rules,
                    on_conflict: { constraint: rule_unique_mgm_id_rule_uid_rule_create_xlate_rule, update_columns: [] }
                ) { affected_rows,  returning { rule_id, rule_uid } }
            }
        """
        for rulebase in rulebases:
            new_rules = self.prepare_rules_for_import(list(rulebase.rules.values()), rulebase.uid)
            if len(new_rules)>0:
                query_variables = { 'rules': [rule.model_dump() for rule in new_rules] }
                try:
                    import_result = self.import_details.api_call.call(upsert_rulebase_with_rules, query_variables=query_variables, analyze_payload=True)
                except Exception:
                    FWOLogger.exception(f"fwo_api:addRulesWithinRulebases - error in addRulesWithinRulebases: {str(traceback.format_exc())}")
                    raise FwoApiWriteError(f"failed to write rules of rulebase {rulebase.uid}: {str(traceback.format_exc())}")
                if 'errors' in import_result:
                    FWOLogger.exception(f"fwo_api:addRulesWithinRulebases - error in addRulesWithinRulebases: {str(import_result['errors'])}")
                    raise FwoApiWriteError(f"failed to write rules of rulebase {rulebase.uid}: {str(import_result['errors'])}")
                else:
                    changes += import_result['data']['insert_rule']['affected_rows']
                    new_rule_ids += import_result['data']['insert_rule']['returning']
        return changes, new_rule_ids

    # adds only new rules to the database
    # unchanged or deleted rules are not touched here
    def add_new_rules(self, rulebases: list[Rulebase]) -> tuple[int, list[dict[str, Any]]]:
        #TODO: currently brute-forcing all rulebases and rules and depending on constraints to avoid duplicates. seems inefficient.
        changes1, _ = self.add_rulebases_without_rules(rulebases)
        changes2, new_rule_ids = self.add_rules_within_rulebases(rulebases)

        return changes1 + changes2, new_rule_ids


    def prepare_new_rule_metadata(self, new_rules: list[Rulebase]) -> list[dict[str, Any]]:
        newRuleMetadata: list[dict[str, Any]] = []
        for rulebase in new_rules:
            for rule_uid, rule in rulebase.rules.items():
                rm4import = RuleMetadatum(
                    rule_uid=rule_uid,
                    mgm_id=self.import_details.mgm_details.current_mgm_id,
                    rule_last_modified=self.import_details.import_id,
                    rule_created=self.import_details.import_id,
                    rule_last_hit=rule.last_hit,
                )
                newRuleMetadata.append(rm4import.model_dump())
        # TODO: add other fields
        return newRuleMetadata    

    # creates a structure of rulebases optinally including rules for import
    def prepare_new_rulebases(self, new_rulebases: list[Rulebase]) -> list[RulebaseForImport]:
        new_rules_for_import: list[RulebaseForImport] = []

        for rulebase in new_rulebases:
            rb4import = RulebaseForImport(
                name=rulebase.name,
                mgm_id=self.import_details.mgm_details.current_mgm_id,
                uid=rulebase.uid,
                is_global=self.import_details.mgm_details.current_mgm_is_super_manager,
                created=self.import_details.import_id,
            )
            new_rules_for_import.append(rb4import)
        # TODO: see where to get real UIDs (both for rulebase and manager)
        # add rules for each rulebase
        return new_rules_for_import    

    def mark_rules_removed(self, removedRuleUids: dict[str, list[str]]) -> tuple[int, list[int]]:
        changes = 0
        collectedRemovedRuleIds: list[int] = []

        # TODO: make sure not to mark new (changed) rules as removed (order of calls!)
        
        for rbName in removedRuleUids:
            removedRuleIds = [] # return values
            if len(removedRuleUids[rbName])>0:   # if nothing to remove, skip this
                removeMutation = """
                    mutation markRulesRemoved($importId: bigint!, $mgmId: Int!, $uids: [String!]!) {
                        update_rule(where: {removed: { _is_null: true }, rule_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}}, _set: {removed: $importId, active:false}) {
                            affected_rows
                            returning { rule_id }
                        }
                    }
                """
                query_variables: dict[str, Any] = {  'importId': self.import_details.import_id,
                                    'mgmId': self.import_details.mgm_details.current_mgm_id,
                                    'uids': list(removedRuleUids[rbName]) }
                
                try:
                    removeResult = self.import_details.api_call.call(removeMutation, query_variables=query_variables)
                except Exception:
                    raise FwoApiWriteError(f"failed to remove rules: {str(traceback.format_exc())}")
                if 'errors' in removeResult:
                    raise FwoApiWriteError(f"failed to remove rules: {str(removeResult['errors'])}")
                else:
                    changes = int(removeResult['data']['update_rule']['affected_rows'])
                    removedRuleIds = removeResult['data']['update_rule']['returning']
                    collectedRemovedRuleIds += [item['rule_id'] for item in removedRuleIds]

        return changes, collectedRemovedRuleIds


    def create_new_rule_version(self, rule_uids: dict[str, list[str]]) -> tuple[int, list[int],  list[dict[str, Any]]]:
        """
        Creates new versions of rules specified in rule_uids by inserting new rule entries and marking the old ones as removed.

        Args:
            rule_uids (dict[str, list[str]]): A dictionary where keys are rulebase UIDs and values are lists of rule UIDs to be updated.

        Returns:
            tuple[int, list[int], list[dict]]: A tuple containing the number of changes made, a list of old rule IDs that were changed, and a list of newly inserted rule entries.
        """
        self._changed_rule_id_map = {}

        if len(rule_uids) == 0:
            return 0, [], []
        
        create_new_rule_versions_mutation = """mutation createNewRuleVersions($objects: [rule_insert_input!]!, $uids: [String!], $mgmId: Int!, $importId: bigint) {
            insert_rule(objects: $objects) {
                affected_rows
                returning {
                rule_id
                rule_src_refs
                rule_dst_refs
                rule_svc_refs
                rule_to_zone
                rule_from_zone
                rule_src_neg
                rule_dst_neg
                rule_svc_neg
                rulebase_id
                rule_installon
                rule_uid
                }
            }

            update_rule(
                where: {
                removed: { _is_null: true },
                rule_uid: { _in: $uids },
                mgm_id: { _eq: $mgmId },
                rule_last_seen: { _neq: $importId }
                },
                _set: {
                removed: $importId,
                active: false
                }
            ) {
                affected_rows
                returning {
                rule_id
                rule_uid
                }
            }
        }
        """

        import_rules: list[Rule] = []

        for rulebase_uid in list(rule_uids.keys()):
                
                changed_rule_of_rulebase: list[RuleNormalized] = [
                    rule_with_changes 
                    for rule_with_changes in self.rule_order_service.target_rules_flat 
                    if rule_with_changes.rule_uid in rule_uids[rulebase_uid]
                ]

                import_rules_of_rulebase = self.prepare_rules_for_import(changed_rule_of_rulebase, rulebase_uid)

                import_rules.extend(import_rules_of_rulebase)

        create_new_rule_version_variables: dict[str, Any] = {
            "objects": [rule.model_dump() for rule in import_rules],
            "uids": [rule.rule_uid for rule in import_rules],
            "mgmId": self.import_details.mgm_details.current_mgm_id,
            "importId": self.import_details.import_id
        }
        
        try:
            create_new_rule_version_result = self.import_details.api_call.call(create_new_rule_versions_mutation, query_variables=create_new_rule_version_variables)
        except Exception:
            raise FwoApiWriteError(f"failed to move rules: {str(traceback.format_exc())}")
        if 'errors' in create_new_rule_version_result:
            raise FwoApiWriteError(f"failed to create new rule versions: {str(create_new_rule_version_result['errors'])}")
        else:
            changes = int(create_new_rule_version_result['data']['update_rule']['affected_rows'])
            update_rules_return: list[dict[str, Any]] = create_new_rule_version_result['data']['update_rule']['returning']
            insert_rules_return: list[dict[str, Any]] = create_new_rule_version_result['data']['insert_rule']['returning']

            self._changed_rule_id_map = {
                update_item['rule_id']: next(
                    insert_item['rule_id']
                    for insert_item in insert_rules_return
                    if insert_item['rule_uid'] == update_item['rule_uid']
                )
                for update_item in update_rules_return
            }


            collected_changed_rule_ids: list[int] = list(self._changed_rule_id_map.keys()) or []


        return changes, collected_changed_rule_ids, insert_rules_return


    def update_rule_enforced_on_gateway_after_move(self, insert_rules_return: list[dict[str, Any]], update_rules_return: list[dict[str, Any]]) -> tuple[int, int, list[str]]:
        """
            Updates the db table rule_enforced_on_gateway by creating new entries for a list of rule_ids and setting the old versions of said rules removed.
        """

        id_map: dict[int, int] = {}

        for insert_rules_return_entry in insert_rules_return:
            id_map[
                insert_rules_return_entry["rule_id"]
            ] = next(
                update_rules_return_entry["rule_id"]
                for update_rules_return_entry in update_rules_return
                if update_rules_return_entry["rule_uid"] == insert_rules_return_entry["rule_uid"]
            )


        set_rule_enforced_on_gateway_entries_removed_mutation = """mutation set_rule_enforced_on_gateway_entries_removed($rule_ids: [Int!], $importId: bigint) {
                update_rule_enforced_on_gateway(
                    where: {
                        rule_id: { _in: $rule_ids },
                    },
                    _set: {
                        removed: $importId,
                    }
                ) {
                    affected_rows
                    returning {
                        rule_id
                        dev_id
                    }
                }
            }
        """

        set_rule_enforced_on_gateway_entries_removed_variables: dict[str, Any] = {
            "rule_ids": list(id_map.values()),
            "importId": self.import_details.import_id,
        }

        insert_rule_enforced_on_gateway_entries_mutation = """
        mutation insert_rule_enforced_on_gateway_entries($new_entries: [rule_enforced_on_gateway_insert_input!]!) {
            insert_rule_enforced_on_gateway(
                objects: $new_entries
            ) {
                affected_rows
            }
        }
        """

        try:
            set_rule_enforced_on_gateway_entries_removed_result =  self.import_details.api_call.call(set_rule_enforced_on_gateway_entries_removed_mutation, set_rule_enforced_on_gateway_entries_removed_variables)

            if 'errors' in set_rule_enforced_on_gateway_entries_removed_result:
                FWOLogger.exception(f"fwo_api:update_rule_enforced_on_gateway_after_move - error while updating moved rules refs: {str(set_rule_enforced_on_gateway_entries_removed_result['errors'])}")
                return 1, 0, []

            insert_rule_enforced_on_gateway_entries_variables: dict[str, Any] = {
                "new_entries": [
                    {
                        "rule_id": new_id,
                        "dev_id": next(entry for entry in  set_rule_enforced_on_gateway_entries_removed_result["data"]["update_rule_enforced_on_gateway"]["returning"] if entry["rule_id"] == id_map[new_id])["dev_id"],
                        "created": self.import_details.import_id,
                    }
                    for new_id in id_map.keys()
                ]
            }

            insert_rule_enforced_on_gateway_entries_result =  self.import_details.api_call.call(insert_rule_enforced_on_gateway_entries_mutation, insert_rule_enforced_on_gateway_entries_variables)

            if 'errors' in insert_rule_enforced_on_gateway_entries_result:
                FWOLogger.exception(f"fwo_api:update_rule_enforced_on_gateway_after_move - error while updating moved rules refs: {str(insert_rule_enforced_on_gateway_entries_result['errors'])}")
                return 1, 0, []
            
            return 0, 0, []


        except Exception:
            FWOLogger.exception(f"failed to move rules: {str(traceback.format_exc())}")
            return 1, 0, []
        
    def verify_rules_moved(self, changed_rule_uids: dict[str, list[str]]) -> tuple[int, list[str]]:
        number_of_moved_rules = 0

        moved_rule_uids: list[str] = []

        changed_rule_uids_flat = [
            uid 
            for uids in changed_rule_uids.values() 
            for uid in uids
        ]

        rule_order_service_moved_rule_uids_flat = [
            rule_uid 
            for rule_uids in self.rule_order_service._moved_rule_uids.values() # type: ignore #TODO: access to protected member
            for rule_uid in rule_uids
        ]

        for rule_uid in rule_order_service_moved_rule_uids_flat:
            if rule_uid in changed_rule_uids_flat:
                moved_rule_uids.append(rule_uid)
                number_of_moved_rules += 1

        return number_of_moved_rules, moved_rule_uids
            
            

    # TODO: limit query to a single rulebase
    def get_rule_num_map(self) -> dict[str, dict[str, float]]:
        query = "query getRuleNumMap($mgmId: Int) { rule(where:{mgm_id:{_eq:$mgmId}}) { rule_uid rulebase_id rule_num_numeric } }"
        try:
            result = self.import_details.api_call.call(query=query, query_variables={"mgmId": self.import_details.mgm_details.current_mgm_id})
        except Exception:
            FWOLogger.error('Error while getting rule number map')
            return {}

        rule_num_map: dict[str, dict[str, float]] = {}
        for rule_num in result['data']['rule']:
            if rule_num['rulebase_id'] not in rule_num_map:
                rule_num_map.update({ rule_num['rulebase_id']: {} })  # initialize rulebase
            rule_num_map[rule_num['rulebase_id']].update({ rule_num['rule_uid']: rule_num['rule_num_numeric']})
        return rule_num_map

    def get_next_rule_num_map(self) -> dict[str, float]: #TODO: implement!
        query = "query getRuleNumMap { rule { rule_uid rule_num_numeric } }"
        try:
            _ = self.import_details.api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error('Error while getting rule number')
            return {}

        rule_num_map: dict[str, float] = {}
        # for ruleNum in result['data']['rule']:
        #     rule_num_map.update({ruleNum['rule_uid']: ruleNum['rule_num_numeric']})
        return rule_num_map

    def get_rule_type_map(self) -> dict[str, int]:
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = self.import_details.api_call.call(query=query, query_variables={})
        except Exception:
            
            FWOLogger.error('Error while getting stm_track')
            return {}
        
        rule_type_map: dict[str, int] = {}
        for track in result['data']['stm_track']:
            rule_type_map.update({track['track_name']: track['track_id']})
        return rule_type_map

    def get_current_rules(self, import_id: int, mgm_id: int, rulebase_name: str) -> list[list[Any]] | None:
        query_variables: dict[str, Any] = {
            "importId": import_id,
            "mgmId": mgm_id,
            "rulebaseName": rulebase_name
        }
        query = """
            query get_rulebase($importId: bigint!, $mgmId: Int!, $rulebaseName: String!) {
                rulebase(where: {mgm_id: {_eq: $mgmId}, name: {_eq: $rulebaseName}}) {
                    id
                    rules(where: {rule: {rule_create: {_lt: $importId}, removed: {_is_null: true}}}, order_by: {rule: {rule_num_numeric: asc}}) {
                        rule_num
                        rule_num_numeric
                        rule_uid
                    }
                }
            }
        """
        
        try:
            query_result = self.import_details.api_call.call(query, query_variables=query_variables)
        except Exception:
            FWOLogger.error(f"error while getting current rulebase: {str(traceback.format_exc())}")
            return
        
        try:
            rule_list = query_result['data']['rulebase'][0]['rules']
        except Exception:
            FWOLogger.error(f'could not find rules in query result: {query_result}')
            return

        rules: list[list[Any]] = []
        for rule in rule_list:
            rules.append([rule['rule']['rule_num'], rule['rule']['rule_num_numeric'], rule['rule']['rule_uid']]) # TODO: change to tuple?
        return rules

    def insert_rulebase(self, rulebase_name: str, is_global: bool = False):
        # call for each rulebase to add
        query_variables: dict[str, Any] = {
            "rulebase": {
                "is_global": is_global,
                "mgm_id": self.import_details.mgm_details.current_mgm_id,
                "name": rulebase_name,
                "created": self.import_details.import_id
            }
        }

        mutation = """
            mutation upsertRulebaseWithRules($rulebases: [rulebase_insert_input!]!) {
                insert_rulebase(
                    objects: $rulebases,
                    on_conflict: {
                        constraint: unique_rulebase_mgm_id_uid,
                        update_columns: [created, is_global]
                    }
                ) {
                    returning {
                        id
                        name
                        rule_id
                        rulebase_id
                    }
                }
            }
        """
        return self.import_details.api_call.call(mutation, query_variables=query_variables)


    def import_insert_rulebase_on_gateway(self, rulebase_id: int, dev_id: int, order_num: int = 0):
        query_variables: dict[str, Any] = {
            "rulebase2gateway": [
                {
                    "dev_id": dev_id,
                    "rulebase_id": rulebase_id,
                    "order_no": order_num
                }
            ]
        }
        mutation = """
            mutation importInsertRulebaseOnGateway($rulebase2gateway: [rulebase_on_gateway_insert_input!]!) {
                insert_rulebase_on_gateway(objects: $rulebase2gateway) {
                affected_rows
                }
            }"""
        
        return self.import_details.api_call.call(mutation, query_variables=query_variables)

    def _get_list_of_enforced_gateways(self, rule: RuleNormalized, import_details: ImportStateController) -> list[int] | None:
        if rule.rule_installon is None:
            return None
        enforced_gw_ids: list[int] = []
        for gw_uid in rule.rule_installon.split(fwo_const.LIST_DELIMITER):
            gw_id = import_details.lookupGatewayId(gw_uid)
            if gw_id is None:
                FWOLogger.warning(f"could not find gateway id for gateway uid {gw_uid} during rule import preparation")
                continue
            enforced_gw_ids.append(gw_id)
        if len(enforced_gw_ids) == 0:
            return None

        return enforced_gw_ids

    def prepare_rules_for_import(self, rules: list[RuleNormalized], rulebase_uid: str) -> list[Rule]:
        # get rulebase_id for rulebaseUid
        rulebase_id = self.import_details.lookupRulebaseId(rulebase_uid)

        prepared_rules = [
            self.prepare_single_rule_for_import(rule, self.import_details, rulebase_id)
            for rule in rules
        ]
        return prepared_rules
    
    def prepare_single_rule_for_import(self, rule: RuleNormalized, importDetails: ImportStateController, rulebase_id: int) -> Rule:
        rule_for_import = Rule(
            mgm_id=importDetails.mgm_details.current_mgm_id,
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
            rule_time=rule.rule_time,
            rule_name=rule.rule_name,
            rule_uid=rule.rule_uid,
            rule_custom_fields=rule.rule_custom_fields,
            rule_implied=rule.rule_implied,
            # parent_rule_id=rule.parent_rule_id,
            rule_comment=rule.rule_comment,
            rule_from_zone=None, #TODO: to be removed or changed to string of joined zone names
            rule_to_zone=None,   #TODO: to be removed or changed to string of joined zone names
            access_rule=True,
            nat_rule=False,
            is_global=False,
            rulebase_id=rulebase_id,
            rule_create=importDetails.import_id,
            rule_last_seen=importDetails.import_id,
            rule_num_numeric=rule.rule_num_numeric,
            action_id = importDetails.lookupAction(rule.rule_action),
            track_id = importDetails.lookupTrack(rule.rule_track),
            rule_head_text=rule.rule_head_text,
            rule_installon=rule.rule_installon,
            last_change_admin=None #TODO: get id from rule.last_change_admin
        )

        return rule_for_import

    def write_changelog_rules(self, added_rules_ids: list[int], removed_rules_ids: list[int]):

        changelog_rule_insert_objects = self.prepare_changelog_rules_insert_objects(added_rules_ids, removed_rules_ids)

        updateChanglogRules = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/updateChanglogRules.graphql"])

        query_variables = {
            'rule_changes': changelog_rule_insert_objects
        }

        if len(changelog_rule_insert_objects) > 0:
            try:
                updateChanglogRules_result = self.import_details.api_call.call(updateChanglogRules, query_variables=query_variables, analyze_payload=True)
                if 'errors' in updateChanglogRules_result:
                    FWOLogger.exception(f"error while adding changelog entries for objects: {str(updateChanglogRules_result['errors'])}")
            except Exception:
                FWOLogger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")


    def prepare_changelog_rules_insert_objects(self, added_rules_ids: list[int], removed_rules_ids: list[int]) -> list[dict[str, Any]]:
        """
            Creates two lists of insert arguments for the changelog_rules db table, one for new rules, one for deleted.
        """

        change_logger = ChangeLogger()
        changelog_rule_insert_objects: list[dict[str, Any]] = []
        importTime = datetime.now().isoformat()
        changeTyp = 3

        if self.import_details.is_full_import or self.import_details.IsClearingImport:
            changeTyp = 2   # TODO: Somehow all imports are treated as im operation.

        for rule_id in added_rules_ids:
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.import_details, 'I', changeTyp, importTime, rule_id))

        for rule_id in removed_rules_ids:
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.import_details, 'D', changeTyp, importTime, rule_id))

        for old_rule_id, new_rule_id in self._changed_rule_id_map.items():
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.import_details, 'C', changeTyp, importTime, new_rule_id, old_rule_id))

        return changelog_rule_insert_objects

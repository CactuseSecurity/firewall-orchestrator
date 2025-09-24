from enum import Enum
import traceback
from difflib import ndiff
import json

import fwo_globals
import fwo_const
import fwo_api_call as fwo_api_call
from fwo_exceptions import FwoApiWriteError, FwoImporterError
from models.rule import Rule
from models.rule_metadatum import RuleMetadatum
from models.rulebase import Rulebase, RulebaseForImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from fwo_log import ChangeLogger, getFwoLogger
from datetime import datetime
from models.rule_from import RuleFrom
from models.rule_to import RuleTo
from models.rule_service import RuleService
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from models.rule import RuleNormalized
from services.group_flats_mapper import GroupFlatsMapper
from services.enums import Services
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

# this class is used for importing rules and rule refs into the FWO API
class FwConfigImportRule():

    _changed_rule_id_map: dict
    uid2id_mapper: Uid2IdMapper
    group_flats_mapper: GroupFlatsMapper
    prev_group_flats_mapper: GroupFlatsMapper
    import_details: ImportStateController
    normalized_config: FwConfigNormalized

    def __init__(self):
        self._changed_rule_id_map = {}

        service_provider = ServiceProvider()
        self.global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self.import_details = self.global_state.import_state
        self.normalized_config = self.global_state.normalized_config
        self.uid2id_mapper = service_provider.get_service(Services.UID2ID_MAPPER, self.import_details.ImportId)
        self.group_flats_mapper = service_provider.get_service(Services.GROUP_FLATS_MAPPER, self.import_details.ImportId)
        self.prev_group_flats_mapper = service_provider.get_service(Services.PREV_GROUP_FLATS_MAPPER, self.import_details.ImportId)


    def updateRulebaseDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.import_details.DebugLevel)

        # calculate rule diffs
        changedRuleUids = {}
        deletedRuleUids = {}
        newRuleUids = {}
        movedRuleUids = {}
        ruleUidsInBoth = {}
        previousRulebaseUids = []
        currentRulebaseUids = []
        new_hit_information = []

        rule_order_service = RuleOrderService()
        deletedRuleUids, newRuleUids, movedRuleUids = rule_order_service.initialize(prevConfig, self)

        # collect rulebase UIDs of previous config
        for rulebase in prevConfig.rulebases:
            previousRulebaseUids.append(rulebase.uid)

        # collect rulebase UIDs of current (just imported) config
        for rulebase in self.normalized_config.rulebases:
            currentRulebaseUids.append(rulebase.uid)

        for rulebaseId in previousRulebaseUids:
            currentRulebase = self.normalized_config.getRulebase(rulebaseId)
            if rulebaseId in currentRulebaseUids:
                # deal with policies contained both in this and previous config
                previousRulebase = prevConfig.getRulebase(rulebaseId)

                ruleUidsInBoth.update({ rulebaseId: list(currentRulebase.Rules.keys() & previousRulebase.Rules.keys()) })
            else:
                logger.info(f"previous rulebase has been deleted: {currentRulebase.name} (id:{rulebaseId})")

        # find changed rules
        for rulebaseId in ruleUidsInBoth:
            changedRuleUids.update({ rulebaseId: [] })
            currentRulebase = self.normalized_config.getRulebase(rulebaseId) # [pol for pol in self.NormalizedConfig.rulebases if pol.Uid == rulebaseId]
            previousRulebase = prevConfig.getRulebase(rulebaseId)
            for ruleUid in ruleUidsInBoth[rulebaseId]:
                self.preserve_rule_num_numeric(currentRulebase, previousRulebase, ruleUid)
                self.collect_changed_rules(ruleUid, currentRulebase, previousRulebase, rulebaseId, changedRuleUids)
                self.collect_last_hit_changes(ruleUid, currentRulebase, previousRulebase, new_hit_information)

        # add full rule details first
        newRulebases = self.getRules(newRuleUids)

        # update rule_metadata before adding rules
        num_added_metadata_rules, new_rule_metadata_ids = self.addNewRuleMetadata(newRulebases)
        _, _ = self.update_rule_metadata_last_hit(new_hit_information)

        # # now update the database with all rule diffs
        self.uid2id_mapper.update_rule_mapping()

        num_added_rules, new_rule_ids = self.addNewRules(newRulebases)
        num_changed_rules, old_rule_ids, updated_rule_ids = self.create_new_rule_version(changedRuleUids)

        self.uid2id_mapper.add_rule_mappings(new_rule_ids + updated_rule_ids)
        num_new_refs = self.add_new_refs(prevConfig)

        num_deleted_rules, removed_rule_ids = self.markRulesRemoved(deletedRuleUids, changedRuleUids)
        num_removed_refs = self.remove_outdated_refs(prevConfig)

        _, num_moved_rules, _ = self.verify_rules_moved(changedRuleUids)

        new_rule_ids = [rule['rule_id'] for rule in new_rule_ids]  # extract rule_ids from the returned list of dicts
        self.write_changelog_rules(new_rule_ids, removed_rule_ids)

        self.import_details.Stats.RuleAddCount += num_added_rules
        self.import_details.Stats.RuleDeleteCount += num_deleted_rules
        self.import_details.Stats.RuleMoveCount += num_moved_rules
        self.import_details.Stats.RuleChangeCount += num_changed_rules

        # TODO: rule_nwobj_resolved fuellen (recert?)
        return new_rule_ids


    def collect_last_hit_changes(self, rule_uid, current_rulebase, previous_rulebase, new_hit_information):
        if self.last_hit_changed(current_rulebase.Rules[rule_uid], previous_rulebase.Rules[rule_uid]):
            self.append_rule_metadata_last_hit(new_hit_information, current_rulebase.Rules[rule_uid], self.import_details.MgmDetails.CurrentMgmId)


    @staticmethod
    def collect_changed_rules(rule_uid, current_rulebase, previous_rulebase, rulebase_id, changed_rule_uids):
        if current_rulebase.Rules[rule_uid] != previous_rulebase.Rules[rule_uid]:
            changed_rule_uids[rulebase_id].append(rule_uid)


    @staticmethod
    def preserve_rule_num_numeric(current_rulebase, previous_rulebase, rule_uid):
        if current_rulebase.Rules[rule_uid].rule_num_numeric == 0:
            current_rulebase.Rules[rule_uid].rule_num_numeric = previous_rulebase.Rules[rule_uid].rule_num_numeric 


    @staticmethod
    def last_hit_changed(current_rule, previous_rule):
        return current_rule.last_hit != previous_rule.last_hit
    

    def get_members(self, type, refs) -> list[str]:
        if type == type.NETWORK_OBJECT:
            return [member.split(fwo_const.user_delimiter)[0] for member in refs.split(fwo_const.list_delimiter) if member] if refs else []
        return refs.split(fwo_const.list_delimiter) if refs else []

    def get_rule_refs(self, rule, is_prev=False) -> dict[RefType, list[str]]:
        froms = []
        tos = []
        users = []
        for src_ref in rule.rule_src_refs.split(fwo_const.list_delimiter):
            user_ref = None
            if fwo_const.user_delimiter in src_ref:
                src_ref, user_ref = src_ref.split(fwo_const.user_delimiter)
                users.append(user_ref)
            froms.append((src_ref, user_ref))
        for dst_ref in rule.rule_dst_refs.split(fwo_const.list_delimiter):
            user_ref = None
            if fwo_const.user_delimiter in dst_ref:
                dst_ref, user_ref = dst_ref.split(fwo_const.user_delimiter)
                users.append(user_ref)
            tos.append((dst_ref, user_ref))
        svcs = rule.rule_svc_refs.split(fwo_const.list_delimiter)
        if is_prev:
            nwobj_resolveds = self.prev_group_flats_mapper.get_network_object_flats([ref[0] for ref in froms + tos])
            svc_resolveds = self.prev_group_flats_mapper.get_service_object_flats(svcs)
            user_resolveds = self.prev_group_flats_mapper.get_user_flats(users)
        else:
            nwobj_resolveds = self.group_flats_mapper.get_network_object_flats([ref[0] for ref in froms + tos])
            svc_resolveds = self.group_flats_mapper.get_service_object_flats(svcs)
            user_resolveds = self.group_flats_mapper.get_user_flats(users)
        return {
            RefType.SRC: froms,
            RefType.DST: tos,
            RefType.SVC: svcs,
            RefType.NWOBJ_RESOLVED: nwobj_resolveds,
            RefType.SVC_RESOLVED: svc_resolveds,
            RefType.USER_RESOLVED: user_resolveds
        }

    def get_ref_objs(self, ref_type, ref_uid, prev_config: FwConfigNormalized):
        if ref_type == RefType.SRC or ref_type == RefType.DST:
            nwobj_uid, user_uid = ref_uid
            return (prev_config.network_objects.get(nwobj_uid, None), prev_config.users.get(user_uid, None) if user_uid else None), \
                     (self.normalized_config.network_objects.get(nwobj_uid, None), self.normalized_config.users.get(user_uid, None) if user_uid else None)
        if ref_type == RefType.NWOBJ_RESOLVED:
            return prev_config.network_objects.get(ref_uid, None), self.normalized_config.network_objects.get(ref_uid, None)
        if ref_type == RefType.SVC or ref_type == RefType.SVC_RESOLVED:
            return prev_config.service_objects.get(ref_uid, None), self.normalized_config.service_objects.get(ref_uid, None)
        return prev_config.users.get(ref_uid, None), self.normalized_config.users.get(ref_uid, None)
    
    def get_ref_remove_statement(self, ref_type, rule_uid, ref_uid):
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
                    {"svc_id": {"_eq": self.uid2id_mapper.get_service_object_id(ref_uid, before_update=True)}}
                ]
            }
        elif ref_type == RefType.NWOBJ_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"obj_id": {"_eq": self.uid2id_mapper.get_network_object_id(ref_uid, before_update=True)}}
                ]
            }
        elif ref_type == RefType.USER_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"user_id": {"_eq": self.uid2id_mapper.get_user_id(ref_uid, before_update=True)}}
                ]
            }


    def get_outdated_refs_to_remove(self, prev_rule: RuleNormalized, rule: RuleNormalized|None, prev_config, remove_all):
        """
        Get the references that need to be removed for a rule based on comparison with the previous rule.
        Args:
            prev_rule (RuleNormalized): The previous version of the rule.
            rule (RuleNormalized): The current version of the rule.
            prev_config (FwConfigNormalized): The previous configuration containing the rules.
            remove_all (bool): If True, all references will be removed. If False, it will check for changes in references that need to be removed.
        """
        ref_uids = { ref_type: [] for ref_type in RefType }
        if not remove_all:
            ref_uids = self.get_rule_refs(rule)
        prev_ref_uids = self.get_rule_refs(prev_rule, is_prev=True)
        refs_to_remove = {}
        for ref_type in RefType:
            refs_to_remove[ref_type] = []
            for prev_ref_uid in prev_ref_uids[ref_type]:
                if prev_ref_uid in ref_uids[ref_type]:
                    prev_ref_obj, ref_obj = self.get_ref_objs(ref_type, prev_ref_uid, prev_config)
                    if prev_ref_obj == ref_obj:
                        continue # ref not removed or changed
                # ref removed or changed
                refs_to_remove[ref_type].append(self.get_ref_remove_statement(ref_type, prev_rule.rule_uid, prev_ref_uid))
        return refs_to_remove

    def remove_outdated_refs(self, prev_config: FwConfigNormalized):
        all_refs_to_remove = {ref_type: [] for ref_type in RefType}
        for prev_rulebase in prev_config.rulebases:
            rules = next((rb.Rules for rb in self.normalized_config.rulebases if rb.uid == prev_rulebase.uid), {})
            for prev_rule in prev_rulebase.Rules.values():
                uid = prev_rule.rule_uid
                rule_removed_or_changed = uid not in rules or prev_rule != rules[uid] # rule removed or changed -> all refs need to be removed
                rule_refs_to_remove = self.get_outdated_refs_to_remove(prev_rule, rules.get(uid, None), prev_config, rule_removed_or_changed)
                for ref_type, ref_statements in rule_refs_to_remove.items():
                    all_refs_to_remove[ref_type].extend(ref_statements)

        if not any(all_refs_to_remove.values()):
            return 0
        
        import_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "rule/updateRuleRefs.graphql"])
        
        query_variables = {
            'importId': self.import_details.ImportId,
            'ruleFroms': all_refs_to_remove[RefType.SRC],
            'ruleTos': all_refs_to_remove[RefType.DST],
            'ruleServices': all_refs_to_remove[RefType.SVC],
            'ruleNwObjResolveds': all_refs_to_remove[RefType.NWOBJ_RESOLVED],
            'ruleSvcResolveds': all_refs_to_remove[RefType.SVC_RESOLVED],
            'ruleUserResolveds': all_refs_to_remove[RefType.USER_RESOLVED]
        }

        try:
            import_result = self.import_details.api_call.call(import_mutation, query_variables=query_variables, analyze_payload=True)
        except Exception:
            raise FwoApiWriteError(f"failed to remove outdated rule references: {str(traceback.format_exc())}")
        if 'errors' in import_result:
            raise FwoApiWriteError(f"failed to remove outdated rule references: {str(import_result['errors'])}")
        else:
            return sum((import_result['data'][f"update_{ref_type.value}"].get('affected_rows', 0) for ref_type in RefType))

    def get_ref_add_statement(self, ref_type, rule, ref_uid):
        if ref_type == RefType.SRC:
            nwobj_uid, user_uid = ref_uid
            obj_id = self.uid2id_mapper.get_network_object_id(nwobj_uid)
            if obj_id is None:
                self.import_details.Stats.addError(f"Network object {nwobj_uid} not found for rule {rule.rule_uid}")
                raise FwoImporterError(f"Network object {nwobj_uid} not found for rule {rule.rule_uid}")
            new_ref_dict = RuleFrom(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                obj_id=self.uid2id_mapper.get_network_object_id(nwobj_uid),
                user_id=self.uid2id_mapper.get_user_id(user_uid) if user_uid else None,
                rf_create=self.import_details.ImportId,
                rf_last_seen=self.import_details.ImportId, #TODO: to be removed in the future
                negated=rule.rule_src_neg
            ).dict()
            return new_ref_dict
        elif ref_type == RefType.DST:
            nwobj_uid, user_uid = ref_uid
            new_ref_dict = RuleTo(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                obj_id=self.uid2id_mapper.get_network_object_id(nwobj_uid),
                user_id=self.uid2id_mapper.get_user_id(user_uid) if user_uid else None,
                rt_create=self.import_details.ImportId,
                rt_last_seen=self.import_details.ImportId, #TODO: to be removed in the future
                negated=rule.rule_dst_neg
            ).dict()
            return new_ref_dict
        elif ref_type == RefType.SVC:
            new_ref_dict = RuleService(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                svc_id=self.uid2id_mapper.get_service_object_id(ref_uid),
                rs_create=self.import_details.ImportId,
                rs_last_seen=self.import_details.ImportId, #TODO: to be removed in the future
            ).dict()
            return new_ref_dict
        elif ref_type == RefType.NWOBJ_RESOLVED:
            return {
                "mgm_id": self.import_details.MgmDetails.CurrentMgmId,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "obj_id": self.uid2id_mapper.get_network_object_id(ref_uid),
                "created": self.import_details.ImportId,
            }
        elif ref_type == RefType.SVC_RESOLVED:
            return {
                "mgm_id": self.import_details.MgmDetails.CurrentMgmId,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "svc_id": self.uid2id_mapper.get_service_object_id(ref_uid),
                "created": self.import_details.ImportId,
            }
        elif ref_type == RefType.USER_RESOLVED:
            return {
                "mgm_id": self.import_details.MgmDetails.CurrentMgmId,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "user_id": self.uid2id_mapper.get_user_id(ref_uid),
                "created": self.import_details.ImportId,
            }


    def get_new_refs_to_add(self, rule, prev_rule, prev_config, add_all):
        """
        Get the references that need to be added for a rule based on comparison with the previous rule.
        Args:
            rule (RuleNormalized): The current version of the rule.
            prev_rule (RuleNormalized): The previous version of the rule.
            prev_config (FwConfigNormalized): The previous configuration containing the rules.
            add_all (bool): If True, all references will be added. If False, it will check for changes in references that need to be added.
        """
        prev_ref_uids = { ref_type: [] for ref_type in RefType }
        if not add_all:
            prev_ref_uids = self.get_rule_refs(prev_rule, is_prev=True)
        ref_uids = self.get_rule_refs(rule)
        refs_to_add = {}
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
        all_refs_to_add = {ref_type: [] for ref_type in RefType}
        for rulebase in self.normalized_config.rulebases:
            prev_rules = next((rb.Rules for rb in prev_config.rulebases if rb.uid == rulebase.uid), {})
            for rule in rulebase.Rules.values():
                uid = rule.rule_uid
                rule_added_or_changed = uid not in prev_rules or rule != prev_rules[uid] # rule added or changed -> all refs need to be added
                rule_refs_to_add = self.get_new_refs_to_add(rule, prev_rules.get(uid, None), prev_config, rule_added_or_changed)
                for ref_type, ref_statements in rule_refs_to_add.items():
                    all_refs_to_add[ref_type].extend(ref_statements)

        if not any(all_refs_to_add.values()):
            return 0
        
        import_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "rule/insertRuleRefs.graphql"])
        query_variables = {
            'ruleFroms': all_refs_to_add[RefType.SRC],
            'ruleTos': all_refs_to_add[RefType.DST],
            'ruleServices': all_refs_to_add[RefType.SVC],
            'ruleNwObjResolveds': all_refs_to_add[RefType.NWOBJ_RESOLVED],
            'ruleSvcResolveds': all_refs_to_add[RefType.SVC_RESOLVED],
            'ruleUserResolveds': all_refs_to_add[RefType.USER_RESOLVED]
        }

        try:
            import_result = self.import_details.api_call.call(import_mutation, query_variables=query_variables)
        except Exception:
            raise FwoApiWriteError(f"failed to add new rule references: {str(traceback.format_exc())}")
        if 'errors' in import_result:
            raise FwoApiWriteError(f"failed to add new rule references: {str(import_result['errors'])}")
        else:
            return sum((import_result['data'][f"insert_{ref_type.value}"].get('affected_rows', 0) for ref_type in RefType))


    def getRulesByIdWithRefUids(self, ruleIds: list[int]) -> tuple[int, int, list[Rule]]:
        logger = getFwoLogger()
        rulesToBeReferenced = []
        getRuleUidRefsQuery = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "rule/getRulesByIdWithRefUids.graphql"])
        query_variables = { 'ruleIds': ruleIds }
        
        try:
            import_result = self.import_details.api_call.call(getRuleUidRefsQuery, query_variables=query_variables)
            if 'errors' in import_result:
                logger.exception(f"fwconfig_import_rule:getRulesByIdWithRefUids - error in addNewRules: {str(import_result['errors'])}")
                return 1, 0, rulesToBeReferenced
            else:
                return 0, 0, import_result['data']['rule']
        except Exception:
            logger.exception(f"failed to get rules from API: {str(traceback.format_exc())}")
            raise


    def getRules(self, ruleUids) -> list[Rulebase]:
        rulebases = []
        for rb in self.normalized_config.rulebases:
            if rb.uid in ruleUids:
                filtered_rules = {uid: rule for uid, rule in rb.Rules.items() if uid in ruleUids[rb.uid]}
                rulebase = Rulebase(
                    name=rb.name,
                    uid=rb.uid,
                    mgm_uid=rb.mgm_uid,
                    is_global=rb.is_global,
                    Rules=filtered_rules
                )
                rulebases.append(rulebase)
        return rulebases


    # assuming input of form:
    # {'rule-uid1': {'rule_num': 17', ... }, 'rule-uid2': {'rule_num': 8, ...}, ... }
    @staticmethod
    def ruleDictToOrderedListOfRuleUids(rules):
        return sorted(rules, key=lambda x: rules[x]['rule_num'])

    @staticmethod
    def listDiff(oldRules, newRules):
        diff = list(ndiff(oldRules, newRules))
        changes = []

        for change in diff:
            if change.startswith("- "):
                changes.append(('delete', change[2:]))
            elif change.startswith("+ "):
                changes.append(('insert', change[2:]))
            elif change.startswith("  "):
                changes.append(('unchanged', change[2:]))
        
        return changes

    def _find_following_rules(self, ruleUid, previousRulebase, rulebaseId):
        """
        Helper method to find the next rule in self that has an existing rule number.
        
        :param ruleUid: The ID of the current rule being processed.
        :param previousRulebase: Dictionary of existing rule IDs and their rule_number values.
        :return: Generator yielding rule IDs that appear after `current_rule_id` in self.new_rules.
        """
        found = False
        currentRulebase = self.normalized_config.getRulebase(rulebaseId)
        for currentUid in currentRulebase.Rules:
            if currentUid == ruleUid:
                found = True
            elif found and ruleUid in previousRulebase:
                yield currentUid


    # adds new rule_metadatum to the database
    def addNewRuleMetadata(self, newRules: list[Rulebase]):
        logger = getFwoLogger()
        changes = 0
        newRuleMetaDataIds = []
        newRuleIds = []
        
        addNewRuleMetadataMutation = """mutation upsertRuleMetadata($ruleMetadata: [rule_metadata_insert_input!]!) {
             insert_rule_metadata(objects: $ruleMetadata, on_conflict: {constraint: rule_metadata_rule_uid_unique, update_columns: [rule_last_modified]}) {
                affected_rows
                returning {
                    rule_metadata_id
                }
            }
        }
        """

        addNewRuleMetadata: list[dict] = self.PrepareNewRuleMetadata(newRules)
        query_variables = { 'ruleMetadata': addNewRuleMetadata }
        
        if fwo_globals.debug_level>9:
            logger.debug(json.dumps(query_variables))    # just for debugging purposes

        try:
            import_result = self.import_details.api_call.call(addNewRuleMetadataMutation, query_variables=query_variables, debug_level=self.import_details.DebugLevel, analyze_payload=True)
        except Exception:
            raise FwoApiWriteError(f"failed to write new RulesMetadata: {str(traceback.format_exc())}")
        if 'errors' in import_result:
            raise FwoApiWriteError(f"failed to write new RulesMetadata: {str(import_result['errors'])}")
        else:
            # reduce change number by number of rulebases
            changes = import_result['data']['insert_rule_metadata']['affected_rows']
            if changes>0:
                for rule_metadata_id in import_result['data']['insert_rule_metadata']['returning']:
                    newRuleMetaDataIds.append(rule_metadata_id)
        
        return changes, newRuleIds


    # collect new last hit information
    @staticmethod
    def append_rule_metadata_last_hit (new_hit_information: list[dict], rule: RuleNormalized, mgm_id: int):
        if new_hit_information is None:
            new_hit_information = []        
        new_hit_information.append({ 
            "where": { "rule_uid": { "_eq": rule.rule_uid } },
            "_set": { "rule_last_hit": rule.last_hit }
        })


    # adds new rule_metadatum to the database
    def update_rule_metadata_last_hit (self, new_hit_information: list[dict]):
        logger = getFwoLogger()
        errors = 0
        changes = 0

        if len(new_hit_information) > 0:
            update_last_hit_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "rule_metadata/updateLastHits.graphql"])
            query_variables = { 'hit_info': new_hit_information  }
            
            try:
                import_result = self.import_details.api_call.call(update_last_hit_mutation, query_variables=query_variables, debug_level=self.import_details.DebugLevel, analyze_payload=True)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addNewRuleMetadata: {str(import_result['errors'])}")
                    return 1, 0
                    # do not count last hit changes as changes here
            except Exception:
                errors = 1
                raise FwoApiWriteError(f"failed to update RuleMetadata last hit info: {str(traceback.format_exc())}")
        
        return errors, changes


    def addRulebasesWithoutRules(self, newRules: list[Rulebase]):
        logger = getFwoLogger()
        changes = 0
        newRulebaseIds = []
        newRuleIds = []
        
        addRulebasesWithoutRulesMutation = """mutation upsertRulebaseWithoutRules($rulebases: [rulebase_insert_input!]!) {
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

        newRulebasesForImport: list[RulebaseForImport] = self.PrepareNewRulebases(newRules, includeRules=False)
        query_variables = { 'rulebases': newRulebasesForImport }
        
        try:
            import_result = self.import_details.api_call.call(addRulebasesWithoutRulesMutation, query_variables=query_variables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importRules - error in addNewRules: {str(import_result['errors'])}")
                return 0, newRulebaseIds
            else:
                # reduce change number by number of rulebases
                changes = import_result['data']['insert_rulebase']['affected_rows']
                if changes>0:
                    for rulebase in import_result['data']['insert_rulebase']['returning']:
                        newRulebaseIds.append(rulebase['id'])
                # finally, add the new rulebases to the map for next step (adding rulebase with rules)
                self.import_details.SetRulebaseMap(self.import_details.api_call) 
                return changes, newRulebaseIds
        except Exception:
            raise FwoApiWriteError(f"failed to write new rulebases: {str(traceback.format_exc())}")
        
    # as we cannot add the rules for all rulebases in one go (using a constraint from the rule table), 
    # we need to add them per rulebase separately
    def addRulesWithinRulebases(self, newRules: list[Rulebase]):
        logger = getFwoLogger()
        changes = 0
        newRuleIds = []
        # TODO: need to update the RulebaseMap here?!

        newRulesForImport: list[RulebaseForImport] = self.PrepareNewRulebases(newRules)
        upsertRulebaseWithRules = """mutation upsertRules($rules: [rule_insert_input!]!) {
                insert_rule(
                    objects: $rules,
                    on_conflict: { constraint: rule_unique_mgm_id_rule_uid_rule_create_xlate_rule, update_columns: [] }
                ) { affected_rows,  returning { rule_id, rule_uid } }
            }
        """
        for rulebase in newRulesForImport:
            if 'rules' in rulebase and 'data' in rulebase['rules'] and len(rulebase['rules']['data'])>0:
                query_variables = { 'rules': rulebase['rules']['data'] }
                try:
                    import_result = self.import_details.api_call.call(upsertRulebaseWithRules, query_variables=query_variables, debug_level=self.import_details.DebugLevel, analyze_payload=True)
                except Exception:
                    raise FwoApiWriteError(f"failed to write new rulebases: {str(traceback.format_exc())}")
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:addRulesWithinRulebases - error in addRulesWithinRulebases: {str(import_result['errors'])}")
                    raise FwoApiWriteError(f"failed to write new rulebases: {str(import_result['errors'])}")
                else:
                    # reduce change number by number of rulebases
                    changesForThisRulebase = import_result['data']['insert_rule']['affected_rows']
                    newRuleIds += import_result['data']['insert_rule']['returning']
                    if changesForThisRulebase>0:
                        changes += changesForThisRulebase
        return changes, newRuleIds

    # adds only new rules to the database
    # unchanged or deleted rules are not touched here
    def addNewRules(self, newRulebases: list[Rulebase]):
        changes1, newRulebaseIds = self.addRulebasesWithoutRules(newRulebases)
        changes2, newRuleIds = self.addRulesWithinRulebases(newRulebases)
       
        return changes1+changes2, newRuleIds


    def PrepareNewRuleMetadata(self, newRules: list[Rulebase]) -> list[dict]:
        newRuleMetadata: list[dict] = []

        now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        for rulebase in newRules:
            for rule_uid, rule in rulebase.Rules.items():
                rm4import = RuleMetadatum(
                    rule_uid=rule_uid,
                    rule_last_modified=now,
                    rule_created=now,
                    rule_last_hit=rule.last_hit,
                )
                newRuleMetadata.append(rm4import.model_dump())
        # TODO: add other fields
        return newRuleMetadata    

    # creates a structure of rulebases optinally including rules for import
    def PrepareNewRulebases(self, newRules: list[Rulebase], includeRules: bool = True) -> list[RulebaseForImport]:
        newRulesForImport: list[RulebaseForImport] = []

        for rulebase in newRules:
            rules = {"data": []}
            if includeRules:
                rules = self.prepare_rules_for_import(self.import_details, list(rulebase.Rules.values()), rulebase.uid)
            rb4import = RulebaseForImport(
                name=rulebase.name,
                mgm_id=self.import_details.MgmDetails.CurrentMgmId,
                uid=rulebase.uid,
                is_global=self.import_details.MgmDetails.CurrentMgmIsSuperManager,
                created=self.import_details.ImportId,
                rules=rules
            )
            newRulesForImport.append(rb4import.dict())
        # TODO: see where to get real UIDs (both for rulebase and manager)
        # add rules for each rulebase
        return newRulesForImport    

    def markRulesRemoved(self, removedRuleUids, changedRuleUids):
        logger = getFwoLogger()
        changes = 0
        collectedRemovedRuleIds = []

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
                query_variables = {  'importId': self.import_details.ImportId,
                                    'mgmId': self.import_details.MgmDetails.CurrentMgmId,
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


    def create_new_rule_version(self, rule_uids):
        logger = getFwoLogger()
        self._changed_rule_id_map = {}
        rule_order_service = RuleOrderService()

        if len(rule_uids) == 0:
            return 0, [], []
        
        createNewRuleVersions = """mutation createNewRuleVersions($objects: [rule_insert_input!]!, $uids: [String!], $mgmId: Int!, $importId: bigint) {
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

        import_rules = []

        for rulebase_uid in list(rule_uids.keys()):
                import_rules.extend(self.prepare_rules_for_import(self.import_details, [rule_with_changes for rule_with_changes in rule_order_service.target_rules_flat if rule_with_changes.rule_uid in rule_uids[rulebase_uid]], rulebase_uid)["data"])

        create_new_rule_version_variables = {
            "objects": import_rules,
            "uids": [rule["rule_uid"] for rule in import_rules],
            "mgmId": self.import_details.MgmDetails.CurrentMgmId,
            "importId": self.import_details.ImportId
        }
        
        try:
            create_new_rule_version_result = self.import_details.api_call.call(createNewRuleVersions, query_variables=create_new_rule_version_variables)
        except Exception:
            raise FwoApiWriteError(f"failed to move rules: {str(traceback.format_exc())}")
        if 'errors' in create_new_rule_version_result:
            raise FwoApiWriteError(f"failed to create new rule versions: {str(create_new_rule_version_result['errors'])}")
        else:
            changes = int(create_new_rule_version_result['data']['update_rule']['affected_rows'])
            update_rules_return = create_new_rule_version_result['data']['update_rule']['returning']
            insert_rules_return = create_new_rule_version_result['data']['insert_rule']['returning']

            self._changed_rule_id_map = {
                update_item['rule_id']: next(
                    insert_item['rule_id']
                    for insert_item in insert_rules_return
                    if insert_item['rule_uid'] == update_item['rule_uid']
                )
                for update_item in update_rules_return
            }


            collected_changed_rule_ids = list(self._changed_rule_id_map.keys())


        return changes, collected_changed_rule_ids, insert_rules_return


    def update_rule_enforced_on_gateway_after_move(self, insert_rules_return, update_rules_return):
        """
            Updates the db table rule_enforced_on_gateway by creating new entries for a list of rule_ids and setting the old versions of said rules removed.
        """

        logger = getFwoLogger()

        id_map = {}

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

        set_rule_enforced_on_gateway_entries_removed_variables = {
            "rule_ids": list(id_map.values()),
            "importId": self.import_details.ImportId,
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
            set_rule_enforced_on_gateway_entries_removed_result =  self.import_details.api_call.call(set_rule_enforced_on_gateway_entries_removed_mutation, set_rule_enforced_on_gateway_entries_removed_variables, self.import_details.DebugLevel)

            if 'errors' in set_rule_enforced_on_gateway_entries_removed_result:
                logger.exception(f"fwo_api:moveRules - error while updating moved rules refs: {str(set_rule_enforced_on_gateway_entries_removed_result['errors'])}")
                return 1, 0, []
            
            insert_rule_enforced_on_gateway_entries_variables = {
                "new_entries": [
                    {
                        "rule_id": new_id,
                        "dev_id": next(entry for entry in  set_rule_enforced_on_gateway_entries_removed_result["data"]["update_rule_enforced_on_gateway"]["returning"] if entry["rule_id"] == id_map[new_id])["dev_id"],
                        "created": self.import_details.ImportId,
                    }
                    for new_id in id_map.keys()
                ]
            }

            insert_rule_enforced_on_gateway_entries_result =  self.import_details.api_call.call(insert_rule_enforced_on_gateway_entries_mutation, insert_rule_enforced_on_gateway_entries_variables, self.import_details.DebugLevel)

            if 'errors' in insert_rule_enforced_on_gateway_entries_result:
                logger.exception(f"fwo_api:moveRules - error while updating moved rules refs: {str(insert_rule_enforced_on_gateway_entries_result['errors'])}")
                return 1, 0, []
            
            return 0, 0, []


        except Exception:
            logger.exception(f"failed to move rules: {str(traceback.format_exc())}")
            return 1, 0, []
        
    def verify_rules_moved(self, changed_rule_uids):
        rule_order_service = RuleOrderService()
        error_count_move = 0 
        number_of_moved_rules = 0

        moved_rule_uids = []

        changed_rule_uids_flat = [
            uid 
            for uids in changed_rule_uids.values() 
            for uid in uids
        ]

        rule_order_service_moved_rule_uids_flat = [
            rule_uid 
            for rule_uids in rule_order_service._moved_rule_uids.values()
            for rule_uid in rule_uids
        ]

        for rule_uid in rule_order_service_moved_rule_uids_flat:
            if rule_uid in changed_rule_uids_flat:
                moved_rule_uids.append(rule_uid)
                number_of_moved_rules += 1
            else:
                error_count_move += 1

        return error_count_move, number_of_moved_rules, moved_rule_uids
            
            

    # TODO: limit query to a single rulebase
    def GetRuleNumMap(self):
        query = "query getRuleNumMap($mgmId: Int) { rule(where:{mgm_id:{_eq:$mgmId}}) { rule_uid rulebase_id rule_num_numeric } }"
        try:
            result = self.import_details.api_call.call(query=query, query_variables={"mgmId": self.import_details.MgmDetails.CurrentMgmId})
        except Exception:
            logger = getFwoLogger()
            logger.error(f'Error while getting rule number map')
            return {}
        
        map = {}
        for ruleNum in result['data']['rule']:
            if ruleNum['rulebase_id'] not in map:
                map.update({ ruleNum['rulebase_id']: {} })  # initialize rulebase
            map[ruleNum['rulebase_id']].update({ ruleNum['rule_uid']: ruleNum['rule_num_numeric']})
        return map

    def GetNextRuleNumMap(self):    # TODO: implement!
        query = "query getRuleNumMap { rule { rule_uid rule_num_numeric } }"
        try:
            result = self.import_details.api_call.call(query=query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error(f'Error while getting rule number')
            return {}
        
        map = {}
        # for ruleNum in result['data']['rule']:
        #     map.update({ruleNum['rule_uid']: ruleNum['rule_num_numeric']})
        return map

    def GetRuleTypeMap(self):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = self.import_details.api_call.call(query=query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for track in result['data']['stm_track']:
            map.update({track['track_name']: track['track_id']})
        return map

    def getCurrentRules(self, importId, mgmId, rulebaseName):
        query_variables = {
            "importId": importId,
            "mgmId": mgmId,
            "rulebaseName": rulebaseName
        }
        query = """
            query getRulebase($importId: bigint!, $mgmId: Int!, $rulebaseName: String!) {
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
            queryResult = self.import_details.api_call.call(query, query_variables=query_variables)
        except Exception:
            logger = getFwoLogger()
            logger.error(f"error while getting current rulebase: {str(traceback.format_exc())}")
            self.import_details.increaseErrorCounterByOne()
            return
        
        try:
            ruleList = queryResult['data']['rulebase'][0]['rules']
        except Exception:
            logger = getFwoLogger()
            logger.error(f'could not find rules in query result: {queryResult}')
            self.import_details.increaseErrorCounterByOne()
            return
        
        rules = []
        for rule in ruleList:
            rules.append([rule['rule']['rule_num'], rule['rule']['rule_num_numeric'], rule['rule']['rule_uid']])
        return rules
    
    def insertRulebase(self, ruleBaseName, isGlobal=False):
        # call for each rulebase to add
        query_variables = {
            "rulebase": {
                "is_global": isGlobal,
                "mgm_id": self.import_details.MgmDetails.CurrentMgmId,
                "name": ruleBaseName,
                "created": self.import_details.ImportId
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


    def importInsertRulebaseOnGateway(self, rulebaseId, devId, orderNo=0):
        query_variables = {
            "rulebase2gateway": [
                {
                    "dev_id": devId,
                    "rulebase_id": rulebaseId,
                    "order_no": orderNo
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


    def prepare_rules_for_import(self, importDetails: ImportStateController, rules: list[RuleNormalized], rulebase_uid: str) -> dict[str, list[Rule]]:
        prepared_rules = []

        # get rulebase_id for rulebaseUid
        rulebase_id = importDetails.lookupRulebaseId(rulebase_uid)

        for rule in rules:
            listOfEnforcedGwIds = []
            for gwUid in rule.rule_installon.split(fwo_const.list_delimiter):
                gwId = importDetails.lookupGatewayId(gwUid)
                if gwId is not None:
                    listOfEnforcedGwIds.append(gwId)
            if len(listOfEnforcedGwIds) == 0:
                listOfEnforcedGwIds = None

            rule_for_import = Rule(
                mgm_id=importDetails.MgmDetails.CurrentMgmId,
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
                rule_from_zone=self.uid2id_mapper.get_zone_object_id(rule.rule_src_zone) if rule.rule_src_zone is not None else None,
                rule_to_zone=self.uid2id_mapper.get_zone_object_id(rule.rule_dst_zone) if rule.rule_dst_zone is not None else None,
                access_rule=True,
                nat_rule=False,
                is_global=False,
                rulebase_id=rulebase_id,
                rule_create=importDetails.ImportId,
                rule_last_seen=importDetails.ImportId,
                rule_num_numeric=rule.rule_num_numeric,
                action_id = importDetails.lookupAction(rule.rule_action),
                track_id = importDetails.lookupTrack(rule.rule_track),
                rule_head_text=rule.rule_head_text,
                rule_installon=rule.rule_installon,
                last_change_admin=None #TODO: get id from rule.last_change_admin
            ).model_dump()

            if listOfEnforcedGwIds is not None and len(listOfEnforcedGwIds) > 0:    # leave out field, if no resolvable gateways are found
                rule_for_import.update({'rule_installon': rule.rule_installon }) #fwo_const.list_delimiter.join(listOfEnforcedGwIds) })

            prepared_rules.append(rule_for_import)
        return { "data": prepared_rules }
    

    def write_changelog_rules(self, added_rules_ids, removed_rules_ids):
        logger = getFwoLogger()
        errors = 0

        changelog_rule_insert_objects = self.prepare_changelog_rules_insert_objects(added_rules_ids, removed_rules_ids)

        updateChanglogRules = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "rule/updateChanglogRules.graphql"])

        query_variables = {
            'rule_changes': changelog_rule_insert_objects
        }

        if len(changelog_rule_insert_objects) > 0:
            try:
                updateChanglogRules_result = self.import_details.api_call.call(updateChanglogRules, query_variables=query_variables, analyze_payload=True)
                if 'errors' in updateChanglogRules_result:
                    logger.exception(f"error while adding changelog entries for objects: {str(updateChanglogRules_result['errors'])}")
                    errors = 1
            except Exception:
                logger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")
                errors = 1
        
        return errors


    def prepare_changelog_rules_insert_objects(self, added_rules_ids, removed_rules_ids):
        """
            Creates two lists of insert arguments for the changelog_rules db table, one for new rules, one for deleted.
        """

        change_logger = ChangeLogger()
        changelog_rule_insert_objects = []
        importTime = datetime.now().isoformat()
        changeTyp = 3

        if self.import_details.IsFullImport or self.import_details.IsClearingImport:
            changeTyp = 2   # TODO: Somehow all imports are treated as im operation.

        for rule_id in added_rules_ids:
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.import_details, 'I', changeTyp, importTime, rule_id))

        for rule_id in removed_rules_ids:
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.import_details, 'D', changeTyp, importTime, rule_id))

        for old_rule_id, new_rule_id in self._changed_rule_id_map.items():
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.import_details, 'C', changeTyp, importTime, new_rule_id, old_rule_id))

        return changelog_rule_insert_objects


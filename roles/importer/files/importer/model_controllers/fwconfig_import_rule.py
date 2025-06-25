import traceback
from difflib import ndiff
import json

import fwo_const
import fwo_api
import fwo_exceptions
from models.rule import Rule
from models.rule_metadatum import RuleMetadatum
from models.rulebase import Rulebase, RulebaseForImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from fwo_log import ChangeLogger, getFwoLogger
from typing import Dict, List
from datetime import datetime
from models.rule_from import RuleFrom
from models.rule_to import RuleTo
from models.rule_service import RuleService
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from models.rule import RuleNormalized
from importer.services.enums import Services
from importer.services.uid2id_mapper import Uid2IdMapper
from importer.services.service_provider import ServiceProvider


# this class is used for importing rules and rule refs into the FWO API
class FwConfigImportRule():

    _changed_rule_id_map: dict
    uid2id_mapper: Uid2IdMapper

    def __init__(self):
        self._changed_rule_id_map = {}

        service_provider = ServiceProvider()
        self.global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self.ImportDetails = self.global_state.import_state
        self.NormalizedConfig = self.global_state.normalized_config
        self.uid2id_mapper = service_provider.get_service(Services.UID2ID_MAPPER, self.ImportDetails.ImportId)
      

    # #   self.ActionMap = self.GetActionMap()
    # #   self.TrackMap = self.GetTrackMap()
    #   self.RuleNumLookup = self.GetRuleNumMap()             # TODO: needs to be updated with each insert
    #   self.NextRuleNumLookup = self.GetNextRuleNumMap()     # TODO: needs to be updated with each insert
    #   # self.RulebaseMap = self.GetRulebaseMap()     # limited to the current mgm_id

    def updateRulebaseDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
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
        for rulebase in self.NormalizedConfig.rulebases:
            currentRulebaseUids.append(rulebase.uid)

        for rulebaseId in previousRulebaseUids:
            currentRulebase = self.NormalizedConfig.getRulebase(rulebaseId)
            if rulebaseId in currentRulebaseUids:
                # deal with policies contained both in this and previous config
                previousRulebase = prevConfig.getRulebase(rulebaseId)

                ruleUidsInBoth.update({ rulebaseId: list(currentRulebase.Rules.keys() & previousRulebase.Rules.keys()) })
            else:
                logger.info(f"previous rulebase has been deleted: {currentRulebase.name} (id:{rulebaseId})")

        # find changed rules
        for rulebaseId in ruleUidsInBoth:
            changedRuleUids.update({ rulebaseId: [] })
            currentRulebase = self.NormalizedConfig.getRulebase(rulebaseId) # [pol for pol in self.NormalizedConfig.rulebases if pol.Uid == rulebaseId]
            previousRulebase = prevConfig.getRulebase(rulebaseId)
            for ruleUid in ruleUidsInBoth[rulebaseId]:
                self.preserve_rule_num_numeric(currentRulebase, previousRulebase, ruleUid)
                self.collect_changed_rules(ruleUid, currentRulebase, previousRulebase, rulebaseId, changedRuleUids)
                self.collect_last_hit_changes(ruleUid, currentRulebase, previousRulebase, new_hit_information)

        # add full rule details first
        newRulebases = self.getRules(newRuleUids)

        # update rule_metadata before adding rules
        errorCountAdd, numberOfAddedMetaRules, newRuleMetadataIds = self.addNewRuleMetadata(newRulebases)
        _, _ = self.update_rule_metadata_last_hit(new_hit_information)

        # # now update the database with all rule diffs
        errorCountAdd, numberOfAddedRules, newRuleIds = self.addNewRules(newRulebases)

        # # try to add the rule ids to the existing rulebase objects
        # # self.updateRuleIds(newRulebases, newRuleIds)

        # # get new rules details from API (for obj refs as well as enforcing gateways)
        # errors, changes, newRules = self.getRulesByIdWithRefUids(newRuleIds)

        # self.addNewRule2ObjRefs(newRules)
        # # TODO: self.addNewRuleSvcRefs(newRulebases, newRuleIds)

        # enforcingController = RuleEnforcedOnGatewayController(self.ImportDetails)
        # ids = enforcingController.addNewRuleEnforcedOnGatewayRefs(newRules, self.ImportDetails)

        errorCountDel, numberOfDeletedRules, removedRuleIds = self.markRulesRemoved(deletedRuleUids)

        error_count_change, number_of_changed_rules, changed_rule_uids = self.create_new_rule_version(changedRuleUids)

        error_count_move, number_of_moved_rules, moved_rule_uids = self.verify_rules_moved(changed_rule_uids)

        self.write_changelog_rules(newRuleIds, removedRuleIds)

        self.ImportDetails.Stats.RuleAddCount += numberOfAddedRules
        self.ImportDetails.Stats.RuleDeleteCount += numberOfDeletedRules
        self.ImportDetails.Stats.RuleMoveCount += number_of_moved_rules
        self.ImportDetails.Stats.RuleChangeCount += number_of_changed_rules

        # TODO: rule_nwobj_resolved fuellen (recert?)
        return newRuleIds


    def collect_last_hit_changes(self, rule_uid, current_rulebase, previous_rulebase, new_hit_information):
        if self.last_hit_changed(current_rulebase.Rules[rule_uid], previous_rulebase.Rules[rule_uid]):
            self.append_rule_metadata_last_hit(new_hit_information, current_rulebase.Rules[rule_uid], self.ImportDetails.MgmDetails.Id)


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


    def addNewRule2ObjRefs(self, newRules):
        # for each new rule: add refs in rule_to and rule_from
        # assuming all nwobjs are already in the database

        # TODO: need to make sure that the references do not already exist!

        ruleRefs = {}
        # now add the references to the rules
        for rule in newRules:
            ruleFromRefs = []
            ruleToRefs = []
            ruleSvcRefs = []
            ruleFromUserRefs = []
            ruleToUserRefs = []

            for srcRef in rule['rule_src_refs'].split(fwo_const.list_delimiter):
                if fwo_const.user_delimiter in srcRef:
                    userRef, nwRef = srcRef.split(fwo_const.user_delimiter)
                    ruleFromUserRefs.append(self.uid2id_mapper.get_user_id(userRef))
                    srcRef = nwRef
                ruleFromRefs.append(self.uid2id_mapper.get_network_object_id(srcRef))
            for dstRef in rule['rule_dst_refs'].split(fwo_const.list_delimiter):
                if fwo_const.user_delimiter in dstRef:
                    userRef, nwRef = dstRef.split(fwo_const.user_delimiter)
                    ruleToUserRefs.append(self.uid2id_mapper.get_user_id(userRef))
                    dstRef = nwRef
                ruleToRefs.append(self.uid2id_mapper.get_network_object_id(dstRef))
            for svcRef in rule['rule_svc_refs'].split(fwo_const.list_delimiter):
                ruleSvcRefs.append(self.uid2id_mapper.get_service_object_id(svcRef))
            ruleRefs.update({ rule['rule_id']: { 
                'from': ruleFromRefs, 
                'to': ruleToRefs, 
                'svc': ruleSvcRefs, 
                'from_negated': rule['rule_src_neg'], 
                'to_negated': rule['rule_src_neg'],
                'svc_negated': rule['rule_svc_neg']
                } })

        # TODO: we need to also add info on negation and user references!
        self.addRuleNwObjRefs(ruleRefs)


    def addRuleNwObjRefs(self, ruleRefs):
        """
        Adds network object references for firewall rules.

        This method processes source, destination and service objects and executes a mutation 
        to insert the data into the API.

        Args:
            ruleRefs (dict): A dictionary containing rule IDs and associated network objects.

        Returns:
            tuple: (errors, changes), where errors is 1 if an error occurred, otherwise 0, 
                   and changes is 1 if modifications were made, otherwise 0.
        """

        logger = getFwoLogger()
        errors = 0
        changes = 0
        ruleFroms = []
        ruleTos = []
        ruleSvcs = []

        # loop over all ruleRefs items
        for ruleId, ruleData in ruleRefs.items():
            negatedFrom = ruleData['from_negated']
            negatedTo = ruleData['to_negated']
            negatedSvc = ruleData['svc_negated']

            # append rule froms
            for srcObjId in ruleData['from']:
                            ruleFroms.append(RuleFrom(
                                rule_id=ruleId, 
                                obj_id=srcObjId,
                                user_id=None,   # TODO: implement getting user information
                                rf_create=self.ImportDetails.ImportId, 
                                rf_last_seen=self.ImportDetails.ImportId,
                                negated=negatedFrom
                            ).dict())

            # append rule tos            
            for dstObjId in ruleData['to']:
                ruleTos.append(RuleTo(
                    rule_id=ruleId, 
                    obj_id=dstObjId,
                    user_id=None,   # TODO: implement getting user information
                    rt_create=self.ImportDetails.ImportId, 
                    rt_last_seen=self.ImportDetails.ImportId,
                    negated=negatedTo
                ).dict())

            # append services         
            for svcObjId in ruleData['svc']:
                ruleSvcs.append(RuleService(
                    rule_id=ruleId, 
                    svc_id=svcObjId,
                    user_id=None,   # TODO: implement getting user information
                    rs_create=self.ImportDetails.ImportId, 
                    rs_last_seen=self.ImportDetails.ImportId,
                    negated=negatedSvc # TODO: Set properly
                ).dict())
        
        # build mutation
        addNewRuleNwObjAndSvcRefsMutation = fwo_api.get_graphql_code([fwo_const.graphqlQueryPath + "rule/insertRuleRefs.graphql"])

        # execute mutation
        try:
            import_result = self.ImportDetails.call(addNewRuleNwObjAndSvcRefsMutation, queryVariables={ 'ruleFroms': ruleFroms, 'ruleTos': ruleTos, 'ruleServices':  ruleSvcs})
            if 'errors' in import_result:
                errors = 1 
                changes = 0
                raise fwo_exceptions.FwoApiWriteError(f"failed to write new rules: {str(traceback.format_exc())}")
                # logger.exception(f"fwo_api:importNwObject - error in addRuleNwObjRefs: {str(import_result['errors'])}")
            else:
                errors = 0
                changes = 1
        except Exception:
            # logger.exception(f"failed to write new rules: {str(traceback.format_exc())}")
            errors = 1 
            changes = 0
            raise fwo_exceptions.FwoApiWriteError(f"failed to write new rules: {str(traceback.format_exc())}")
        
        return errors, changes

    def getRulesByIdWithRefUids(self, ruleIds: List[int]) -> List[Rule]:
        logger = getFwoLogger()
        rulesToBeReferenced = {}
        getRuleUidRefsQuery = fwo_api.get_graphql_code([fwo_const.graphqlQueryPath + "rule/getRulesByIdWithRefUids.graphql"])
        queryVariables = { 'ruleIds': ruleIds }
        
        try:
            import_result = self.ImportDetails.call(getRuleUidRefsQuery, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwconfig_import_rule:getRulesByIdWithRefUids - error in addNewRules: {str(import_result['errors'])}")
                return 1, 0, rulesToBeReferenced
            else:
                return 0, 0, import_result['data']['rule']
        except Exception:
            logger.exception(f"failed to get rules from API: {str(traceback.format_exc())}")


    def getRules(self, ruleUids) -> List[Rulebase]:
        rulebases = []
        for rb in self.NormalizedConfig.rulebases:
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
        # transform rule from dict (key=uid) to list, also adding a data: layer
        for rb in rulebases:
            rb.Rules = list(rb.Rules.values())
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
        currentRulebase = self.NormalizedConfig.getRulebase(rulebaseId)
        for currentUid in currentRulebase.Rules:
            if currentUid == ruleUid:
                found = True
            elif found and ruleUid in previousRulebase:
                yield currentUid


    # adds new rule_metadatum to the database
    def addNewRuleMetadata(self, newRules: List[Rulebase]):
        logger = getFwoLogger()
        errors = 0
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

        addNewRuleMetadata: List[RuleMetadatum] = self.PrepareNewRuleMetadata(newRules)
        queryVariables = { 'ruleMetadata': addNewRuleMetadata }
        
        # queryVarJson = json.dumps(queryVariables)    # just for debugging purposes, remove in prod

        try:
            import_result = self.ImportDetails.call(addNewRuleMetadataMutation, queryVariables=queryVariables, debug_level=self.ImportDetails.DebugLevel, analyze_payload=True)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in addNewRuleMetadata: {str(import_result['errors'])}")
                return 1, 0, newRuleMetaDataIds
            else:
                # reduce change number by number of rulebases
                changes = import_result['data']['insert_rule_metadata']['affected_rows']
                if changes>0:
                    for rule_metadata_id in import_result['data']['insert_rule_metadata']['returning']:
                        newRuleMetaDataIds.append(rule_metadata_id)
        except Exception:
            raise fwo_exceptions.FwoApiWriteError(f"failed to write new RulesMetadata: {str(traceback.format_exc())}")
        
        return errors, changes, newRuleIds


    # collect new last hit information
    @staticmethod
    def append_rule_metadata_last_hit (new_hit_information: List[dict], rule: RuleNormalized, mgm_id: int):
        if new_hit_information is None:
            new_hit_information = []        
        new_hit_information.append({ 
            "where": { "rule_uid": { "_eq": rule.rule_uid } },
            "_set": { "rule_last_hit": rule.last_hit }
        })


    # adds new rule_metadatum to the database
    def update_rule_metadata_last_hit (self, new_hit_information: List[dict]):
        logger = getFwoLogger()
        errors = 0
        changes = 0

        if len(new_hit_information) > 0:
            update_last_hit_mutation = fwo_api.get_graphql_code([fwo_const.graphqlQueryPath + "rule_metadata/updateLastHits.graphql"])
            query_variables = { 'hit_info': new_hit_information  }
            
            try:
                import_result = self.ImportDetails.call(update_last_hit_mutation, queryVariables=query_variables, debug_level=self.ImportDetails.DebugLevel, analyze_payload=True)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addNewRuleMetadata: {str(import_result['errors'])}")
                    return 1, 0
                    # do not count last hit changes as changes here
            except Exception:
                errors = 1
                raise fwo_exceptions.FwoApiWriteError(f"failed to update RuleMetadata last hit info: {str(traceback.format_exc())}")
        
        return errors, changes


    def addRulebasesWithoutRules(self, newRules: List[Rulebase]):
        logger = getFwoLogger()
        errors = 0
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

        newRulebasesForImport: List[RulebaseForImport] = self.PrepareNewRulebases(newRules, includeRules=False)
        queryVariables = { 'rulebases': newRulebasesForImport }
        
        try:
            import_result = self.ImportDetails.call(addRulebasesWithoutRulesMutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importRules - error in addNewRules: {str(import_result['errors'])}")
                return 1, 0, newRulebaseIds
            else:
                # reduce change number by number of rulebases
                changes = import_result['data']['insert_rulebase']['affected_rows']
                if changes>0:
                    for rulebase in import_result['data']['insert_rulebase']['returning']:
                        newRulebaseIds.append(rulebase['id'])
                    # finally, add the new rulebases to the map for next step (adding rulebase with rules)
                    self.ImportDetails.SetRulebaseMap() 
                return 0, changes, newRulebaseIds
        except Exception:
            raise fwo_exceptions.FwoApiWriteError(f"failed to write new rulebases: {str(traceback.format_exc())}")
        
    # as we cannot add the rules for all rulebases in one go (using a constraint from the rule table), 
    # we need to add them per rulebase separately
    def addRulesWithinRulebases(self, newRules: List[Rulebase]):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newRuleIds = []
        # TODO: need to update the RulebaseMap here?!

        newRulesForImport: List[RulebaseForImport] = self.PrepareNewRulebases(newRules)
        upsertRulebaseWithRules = """mutation upsertRules($rules: [rule_insert_input!]!) {
                insert_rule(
                    objects: $rules,
                    on_conflict: { constraint: rule_unique_mgm_id_rule_uid_rule_create_xlate_rule, update_columns: [] }
                ) { affected_rows,  returning { rule_id } }
            }
        """
        for rulebase in newRulesForImport:
            if 'rules' in rulebase and 'data' in rulebase['rules'] and len(rulebase['rules']['data'])>0:
                queryVariables = { 'rules': rulebase['rules']['data'] }
                try:
                    import_result = self.ImportDetails.call(upsertRulebaseWithRules, queryVariables=queryVariables, debug_level=self.ImportDetails.DebugLevel, analyze_payload=True)
                    if 'errors' in import_result:
                        logger.exception(f"fwo_api:addRulesWithinRulebases - error in addRulesWithinRulebases: {str(import_result['errors'])}")
                        errors += 1
                    else:
                        # reduce change number by number of rulebases
                        changesForThisRulebase = import_result['data']['insert_rule']['affected_rows']
                        if changesForThisRulebase>0:
                            for rule in import_result['data']['insert_rule']['returning']:
                                newRuleIds.append(rule['rule_id'])
                            changes += changesForThisRulebase
                except Exception:
                    raise fwo_exceptions.FwoApiWriteError(f"failed to write new rulebases: {str(traceback.format_exc())}")
        return errors, changes, newRuleIds

    # adds only new rules to the database
    # unchanged or deleted rules are not touched here
    def addNewRules(self, newRules: List[Rulebase]):
        newRulebaseIds = []
        newRuleIds = []

        errors1, changes1, newRulebaseIds = self.addRulebasesWithoutRules(newRules)
        errors2, changes2, newRuleIds = self.addRulesWithinRulebases(newRules)
       
        return errors1+errors2, changes1+changes2, newRuleIds


    # creates a structure of rulebases optinally including rules for import
    def PrepareNewRuleMetadata(self, newRules: List[Rulebase], includeRules: bool = True) -> List[RulebaseForImport]:
        newRuleMetadata: List[RuleMetadatum] = []

	# "rule_metadata_id" BIGSERIAL,
	# "rule_uid" Text NOT NULL,
	# "rule_created" Timestamp NOT NULL Default now(),
	# "rule_last_modified" Timestamp NOT NULL Default now(),
	# "rule_first_hit" Timestamp,
	# "rule_last_hit" Timestamp,
	# "rule_hit_counter" BIGINT,
	# "rule_last_certified" Timestamp,
	# "rule_last_certifier" Integer,
	# "rule_last_certifier_dn" VARCHAR,
	# "rule_owner" Integer, -- points to a uiuser (not an owner)
	# "rule_owner_dn" Varchar, -- distinguished name pointing to ldap group, path or user
	# "rule_to_be_removed" Boolean NOT NULL Default FALSE,
	# "last_change_admin" Integer,
	# "rule_decert_date" Timestamp,
	# "rule_recertification_comment" Varchar,

        now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        for rulebase in newRules:
            for rule in rulebase.Rules:
                rm4import = RuleMetadatum(
                    rule_uid=rule.rule_uid,
                    rule_last_modified=now,
                    rule_created=now
                )
                newRuleMetadata.append(rm4import.dict())
        # TODO: add other fields
        return newRuleMetadata    

    # creates a structure of rulebases optinally including rules for import
    def PrepareNewRulebases(self, newRules: List[Rulebase], includeRules: bool = True) -> List[RulebaseForImport]:
            # name: str
            # uid: str
            # mgm_uid: str
            # is_global: bool = False
            # Rules: List[Rule] = []

        newRulesForImport: List[RulebaseForImport] = []


        for rulebase in newRules:
            rules = {"data": []}
            if includeRules:
                rules = self.PrepareRuleForImport(self.ImportDetails, rulebase.Rules, rulebaseUid=rulebase.uid)
            rb4import = RulebaseForImport(
                name=rulebase.name,
                mgm_uid=self.ImportDetails.MgmDetails.Name,
                mgm_id=self.ImportDetails.MgmDetails.Id,
                uid=rulebase.uid,
                is_global=self.ImportDetails.MgmDetails.IsSuperManager,
                created=self.ImportDetails.ImportId,
                rules=rules
            )
            newRulesForImport.append(rb4import.dict())
        # TODO: see where to get real UIDs (both for rulebase and manager)
        # add rules for each rulebase
        return newRulesForImport    

    def markRulesRemoved(self, removedRuleUids):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        collectedRemovedRuleIds = []

        # TODO: make sure not to mark new (changed) rules as removed (order of calls!)
        
        for rbName in removedRuleUids:
            removedRuleIds = [] # return values
            if len(removedRuleUids[rbName])>0:   # if nothing to remove, skip this
                removeMutation = """
                    mutation markRulesRemoved($importId: bigint!, $mgmId: Int!, $uids: [String!]!) {
                        update_rule(where: {active: {_eq: true}, rule_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}}, _set: {removed: $importId, active:false}) {
                            affected_rows
                            returning { rule_id }
                        }
                    }
                """
                queryVariables = {  'importId': self.ImportDetails.ImportId,
                                    'mgmId': self.ImportDetails.MgmDetails.Id,
                                    'uids': list(removedRuleUids[rbName]) }
                
                try:
                    removeResult = self.ImportDetails.call(removeMutation, queryVariables=queryVariables)
                    if 'errors' in removeResult:
                        errors = 1
                        logger.exception(f"fwo_api:removeRules - error while removing rules: {str(removeResult['errors'])}")
                        return errors, changes, removedRuleIds
                    else:
                        changes = int(removeResult['data']['update_rule']['affected_rows'])
                        removedRuleIds = removeResult['data']['update_rule']['returning']
                        collectedRemovedRuleIds += [item['rule_id'] for item in removedRuleIds]
                except Exception:
                    errors = 1
                    logger.exception(f"failed to remove rules: {str(traceback.format_exc())}")
                    return errors, changes, collectedRemovedRuleIds


        # also delete rule_to, rule_from, rule_service, ... entries
        if len(collectedRemovedRuleIds)>0: 
            removeRefsMutation = """
                mutation markRemovedRulesRefsAsRemoved($importId: bigint!, $ruleIds: [bigint!]!) {
                    update_rule_from(where: {active: {_eq: true}, rule_id: {_in: $ruleIds}}, _set: {removed: $importId, active: false}) {
                        affected_rows
                    }
                    update_rule_to(where: {active: {_eq: true}, rule_id: {_in: $ruleIds}}, _set: {removed: $importId, active: false}) {
                        affected_rows
                    }
                    update_rule_service(where: {active: {_eq: true}, rule_id: {_in: $ruleIds}}, _set: {removed: $importId, active: false}) {
                        affected_rows
                    }
                    update_rule_nwobj_resolved(where: {rule_id: {_in: $ruleIds}}, _set: {removed: $importId}) {
                        affected_rows
                    }
                    update_rule_svc_resolved(where: {rule_id: {_in: $ruleIds}}, _set: {removed: $importId}) {
                        affected_rows
                    }
                    update_rule_user_resolved(where: {rule_id: {_in: $ruleIds}}, _set: {removed: $importId}) {
                        affected_rows
                    }
                }
            """    

            queryVariables = {  'importId': self.ImportDetails.ImportId,
                                'ruleIds': collectedRemovedRuleIds }
        
            try:
                removeResult = self.ImportDetails.call(removeRefsMutation, queryVariables=queryVariables)
                if 'errors' in removeResult:
                    errors = 1
                    logger.exception(f"fwo_api:removeRuleRefs - error while removing rule refs: {str(removeResult['errors'])}")
                    return errors, changes, removedRuleIds
            except Exception:
                errors = 1
                logger.exception(f"failed to remove rules: {str(traceback.format_exc())}")
                return errors, changes, collectedRemovedRuleIds

        return errors, changes, collectedRemovedRuleIds


    def create_new_rule_version(self, rule_uids):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        collected_changed_rule_ids = []
        self._changed_rule_id_map = {}
        rule_order_service = RuleOrderService()

        if len(rule_uids) == 0:
            return errors, changes, collected_changed_rule_ids
        
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
                active: { _eq: true },
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
                import_rules.extend(self.PrepareRuleForImport(self.ImportDetails, [rule_with_changes for rule_with_changes in rule_order_service.target_rules_flat if rule_with_changes.rule_uid in rule_uids[rulebase_uid]], rulebaseUid=rulebase_uid)["data"])

        create_new_rule_version_variables = {
            "objects": import_rules,
            "uids": [rule["rule_uid"] for rule in import_rules],
            "mgmId": self.ImportDetails.MgmDetails.Id,
            "importId": self.ImportDetails.ImportId
        }
        
        try:
            create_new_rule_version_result = self.ImportDetails.call(createNewRuleVersions, queryVariables=create_new_rule_version_variables)
            if 'errors' in create_new_rule_version_result:
                errors = 1
                logger.exception(f"fwo_api:createNewRuleVersions - error while creating new rule versions: {str(create_new_rule_version_result['errors'])}")
                return errors, changes, collected_changed_rule_ids
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


                collected_changed_rule_ids.extend(list(self._changed_rule_id_map.keys()))
                self.update_refs_after_move(insert_rules_return, update_rules_return)

        except Exception:
            errors = 1
            logger.exception(f"failed to move rules: {str(traceback.format_exc())}")

        return errors, changes, collected_changed_rule_ids


    def update_refs_after_move(self, insert_rules_return, update_rules_return):
        """
            Updates every occurence of the moved rules ids in relevant tables to the newly created versions ids…
        """

        logger = getFwoLogger()

        update_moved_rules_refs_mutation = """mutation UpdateRulesRefsAfterMoves($rule_ids: [bigint!], $importId: bigint) {

            update_rule_to(
                where: {
                active: { _eq: true },
                rule_id: { _in: $rule_ids },
                rt_last_seen: { _neq: $importId }
                },
                _set: {
                removed: $importId,
                rt_last_seen: $importId,
                active: false
                }
            ) {
                affected_rows
            }

            update_rule_from(
                where: {
                active: { _eq: true },
                rule_id: { _in: $rule_ids },
                rf_last_seen: { _neq: $importId }
                },
                _set: {
                removed: $importId,
                active: false,
                rf_last_seen: $importId
                }
            ) {
                affected_rows
            }

            update_rule_service(
                where: {
                active: { _eq: true },
                rule_id: { _in: $rule_ids },
                rs_last_seen: { _neq: $importId }
                },
                _set: {
                removed: $importId,
                active: false,
                rs_last_seen: $importId
                }
            ) {
                affected_rows
            }
        }
        """

        update_moved_rules_refs_variables = {
            "rule_ids": [returned_ref["rule_id"] for returned_ref in update_rules_return],
            "importId": self.ImportDetails.ImportId
        }

        try:
            self.addNewRule2ObjRefs(insert_rules_return)
            update_moved_rules_refs_result = self.ImportDetails.call(update_moved_rules_refs_mutation, queryVariables=update_moved_rules_refs_variables, debug_level=self.ImportDetails.DebugLevel, analyze_payload=True)
            
            if 'errors' in update_moved_rules_refs_result:
                logger.exception(f"fwo_api:moveRules - error while updating moved rules refs: {str(update_moved_rules_refs_result['errors'])}")
                return 1, 0, []

            errors_update_enforced_on_gateway, changes_update_enforced_on_gateway, _ = self.update_rule_enforced_on_gateway_after_move(insert_rules_return, update_rules_return)
            errors_update_rulebase_links, changes_update_rulebase_links, _ = self.update_rulebase_links_after_move(insert_rules_return, update_rules_return)

            return errors_update_enforced_on_gateway + errors_update_rulebase_links, changes_update_enforced_on_gateway + changes_update_rulebase_links, []

        except Exception:
            logger.exception(f"failed to move rules: {str(traceback.format_exc())}")
            return 1, 0, []


    def update_rulebase_links_after_move(self, insert_rules_return, update_rules_return):
        """
            Updates the db table rulebase_link by marking old links as removed,
            and inserting new ones with updated from_rule_ids.
        """

        logger = getFwoLogger()

        insertNewRulebaseLinks = """mutation insertNewRulebaseLinks($objects: [rulebase_link_insert_input!]!) {
            insert_rulebase_link(objects: $objects) {
                affected_rows
            }
        }
        """

        updateRulesbaseLinksAfterMoves = """mutation updateRulesbaseLinksAfterMoves($rule_ids: [bigint!], $importId: bigint) {
            update_rulebase_link(
                where: {
                    from_rule_id: { _in: $rule_ids },
                    removed: { _is_null: true }
                },
                _set: {
                    removed: $importId
                }
            ) {
                affected_rows
                returning {
                    from_rule_id
                    gw_id
                    from_rulebase_id
                    to_rulebase_id
                    link_type
                    is_initial
                    is_global
                    created
                }
            }
        }
        """

        updateRulesbaseLinksAfterMoves_variables = {
            "rule_ids": [update_rules_return_element["rule_id"] for update_rules_return_element in update_rules_return],
            "importId": self.ImportDetails.ImportId
        }

        try:
            updateRulesbaseLinksAfterMoves_result =  self.ImportDetails.call(updateRulesbaseLinksAfterMoves, updateRulesbaseLinksAfterMoves_variables, self.ImportDetails.DebugLevel)

            if 'errors' in updateRulesbaseLinksAfterMoves_result:
                logger.exception(f"fwo_api:moveRules - error while updating moved rules refs: {str(updateRulesbaseLinksAfterMoves_result['errors'])}")
                return 1, 0, []
            
            updated_rows = updateRulesbaseLinksAfterMoves_result["data"]["update_rulebase_link"]["returning"]

            insert_objects = []

            for updated_row in updated_rows:
                from_rule_uid = next(update_rules_return_element for update_rules_return_element in update_rules_return if update_rules_return_element["rule_id"] == updated_row["from_rule_id"])["rule_uid"]
                new_rule_id = next(insert_rules_return_element for insert_rules_return_element in insert_rules_return if insert_rules_return_element["rule_uid"] == from_rule_uid)["rule_id"]
                updated_row["new_rule_id"] = new_rule_id
                insert_objects.append({
                    "gw_id": updated_row["gw_id"],
                    "from_rulebase_id": updated_row["from_rulebase_id"],
                    "from_rule_id": new_rule_id,
                    "to_rulebase_id": updated_row["to_rulebase_id"],
                    "link_type": updated_row["link_type"],
                    "is_initial": updated_row["is_initial"],
                    "is_global": updated_row["is_global"],
                    "created": updated_row["created"],
                })

            insertNewRulebaseLinks_result =  self.ImportDetails.call(insertNewRulebaseLinks, {"objects": insert_objects}, debug_level=self.ImportDetails.DebugLevel)

            if 'errors' in insertNewRulebaseLinks_result:
                logger.exception(f"fwo_api:moveRules - error while updating moved rules refs: {str(insertNewRulebaseLinks_result['errors'])}")
                return 1, 0, []
            
            return 0, 0, []
        
        except Exception:
            logger.exception(f"failed to move rules: {str(traceback.format_exc())}")
            return 1, 0, []
        



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
            "importId": self.ImportDetails.ImportId,
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
            set_rule_enforced_on_gateway_entries_removed_result =  self.ImportDetails.call(set_rule_enforced_on_gateway_entries_removed_mutation, set_rule_enforced_on_gateway_entries_removed_variables, self.ImportDetails.DebugLevel)

            if 'errors' in set_rule_enforced_on_gateway_entries_removed_result:
                logger.exception(f"fwo_api:moveRules - error while updating moved rules refs: {str(set_rule_enforced_on_gateway_entries_removed_result['errors'])}")
                return 1, 0, []
            
            insert_rule_enforced_on_gateway_entries_variables = {
                "new_entries": [
                    {
                        "rule_id": new_id,
                        "dev_id": next(entry for entry in  set_rule_enforced_on_gateway_entries_removed_result["data"]["update_rule_enforced_on_gateway"]["returning"] if entry["rule_id"] == id_map[new_id])["dev_id"],
                        "created": self.ImportDetails.ImportId,
                    }
                    for new_id in id_map.keys()
                ]
            }

            insert_rule_enforced_on_gateway_entries_result =  self.ImportDetails.call(insert_rule_enforced_on_gateway_entries_mutation, insert_rule_enforced_on_gateway_entries_variables, self.ImportDetails.DebugLevel)

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

        for rule_uid in rule_order_service._moved_rule_uids.values():

            if rule_uid in changed_rule_uids:
                moved_rule_uids.append(rule_uid)
                number_of_moved_rules += 1
            else:
                error_count_move += 1

        return error_count_move, number_of_moved_rules, moved_rule_uids
            
            

    # TODO: limit query to a single rulebase
    def GetRuleNumMap(self):
        query = "query getRuleNumMap($mgmId: Int) { rule(where:{mgm_id:{_eq:$mgmId}}) { rule_uid rulebase_id rule_num_numeric } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={"mgmId": self.ImportDetails.MgmDetails.Id})
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
            result = self.ImportDetails.call(query=query, queryVariables={})
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
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for track in result['data']['stm_track']:
            map.update({track['track_name']: track['track_id']})
        return map

    # prepare new rules for import: fill-in all references (database ids) for actions, tracks, ...
    # def prepareNewRules(self, newRuleUids, newRulebaseForImport: RulebaseForImport):
    #     newRules = []
    #     for rulebaseUid in newRuleUids:
    #         currentRulebase = [pol for pol in self.NormalizedConfig.rulebases if pol.uid == rulebaseUid]
    #         if len(currentRulebase)==1:
    #             currentRulebase = currentRulebase.pop()
    #         else:
    #             logger = getFwoLogger()
    #             logger.warning("did not find exactly one rulebase for rulebaseUid")

    #         for ruleUid in newRuleUids[rulebaseUid]:
    #             rule = currentRulebase.Rules[ruleUid]
    #             rule_action_id = self.lookupAction(rule.rule_action)
    #             rule_track_id = self.lookupTrack(rule.rule_track)
    #             rulebaseId = self.lookupRulebaseId(rulebaseUid)

    #             rule_type = rule.rule_type
    #             if rule_type == RuleType.ACCESS:
    #                 access_rule = True
    #                 nat_rule = False
    #             elif rule_type == RuleType.NAT:
    #                 access_rule = False
    #                 nat_rule = True
    #             else:   # mast be both then
    #                 access_rule = True
    #                 nat_rule = True

    #             lastHit = rule.last_hit # TODO: write last_hit to rule_metadata
    #             parentRuleId = None # rule.parent_rule_uid # TODO: link to parent rule if it is set
    #             lastChangeAdmin = rule.rule_last_change_admin
    #             importId = self.ImportDetails.ImportId
    #             is_global = self.ImportDetails.MgmDetails.IsSuperManager
    #             rule_number_float = 1.0 # TODO dummy, calculate!

    #             # TODO: resolve:
    #             #   "rule_num": 1, // no - need to handle order otherwise!
    #             #   "parent_rule_id": null,
    #             # - parent_rule_uid
    #             # rulebase_id

    #             newEnrichedRule = RuleForImport(
    #                 mgm_id=self.ImportDetails.MgmDetails.Id,
    #                 rule_num=rule.rule_num,
    #                 rule_disabled=rule.rule_disabled,
    #                 rule_src_neg=rule.rule_src_neg,
    #                 rule_src=rule.rule_src,
    #                 rule_src_refs=rule.rule_src_refs,
    #                 rule_dst_neg=rule.rule_dst_neg,
    #                 rule_dst=rule.rule_dst,
    #                 rule_dst_refs=rule.rule_dst_refs,
    #                 rule_svc_neg=rule.rule_svc_neg,
    #                 rule_svc=rule.rule_svc,
    #                 rule_svc_refs=rule.rule_svc_refs,
    #                 rule_action=rule.rule_action,
    #                 rule_track=rule.rule_track,
    #                 rule_installon=rule.rule_installon,
    #                 rule_time=rule.rule_time,
    #                 rule_name=rule.rule_name,
    #                 rule_uid=rule.rule_uid,
    #                 rule_custom_fields=rule.rule_custom_fields,
    #                 rule_implied=rule.rule_implied,
    #                 # last_change_admin=lastChangeAdmin,
    #                 parent_rule_id=parentRuleId,
    #                 # last_hit=rule.last_hit,   # TODO: add this to rule_metadata
    #                 rule_comment=rule.rule_comment,
    #                 rule_from_zone=rule.rule_src_zone,
    #                 rule_to_zone=rule.rule_dst_zone,
    #                 access_rule=access_rule,
    #                 nat_rule=nat_rule,
    #                 is_global=is_global,
    #                 rulebase_id=rulebaseId,
    #                 rule_create=importId,
    #                 rule_last_seen=importId,
    #                 rule_num_numeric=rule_number_float,
    #                 action_id=rule_action_id,
    #                 track_id=rule_track_id,
    #                 rule_head_text=rule.rule_head_text
    #             )

    #             # 'rule_num': 1   # TODO: need to fix this!!!!!!!!!!!!!!!
                    
    #             newRules.append(newEnrichedRule.dict())
           
    #     return newRules

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
                    rules(where: {rule: {rule_create: {_lt: $importId}, removed: {_is_null: true}, active: {_eq: true}}}, order_by: {rule: {rule_num_numeric: asc}}) {
                        rule_num
                        rule_num_numeric
                        rule_uid
                    }
                }
            }
        """
        
        try:
            queryResult = self.ImportDetails.call(query, queryVariables=query_variables)
        except Exception:
            logger = getFwoLogger()
            logger.error(f"error while getting current rulebase: {str(traceback.format_exc())}")
            self.ImportDetails.increaseErrorCounterByOne()
            return
        
        try:
            ruleList = queryResult['data']['rulebase'][0]['rules']
        except Exception:
            logger = getFwoLogger()
            logger.error(f'could not find rules in query result: {queryResult}')
            self.ImportDetails.increaseErrorCounterByOne()
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
                "mgm_id": self.ImportDetails.MgmDetails.Id,
                "name": ruleBaseName,
                "created": self.ImportDetails.ImportId
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
        return self.ImportDetails.call(mutation, queryVariables=query_variables)


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
        
        return self.ImportDetails.call(mutation, queryVariables=query_variables)


    def PrepareRuleForImport(self, importDetails: ImportStateController, Rules, rulebaseUid: str) -> Dict[str, List[Rule]]:
        prepared_rules = []

        # get rulebase_id for rulebaseUid
        rulebase_id = importDetails.lookupRulebaseId(rulebaseUid)

        for rule in Rules:
            listOfEnforcedGwIds = []
            for gwUid in rule.rule_installon.split(fwo_const.list_delimiter):
                gwId = importDetails.lookupGatewayId(gwUid)
                if gwId is not None:
                    listOfEnforcedGwIds.append(gwId)
            if len(listOfEnforcedGwIds) == 0:
                listOfEnforcedGwIds = None

            rule_for_import = Rule(
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
                # rulebase_id=importDetails.lookupRulebaseId(rulebaseUid),
                rulebase_id=rulebase_id,
                rule_create=importDetails.ImportId,
                rule_last_seen=importDetails.ImportId,
                rule_num_numeric=rule.rule_num_numeric,
                action_id = importDetails.lookupAction(rule.rule_action),
                track_id = importDetails.lookupTrack(rule.rule_track),
                rule_head_text=rule.rule_head_text
            ).dict()

            if listOfEnforcedGwIds is not None and len(listOfEnforcedGwIds) > 0:    # leave out field, if no resolvable gateways are found
                rule_for_import.update({'rule_installon': rule.rule_installon }) #fwo_const.list_delimiter.join(listOfEnforcedGwIds) })

            prepared_rules.append(rule_for_import)
        return { "data": prepared_rules }
    

    def write_changelog_rules(self, added_rules_ids, removed_rules_ids):
        logger = getFwoLogger()
        errors = 0

        changelog_rule_insert_objects = self.prepare_changelog_rules_insert_objects(added_rules_ids, removed_rules_ids)

        updateChanglogRules = fwo_api.get_graphql_code([fwo_const.graphqlQueryPath + "rule/updateChanglogRules.graphql"])

        queryVariables = {
            'rule_changes': changelog_rule_insert_objects
        }

        if len(changelog_rule_insert_objects) > 0:
            try:
                updateChanglogRules_result = self.ImportDetails.call(updateChanglogRules, queryVariables=queryVariables, analyze_payload=True)
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

        if self.ImportDetails.IsFullImport or self.ImportDetails.IsClearingImport:
            changeTyp = 2   # TODO: Somehow all imports are treated as im operation.

        for rule_id in added_rules_ids:
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.ImportDetails, 'I', changeTyp, importTime, rule_id))

        for rule_id in removed_rules_ids:
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.ImportDetails, 'D', changeTyp, importTime, rule_id))

        for old_rule_id, new_rule_id in self._changed_rule_id_map.items():
            changelog_rule_insert_objects.append(change_logger.create_changelog_import_object("rule", self.ImportDetails, 'C', changeTyp, importTime, new_rule_id, old_rule_id))

        return changelog_rule_insert_objects


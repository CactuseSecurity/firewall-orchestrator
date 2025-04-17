import traceback
from difflib import ndiff
import fwo_const
import fwo_api
import fwo_exceptions
from models.rule import Rule
from models.rule_metadatum import RuleMetadatum
from models.rulebase import Rulebase, RulebaseForImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger
from typing import List
from datetime import datetime
from model_controllers.fwconfig_import_object import FwConfigImportObject
from models.rule_from import RuleFrom
from models.rule_to import RuleTo
from models.rule_service import RuleService


# this class is used for importing rules and rule refs into the FWO API
class FwConfigImportRule(FwConfigImportBase):

    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
      super().__init__(importState, config)
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
        ruleUidsInBoth = {}
        previousRulebaseUids = []
        currentRulebaseUids = []

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

                deletedRuleUids.update({ rulebaseId: list(previousRulebase.Rules.keys() - currentRulebase.Rules.keys()) })
                newRuleUids.update({ rulebaseId: list(currentRulebase.Rules.keys() - previousRulebase.Rules.keys()) })
                ruleUidsInBoth.update({ rulebaseId: list(currentRulebase.Rules.keys() & previousRulebase.Rules.keys()) })
            else:
                logger.info(f"previous rulebase has been deleted: {rulebaseId}")
                # TODO: also dispaly rulebase name
                deletedRuleUids.update({ rulebaseId: list(currentRulebase.Rules.keys()) })

        # now deal with new rulebases (not contained in previous config)
        for rulebase in self.NormalizedConfig.rulebases:
            if rulebase.uid not in previousRulebaseUids:
                newRuleUids.update({ rulebase.uid: list(rulebase.Rules.keys()) })

        # find changed rules
        # TODO: need to ignore last_hit! 
        for rulebaseId in ruleUidsInBoth:
            changedRuleUids.update({ rulebaseId: [] })
            currentRulebase = self.NormalizedConfig.getRulebase(rulebaseId) # [pol for pol in self.NormalizedConfig.rulebases if pol.Uid == rulebaseId]
            previousRulebase = prevConfig.getRulebase(rulebaseId)
            for ruleUid in ruleUidsInBoth[rulebaseId]:
                if self.ruleChanged(rulebaseId, ruleUid, currentRulebase, previousRulebase):
                    changedRuleUids[rulebaseId].append(ruleUid)

        # TODO: handle changedRuleUids        

        # add full rule details first
        newRulebases = self.getRules(newRuleUids)

        # update rule_metadata before adding rules
        errorCountAdd, numberOfAddedMetaRules, newRuleMetadataIds = self.addNewRuleMetadata(newRulebases)

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

        self.ImportDetails.Stats.RuleAddCount += numberOfAddedRules
        self.ImportDetails.Stats.RuleDeleteCount += numberOfDeletedRules

        # TODO: rule_nwobj_resolved fuellen (recert?)
        return newRuleIds


    def addNewRule2ObjRefs(self, newRules):
        # for each new rule: add refs in rule_to and rule_from
        # assuming all nwobjs are already in the database

        # TODO: need to make sure that the references do not already exist!

        # first get all network objects via API that are used in the new rules
        objectUid2IdMapper = FwConfigImportObject(self.ImportDetails, self.NormalizedConfig)
        objectUid2IdMapper.buildObjUidToIdMapFromApi(newRules)  # creates a dict with all relevant mapping
            # service, network objects, users, zones, ...
            # (of the current management and possibly its super management)
            # this will be limited to refs found in newRules

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
                    ruleFromUserRefs.append(objectUid2IdMapper.UserObjUidToIdMap[userRef])
                    srcRef = nwRef
                if srcRef in objectUid2IdMapper.NwObjUidToIdMap:
                    ruleFromRefs.append(objectUid2IdMapper.NwObjUidToIdMap[srcRef])
            for dstRef in rule['rule_dst_refs'].split(fwo_const.list_delimiter):
                if fwo_const.user_delimiter in dstRef:
                    userRef, nwRef = dstRef.split(fwo_const.user_delimiter)
                    ruleToUserRefs.append(objectUid2IdMapper.UserObjUidToIdMap[userRef])
                    dstRef = nwRef
                if dstRef in objectUid2IdMapper.NwObjUidToIdMap:
                    ruleToRefs.append(objectUid2IdMapper.NwObjUidToIdMap[dstRef])
            for svcRef in rule['rule_svc_refs'].split(fwo_const.list_delimiter):
                if svcRef in objectUid2IdMapper.SvcObjUidToIdMap:
                    ruleSvcRefs.append(objectUid2IdMapper.SvcObjUidToIdMap[svcRef])
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
        addNewRuleNwObjAndSvcRefsMutation = fwo_api.getGraphqlCode([fwo_const.graphqlQueryPath + "rule/inserRuleRefs.graphql"])

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
        getRuleUidRefsQuery = fwo_api.getGraphqlCode([fwo_const.graphqlQueryPath + "rule/getRulesByIdWithRefUids.graphql"])
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
    

    def ruleChanged(self, rulebaseId, ruleUid, currentRulebase: Rulebase, prevRulebase: Rulebase):
        # TODO: need to ignore rule_num, last_hit, ...?
        return prevRulebase.Rules[ruleUid] != currentRulebase.Rules[ruleUid]
        # return prevConfig.rulebases[rulebaseId].Rules[ruleUid] != self.NormalizedConfig.rulebases[rulebaseId].Rules[ruleUid]
        # return prevConfig['rules'][rulebaseId]['Rules'][ruleUid] != self.rulebases[rulebaseId]['Rules'][ruleUid]

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

    """
        return


        # first deal with new rulebases
        for newRbName in self.NormalizedConfig.rulebases:
            if newRbName not in previousRules:
                # if rulebase is new, simply for all rules: set rule_num_numeric to 1000*rule_num
                for ruleUid in self.rulebases[newRbName]['Rules']:
                    self.rulebases[newRbName].Rules[ruleUid].update({'rule_num_numeric': self.rulebases[newRbName].Rules[ruleUid]['rule_num']*1000.0})
                    # self.rulebases[newRbName]['Rules'][ruleUid].update({'rule_num_numeric': self.rulebases[newRbName]['Rules'][ruleUid]['rule_num']*1000.0})
        
        # now handle new rules in existing rulebases
        for rulebaseName in previousRules:
            previousUidList = []
            currentUidList = []
            previousUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(previousRules[rulebaseName]['Rules'])

            if rulebaseName in self.rulebases:  # ignore rulebases that have been deleted
                currentUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(self.rulebases[rulebaseName].Rules)
                # currentUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(self.rulebases[rulebaseName]['Rules'])

                # Calculate the rules differences
                changes = FwConfigImportRule.listDiff(previousUidList, currentUidList)
                
                # Retrieve the current list from the database ordered by order_number
                # cursor.execute("SELECT rule_num, rule_num_numeric, rule_uid FROM rule ORDER BY rule_num_numeric")
                current_db_list = self.getCurrentRules(self.ImportDetails.ImportId, self.ImportDetails.MgmDetails.Id, rulebaseName)

                db_index = 0  # Tracks the position in the current_db_list
                order_number_increment = 1.0  # Incremental order number step

                for change_type, uid in changes:
                    if change_type == 'delete':
                        # Find the uid in the current_db_list and delete it
                        for db_item in current_db_list:
                            if db_item[2] == uid:  # Compare by uid
                                # ignore deletes: cursor.execute("DELETE FROM list_items WHERE id = ?", (db_item[0],))
                                current_db_list.remove(db_item)
                                break

                    elif change_type == 'insert':
                        # Calculate the new order number
                        if db_index == 0:
                            new_order_number = 0.5 if len(current_db_list) > 0 else 1.0
                        elif db_index >= len(current_db_list):
                            new_order_number = current_db_list[db_index-1][1] + order_number_increment
                        else:
                            prev_order_number = current_db_list[db_index-1][1]
                            next_order_number = current_db_list[db_index][1]
                            new_order_number = (prev_order_number + next_order_number) / 2.0
                        
                        # Insert the new uid with the calculated order number
                        # cursor.execute("INSERT INTO list_items (order_number, uid) VALUES (?, ?)", 
                        #             (new_order_number, uid))
                        self.rulebases[rulebaseName]['Rules'][uid].update( { 'rule_num_numeric': new_order_number })
                        # Add to current_db_list to keep track of new state
                        current_db_list.insert(db_index, (None, new_order_number, uid))

                    elif change_type == 'unchanged':
                        db_index += 1  # Move to the next uid in the current_db_list
    """


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
            import_result = self.ImportDetails.call(addNewRuleMetadataMutation, queryVariables=queryVariables)
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
                        constraint: unique_rulebase_mgm_id_name,
                        update_columns: []
                    }
                ) {
                    affected_rows
                    returning {
                        id
                        name
                    }
                }
            }
        """

        newRulebasesForImport: List[RulebaseForImport] = self.PrepareNewRulebases(newRules, includeRules=False)
        queryVariables = { 'rulebases': newRulebasesForImport }
        
        try:
            import_result = self.ImportDetails.call(addRulebasesWithoutRulesMutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in addNewRules: {str(import_result['errors'])}")
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
                    import_result = self.ImportDetails.call(upsertRulebaseWithRules, queryVariables=queryVariables)
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
                        update_rulebase(where: {rules: {active: {_eq: true}, rule_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}}}, _set: {removed: $importId}) {
                            affected_rows
                            returning { id }
                        }
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
                        changes = int(removeResult['data']['update_rulebase']['affected_rows'])
                        removedRuleIds = removeResult['data']['update_rulebase']['returning']
                        collectedRemovedRuleIds += [item['id'] for item in removedRuleIds]
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
                        constraint: unique_rulebase_mgm_id_name,
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


    def PrepareRuleForImport(self, importDetails: ImportStateController, Rules, rulebaseUid: str) -> List[Rule]:
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
                rule_num_numeric=1,
                action_id = importDetails.lookupAction(rule.rule_action),
                track_id = importDetails.lookupTrack(rule.rule_track),
                rule_head_text=rule.rule_head_text
            ).dict()

            if listOfEnforcedGwIds is not None and len(listOfEnforcedGwIds) > 0:    # leave out field, if no resolvable gateways are found
                rule_for_import.update({'rule_installon': rule.rule_installon }) #fwo_const.list_delimiter.join(listOfEnforcedGwIds) })

            prepared_rules.append(rule_for_import)
        return { "data": prepared_rules }
    
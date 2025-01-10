import traceback
from difflib import ndiff
import json

from models.rule import RuleForImport, RuleType
from roles.importer.files.importer.models.rulebase import Rulebase
from models.rulebase import Rulebase, RulebaseForImport
from fwoBaseImport import ImportState
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger
from typing import List

# this class is used for importing a config into the FWO API
class FwConfigImportRule(FwConfigImportBase):

    # @root_validator(pre=True)
    # def custom_initialization(cls, values):
    #     values['ActionMap'] = cls.GetActionMap()
    #     values['TrackMap'] = cls.GetTrackMap()
    #     values['RuleNumLookup'] = cls.GetRuleNumMap()  # TODO: needs to be updated with each insert
    #     values['NextRuleNumLookup'] = cls.GetNextRuleNumMap() # TODO: needs to be updated with each insert
    #     values['RulebaseMap'] = cls.GetRulebaseMap() # limited to the current mgm_id
    #     return values

    def __init__(self, importState: ImportState, config: FwConfigNormalized):
      super().__init__(importState, config)
    #   self.ActionMap = self.GetActionMap()
    #   self.TrackMap = self.GetTrackMap()
      self.RuleNumLookup = self.GetRuleNumMap()             # TODO: needs to be updated with each insert
      self.NextRuleNumLookup = self.GetNextRuleNumMap()     # TODO: needs to be updated with each insert
      # self.RulebaseMap = self.GetRulebaseMap()     # limited to the current mgm_id

    def updateRuleDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger()
        # calculate rule diffs
        changedRuleUids = {}
        deletedRuleUids = {}
        newRuleUids = {}
        ruleUidsInBoth = {}
        previousPolicyUids = []
        currentPolicyUids = []

        # collect policy UIDs of previous config
        for policy in prevConfig.rules:
            previousPolicyUids.append(policy.uid)

        # collect policy UIDs of current (just imported) config
        for policy in self.NormalizedConfig.rules:
            currentPolicyUids.append(policy.uid)

        for rulebaseId in previousPolicyUids:
            currentPolicy = self.NormalizedConfig.getPolicy(rulebaseId)
            if rulebaseId in currentPolicyUids:
                # deal with policies contained both in this and previous config
                previousPolicy = prevConfig.getPolicy(rulebaseId)

                deletedRuleUids.update({ rulebaseId: list(previousPolicy.Rules.keys() - currentPolicy.Rules.keys()) })
                newRuleUids.update({ rulebaseId: list(currentPolicy.Rules.keys() - previousPolicy.Rules.keys()) })
                ruleUidsInBoth.update({ rulebaseId: list(currentPolicy.Rules.keys() & previousPolicy.Rules.keys()) })
            else:
                logger.info(f"previous rulebase has been deleted: {rulebaseId}")
                deletedRuleUids.update({ rulebaseId: list(currentPolicy.Rules.keys()) })

        # now deal with new rulebases (not contained in previous config)
        for policy in self.NormalizedConfig.rules:
            if policy.uid not in previousPolicyUids:
                newRuleUids.update({ policy.uid: list(policy.Rules.keys()) })

        # find changed rules
        # TODO: need to ignore last_hit! 
        for rulebaseId in ruleUidsInBoth:
            changedRuleUids.update({ rulebaseId: [] })
            currentPolicy = self.NormalizedConfig.getPolicy(rulebaseId) # [pol for pol in self.NormalizedConfig.rules if pol.Uid == rulebaseId]
            previousPolicy = prevConfig.getPolicy(rulebaseId)
            for ruleUid in ruleUidsInBoth[rulebaseId]:
                if self.ruleChanged(rulebaseId, ruleUid, currentPolicy, previousPolicy):
                    changedRuleUids[rulebaseId].append(ruleUid)

        # changed rules will get the same rule_num_numeric as their previous version?!
        # new rules will be fitted between the respective rules of the previous rulebase
        self.setNewRulesNumbering(prevConfig)

        # update rule diffs

        # add full rule details first
        newRulebases = self.getRules(newRuleUids)
        # transform rule from dict (key=uid) to list, also adding a data: layer
        for rb in newRulebases:
            rb.Rules = list(rb.Rules.values())
        # now update the database
        errorCountAdd, numberOfAddedRules, newRuleIds = self.addNewRules(newRulebases)
        # if errorCountAdd>0:
        #     self.ImportDetails.increaseErrorCounter(errorCountAdd)
        # if numberOfAddedRules>0:
        #     self.ImportDetails.setChangeCounter(self.ImportDetails.ChangeCount+numberOfAddedRules)
        errorCountDel, numberOfDeletedRules, removedRuleIds = self.markRulesRemoved(deletedRuleUids)
        # if errorCountAdd>0:
        #     self.ImportDetails.increaseErrorCounter(errorCountAdd)
        # if numberOfDeletedRules>0:
        #     self.ImportDetails.setChangeCounter(self.ImportDetails.ChangeCount+numberOfDeletedRules)

        # TODO: rule_nwobj_resolved fuellen (recert?)

        return errorCountAdd + errorCountDel, numberOfDeletedRules + numberOfAddedRules

    def getRules(self, ruleUids) -> List[Rulebase]:
        rulebases = []
        for rb in self.NormalizedConfig.rules:
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
    
    def ruleChanged(self, rulebaseId, ruleUid, currentPolicy: Rulebase, prevPolicy: Rulebase):
        # TODO: need to ignore rule_num, last_hit, ...?
        return prevPolicy.Rules[ruleUid] != currentPolicy.Rules[ruleUid]
        # return prevConfig.rules[rulebaseId].Rules[ruleUid] != self.NormalizedConfig.rules[rulebaseId].Rules[ruleUid]
        # return prevConfig['rules'][rulebaseId]['Rules'][ruleUid] != self.rules[rulebaseId]['Rules'][ruleUid]

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


    # TODO: rework this as we get errors:
    # File "/home/tim/dev/firewall-orchestrator/roles/importer/files/importer/fwconfig_import_rule.py", line 148, in setNewRulesNumbering
    #     new_order_number = current_db_list[db_index-1][1] + order_number_increment

    # input: 
    # - list of previously existing rules (in previous successful import) per rulebase
    # - list of rules to be imported (only the changes - rules to be added) per rulebase
    # - full database table of current rules of the rulebase at hand
    # update attribute rule_num_numeric of all new rules in current rulebases

    def setNewRulesNumbering(self, previousConfig: FwConfigNormalized):
        """
        Updates the PostgreSQL table 'rule' to reflect changes between old and new rules,
        inserting new rules at the correct positions using float rule_num_numeric values.

        :param old_rules: List of tuples representing previous rules (e.g., [(1, 1.0), (2, 2.0)]).
        :param conn: Connection to the PostgreSQL database.
        """

        for policy in self.NormalizedConfig.rules:
            # Step 1: Identify the old and new rule IDs
            oldRuleUids  = {}
            if policy.uid in previousConfig.rules:
                oldRuleUids = previousConfig.rules[policy.uid].Rules.keys()
            newRuleUids = self.NormalizedConfig.getPolicy(policy.uid).Rules.keys()

            # Rules to delete and add
            deleted_rules = oldRuleUids - newRuleUids
            added_rules = newRuleUids - oldRuleUids

            # Map existing rules to their current rule numbers for quick lookup
            if policy.uid in previousConfig.rules:
                existing_rule_numbers = previousConfig.getPolicy(policy.uid).Rules
            else:
                existing_rule_numbers = {}

            try:
                # # Step 2: Delete the rules no longer in the new list
                # if deleted_rules:
                #     delete_query = "DELETE FROM rule WHERE rule_id IN %s"
                #     cursor.execute(delete_query, (tuple(deleted_rules),))

                # Step 3: Traverse the new list and handle added rules using list operations
                current_rule_number = None
                for ruleUid in policy.Rules.keys():
                    if ruleUid not in existing_rule_numbers:
                        # This is a new rule and needs a new `rule_number`

                        # Find the previous rule in the list that already exists
                        previous_rule_number = current_rule_number

                        # Get the next existing rule number in the new list
                        next_rule_number = None
                        for nextRuleUid in self._find_following_rules(ruleUid, existing_rule_numbers, policy.uid):
                            if nextRuleUid in existing_rule_numbers:
                                next_rule_number = existing_rule_numbers[nextRuleUid]
                                break

                        # Calculate the new rule number based on neighbors
                        if previous_rule_number is not None and next_rule_number is not None:
                            new_rule_number = (previous_rule_number + next_rule_number) / 2.0
                        elif previous_rule_number is not None:
                            new_rule_number = previous_rule_number + 1.0
                        elif next_rule_number is not None:
                            new_rule_number = next_rule_number - 1.0
                        else:
                            new_rule_number = 1.0  # Default when no neighbors exist

                        # # Insert the new rule into the database
                        # insert_query = "INSERT INTO rule (rule_id, rule_num_numeric) VALUES (%s, %s)"
                        # cursor.execute(insert_query, (rule_id, new_rule_number))

                        # Update the existing rule numbers dictionary
                        existing_rule_numbers[ruleUid] = new_rule_number

                    # Update the current rule number for the next iteration
                    current_rule_number = existing_rule_numbers[ruleUid]

            except Exception as e:
                print(f"An error occurred: {e}")

    def _find_following_rules(self, ruleUid, previousPolicy, policyId):
        """
        Helper method to find the next rule in self that has an existing rule number.
        
        :param ruleUid: The ID of the current rule being processed.
        :param previousPolicy: Dictionary of existing rule IDs and their rule_number values.
        :return: Generator yielding rule IDs that appear after `current_rule_id` in self.new_rules.
        """
        found = False
        currentPolicy = self.NormalizedConfig.getPolicy(policyId)
        for currentUid in currentPolicy.Rules:
            if currentUid == ruleUid:
                found = True
            elif found and ruleUid in previousPolicy:
                yield currentUid

    """
        return


        # first deal with new rulebases
        for newRbName in self.NormalizedConfig.rules:
            if newRbName not in previousRules:
                # if rulebase is new, simply for all rules: set rule_num_numeric to 1000*rule_num
                for ruleUid in self.rules[newRbName]['Rules']:
                    self.rules[newRbName].Rules[ruleUid].update({'rule_num_numeric': self.rules[newRbName].Rules[ruleUid]['rule_num']*1000.0})
                    # self.rules[newRbName]['Rules'][ruleUid].update({'rule_num_numeric': self.rules[newRbName]['Rules'][ruleUid]['rule_num']*1000.0})
        
        # now handle new rules in existing rulebases
        for rulebaseName in previousRules:
            previousUidList = []
            currentUidList = []
            previousUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(previousRules[rulebaseName]['Rules'])

            if rulebaseName in self.rules:  # ignore rulebases that have been deleted
                currentUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(self.rules[rulebaseName].Rules)
                # currentUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(self.rules[rulebaseName]['Rules'])

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
                        self.rules[rulebaseName]['Rules'][uid].update( { 'rule_num_numeric': new_order_number })
                        # Add to current_db_list to keep track of new state
                        current_db_list.insert(db_index, (None, new_order_number, uid))

                    elif change_type == 'unchanged':
                        db_index += 1  # Move to the next uid in the current_db_list
    """

    # adds only new rules to the database
    # unchanged or deleted rules are not touched here
    def addNewRules(self, newRules: List[Rulebase]):
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
                        update_columns: [is_global]
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
        
        querVarJson = json.dumps(queryVariables)    # just for debugging purposes, remove in prod

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
        except:
            logger.exception(f"failed to write new rules: {str(traceback.format_exc())}")
            return 1, 0, newRulebaseIds
        
        upsertRulebaseWithRules = """mutation upsertRulebaseWithRules($rulebases: [rulebase_insert_input!]!) {
                insert_rulebase(
                    objects: $rulebases,
                    on_conflict: {
                        constraint: unique_rulebase_mgm_id_name,
                        update_columns: [is_global]
                    }
                ) {
                    affected_rows
                    returning {
                        id
                        name
                        rules {
                            id
                            rule_uid
                        }
                    }
                }
            }
        """
        
        newRulesForImport: List[RulebaseForImport] = self.PrepareNewRulebases(newRules)
        queryVariables = { 'rulebases': newRulesForImport }
        
        querVarJson = json.dumps(queryVariables)

        try:
            import_result = self.ImportDetails.call(upsertRulebaseWithRules, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in upsertRulebaseWithRules: {str(import_result['errors'])}")
                return 1, 0, []
            else:
                # reduce change number by number of rulebases
                changes = import_result['data']['insert_rulebase']['affected_rows']
                if changes>0:
                    for rule in import_result['data']['insert_rulebase']['returning']:
                        newRuleIds.append(rule['rulebase_id'])
        except:
            logger.exception(f"failed to write new rules: {str(traceback.format_exc())}")
            return 1, 0, []
        
        return errors, changes, newRuleIds

    # creates a structure of rulebases optinally including rules for import
    def PrepareNewRulebases(self, newRules: List[Rulebase], includeRules: bool = True) -> List[RulebaseForImport]:
            # name: str
            # uid: str
            # mgm_uid: str
            # is_global: bool = False
            # Rules: List[Rule] = []

        newRulesForImport: List[RulebaseForImport] = []

        for rulebase in newRules:
            rb4import = RulebaseForImport(
                name=rulebase.name,
                mgm_uid=self.ImportDetails.MgmDetails.Name,
                mgm_id=self.ImportDetails.MgmDetails.Id,
                uid=rulebase.uid,
                is_global=self.ImportDetails.MgmDetails.IsSuperManager,
                created=self.ImportDetails.ImportId,
                rules= self.PrepareForImport(self.ImportDetails, rulebase.Rules) if includeRules else { "data": [] }
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
                except:
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
            except:
                errors = 1
                logger.exception(f"failed to remove rules: {str(traceback.format_exc())}")
                return errors, changes, collectedRemovedRuleIds

        return errors, changes, collectedRemovedRuleIds

    # TODO: limit query to a single rulebase
    def GetRuleNumMap(self):
        query = "query getRuleNumMap($mgmId: Int) { rule(where:{mgm_id:{_eq:$mgmId}}) { rule_uid rulebase_id rule_num_numeric } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={"mgmId": self.ImportDetails.MgmDetails.Id})
        except:
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
        except:
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
        except:
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
    #         currentPolicy = [pol for pol in self.NormalizedConfig.rules if pol.uid == rulebaseUid]
    #         if len(currentPolicy)==1:
    #             currentPolicy = currentPolicy.pop()
    #         else:
    #             logger = getFwoLogger()
    #             logger.warning("did not find exactly one policy for rulebaseUid")

    #         for ruleUid in newRuleUids[rulebaseUid]:
    #             rule = currentPolicy.Rules[ruleUid]
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
        except:
            logger = getFwoLogger()
            logger.error(f"error while getting current rulebase: {str(traceback.format_exc())}")
            self.ImportDetails.increaseErrorCounterByOne()
            return
        
        try:
            ruleList = queryResult['data']['rulebase'][0]['rules']
        except:
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

    def insertRulesEnforcedOnGateway(self, ruleIds, devId):
        rulesEnforcedOnGateway = []
        for ruleId in ruleIds:
            rulesEnforcedOnGateway.append({
                "rule_id": ruleId,
                "dev_id": devId,
                "created": self.ImportDetails.ImportId
            })

        query_variables = {
            "ruleEnforcedOnGateway": rulesEnforcedOnGateway
        }
        mutation = """
            mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                    affected_rows
                }
            }"""
        
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


    def PrepareForImport(self, importDetails: ImportState, Rules) -> List[RuleForImport]:
        prepared_rules = []
        for rule in Rules:

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
                rulebase_id=importDetails.lookupRulebaseId(rule.rulebase_name), ## this needs to be fixed: need lookup criteria for rulebase of a rule
                rule_create=importDetails.ImportId,
                rule_last_seen=importDetails.ImportId,
                rule_num_numeric=1,
                action_id = importDetails.lookupAction(rule.rule_action),
                track_id = importDetails.lookupTrack(rule.rule_track),
                rule_head_text=rule.rule_head_text
            ).dict()
            prepared_rules.append(rule_for_import)
        return { "data": prepared_rules }
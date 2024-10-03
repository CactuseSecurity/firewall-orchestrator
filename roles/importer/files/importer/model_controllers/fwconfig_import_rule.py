import traceback
from difflib import ndiff

from models.rule import RuleForImport
from fwoBaseImport import ImportState
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger

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
      self.ActionMap = self.GetActionMap()
      self.TrackMap = self.GetTrackMap()
      self.RuleNumLookup = self.GetRuleNumMap()             # TODO: needs to be updated with each insert
      self.NextRuleNumLookup = self.GetNextRuleNumMap()     # TODO: needs to be updated with each insert
      self.RulebaseMap = self.GetRulebaseMap()     # limited to the current mgm_id

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
            previousPolicyUids.append(policy.Uid)

        # collect policy UIDs of current (just imported) config
        for policy in self.NormalizedConfig.rules:
            currentPolicyUids.append(policy.Uid)

        for rulebaseId in previousPolicyUids:
            if rulebaseId in currentPolicyUids:
                # deal with policies contained both in this and previous config
                currentPolicy = [pol for pol in self.NormalizedConfig.rules if pol.Uid == rulebaseId]
                previousPolicy = [pol for pol in prevConfig.rules if pol.Uid == rulebaseId]

                deletedRuleUids.update({ rulebaseId: list(previousPolicy.Rules.keys() - currentPolicy.Rules.keys()) })
                newRuleUids.update({ rulebaseId: list(currentPolicy.Rules.keys() - previousPolicy.Rules.keys()) })
                ruleUidsInBoth.update({ rulebaseId: list(currentPolicy.Rules.keys() & previousPolicy.Rules.keys()) })
            else:
                logger.info(f"previous rulebase has been deleted: {rulebaseId}")
                deletedRuleUids.update({ rulebaseId: list(previousPolicy.Rules.keys()) })

        # now deal with new rulebases (not contained in previous config)
        for policy in self.NormalizedConfig.rules:
            if policy.Uid not in previousPolicyUids:
                newRuleUids.update({ policy.Uid: list(policy.Rules.keys()) })

        # find changed rules
        # TODO: need to ignore last_hit! 
        for rulebaseId in ruleUidsInBoth:
            changedRuleUids.update({ rulebaseId: [] })
            currentPolicy = [pol for pol in self.NormalizedConfig.rules if pol.Uid == rulebaseId]
            previousPolicy = [pol for pol in prevConfig.rules if pol.Uid == rulebaseId]
            for ruleUid in ruleUidsInBoth[rulebaseId]:
                if self.ruleChanged(rulebaseId, ruleUid, prevConfig):
                    changedRuleUids[rulebaseId].append(ruleUid)

        # changed rules will get the same rule_num_numeric as their previous version?!
        # new rules will be fitted between the respective rules of the previous rulebase
        self.setNewRulesNumbering(prevConfig.rules)

        # update rule diffs
        errorCountAdd, numberOfAddedRules, newRuleIds = self.addNewRules(newRuleUids)
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

    def ruleChanged(self, rulebaseId, ruleUid, prevConfig: FwConfigNormalized):
        # TODO: need to ignore rule_num, last_hit, ...?
        return prevConfig.rules[rulebaseId].Rules[ruleUid] != self.NormalizedConfig.rules[rulebaseId].Rules[ruleUid]
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
    def setNewRulesNumbering(self, previousRules):
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

    def addNewRules(self, newRuleUids):
        logger = getFwoLogger()
        errors = 0
        changes = 0

        newRuleIds = [] # return values
        addRuleMutation = """
            mutation insertRules($rules: [rule_insert_input!]!) {
                insert_rule(objects: $rules) {
                    affected_rows
                    returning {
                        rule_id
                    }
                }
            }
        """
        # newRulebases = self.prepareNewRulebases(newRuleUids)
        newRules = self.prepareNewRules(newRuleUids)

        queryVariables = { 'rules': newRules }
            
        try:
            import_result = self.call(addRuleMutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in addNewRules: {str(import_result['errors'])}")
                return 1, 0, []
            else:
                # reduce change number by number of rulebases
                changes = import_result['data']['insert_rule']['affected_rows']
                if changes>0:
                    for rule in import_result['data']['insert_rule']['returning']:
                        newRuleIds.append(rule['rule_id'])
                else:
                    newRuleIds=[]
        except:
            logger.exception(f"failed to write new rules: {str(traceback.format_exc())}")
            return 1, 0, []
        
        return errors, changes, newRuleIds

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
                    removeResult = self.call(removeMutation, queryVariables=queryVariables)
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
                removeResult = self.call(removeRefsMutation, queryVariables=queryVariables)
                if 'errors' in removeResult:
                    errors = 1
                    logger.exception(f"fwo_api:removeRuleRefs - error while removing rule refs: {str(removeResult['errors'])}")
                    return errors, changes, removedRuleIds
            except:
                errors = 1
                logger.exception(f"failed to remove rules: {str(traceback.format_exc())}")
                return errors, changes, collectedRemovedRuleIds

        return errors, changes, collectedRemovedRuleIds

    def GetActionMap(self):
        query = "query getActionMap { stm_action { action_name action_id } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_action')
            return {}
        
        map = {}
        for action in result['data']['stm_action']:
            map.update({action['action_name']: action['action_id']})
        return map

    def GetTrackMap(self):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for track in result['data']['stm_track']:
            map.update({track['track_name']: track['track_id']})
        return map

    # TODO: limit query to a single rulebase
    def GetRuleNumMap(self):
        query = "query getRuleNumMap($mgmId: Int) { rule(where:{mgm_id:{_eq:$mgmId}}) { rule_uid rulebase_id rule_num_numeric } }"
        try:
            result = self.call(query=query, queryVariables={"mgmId": self.ImportDetails.MgmDetails.Id})
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

    # limited to the current mgm_id
    # creats a dict with key = rulebase.name and value = rulebase.id
    def GetRulebaseMap(self):
        query = """query getRulebaseMap($mgmId: Int) { rulebase(where:{mgm_id: {_eq: $mgmId}}) { id name } }"""
        try:
            result = self.call(query=query, queryVariables= {"mgmId": self.ImportDetails.MgmDetails.Id})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting rulebases')
            return {}
        
        map = {}
        for rulebase in result['data']['rulebase']:
            map.update({rulebase['name']: rulebase['id']})
        return map

    def GetNextRuleNumMap(self):    # TODO: implement!
        query = "query getRuleNumMap { rule { rule_uid rule_num_numeric } }"
        try:
            result = self.call(query=query, queryVariables={})
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
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for track in result['data']['stm_track']:
            map.update({track['track_name']: track['track_id']})
        return map

    def prepareNewRules(self, newRuleUids):
        newRules = []
        for rulebaseUid in newRuleUids:
            currentPolicy = [pol for pol in self.NormalizedConfig.rules if pol.Uid == rulebaseUid]
            if len(currentPolicy)==1:
                currentPolicy = currentPolicy.pop()
            else:
                logger = getFwoLogger()
                logger.warning("did not find exactly one policy for rulebaseUid")

            for ruleUid in newRuleUids[rulebaseUid]:
                newEnrichedRule = RuleForImport(currentPolicy.Rules[ruleUid]) # leave the original dict as is

                # TODO: resolve:
                #   "rule_num": 1, // no - need to handle order otherwise!
                #   "parent_rule_id": null,
                # - parent_rule_uid
                # rulebase_id

                rule_action = newEnrichedRule.get('rule_action', None)    
                rule_action_id = self.lookupAction(rule_action)
                rule_track = newEnrichedRule.get('rule_track', None)     
                rule_track_id = self.lookupTrack(rule_track)

                rule_type = newEnrichedRule.pop('rule_type', None)     # get and remove
                if rule_type == 'access':
                    access_rule = True
                    nat_rule = False
                elif rule_type == 'nat':
                    access_rule = False
                    nat_rule = True
                else:   # mast be both then
                    access_rule = True
                    nat_rule = True

                lastHit = newEnrichedRule.pop('last_hit', None)     # get and remove
                # TODO: write last_hit to rule_metadata

                parentRuleUId = newEnrichedRule.pop('parent_rule_uid', None)     # get and remove
                # TODO: link to parten rule if it is set

                newEnrichedRule.pop('rule_last_change_admin', None)     # ignore this (not used anyway)

                newEnrichedRule.update({
                        'mgm_id': self.ImportDetails.MgmDetails.Id,
                        'rule_create': self.ImportDetails.ImportId,
                        'rule_last_seen': self.ImportDetails.ImportId,    # could be left out
                        'track_id': rule_track_id,
                        'action_id': rule_action_id,
                        'access_rule': access_rule,
                        'nat_rule': nat_rule,
                        'rulebase_id': self.lookupRulebaseId(rulebaseName),
                        'rule_num': 1   # TODO: need to fix this!!!!!!!!!!!!!!!
                    })
                # end of adaption
                    
                newRules.append(newEnrichedRule)
            
        return newRules

    # def prepareNewRulebases(self, newRuleUids):
    #     newRulebases = []
    #     for rulebaseName in newRuleUids:
    #         newRulebase = []
    #         for ruleUid in newRuleUids[rulebaseName]:
    #             newEnrichedRule = self.rules[rulebaseName]['Rules'][ruleUid].copy() # leave the original dict as is

    #             # TODO: resolve:
    #             #   "rule_num": 1, // no - need to handle order otherwise!
    #             #   "parent_rule_id": null,
    #             # - parent_rule_uid
    #             # rulebase_id

    #             rule_action = newEnrichedRule.get('rule_action', None)    
    #             rule_action_id = self.lookupAction(rule_action)
    #             rule_track = newEnrichedRule.get('rule_track', None)     
    #             rule_track_id = self.lookupTrack(rule_track)

    #             rule_type = newEnrichedRule.pop('rule_type', None)     # get and remove
    #             if rule_type == 'access':
    #                 access_rule = True
    #                 nat_rule = False
    #             elif rule_type == 'nat':
    #                 access_rule = False
    #                 nat_rule = True
    #             else:   # mast be both then
    #                 access_rule = True
    #                 nat_rule = True

    #             lastHit = newEnrichedRule.pop('last_hit', None)     # get and remove
    #             # TODO: write last_hit to rule_metadata

    #             parentRuleUId = newEnrichedRule.pop('parent_rule_uid', None)     # get and remove
    #             # TODO: link to parten rule if it is set

    #             newEnrichedRule.pop('rule_last_change_admin', None)     # ignore this (not used anyway)

    #             newEnrichedRule.update({
    #                     'mgm_id': self.ImportDetails.MgmDetails.Id,
    #                     'rule_create': self.ImportDetails.ImportId,
    #                     'rule_last_seen': self.ImportDetails.ImportId,    # could be left out
    #                     'track_id': rule_track_id,
    #                     'action_id': rule_action_id,
    #                     'access_rule': access_rule,
    #                     'nat_rule': nat_rule,
    #                     'rulebase_id': self.lookupRulebaseId(rulebaseName),
    #                     'rule_num': 1   # TODO: need to fix this!!!!!!!!!!!!!!!
    #                 })
    #             # end of adaption

    #             newRulebase.append({ 
    #                 "rule": { "data": newEnrichedRule },
    #                 "mgm_id": self.ImportDetails.MgmDetails.Id
    #             })
                    
    #         newRulebases.append({
    #             "name": rulebaseName,
    #             "mgm_id": self.ImportDetails.MgmDetails.Id,
    #             "created": self.ImportDetails.ImportId,
    #             "is_global": False,
    #             "data": newRulebase
    #         })
            
    #     return newRulebases

    def lookupAction(self, actionStr):
        return self.ActionMap.get(actionStr.lower(), None)

    def lookupTrack(self, trackStr):
        return self.TrackMap.get(trackStr.lower(), None)

    def lookupRulebaseId(self, rulebaseName):
        return self.RulebaseMap.get(rulebaseName, None)

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
            queryResult = self.call(query, queryVariables=query_variables)
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
        return self.call(mutation, queryVariables=query_variables)

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
        
        return self.call(mutation, queryVariables=query_variables)


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
        
        return self.call(mutation, queryVariables=query_variables)

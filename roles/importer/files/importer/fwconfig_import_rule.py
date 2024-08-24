from typing import List
import traceback
from difflib import ndiff

from fwoBaseImport import ImportState
from fwconfig_normalized import FwConfigNormalized
from fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger

# this class is used for importing a config into the FWO API
class FwConfigImportRule(FwConfigImportBase):
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
      super().__init__(importState, config)
      self.ActionMap = self.GetActionMap()
      self.TrackMap = self.GetTrackMap()
      self.RuleNumLookup = self.GetRuleNumMap()
      self.NextRuleNumLookup = self.GetNextRuleNumMap()

    def updateRuleDiffs(self, prevConfig: dict):
        logger = getFwoLogger()
        # calculate rule diffs
        changedRuleUids = {}
        deletedRuleUids = {}
        newRuleUids = {}
        ruleUidsInBoth = {}
        previousRules = prevConfig['rules']
        for rulebaseId in prevConfig['rules']:
            if rulebaseId in self.rules:
                deletedRuleUids.update({ rulebaseId: previousRules[rulebaseId]['Rules'].keys() - self.rules[rulebaseId]['Rules'].keys() })
                newRuleUids.update({ rulebaseId: list(self.rules[rulebaseId]['Rules'].keys() - previousRules[rulebaseId]['Rules'].keys()) })
                ruleUidsInBoth.update({ rulebaseId: self.rules[rulebaseId]['Rules'].keys() & previousRules[rulebaseId]['Rules'].keys() })
            else:
                logger.info(f"previous rulebase does has been deleted: {rulebaseId}")
                deletedRuleUids.update({ rulebaseId: previousRules[rulebaseId]['Rules'].keys() })

        # now deal with new rulebases (not contained in previous config)
        for rulebaseId in self.rules - previousRules.keys():
            newRuleUids.update({ rulebaseId: list(self.rules[rulebaseId]['Rules'].keys()) })

        self.setNewRulesNumbering2(previousRules)
        # self.setNewRulesNumbering(self, newRuleUids)

        # find changed rules
        for rulebaseId in ruleUidsInBoth:
            changedRuleUids.update({ rulebaseId: [] })
            for ruleUid in ruleUidsInBoth[rulebaseId]:
                if prevConfig['rules'][rulebaseId]['Rules'][ruleUid] != self.rules[rulebaseId]['Rules'][ruleUid]:
                    changedRuleUids.update({ rulebaseId: changedRuleUids[rulebaseId].append(ruleUid) })

        # update rule diffs
        # TODO: need to calculate rule position before adding new rules
        errorCountAdd, numberOfAddedRules, newRuleIds = self.addNewRules(newRuleUids)

        # TODO: rule_nwobj_resolved füllen (recert?)

        return errorCountAdd, numberOfAddedRules

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

    def setNewRulesNumbering2(self, previousRules):
        for rulebaseName in previousRules:
            previousUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(previousRules[rulebaseName]['Rules'])
            currentUidList = FwConfigImportRule.ruleDictToOrderedListOfRuleUids(self.rules[rulebaseName]['Rules'])

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
                        new_order_number = current_db_list[-1][1] + order_number_increment
                    else:
                        prev_order_number = current_db_list[db_index-1][1]
                        next_order_number = current_db_list[db_index][1]
                        new_order_number = (prev_order_number + next_order_number) / 2.0
                    
                    # Insert the new uid with the calculated order number
                    # cursor.execute("INSERT INTO list_items (order_number, uid) VALUES (?, ?)", 
                    #             (new_order_number, uid))
                    self.rules[rulebaseName]['Rules'][uid].update( { 'rule_num_numeric':  new_order_number })
                    # Add to current_db_list to keep track of new state
                    current_db_list.insert(db_index, (None, new_order_number, uid))

                elif change_type == 'unchanged':
                    db_index += 1  # Move to the next uid in the current_db_list

            # # Example Usage
            # db_path = 'my_database.db'
            # old_list = ["Item A", "Item B", "Item C"]
            # new_list = ["Item A", "NewItem1", "Item B", "NewItem2", "Item C", "Item D"]

            # update_database_with_changes(db_path, old_list, new_list)


    # def setNewRulesNumbering(self, newRuleUids):
    #     # for each new uid find out the rule's UID before that rule to place it behind that one
    #     for rulebaseId in self.rules:
    #         prevRuleNumberInt = 0
    #         highestRuleNumberInt = self.lookupHighestRuleNumberInt(rulebaseId)
    #         for ruleUid in self.rules[rulebaseId]['Rules']:
    #             if ruleUid in newRuleUids[rulebaseId]:
    #                 if self.rules[rulebaseId]['Rules'][ruleUid]['rule_num'] == 0:   # this is now the first rule
    #                     # newFirstRuleNum = self.ruleNumNumericLookup(ruleUid)-1
    #                     newFirstRuleNum = self.lookupLowestRuleNumber(rulebaseId)/2    # taking rule number from DB
    #                     self.rules[rulebaseId]['Rules'][ruleUid].update( { 'rule_num_numeric': newFirstRuleNum })
    #                     prevRuleNum = newFirstRuleNum
    #                 elif self.rules[rulebaseId]['Rules'][ruleUid]['rule_num'] == 0:   # this is now the first rule
    #                     # newFirstRuleNum = self.ruleNumNumericLookup(ruleUid)-1
    #                     newFirstRuleNum = self.lookupLowestRuleNumber(rulebaseId)/2    # taking rule number from DB
    #                 else:
    #                     prevConfigNextRuleUid = x
    #                     oldNextRuleNum = self.ruleNumNumericLookup(prevConfigNextRuleUid)
    #                     newRuleNum = (prevRuleNum + oldNextRuleNum)/2
    #                     self.rules[rulebaseId]['Rules'][ruleUid].update( { 'rule_num_numeric':  newRuleNum })
    #                     prevRuleNum = newRuleNum

    def addNewRules(self, newRuleUids):
        logger = getFwoLogger()
        errors = 0
        changes = 0

        newRuleIds = [] # return values
        import_mutation = """
            mutation upsertRulebaseWithRules($rulebases: [rulebase_insert_input!]!) {
                insert_rulebase(objects: $rulebases, on_conflict: {constraint: unique_rulebase_mgm_id_name, update_columns: [is_global]}) {
                    affected_rows
                    returning {
                        id
                    }
                }
            }
        """
        queryVariables = { 'rulebases': self.prepareNewRulebases(newRuleUids) }
        
        try:
            import_result = self.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error while adding new nw objects: {str(import_result['errors'])}")
                return 1, 0, []
            else:
                changes = int(import_result['data']['insert_rulebase']['affected_rows'])
                newRuleIds = import_result['data']['insert_rulebase']['returning']
                ids = [item['id'] for item in newRuleIds]
        except:
            logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
            return 1, 0, []
        
        return errors, changes, ids

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

    def GetRuleNumMap(self):
        query = "query getRuleNumMap { rule { rule_uid rule_num_numeric } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for ruleNum in result['data']['rule']:
            map.update({ruleNum['rule_uid']: ruleNum['rule_num_numeric']})
        return map

    def GetNextRuleNumMap(self):    # TODO: implement!
        query = "query getRuleNumMap { rule { rule_uid rule_num_numeric } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_track')
            return {}
        
        map = {}
        for ruleNum in result['data']['rule']:
            map.update({ruleNum['rule_uid']: ruleNum['rule_num_numeric']})
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

    def prepareNewRulebases(self, newRuleUids):
        newRulebases = []
        for rulebaseId in newRuleUids:
            newRulebase = []
            for ruleUid in newRuleUids[rulebaseId]:
                newEnrichedRule = self.rules[rulebaseId]['Rules'][ruleUid].copy() # leave the original dict as is

                # TODO: resolve:
                #   "rule_num": 1, // no - need to handle order otherwise!
                #   "parent_rule_id": null,
                # - parent_rule_uid

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
                        'nat_rule': nat_rule
                    })
                # end of adaption

                newRulebase.append({ 
                    "rule": { "data": newEnrichedRule }
                })
                    
            newRulebases.append({
                "name": rulebaseId,
                "mgm_id": self.ImportDetails.MgmDetails.Id,
                "created": self.ImportDetails.ImportId,
                "is_global": False,
                "rule_to_rulebases": { "data": newRulebase }
            })
            
        return newRulebases

    def lookupAction(self, actionStr):
        return self.ActionMap.get(actionStr.lower(), None)

    def lookupTrack(self, trackStr):
        return self.TrackMap.get(trackStr.lower(), None)

    def getCurrentRules(self, importId, mgmId, rulebaseName):
        query_variables = {
            "importId": importId,
            "mgmId": mgmId,
            "rulebaseName": rulebaseName
        }
        query = """
            query getRulebase($importId: bigint!, $mgmId: Int!, $rulebaseName: String!) {
                rulebase(where: {mgm_id: {_eq: $mgmId}, name: {_eq: $rulebaseName}}) {
                    rule_to_rulebases(where: {rule: {rule_create: {_lt: $importId}, removed: {_is_null: true}, active: {_eq: true}}}, order_by: {rule: {rule_num_numeric: asc}}) {
                        rule {
                            rule_uid
                        }
                    }
                }
            }
        """
        
        try:
            queryResult = self.call(query, queryVariables=query_variables)
        except:
            logger = getFwoLogger()
            logger.error(f"error while getting current rulebase: {queryResult['errors']}")
            self.ImportDetails.setErrorCounter(self.ImportDetails.ErrorCount+1)
            return
        
        try:
            ruleList = queryResult['data']['rulebase'][0]['rule_to_rulebases']
        except:
            logger = getFwoLogger()
            logger.error(f'could not find rules in query result: {queryResult}')
            self.ImportDetails.setErrorCounter(self.ImportDetails.ErrorCount+1)
            return
        
        ruleUids = []
        for rule in ruleList:
            ruleUids.append(rule['rule']['rule_uid'])
        return ruleUids
    
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
        """
            {
                "rulebases":
                [
                    {
                        "name": "cactus_Security3",
                        "mgm_id": self.ImportDetails.MgmDetails.Id,
                        "created": self.ImportDetails.ImportId,
                        "is_global": false,
                        "rule_to_rulebases": {
                            "data": [{ "rule_id": 84779 }],
                            "on_conflict": {
                                "constraint": "rule_to_rulebase_pkey",
                                "update_columns": []  
                            }
                        }
                    }
                ]
            }
    -----------------------------------------------
        {
            "rulebases": [
                {
                "name": "cactus_Security3",
                "mgm_id": 26,
                "created": 123,
                "is_global": false,
                "rule_to_rulebases": {
                    "data": [
                    {
                        "rule": {
                        "data": 
                            {
                            "rule_disabled": true,
                            "rule_src_neg": false,
                            "rule_src": "test-ext-vpn-gw",
                            "rule_src_refs": "a580c5a3-379c-479b-b49d-487faba2442e",
                            "rule_dst_neg": false,
                            "rule_dst": "Barracuda-CC",
                            "rule_dst_refs": "c896cae0-bded-4996-b8cc-6ec3214661d2",
                            "rule_svc_neg": false,
                            "rule_svc": "IPSEC|test-adp_proto_igmp",
                            "rule_svc_refs": "97aeb475-9aea-11d5-bd16-0090272ccb30|c5f5acc0-5e92-4751-962c-6f8821cdffa6",
                            "rule_action": "accept",
                            "rule_track": "log",
                            "rule_installon": "Policy Targets",
                            "rule_time": "Any",
                            "rule_name": null,
                            "rule_uid": "4b03cdb6-6209-4506-91ea-77403bda9dad",
                            "rule_custom_fields": "",
                            "rule_implied": false,
                            "access_rule": true,
                            "rule_comment": "cooment with apostrophes .,,j",
                            "rule_num": 1,
                            "mgm_id": 26,
                            "parent_rule_id": null,
                            "track_id": 1,
                            "action_id": 2,
                            "rule_create": 123,
                            "rule_last_seen": 123
                            }
                        }
                    }
                    ],
                    "on_conflict": {
                    "constraint": "rule_to_rulebase_pkey",
                    "update_columns": []
                    }
                }
                }
            ]
        }                    
        """
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
                        rule_to_rulebases {
                            rule_id
                            rulebase_id
                        }
                    }
                }
            }
        """
        return self.call(mutation, queryVariables=query_variables)

        """
        query getRulebases {
            rulebase {
                id
                name
                mgm_id
                is_global
                rulebase_on_gateways(order_by: {order_no: asc}) {
                    dev_id
                    rulebase_id
                    order_no
                }
                rule_to_rulebases {
                    rule {
                        rule_uid
                    }
                }
            }
        }

        mutation upsertRulebase($rulebases: [rulebase_insert_input!]!) {
            insert_rulebase(objects: $rulebases, on_conflict: {constraint: unique_rulebase_mgm_id_name, update_columns: [name, is_global]}) {
                returning {
                    id
                }
                affected_rows
            }
        }
        
        {
            "rulebases": [
                {
                "name": "cactus_new",
                "mgm_id": 26,
                "is_global": false,
                "rulebase_on_gateways": {"data": [
                    {
                        "dev_id": 1,
                        "order_no": 3
                    },
                    {
                        "dev_id": 2,
                        "order_no": 4
                    }
                ]},
                "rule_to_rulebases": {
                    "data": [
                        {
                            "rule_id": 24
                        },
                        {
                            "rule_id": 25
                        }
                    ]
                    }
                }
            ]
        }


        """


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

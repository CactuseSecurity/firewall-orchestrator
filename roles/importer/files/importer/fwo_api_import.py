# library containing FWO API calls for new import (8/2024)
import requests.packages
import requests

from fwo_api import call


def importInsertRulebase(importState, ruleBaseName, isGlobal=False):
    # call for each rulebase to add
    query_variables = {
        "rulebase": {
            "is_global": isGlobal,
            "mgm_id": importState.MgmDetails.Id,
            "name": ruleBaseName,
            "created": importState.ImportId
        }
    }
    mutation = """
        mutation insertRulebase($rulebase: [rulebase_insert_input!]!) {
            insert_rulebase(objects: $rulebase) {
                returning {id}
            }
        }"""
    return call(importState.FwoConfig.FwoApiUri, importState.Jwt, mutation, query_variables=query_variables, role='importer')


def importInsertRulesEnforcedOnGateway(importState, ruleIds, devId):
    rulesEnforcedOnGateway = []
    for ruleId in ruleIds:
        rulesEnforcedOnGateway.append({
            "rule_id": ruleId,
            "dev_id": devId,
            "created": importState.ImportId
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
    
    return call(importState.FwoConfig.FwoApiUri, importState.Jwt, mutation, query_variables=query_variables, role='importer')


def importInsertRulebaseOnGateway(importState, rulebaseId, devId, orderNo=0):
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
    
    return call(importState.FwoConfig.FwoApiUri, importState.Jwt, mutation, query_variables=query_variables, role='importer')

'action_id'
def resolveRuleRefs(importState, rule2Import, refLists):
    actionId = refLists['action'][rule2Import['action']]
    # ...
    rule = Rule(actionId=actionId)

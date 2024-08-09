# library containing FWO API calls for new import (8/2024)
import requests.packages
import requests

from fwo_log import getFwoLogger
from fwo_api import call
import traceback

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


def importLatestConfig(importState, config):
    logger = getFwoLogger()
    import_mutation = """
        mutation importLatestConfig($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
            insert_latest_config(objects: {import_id: $importId, mgm_id: $mgmId, config: $config}) {
                affected_rows
            }
        }
    """
    try:
        queryVariables = {
            'mgmId': importState.MgmDetails.Id,
            'importId': importState.ImportId,
            'config': config
        }
        import_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, import_mutation,
                             query_variables=queryVariables, role='importer')
        if 'errors' in import_result:
            logger.exception("fwo_api:import_latest_config - error while writing importable config for mgm id " +
                              str(importState.MgmDetails.Id) + ": " + str(import_result['errors']))
            return 1 # error
        else:
            changes = import_result['data']['insert_latest_config']['affected_rows']
    except:
        logger.exception(f"failed to write latest normalized config for mgm id {str(importState.MgmDetails.Id)}: {str(traceback.format_exc())}")
        return 1 # error
    
    if changes==1:
        return 0
    else:
        return 1
    

def deleteLatestConfig(importState):
    logger = getFwoLogger()
    import_mutation = """
        mutation deleteLatestConfig($mgmId: Int!) {
            delete_latest_config(where: { mgm_id: {_eq: $mgmId} }) {
                affected_rows
            }
        }
    """
    try:
        queryVariables = { 'mgmId': importState.MgmDetails.Id }
        import_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, import_mutation,
                             query_variables=queryVariables, role='importer')
        if 'errors' in import_result:
            logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                              str(importState.MgmDetails.Id) + ": " + str(import_result['errors']))
            return 1 # error
        else:
            changes = import_result['data']['delete_latest_config']['affected_rows']
    except:
        logger.exception(f"failed to delete latest normalized config for mgm id {str(importState.MgmDetails.Id)}: {str(traceback.format_exc())}")
        return 1 # error
    
    if changes<=1:  # if nothing was changed, we are also happy (assuming this to be the first config of the current management)
        return 0
    else:
        return 1

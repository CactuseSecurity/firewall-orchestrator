from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from models.rulebase_link import RulebaseLinkUidBased, RulebaseLink
from models.fwconfig_normalized import FwConfigNormalized

class RulebaseLinkUidBasedController(RulebaseLinkUidBased):

    def getDiffs(importState: ImportState, previousConfig: FwConfigNormalized, rulebaseLinks: RulebaseLinkUidBased) -> RulebaseLink:

        # compare with previous config
        # if previous config does not exist, create all links
        if previousConfig is None:
            #  
            return rulebaseLinks
        # rbLinks = RulebaseLink()

        logger = getFwoLogger()
        query = "query getRulebaseLinks($mgmId: Int!) { latest_config(where: {mgm_id: {_eq: $mgmId}}) { config } }"
        queryVariables = { 'mgmId': importState.MgmDetails.Id }
        try:
            queryResult = self.ImportDetails.call(query, queryVariables=queryVariables)
            if 'errors' in queryResult:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(queryResult['errors']))
                return 1 # error
            else:
                if len(queryResult['data']['latest_config'])>0: # do we have a prev config?
                    # prevConfigDict = json.loads(queryResult['data']['latest_config'][0]['config'])
                    prevConfig = FwConfigNormalized.parse_raw(queryResult['data']['latest_config'][0]['config'])
                else:
                    prevConfigDict = {
                        'action': ConfigAction.INSERT,
                        'network_objects': {},
                        'service_objects': {},
                        'users': {},
                        'zone_objects': {},
                        'rules': [],
                        'gateways': [],
                        'ConfigFormat': ConfFormat.NORMALIZED_LEGACY
                    }
                    prevConfig = FwConfigNormalized(**prevConfigDict)
                return prevConfig
        except:
            logger.exception(f"failed to get latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise Exception(f"error while trying to get the previous config")



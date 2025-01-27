import requests
import traceback

#from fwo_globals import verify_certs, suppress_cert_warnings, debug_level
from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from roles.importer.files.importer.model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from fwo_base import ConfigAction, ConfFormat

# this class is used for importing a config into the FWO API
class FwConfigImportBase():
    ImportDetails: ImportState
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        self.NormalizedConfig = config

    # return previous config or empty config if there is none
    def getPreviousConfig(self) -> FwConfigNormalized:
        logger = getFwoLogger()
        query = "query getLatestConfig($mgmId: Int!) { latest_config(where: {mgm_id: {_eq: $mgmId}}) { config } }"
        queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
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


    # def isInitialImport(self):
    #     query = """
    #         query isInitialImport($mgmId: Int!) {
    #             import_control_aggregate(where: {mgm_id: {_eq: $mgmId}, successful_import: {_eq: true}}) {
    #                 imports: aggregate {
    #                 count
    #                 }
    #             }
    #         }
    #     """
    #     queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }

    #     try:
    #         result = self.ImportDetails.call(query=query, queryVariables=queryVariables)
    #         if result['data']['import_control_aggregate']['imports']['count']==0:
    #             return True
    #         else:
    #             return False
    #     except:
    #         logger = getFwoLogger()
    #         logger.error(f'Error while getting imports count')

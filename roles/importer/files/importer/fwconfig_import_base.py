import requests.packages
import requests
import json
import traceback

import fwo_globals
#from fwo_globals import verify_certs, suppress_cert_warnings, debug_level
from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from fwconfig_normalized import FwConfigNormalized
from fwo_const import fwo_api_http_import_timeout, import_tmp_path
from fwo_exception import FwoApiServiceUnavailable, FwoApiTimeout
from fwo_base import ConfigAction

# this class is used for importing a config into the FWO API
class FwConfigImportBase(FwConfigNormalized):
    ImportDetails: ImportState
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        super().__init__(action=config.action,
                         network_objects=config.network_objects,
                         service_objects=config.service_objects,
                         users=config.users,
                         zone_objects=config.zone_objects,
                         rules=config.rules,
                         gateways=config.gateways,
                         ConfigFormat=config.ConfigFormat)

    # standard FWO API call
    def call(self, query, queryVariables=""):
        role = 'importer'
        request_headers = { 
            'Content-Type': 'application/json', 
            'Authorization': f'Bearer {self.FwoJwt}', 
            'x-hasura-role': role 
        }
        full_query = {"query": query, "variables": queryVariables}
        logger = getFwoLogger()

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else: 
                session.verify = fwo_globals.verify_certs
            session.headers = request_headers

            try:
                r = session.post(self.FwoApiUrl, data=json.dumps(full_query), timeout=int(fwo_api_http_import_timeout))
                r.raise_for_status()
            except requests.exceptions.RequestException:
                logger.error(self.showImportApiCallInfo(full_query, request_headers, typ='error') + ":\n" + str(traceback.format_exc()))
                if r != None:
                    if r.status_code == 503:
                        raise FwoApiServiceUnavailable("FWO API HTTP error 503 (FWO API died?)" )
                    if r.status_code == 502:
                        raise FwoApiTimeout("FWO API HTTP error 502 (might have reached timeout of " + str(int(fwo_api_http_import_timeout)/60) + " minutes)" )
                else:
                    raise
            if int(fwo_globals.debug_level) > 8:
                logger.debug (self.showImportApiCallInfo(self.FwoApiUrl, full_query, request_headers, typ='debug'))
            if r != None:
                return r.json()
            else:
                return None

    def showImportApiCallInfo(self, query, headers, typ='debug'):
        max_query_size_to_display = 1000
        query_string = json.dumps(query, indent=2)
        header_string = json.dumps(headers, indent=2)
        query_size = len(query_string)

        if typ=='error':
            result = "error while sending api_call to url "
        else:
            result = "successful FWO API call to url "        
        result += str(self.FwoApiUrl) + " with payload \n"
        if query_size < max_query_size_to_display:
            result += query_string 
        else:
            result += str(query)[:round(max_query_size_to_display/2)] +   "\n ... [snip] ... \n" + \
                query_string[query_size-round(max_query_size_to_display/2):] + " (total query size=" + str(query_size) + " bytes)"
        result += "\n and  headers: \n" + header_string
        return result

    # return previous config or empty config if there is none
    def getPreviousConfig(self) -> FwConfigNormalized:
        logger = getFwoLogger()
        query = """
          query getLatestConfig($mgmId: Int!) { latest_config(where: {mgm_id: {_eq: $mgmId}}) { config } }
        """
        queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
        try:
            queryResult = self.call(query, queryVariables=queryVariables)
            if 'errors' in queryResult:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(queryResult['errors']))
                return 1 # error
            else:
                if len(queryResult['data']['latest_config'])>0:
                    return queryResult['data']['latest_config'][0]['config']
                    # return FwConfigNormalized(ConfigAction.INSERT, json.loads(queryResult['data']['latest_config'][0]['config']))
                else:
                    # if we do not get a previous config, simply return empty config
                    return FwConfigNormalized(action=ConfigAction.INSERT)
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
    #         result = self.call(query=query, queryVariables=queryVariables)
    #         if result['data']['import_control_aggregate']['imports']['count']==0:
    #             return True
    #         else:
    #             return False
    #     except:
    #         logger = getFwoLogger()
    #         logger.error(f'Error while getting imports count')

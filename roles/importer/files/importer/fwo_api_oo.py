import requests.packages
import requests
import json
import traceback

import fwo_globals
from fwo_log import getFwoLogger
from fwo_const import fwo_api_http_import_timeout
from fwo_exception import FwoApiServiceUnavailable, FwoApiTimeout

# this class is used for making calls to the FWO API (will supersede fwo_api.py)
class FwoApi():
    
    FwoApiUrl: str
    FwoJwt: str


    def __init__(self, ApiUri, Jwt):
        self.FwoApiUrl = ApiUri
        self.FwoJwt = Jwt

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

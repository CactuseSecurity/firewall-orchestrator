import requests.packages
import requests
import json
import traceback
import time
from pprint import pformat

import fwo_globals
from fwo_log import getFwoLogger
from fwo_const import fwo_api_http_import_timeout
from fwo_exceptions import FwoApiServiceUnavailable, FwoApiTimeout
from query_analyzer import QueryAnalyzer
from fwo_exceptions import FwoImporterError


# this class is used for making calls to the FWO API (will supersede fwo_api.py)
class FwoApi():
    
    FwoApiUrl: str
    FwoJwt: str
    query_info: dict
    query_analyzer: QueryAnalyzer


    def __init__(self, ApiUri, Jwt):
        self.FwoApiUrl = ApiUri
        self.FwoJwt = Jwt
        self.query_info = {}
        self.query_analyzer = QueryAnalyzer()


    def call(self, query, queryVariables={}, debug_level=0, analyze_payload=False) -> dict:
        """
            The standard FWO API call.
        """

        role = 'importer'
        request_headers = { 
            'Content-Type': 'application/json', 
            'Authorization': f'Bearer {self.FwoJwt}', 
            'x-hasura-role': role 
        }
        full_query = {"query": query, "variables": queryVariables}
        logger = getFwoLogger(debug_level=debug_level)

        if analyze_payload:
            # self._analyze_payload(query, queryVariables)
            self.query_info = self.query_analyzer.analyze_payload(query, queryVariables)

        try: 
            with requests.Session() as session:
                if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
                    session.verify = False
                else: 
                    session.verify = fwo_globals.verify_certs
                session.headers = request_headers

                if analyze_payload and self.query_info["chunking_info"]["needs_chunking"]:
                    started = time.time()
                    return_object = self._call_chunked(session, query, queryVariables, debug_level)
                    elapsed_time = time.time() - started
                    affected_rows = 0
                    if 'data' in return_object.keys():
                        # If the return object contains data, we can log the affected rows.
                        if 'affected_rows' in return_object['data'].keys():
                            affected_rows = sum(obj["affected_rows"] for obj in return_object["data"].values())
                    logger.debug(f"Chunked API call ({self.query_info['query_name']}) processed in {elapsed_time:.4f} s. Affected rows: {affected_rows}.")
                    self.query_info = {}
                else:
                    return_object = self._post_query(session, full_query)

                if int(fwo_globals.debug_level) > 8:
                    logger.debug (self.showImportApiCallInfo(self.FwoApiUrl, full_query, request_headers, typ='debug'))

                return return_object

        except requests.exceptions.RequestException as e: 
            self._handle_request_exception(e, full_query, request_headers)
        except FwoImporterError as e:
            # Handle FwoImporterError specifically, logging it and re-raising.
            logger.error(f"FwoImporterError during API call: {str(e)}")
            raise
        except Exception as e:
            # Catch all other exceptions and log them.
            logger.error(f"Unexpected error during API call: {str(e)}")

            if int(fwo_globals.debug_level) > 8:
                logger.debug(pformat(return_object))
                logger.debug(pformat(self.query_info))

            raise FwoImporterError(f"Unexpected error during API call: {str(e)}")

    
    def _handle_request_exception(self, exception, query_payload, headers):
        """
            Error handling for the standard API call.
        """

        logger = getFwoLogger(debug_level=int(fwo_globals.debug_level))

        if int(fwo_globals.debug_level) > 1:
            logger.error(self.showImportApiCallInfo(self.FwoApiUrl, query_payload, headers, typ='error') + ":\n" + str(traceback.format_exc()))
        if hasattr(exception, 'response') and exception.response is not None:
            if exception.response.status_code == 503:
                raise FwoApiServiceUnavailable("FWO API HTTP error 503 (FWO API died?)")
            elif exception.response.status_code == 502:
                raise FwoApiTimeout(f"FWO API HTTP error 502 (might have reached timeout of {int(fwo_api_http_import_timeout)/60} minutes)")
        raise exception


    def _call_chunked(self, session, query, query_variables="", debug_level=0):
        """
            Splits a defined query variable into chunks and posts the queries chunk by chunk.
        """

        chunk_number = 1
        total_processed_elements = 0
        return_object = {}
        logger = getFwoLogger(debug_level=debug_level)
        logger.info(f"Processing chunked API call ({self.query_info['query_name']})...")

        # Separate chunkable variables.

        chunkable_variables = {
            variable: list_object
            for variable, list_object in query_variables.items()
            if variable in list(self.query_info["chunking_info"]["chunkable_variables"])
        }

        # Loops until all elements of the the query variable have been processed.

        while(total_processed_elements < self.query_info["chunking_info"]["total_elements"]):
            
            # Updates query variables to the current chunks data.

            self.query_info["chunking_info"]["adjusted_chunk_size"] = self.query_analyzer.get_adjusted_chunk_size(chunkable_variables)

            if fwo_globals.debug_level > 8:
                logger.debug(f"Chunk {chunk_number}:  Chunk size adjusted\n{self.query_info['chunking_info']['adjusted_chunk_size']}")

            total_chunk_elements = self._update_query_variables_by_chunk(query_variables, chunkable_variables)

            if fwo_globals.debug_level > 8:
                logger.debug(f"Chunk {chunk_number}:  Query variables updated\n{pformat(query_variables)}")

            # Post query.

            response = self._post_query(session, {"query": query, "variables": query_variables})

            if fwo_globals.debug_level > 8:
                logger.debug(f"Chunk {chunk_number}:  Query posted")

            # Gather and merge returning data.

            return_object = self._handle_chunked_calls_response(return_object, response)

            # Log current state of the process and increment variables.

            total_processed_elements += total_chunk_elements
            logger.debug(f"Chunk {chunk_number}: {total_processed_elements}/{self.query_info['chunking_info']['total_elements']} processed elements.")
            chunk_number += 1

        return return_object


    def _update_query_variables_by_chunk(self, query_variables, chunkable_variables):
            chunks = {}
            total_chunk_elements = 0

            for variable, list_object in chunkable_variables.items():
                chunks[variable] = list_object[:self.query_info["chunking_info"]["adjusted_chunk_size"]]
                chunkable_variables[variable] = list_object[self.query_info["chunking_info"]["adjusted_chunk_size"]:]

            for variable, chunk in chunks.items():
                query_variables[variable] = chunk
                total_chunk_elements += len(chunk)

            return total_chunk_elements


    def _handle_chunked_calls_response(self, return_object, response):
        logger = getFwoLogger(debug_level=int(fwo_globals.debug_level))

        if return_object == {}:

            if fwo_globals.debug_level > 8:
                logger.debug(f"Return object is empty, initializing with response data: {pformat(response)}")

            return response
        
        if 'errors' in response:
            error_txt = f"encountered error while handling chunked call: {str(response['errors'])}"
            logger.error(error_txt)
            raise FwoImporterError(error_txt)
        
        for new_return_object_type, new_return_object in response["data"].items():
            if 'data' in return_object.keys():
                self._handle_chunked_calls_response_with_return_data(return_object, new_return_object_type, new_return_object)
            else:
                if 'affected_rows' not in new_return_object:
                    logger.warning(f"no data found: {return_object} not found in return_object['data'].")
                else:
                    if new_return_object["affected_rows"] == 0:
                        logger.warning(f"no data found: {new_return_object} not found in return_object['data'].")
        return return_object

    def _handle_chunked_calls_response_with_return_data(self, return_object, new_return_object_type, new_return_object):
        logger = getFwoLogger(debug_level=int(fwo_globals.debug_level))

        if not isinstance(return_object["data"].get(new_return_object_type), dict):

            if int(fwo_globals.debug_level) > 8:
                logger.debug(f"Initializing return_object['data']['{new_return_object_type}'] as an empty dict.")

            return_object["data"][new_return_object_type] = {}
            return_object["data"][new_return_object_type]["affected_rows"] = 0
            return_object["data"][new_return_object_type]["returning"] = []
        return_object["data"][new_return_object_type]["affected_rows"] += new_return_object["affected_rows"]
        if "returning" in return_object["data"][new_return_object_type].keys():

            if int(fwo_globals.debug_level) > 8:
                logger.debug(f"Extending return_object['data']['{new_return_object_type}']['returning'] with new data: {pformat(new_return_object['returning'])}")

            return_object["data"][new_return_object_type]["returning"].extend(new_return_object["returning"])

    def _post_query(self, session, query_payload):
        """
            Posts the given payload to the api endpoint. Returns the response as json or None if the response object is None.
        """

        logger = getFwoLogger(debug_level=int(fwo_globals.debug_level))

        if int(fwo_globals.debug_level) > 8:
            logger.debug (self.showImportApiCallInfo(self.FwoApiUrl, query_payload, session.headers, typ='debug', show_query_info=True))

        r = session.post(self.FwoApiUrl, data=json.dumps(query_payload), timeout=int(fwo_api_http_import_timeout))
        
        if int(fwo_globals.debug_level) > 9:
            logger.debug ("API response: " + pformat(r.json(), indent=2))

        r.raise_for_status()

        return r.json() if r is not None else None


    def showImportApiCallInfo(self, api_url, query, headers, typ='debug', show_query_info=False):
        max_query_size_to_display = 1000
        query_string = json.dumps(query, indent=2)
        header_string = json.dumps(headers, indent=2)
        api_url = json.dumps(api_url, indent=2)
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
        result += "\n and  headers: \n" + header_string + ", api_url: " + api_url

        if show_query_info and self.query_info:
            result += "\nQuery Info: \n" + pformat(self.query_info)

        return result


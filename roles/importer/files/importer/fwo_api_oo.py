import requests.packages
import requests
import json
import traceback
import copy
import time

import fwo_globals
from fwo_log import getFwoLogger
from fwo_const import fwo_api_http_import_timeout, api_call_chunk_size
from fwo_exceptions import FwoApiServiceUnavailable, FwoApiTimeout
from fwo_base import ChunkableVariableType

# this class is used for making calls to the FWO API (will supersede fwo_api.py)
class FwoApi():
    
    FwoApiUrl: str
    FwoJwt: str


    def __init__(self, ApiUri, Jwt):
        self.FwoApiUrl = ApiUri
        self.FwoJwt = Jwt


    def call(self, query, queryVariables="", chunkable_variable="", query_name="", return_object_name="", debug_level=0):
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
        is_chunked_call, chunkable_variable_type, total_elements = self._check_is_chunkable_call(queryVariables, chunkable_variable)
        started = time.time()

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else: 
                session.verify = fwo_globals.verify_certs
            session.headers = request_headers

            try: 
                if is_chunked_call:
                    return_object = self._call_chunked(session, query, queryVariables, chunkable_variable, query_name, return_object_name, debug_level, chunkable_variable_type, total_elements)
                    elapsed_time = time.time() - started
                    logger.debug(f"Chunked API call ({query_name}) processed in {elapsed_time:.4f} s.")
                else:
                    return_object = self._post_query(session, full_query)

                if int(fwo_globals.debug_level) > 8:
                    logger.debug (self.showImportApiCallInfo(self.FwoApiUrl, full_query, request_headers, typ='debug'))

                return return_object

            except requests.exceptions.RequestException as e: 
                self._handle_request_exception(e, full_query, request_headers)



    
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


    def _check_is_chunkable_call(self, query_variables, chunkable_variable):
        """
            Evaluates the type of the given chunkable_variable and the need for chunking (depending on the number of elements within the chunkable variable).
        """

        is_chunkable_call = False
        chunkable_variable_type = ChunkableVariableType.DEFAULT
        total_elements = 0

        if chunkable_variable != "":

            # Check if call qualifies as standard chunked call.

            if isinstance(chunkable_variable, str):
                chunkable_variable_type = ChunkableVariableType.STRING
                total_elements = len(query_variables[chunkable_variable])

            # Check if call qualifies as chunked multi-table call.

            elif isinstance(chunkable_variable, list) and all(isinstance(item, tuple) for item in chunkable_variable):
                chunkable_variable_type = ChunkableVariableType.LIST_OF_TUPLES
                referenced_update_args = chunkable_variable[0][1]
                total_elements = len(referenced_update_args)
            
            else:
                chunkable_variable_type = ChunkableVariableType.UNKNOWN
            
            if total_elements > api_call_chunk_size:
                is_chunkable_call = True

        return is_chunkable_call, chunkable_variable_type, total_elements
        

    def _call_chunked(self, session, query, query_variables="", chunkable_variable="", query_name="", return_object_name="", debug_level=0, chunkable_variable_type=ChunkableVariableType.DEFAULT, total_elements=0):
        """
            Splits a defined query variable into chunks and posts the queries chunk by chunk.
        """

        chunk_number = 1
        obj_count = 0
        return_object = {}
        chunked_query_variables = copy.deepcopy(query_variables)
        logger = getFwoLogger(debug_level=debug_level)
        logger.debug(f"Processing chunked API call ({query_name})...")

        # Loops until all elements of the the query variable have been processed.
        while(obj_count < chunk_number * api_call_chunk_size and obj_count < total_elements):
            # Gets current chunk and sets it as query variable
            chunk = []

            if chunkable_variable_type == ChunkableVariableType.STRING:
                chunk = query_variables[chunkable_variable][obj_count : obj_count + api_call_chunk_size]
                chunked_query_variables[chunkable_variable] = chunk
            elif chunkable_variable_type == ChunkableVariableType.LIST_OF_TUPLES:
                update_args = chunkable_variable[0][1]
                chunk = update_args[obj_count : obj_count + api_call_chunk_size]
                for key in chunked_query_variables.keys():
                    chunked_query_variables[key] = chunk
            else:
                raise NotImplementedError()

            # Post query.
            response = self._post_query(session, {"query": query, "variables": chunked_query_variables})
            # Gather and merge returning data.
            if return_object == {}:
                return_object = response
            else:
                new_return = response
                if chunkable_variable_type != ChunkableVariableType.LIST_OF_TUPLES:
                    return_object["data"][return_object_name]["returning"].extend(new_return["data"][return_object_name]["returning"])
                    return_object["data"][return_object_name]["affected_rows"] += new_return["data"][return_object_name]["affected_rows"]
            # Log current state of the process and increment variables.
            obj_count += len(chunk)
            logger.debug(f"Chunk nr: {chunk_number}; Total nr of processed elements: {obj_count}")
            chunk_number += 1

        return return_object


    def _post_query(self, session, query_payload):
        """
            Posts the given payload to the api endpoint. Returns the response as json or None if the response object is None.
        """

        r = session.post(self.FwoApiUrl, data=json.dumps(query_payload), timeout=int(fwo_api_http_import_timeout))
        r.raise_for_status()

        return r.json() if r is not None else None


    def showImportApiCallInfo(self, api_url, query, headers, typ='debug'):
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
        return result
    

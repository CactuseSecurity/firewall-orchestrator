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

    # standard FWO API call
    def call(self, query, queryVariables="", chunkable_variable="", query_name="", return_object_name="", debug_level=0):

        def call_chunked(self, query, queryVariables="", chunkable_variable="", query_name="", return_object_name="", debug_level=0, chunkable_variable_type=ChunkableVariableType.DEFAULT, total_elements=0):
            """
                Splits a defined query variable into chunks and posts the queries chunk by chunk.
            """
            chunk_number = 1
            obj_count = 0
            return_object = {}
            chunked_query_variables = copy.deepcopy(queryVariables)
            logger = getFwoLogger(debug_level=debug_level)
            logger.debug(f"Processing chunked API call ({query_name})...")

            # Loops until all elements of the the query variable have been processed.
            while(obj_count < chunk_number * api_call_chunk_size and obj_count < total_elements):
                # Gets current chunk and sets it as query variable
                chunk = []

                if chunkable_variable_type == ChunkableVariableType.STRING:
                    chunk = queryVariables[chunkable_variable][obj_count : obj_count + api_call_chunk_size]
                    chunked_query_variables[chunkable_variable] = chunk
                elif chunkable_variable_type == ChunkableVariableType.LIST_OF_TUPLES:
                    update_args = chunkable_variable[0][1]
                    chunk = update_args[obj_count : obj_count + api_call_chunk_size]
                    for key in chunked_query_variables.keys():
                        chunked_query_variables[key] = chunk
                else:
                    raise NotImplementedError()

                # Post query.
                r = session.post(self.FwoApiUrl, data=json.dumps({"query": query, "variables": chunked_query_variables}), timeout=int(fwo_api_http_import_timeout))
                r.raise_for_status()
                # Gather and merge returning data.
                if return_object == {}:
                    return_object = r.json()
                else:
                    new_return = r.json()
                    if chunkable_variable_type != ChunkableVariableType.LIST_OF_TUPLES:
                        return_object["data"][return_object_name]["returning"].extend(new_return["data"][return_object_name]["returning"])
                        return_object["data"][return_object_name]["affected_rows"] += new_return["data"][return_object_name]["affected_rows"]
                # Log current state of the process and increment variables.
                obj_count += len(chunk)
                logger.debug(f"Chunk nr: {chunk_number}; Total nr of processed elements: {obj_count}")
                chunk_number += 1

            return return_object
        
        def check_is_chunkable_call(self, chunkable_variable):
            is_chunkable_call = False
            chunkable_variable_type = ChunkableVariableType.DEFAULT
            total_elements = 0

            if chunkable_variable != "":

                # Check if call qualifies as standard chunked call.

                if isinstance(chunkable_variable, str):
                    chunkable_variable_type = ChunkableVariableType.STRING
                    total_elements = len(queryVariables[chunkable_variable])

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


        role = 'importer'
        request_headers = { 
            'Content-Type': 'application/json', 
            'Authorization': f'Bearer {self.FwoJwt}', 
            'x-hasura-role': role 
        }
        full_query = {"query": query, "variables": queryVariables}
        logger = getFwoLogger(debug_level=debug_level)
        is_chunked_call, chunkable_variable_type, total_elements = check_is_chunkable_call(self, chunkable_variable)
        started = time.time()

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else: 
                session.verify = fwo_globals.verify_certs
            session.headers = request_headers

            try: 
                if is_chunked_call:
                    return_object = call_chunked(self, query, queryVariables, chunkable_variable, query_name, return_object_name, debug_level, chunkable_variable_type, total_elements)
                    elapsed_time = time.time() - started
                    logger.debug(f"Chunked API call ({query_name}) processed in {elapsed_time:.4f} s.")
                else:
                    r = session.post(self.FwoApiUrl, data=json.dumps(full_query), timeout=int(fwo_api_http_import_timeout))
                    r.raise_for_status()

            except requests.exceptions.RequestException:
                if int(fwo_globals.debug_level) > 1:
                    logger.error(self.showImportApiCallInfo(self.FwoApiUrl, full_query, request_headers, typ='error') + ":\n" + str(traceback.format_exc()))
                if r != None:
                    if r.status_code == 503:
                        raise FwoApiServiceUnavailable("FWO API HTTP error 503 (FWO API died?)" )
                    if r.status_code == 502:
                        raise FwoApiTimeout("FWO API HTTP error 502 (might have reached timeout of " + str(int(fwo_api_http_import_timeout)/60) + " minutes)" )
                else:
                    raise
            if int(fwo_globals.debug_level) > 8:
                logger.debug (self.showImportApiCallInfo(self.FwoApiUrl, full_query, request_headers, typ='debug'))
            if is_chunked_call:
                return return_object
            elif r != None:
                    return r.json()
            else:
                return None

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
    
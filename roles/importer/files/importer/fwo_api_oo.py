import requests.packages
import requests
import json
import traceback
import copy
import time
import re

import fwo_globals

from fwo_log import getFwoLogger
from fwo_const import fwo_api_http_import_timeout, api_call_chunk_size
from fwo_exceptions import FwoApiServiceUnavailable, FwoApiTimeout


# this class is used for making calls to the FWO API (will supersede fwo_api.py)
class FwoApi():
    
    FwoApiUrl: str
    FwoJwt: str
    query_info: dict


    def __init__(self, ApiUri, Jwt):
        self.FwoApiUrl = ApiUri
        self.FwoJwt = Jwt
        self.query_info = {}


    def call(self, query, queryVariables="", debug_level=0, analyze_payload=False):
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
            self._analyze_payload(query, queryVariables)

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else: 
                session.verify = fwo_globals.verify_certs
            session.headers = request_headers

            try: 
                if analyze_payload and self.query_info["chunking_info"]["needs_chunking"]:
                    started = time.time()
                    return_object = self._call_chunked(session, query, queryVariables, debug_level)
                    elapsed_time = time.time() - started
                    affected_rows = sum(obj["affected_rows"] for obj in return_object["data"].values())
                    logger.debug(f"Chunked API call ({self.query_info["query_name"]}) processed in {elapsed_time:.4f} s. Affected rows: {affected_rows}.")
                    self.query_info = {}
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

    def _analyze_payload(self, query, query_variables):
        """
            Analyzes the query and the query_variables and writes the findings to 'self.query_info'.
        """

        query_string = query
        query_length = len(query_string)

        # Parse query type and name from query string.

        query_string_split = query_string.strip().split()
        query_type = query_string_split[0] if query_string_split[0] in ("query", "mutation", "subscription") else "unknown"
        query_name_regex_result = re.match(r"[A-Za-z]+", query_string_split[1])
        query_name = query_name_regex_result.group(0) if query_name_regex_result else ""

        # Parse args from query string.       
        
        query_args = {}
        query_string_substring = query_string.removeprefix(f"{query_type} {query_name}")
        query_args_regex_result = re.match(r"\(([^)]*)\)", query_string_substring)
        query_args_string = query_args_regex_result.group(1) if query_args_regex_result else ""
        query_args_string_split = re.split(r'[,\n]', query_args_string)
        
        for query_args_string_split_element in query_args_string_split:
            element_split = query_args_string_split_element.strip().split()
            arg_name = element_split[0].removeprefix("$").removesuffix(":")
            arg_type = element_split[1]
            query_args[arg_name] = arg_type

        # Parse chunking info.

        needs_chunking, adjusted_chunk_size, list_elements_length, chunkable_variables = self._get_chunking_info(query_variables)
        chunking_info = {
            "needs_chunking": needs_chunking,
            "adjusted_chunk_size": adjusted_chunk_size,
            "chunkable_variables": chunkable_variables,
            "total_elements": list_elements_length
        }
        
        # Save information in field query_info.

        self.query_info = {
            "query_string": query_string,
            "query_length": query_length,
            "query_type": query_type,
            "query_name": query_name,
            "query_args": query_args,
            "chunking_info": chunking_info
        }
        

    def _get_chunking_info(self, query_variables: dict):

        # Get all query variables of type list.

        lists_in_query_variable = {
            chunkable_variable_name: list_object 
            for chunkable_variable_name, list_object in query_variables.items() 
            if isinstance(list_object, list)
        }

        # If there is no list typed query variable there is nothing chunkable.

        if not lists_in_query_variable or len(lists_in_query_variable.items()) == 0:
            return False, 0, []
        
        list_elements_length = sum(
            len(list_object) 
            for list_object in lists_in_query_variable.values() 
        )

        # If the number of all elements is lower than the configured threshold, there is no need for chunking.

        if list_elements_length < api_call_chunk_size:
            return False, 0, 0, []
        
        # If there are more than one chunkable variable, the chunk_size has to be adjusted accordingly.

        adjusted_chunk_size = int(api_call_chunk_size / len(lists_in_query_variable.items()))

        return True, adjusted_chunk_size, list_elements_length, list(lists_in_query_variable.keys())
        

    def _call_chunked(self, session, query, query_variables="", debug_level=0):
        """
            Splits a defined query variable into chunks and posts the queries chunk by chunk.
        """

        chunk_number = 1
        total_processed_elements = 0
        return_object = {}
        logger = getFwoLogger(debug_level=debug_level)
        logger.debug(f"Processing chunked API call ({self.query_info["query_name"]})...")

        # Separate chunkable variables.

        chunkable_variables = {
            variable: list_object
            for variable, list_object in query_variables.items()
            if variable in list(self.query_info["chunking_info"]["chunkable_variables"])
        }

        # Loops until all elements of the the query variable have been processed.
        while(total_processed_elements < self.query_info["chunking_info"]["total_elements"]):
            
            total_chunk_elements = 0

            # Gets current chunks slice borders.

            slice_start = (chunk_number -1) * self.query_info["chunking_info"]["adjusted_chunk_size"]
            slice_end = chunk_number * self.query_info["chunking_info"]["adjusted_chunk_size"]

            # Gets current chunks.

            chunks = {
                variable: list_object[slice_start:slice_end]
                for variable, list_object in chunkable_variables.items()
            }

            for variable, chunk in chunks.items():
                query_variables[variable] = chunk
                total_chunk_elements = len(chunk)

            # Post query.

            response = self._post_query(session, {"query": query, "variables": query_variables})

            # Gather and merge returning data.

            if return_object == {}:
                return_object = response
            else:
                new_return = response
                for new_return_object_type, new_return_object in new_return["data"].items():
                    if not isinstance(return_object["data"].get(new_return_object_type), dict):
                        return_object["data"][new_return_object_type] = {}
                        return_object["data"][new_return_object_type]["affected_rows"] = 0
                        return_object["data"][new_return_object_type]["returning"] = []
                    return_object["data"][new_return_object_type]["affected_rows"] += new_return_object["affected_rows"]
                    if "returning" in return_object["data"][new_return_object_type].keys():
                        return_object["data"][new_return_object_type]["returning"].extend(new_return_object["returning"])

            # Log current state of the process and increment variables.

            total_processed_elements += total_chunk_elements
            logger.debug(f"{chunk_number}: {total_processed_elements}/{self.query_info["chunking_info"]["total_elements"]}")
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
    

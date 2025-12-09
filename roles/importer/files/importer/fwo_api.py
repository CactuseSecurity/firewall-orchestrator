import json
import string
import time
import traceback
from collections.abc import MutableMapping
from pprint import pformat
from typing import Any

import fwo_globals
import requests
from fwo_const import FWO_API_HTTP_IMPORT_TIMEOUT
from fwo_exceptions import FwoApiLoginFailed, FwoApiServiceUnavailable, FwoApiTimeout, FwoImporterError
from fwo_log import FWOLogger
from query_analyzer import QueryAnalyzer
from services.service_provider import ServiceProvider

JSON_CONTENT_TYPE = "application/json"


# this class is used for making calls to the FWO API (will supersede fwo_api.py)
class FwoApi:
    fwo_api_url: str
    fwo_jwt: str
    query_info: dict[str, Any]
    query_analyzer: QueryAnalyzer

    def __init__(self, api_uri: str, jwt: str):
        self.fwo_api_url = api_uri
        self.fwo_jwt = jwt
        self.query_info = {}
        self.query_analyzer = QueryAnalyzer()

    def call(
        self, query: str, query_variables: dict[str, list[Any] | Any] | None = None, analyze_payload: bool = False
    ) -> dict[str, Any]:
        """
        The standard FWO API call.
        """
        if query_variables is None:
            query_variables = {}
        role = "importer"
        request_headers = {
            "Content-Type": JSON_CONTENT_TYPE,
            "Authorization": f"Bearer {self.fwo_jwt}",
            "x-hasura-role": role,
        }
        full_query: dict[str, Any] = {"query": query, "variables": query_variables}
        return_object = {}

        if analyze_payload:
            self.query_info = self.query_analyzer.analyze_payload(query, query_variables)

        try:
            with requests.Session() as session:
                if fwo_globals.verify_certs is None:  # only for first FWO API call (getting info on cert verification)
                    session.verify = False
                else:
                    session.verify = fwo_globals.verify_certs
                session.headers.update(request_headers)

                if analyze_payload and self.query_info["chunking_info"]["needs_chunking"]:
                    started = time.time()
                    return_object: dict[str, Any] = self._call_chunked(session, query, query_variables)
                    elapsed_time = time.time() - started
                    affected_rows = 0
                    if "data" in return_object and "affected_rows" in return_object["data"]:
                        # If the return object contains data, we can log the affected rows.
                        affected_rows = sum(obj["affected_rows"] for obj in return_object["data"].values())
                    FWOLogger.debug(
                        f"Chunked API call ({self.query_info['query_name']}) processed in {elapsed_time:.4f} s. Affected rows: {affected_rows}."
                    )
                    self.query_info = {}
                else:
                    return_object: dict[str, Any] = self._post_query(session, full_query)

                self._try_show_api_call_info(full_query, request_headers)

                return return_object

        except requests.exceptions.RequestException as e:
            self._handle_request_exception(e, full_query, request_headers)
        except FwoImporterError as e:
            # Handle FwoImporterError specifically, logging it and re-raising.
            FWOLogger.error(f"FwoImporterError during API call: {e!s}")
            raise
        except Exception as e:
            # Catch all other exceptions and log them.
            FWOLogger.error(f"Unexpected error during API call: {e!s}")
            FWOLogger.debug(pformat(self.query_info))
            try:
                FWOLogger.debug(pformat(return_object))
            except NameError:
                FWOLogger.error(f"Unexpected error during API call: {e!s}")
                raise FwoImporterError(f"return_object not defined. Error during API call: {e!s}")
            raise FwoImporterError(f"Unexpected error during API call: {e!s}")
        return return_object

    @staticmethod
    def login(
        user: str,
        password: str | None,
        user_management_api_base_url: str | None,
        method: str = "api/AuthenticationToken/Get",
    ):
        payload: dict[str, str | None] = {"Username": user, "Password": password}

        if user_management_api_base_url is None:
            raise FwoApiLoginFailed("fwo_api: user_management_api_base_url is None during login")

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:  # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else:
                session.verify = fwo_globals.verify_certs
            session.headers = {"Content-Type": JSON_CONTENT_TYPE}

            try:
                response = session.post(user_management_api_base_url + method, data=json.dumps(payload))
            except requests.exceptions.RequestException:
                raise FwoApiLoginFailed(
                    "fwo_api: error during login to url: " + str(user_management_api_base_url) + " with user " + user
                ) from None

            if response.status_code == 200:
                return response.text
            error_txt = (
                "fwo_api: ERROR: did not receive a JWT during login"
                ", api_url: "
                + str(user_management_api_base_url)
                + ", ssl_verification: "
                + str(fwo_globals.verify_certs)
            )
            raise FwoApiLoginFailed(error_txt)

    def call_endpoint(self, method: str, endpoint: str, params: Any = None) -> Any:
        """
        Generic method to call any middleware endpoint.

        Args:
            method: HTTP method (GET, POST, PUT, DELETE, PATCH)
            endpoint: API endpoint path (e.g., "AuthenticationToken/Get", "User", "Role/User")
            data: Request payload data

        Returns:
            Response data - could be various types based on the endpoint

        Raises:
            FwoApiLoginFailed: If authentication fails
            FwoImporterError: If request fails or returns error

        """
        service_provider = ServiceProvider()
        fwo_config = service_provider.get_fwo_config()
        url = fwo_config["user_management_api_base_url"] + endpoint.lstrip("/")

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:
                session.verify = False
            else:
                session.verify = fwo_globals.verify_certs

            session.headers = {"Authorization": f"Bearer {self.fwo_jwt}", "Content-Type": JSON_CONTENT_TYPE}

            try:
                if method.upper() == "GET":
                    response = session.get(url, json=params, timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))
                elif method.upper() == "POST":
                    response = session.post(url, json=params, timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))
                elif method.upper() == "PUT":
                    response = session.put(url, json=params, timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))
                elif method.upper() == "DELETE":
                    response = session.delete(url, json=params, timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))
                elif method.upper() == "PATCH":
                    response = session.patch(url, json=params, timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))
                else:
                    raise FwoImporterError(f"Unsupported HTTP method: {method}")

                # Check for HTTP errors
                if response.status_code == 401:
                    raise FwoApiLoginFailed(f"Authentication failed for endpoint: {endpoint}")
                if response.status_code == 503:
                    raise FwoApiServiceUnavailable("FWO Middleware API HTTP error 503 (middleware died?)")
                if response.status_code == 502:
                    raise FwoApiTimeout("FWO Middleware API HTTP error 502 (might have reached timeout)")

                response.raise_for_status()

                # Try to parse JSON response
                try:
                    return response.json()
                except ValueError:
                    # If response is not JSON, return the text content
                    return response.text

            except requests.exceptions.RequestException as e:
                FWOLogger.error(f"Middleware API request failed: {e!s}")
                raise FwoImporterError(f"Middleware API request failed: {e!s}")

    def _handle_request_exception(
        self, exception: requests.exceptions.RequestException, query_payload: dict[str, Any], headers: dict[str, Any]
    ) -> None:
        """
        Error handling for the standard API call.
        """
        FWOLogger.debug(
            self.show_import_api_call_info(self.fwo_api_url, query_payload, headers, typ="error")
            + ":\n"
            + str(traceback.format_exc()),
            2,
        )
        if hasattr(exception, "response") and exception.response is not None:
            if exception.response.status_code == 503:
                raise FwoApiServiceUnavailable("FWO API HTTP error 503 (FWO API died?)")
            if exception.response.status_code == 502:
                raise FwoApiTimeout(
                    f"FWO API HTTP error 502 (might have reached timeout of {int(FWO_API_HTTP_IMPORT_TIMEOUT) / 60} minutes)"
                )
        raise exception

    def _call_chunked(
        self, session: requests.Session, query: str, query_variables: dict[str, list[Any]] | None = None
    ) -> dict[str, Any]:
        """
        Splits a defined query variable into chunks and posts the queries chunk by chunk.
        """
        if query_variables is None:
            query_variables = {}
        chunk_number = 1
        total_processed_elements = 0
        return_object = {}
        FWOLogger.info(f"Processing chunked API call ({self.query_info['query_name']})...")

        # Separate chunkable variables.

        chunkable_variables = {
            variable: list_object
            for variable, list_object in query_variables.items()
            if variable in list(self.query_info["chunking_info"]["chunkable_variables"])
        }

        # Loops until all elements of the the query variable have been processed.

        while total_processed_elements < self.query_info["chunking_info"]["total_elements"]:
            # Updates query variables to the current chunks data.

            self.query_info["chunking_info"]["adjusted_chunk_size"] = self.query_analyzer.get_adjusted_chunk_size(
                chunkable_variables
            )

            FWOLogger.debug(
                f"Chunk {chunk_number}:  Chunk size adjusted\n{self.query_info['chunking_info']['adjusted_chunk_size']}",
                9,
            )

            total_chunk_elements = self._update_query_variables_by_chunk(query_variables, chunkable_variables)

            FWOLogger.debug(f"Chunk {chunk_number}:  Query variables updated\n{pformat(query_variables)}", 9)

            # Post query.

            response = self._post_query(session, {"query": query, "variables": query_variables})

            FWOLogger.debug(f"Chunk {chunk_number}:  Query posted", 9)

            # Gather and merge returning data.

            return_object = self._handle_chunked_calls_response(return_object, response)

            # Log current state of the process and increment variables.

            total_processed_elements += total_chunk_elements
            FWOLogger.debug(
                f"Chunk {chunk_number}: {total_processed_elements}/{self.query_info['chunking_info']['total_elements']} processed elements."
            )
            chunk_number += 1

        return return_object

    def _update_query_variables_by_chunk(
        self, query_variables: dict[str, list[Any]], chunkable_variables: dict[str, list[Any]]
    ) -> int:
        chunks: dict[str, Any] = {}
        total_chunk_elements = 0

        for variable, list_object in chunkable_variables.items():
            chunks[variable] = list_object[: self.query_info["chunking_info"]["adjusted_chunk_size"]]
            chunkable_variables[variable] = list_object[self.query_info["chunking_info"]["adjusted_chunk_size"] :]

        for variable, chunk in chunks.items():
            query_variables[variable] = chunk
            total_chunk_elements += len(chunk)

        return total_chunk_elements

    def _handle_chunked_calls_response(self, return_object: dict[str, Any], response: dict[str, Any]) -> dict[str, Any]:
        if return_object == {}:
            self._try_write_extended_log(
                message=f"Return object is empty, initializing with response data: {pformat(response)}"
            )

            return response

        if "errors" in response:
            error_txt = f"encountered error while handling chunked call: {response['errors']!s}"
            FWOLogger.error(error_txt)
            raise FwoImporterError(error_txt)

        for new_return_object_type, new_return_object in response["data"].items():
            if "data" in return_object:
                self._handle_chunked_calls_response_with_return_data(
                    return_object, new_return_object_type, new_return_object
                )
            elif "affected_rows" not in new_return_object:
                FWOLogger.warning(f"no data found: {return_object} not found in return_object['data'].")
            elif new_return_object["affected_rows"] == 0:
                FWOLogger.warning(f"no data found: {new_return_object} not found in return_object['data'].")

        self._try_write_extended_log(
            message=f"Returning object after handling chunked calls response: {pformat(return_object)}"
        )

        return return_object

    def _handle_chunked_calls_response_with_return_data(
        self, return_object: dict[str, Any], new_return_object_type: str, new_return_object: dict[str, Any] | list[Any]
    ) -> None:
        total_affected_rows = 0
        returning_data: list[dict[str, Any]] = []

        self._try_write_extended_log(
            message=f"Handling chunked calls response for type '{new_return_object_type}' with data: {pformat(new_return_object)}"
        )

        if not isinstance(return_object["data"].get(new_return_object_type), dict):
            return_object["data"][new_return_object_type] = {}
            return_object["data"][new_return_object_type]["affected_rows"] = 0
            return_object["data"][new_return_object_type]["returning"] = []

            self._try_write_extended_log(
                message=f"Initialized return_object['data']['{new_return_object_type}'] as an empty dict: {pformat(return_object['data'][new_return_object_type])}"
            )

        # If the return object is a list we need to sum the affected rows and accumuluate the returning data, else we can set the values directly.

        if isinstance(new_return_object, list):
            returning_data = [obj.get("returning", []) for obj in new_return_object if "returning" in obj]
            total_affected_rows = sum(obj.get("affected_rows", 0) for obj in new_return_object)
        else:
            total_affected_rows = new_return_object.get("affected_rows", 0)
            returning_data = new_return_object.get("returning", [])

        return_object["data"][new_return_object_type]["affected_rows"] += total_affected_rows

        if "returning" in return_object["data"][new_return_object_type] and len(returning_data) > 0:
            self._try_write_extended_log(
                message=f"Extending return_object['data']['{new_return_object_type}']['returning'] with new data: {pformat(returning_data)}"
            )

            return_object["data"][new_return_object_type]["returning"].extend(returning_data)

    def _post_query(self, session: requests.Session, query_payload: dict[str, Any]) -> dict[str, Any]:
        """
        Posts the given payload to the api endpoint. Returns the response as json or None if the response object is None.
        """
        FWOLogger.debug(
            self.show_import_api_call_info(
                self.fwo_api_url, query_payload, session.headers, typ="debug", show_query_info=True
            ),
            9,
        )

        r = session.post(self.fwo_api_url, data=json.dumps(query_payload), timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))

        FWOLogger.debug("API response: " + pformat(r.json(), indent=2), 10)

        r.raise_for_status()

        return r.json()

    def show_api_call_info(self, url: str, query: dict[str, Any], headers: dict[str, Any], type: str = "debug"):
        max_query_size_to_display = 1000
        query_string = json.dumps(query, indent=2)
        header_string = json.dumps(headers, indent=2)
        query_size = len(query_string)

        result = "error while sending api_call to url " if type == "error" else "successful FWO API call to url "
        result += str(url) + " with payload \n"
        if query_size < max_query_size_to_display:
            result += query_string
        else:
            result += (
                str(query)[: round(max_query_size_to_display / 2)]
                + "\n ... [snip] ... \n"
                + query_string[query_size - round(max_query_size_to_display / 2) :]
                + " (total query size="
                + str(query_size)
                + " bytes)"
            )
        result += "\n and  headers: \n" + header_string
        return result

    def _try_show_api_call_info(self, full_query: dict[str, Any], request_headers: dict[str, Any]) -> None:
        """
        Tries to show the API call info if the debug level is high enough.
        """
        FWOLogger.debug(
            self.show_import_api_call_info(
                self.fwo_api_url, full_query, request_headers, typ="debug", show_query_info=True
            ),
            9,
        )

    def _try_write_extended_log(self, message: str) -> None:
        """
        Writes an extended log message if the debug level is high enough.
        """
        FWOLogger.debug(message, 10)

    def show_import_api_call_info(
        self,
        api_url: str,
        query: dict[str, Any],
        headers: dict[str, Any] | MutableMapping[str, str | bytes],
        typ: str = "debug",
        show_query_info: bool = False,
    ):
        max_query_size_to_display = 1000
        query_string = json.dumps(query, indent=2)
        header_string = json.dumps(dict(headers), indent=2)
        api_url = json.dumps(api_url, indent=2)
        query_size = len(query_string)
        result = "error while sending api_call to url " if typ == "error" else "successful FWO API call to url "
        result += str(self.fwo_api_url) + " with payload \n"
        if query_size < max_query_size_to_display:
            result += query_string
        else:
            result += (
                str(query)[: round(max_query_size_to_display / 2)]
                + "\n ... [snip] ... \n"
                + query_string[query_size - round(max_query_size_to_display / 2) :]
                + " (total query size="
                + str(query_size)
                + " bytes)"
            )
        result += "\n and  headers: \n" + header_string + ", api_url: " + api_url

        if show_query_info and self.query_info:
            result += "\nQuery Info: \n" + pformat(self.query_info)

        return result

    @classmethod
    def get_graphql_code(cls, file_list: list[str]) -> str:
        code = ""

        for file in file_list:
            try:
                # read graphql code from file
                printable_chars = set(string.printable)
                with open(file, encoding="utf-8", errors="ignore") as f:
                    code += "".join(filter(printable_chars.__contains__, f.read())) + " "
            except FileNotFoundError:
                FWOLogger.error("fwo_api: file not found: " + file)
                raise

        return code.replace("\n", " ").replace("\r", " ")

    @staticmethod
    def _read_clean_text_from_file(file_path: str) -> str:
        printable_chars = set(string.printable)
        with open(file_path, encoding="utf-8", errors="ignore") as f:
            return "".join(filter(printable_chars.__contains__, f.read()))

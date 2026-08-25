from __future__ import annotations

import base64
import json
import string
import time
import traceback
from pprint import pformat
from typing import TYPE_CHECKING, Any, cast

import fwo_globals
import requests
from fwo_const import FWO_API_HTTP_IMPORT_TIMEOUT, FWO_HTTP_TIMEOUT
from fwo_exceptions import FwoApiLoginFailedError, FwoApiServiceUnavailableError, FwoApiTimeoutError, FwoImporterError
from fwo_log import FWOLogger
from query_analyzer import QueryAnalyzer
from services.service_provider import ServiceProvider

if TYPE_CHECKING:
    from collections.abc import Mapping

JSON_CONTENT_TYPE = "application/json"
REDACTED_VALUE = "<redacted>"
HTTP_STATUS_OK = 200
HTTP_STATUS_UNAUTHORIZED = 401
HTTP_STATUS_BAD_GATEWAY = 502
HTTP_STATUS_SERVICE_UNAVAILABLE = 503
MAX_LOGIN_ERROR_RESPONSE_LEN = 200  # keep the login failure reason readable without dumping a full error page
SENSITIVE_HEADER_NAMES = {"authorization", "x-hasura-admin-secret"}
JWT_EXPIRED_ERROR_MARKER = "JWTExpired"  # substring Hasura uses in its "Could not verify JWT: JWTExpired" error
JWT_REFRESH_MARGIN_SECONDS = 60  # proactively refresh once the JWT has less than this long left to live

# sentinel returned by _handle_endpoint_error_status() when the response status is not an error it handles
_NO_ENDPOINT_ERROR = object()


class _JwtExpiredResponseError(Exception):
    """
    Internal signal raised when the FWO API accepted the HTTP request but rejected the JWT
    itself (e.g. Hasura's "Could not verify JWT: JWTExpired"). Caught by call() to trigger
    a token refresh followed by a single retry - never meant to escape this module.
    """


# this class is used for making calls to the FWO API (will supersede fwo_api.py)
class FwoApi:
    fwo_api_url: str
    fwo_jwt: str
    fwo_refresh_token: str | None
    query_info: dict[str, Any]
    query_analyzer: QueryAnalyzer

    def __init__(self, api_uri: str, jwt: str, refresh_token: str | None = None):
        self.fwo_api_url = api_uri
        self.fwo_jwt = jwt
        # optional: without it we can still proactively/reactively detect an expired JWT,
        # we just can't refresh it and the caller has to fall back to a full login
        self.fwo_refresh_token = refresh_token
        self.query_info = {}
        self.query_analyzer = QueryAnalyzer()

    def call(
        self,
        query: str,
        query_variables: dict[str, list[Any] | Any] | None = None,
        analyze_payload: bool = False,
        _retry_on_jwt_expiry: bool = True,
    ) -> dict[str, Any]:
        """
        The standard FWO API call.
        """
        if query_variables is None:
            query_variables = {}

        # refresh ahead of time if the JWT is about to expire, so the call below has a fresh one
        self._ensure_jwt_fresh()

        request_headers = self._build_request_headers()
        full_query: dict[str, Any] = {"query": query, "variables": query_variables}
        return_object: dict[str, Any] = {}

        if analyze_payload:
            self.query_info = self.query_analyzer.analyze_payload(query, query_variables)

        try:
            return_object = self._send_query(full_query, request_headers, analyze_payload)
            self._try_show_api_call_info(full_query, request_headers)
            return return_object

        except _JwtExpiredResponseError:
            return self._retry_after_jwt_expiry(query, query_variables, analyze_payload, _retry_on_jwt_expiry)
        except requests.exceptions.RequestException as e:
            self._handle_request_exception(e, full_query, request_headers)
        except FwoImporterError as e:
            # Handle FwoImporterError specifically, logging it and re-raising.
            FWOLogger.error(f"FwoImporterError during API call: {e!s}")
            raise
        except Exception as e:
            self._handle_unexpected_error(e, return_object)
        return return_object

    def _build_request_headers(self) -> dict[str, str]:
        role = "importer"
        return {
            "Content-Type": JSON_CONTENT_TYPE,
            "Authorization": f"Bearer {self.fwo_jwt}",
            "x-hasura-role": role,
        }

    def _send_query(
        self,
        full_query: dict[str, Any],
        request_headers: dict[str, str],
        analyze_payload: bool,
    ) -> dict[str, Any]:
        with requests.Session() as session:
            # session.verify is None only for the first FWO API call (getting info on cert verification)
            session.verify = False if fwo_globals.verify_certs is None else fwo_globals.verify_certs
            session.headers.update(request_headers)

            if analyze_payload and self.query_info["chunking_info"]["needs_chunking"]:
                return self._call_chunked_and_log(session, full_query["query"], full_query["variables"])
            return self._post_query(session, full_query)

    def _call_chunked_and_log(
        self, session: requests.Session, query: str, query_variables: dict[str, list[Any] | Any]
    ) -> dict[str, Any]:
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
        return return_object

    def _retry_after_jwt_expiry(
        self,
        query: str,
        query_variables: dict[str, list[Any] | Any],
        analyze_payload: bool,
        retry_on_jwt_expiry: bool,
    ) -> dict[str, Any]:
        if not retry_on_jwt_expiry or not self._try_refresh_jwt():
            raise FwoImporterError("fwo_api: JWT expired and could not be refreshed")
        FWOLogger.info("fwo_api: JWT had expired - refreshed token and retrying call once")
        return self.call(query, query_variables, analyze_payload, _retry_on_jwt_expiry=False)

    def _handle_unexpected_error(self, error: Exception, return_object: dict[str, Any]) -> None:
        # Catch-all for unforeseen errors: log context and re-raise as FwoImporterError.
        FWOLogger.error(f"Unexpected error during API call: {error!s}")
        FWOLogger.debug(pformat(self.query_info))
        FWOLogger.debug(pformat(return_object))
        raise FwoImporterError(f"Unexpected error during API call: {error!s}")

    @staticmethod
    def refresh(
        refresh_token: str,
        user_management_api_base_url: str | None,
        method: str = "api/AuthenticationToken/Refresh",
    ) -> str:
        """
        Exchanges a still-valid refresh token for a new access/refresh token pair,
        so callers can recover from an expired JWT without a full re-login.
        """
        payload: dict[str, str] = {"RefreshToken": refresh_token}

        if user_management_api_base_url is None:
            raise FwoApiLoginFailedError("fwo_api: user_management_api_base_url is None during token refresh")

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:  # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else:
                session.verify = fwo_globals.verify_certs
            session.headers.update({"Content-Type": JSON_CONTENT_TYPE})

            try:
                response = session.post(
                    user_management_api_base_url + method,
                    data=json.dumps(payload),
                    timeout=FWO_HTTP_TIMEOUT,
                )
            except requests.exceptions.RequestException:
                raise FwoApiLoginFailedError(
                    "fwo_api: error during token refresh at url: " + str(user_management_api_base_url)
                ) from None

            if response.status_code == HTTP_STATUS_OK:
                return response.text
            # the status and the response body carry the actual reason (e.g. expired/invalid refresh token),
            # without them a refresh failure is indistinguishable from a misconfigured url
            error_txt = (
                "fwo_api: ERROR: did not receive a JWT during token refresh"
                f", api_url: {user_management_api_base_url}{method}"
                f", http_status: {response.status_code}"
                f", response: {response.text[:MAX_LOGIN_ERROR_RESPONSE_LEN]}"
                f", ssl_verification: {fwo_globals.verify_certs}"
            )
            raise FwoApiLoginFailedError(error_txt)

    def refresh_jwt(
        self,
        refresh_token: str,
        user_management_api_base_url: str | None,
        method: str = "api/AuthenticationToken/Refresh",
    ) -> str:
        """
        Refreshes this instance's JWT in place and returns the new access token.

        On failure self.fwo_jwt is left untouched so a caller can decide whether
        to fall back to a full login.
        """
        json_raw = self.refresh(refresh_token, user_management_api_base_url, method)
        json_data = json.loads(json_raw)
        self.fwo_jwt = json_data["AccessToken"]
        # the refresh endpoint typically rotates the refresh token as well - pick up the new one if present
        if json_data.get("RefreshToken"):
            self.fwo_refresh_token = json_data["RefreshToken"]
        return self.fwo_jwt

    def _try_refresh_jwt(self) -> bool:
        """
        Best-effort JWT refresh using the instance's stored refresh token.

        Returns True on success, False if there is no refresh token to use or the
        refresh call itself failed (in which case self.fwo_jwt is left untouched).
        """
        if not self.fwo_refresh_token:
            FWOLogger.debug("fwo_api: no refresh token available - cannot refresh JWT", 3)
            return False
        try:
            service_provider = ServiceProvider()
            fwo_config = service_provider.get_fwo_config()
            user_management_api_base_url = fwo_config["user_management_api_base_url"]
            self.refresh_jwt(self.fwo_refresh_token, user_management_api_base_url)
        except FwoApiLoginFailedError as e:
            FWOLogger.error(f"fwo_api: JWT refresh failed: {e.message}")
            return False
        except Exception:
            FWOLogger.error(f"fwo_api: unexpected error while refreshing JWT: {traceback.format_exc()}")
            return False
        else:
            return True

    def _ensure_jwt_fresh(self, margin_seconds: int = JWT_REFRESH_MARGIN_SECONDS) -> None:
        """
        Proactively refreshes the JWT if it is about to expire within margin_seconds.

        Silently does nothing if the expiry can't be determined or there is no refresh
        token available - the reactive handling in call() still catches an expired JWT.
        """
        if not self.fwo_refresh_token:
            return
        expiry = self._get_jwt_expiry_epoch(self.fwo_jwt)
        if expiry is None or expiry > time.time() + margin_seconds:
            return
        FWOLogger.debug("fwo_api: JWT is about to expire - refreshing proactively", 5)
        self._try_refresh_jwt()

    @staticmethod
    def _get_jwt_expiry_epoch(jwt_token: str) -> float | None:
        """
        Best-effort, unverified read of a JWT's 'exp' claim (seconds since epoch).

        This only informs the decision to proactively refresh; the API itself remains
        the authority on whether a token is actually still valid.
        """
        try:
            payload_segment = jwt_token.split(".")[1]
            padding = "=" * (-len(payload_segment) % 4)
            claims = json.loads(base64.urlsafe_b64decode(payload_segment + padding))
            return claims.get("exp")
        except Exception:
            return None

    @staticmethod
    def _contains_jwt_expired_error(response_body: dict[str, Any] | list[Any] | None) -> bool:
        """
        Detects a GraphQL-level "JWT expired" error, regardless of whether response_body
        is the usual {"errors": [...]} shape or a bare list of error objects.
        """
        errors: list[Any] | None
        if isinstance(response_body, dict):
            errors = response_body.get("errors")
        elif isinstance(response_body, list):
            errors = response_body
        else:
            errors = None

        if not errors:
            return False

        return any(
            FwoApi._is_jwt_expired_error_entry(cast("dict[str, Any]", error))
            for error in errors
            if isinstance(error, dict)
        )

    @staticmethod
    def _is_jwt_expired_error_entry(error: dict[str, Any]) -> bool:
        return JWT_EXPIRED_ERROR_MARKER in str(error.get("message", ""))

    @staticmethod
    def login(
        user: str,
        password: str | None,
        user_management_api_base_url: str | None,
        method: str = "api/AuthenticationToken/GetTokenPair",
    ):
        payload: dict[str, str | None] = {"Username": user, "Password": password}

        if user_management_api_base_url is None:
            raise FwoApiLoginFailedError("fwo_api: user_management_api_base_url is None during login")

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:  # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else:
                session.verify = fwo_globals.verify_certs
            session.headers.update({"Content-Type": JSON_CONTENT_TYPE})

            try:
                response = session.post(
                    user_management_api_base_url + method,
                    data=json.dumps(payload),
                    timeout=FWO_HTTP_TIMEOUT,
                )
            except requests.exceptions.RequestException:
                raise FwoApiLoginFailedError(
                    "fwo_api: error during login to url: " + str(user_management_api_base_url) + " with user " + user
                ) from None

            if response.status_code == HTTP_STATUS_OK:
                return response.text
            # the status and the response body carry the actual reason (e.g. invalid credentials),
            # without them a login failure is indistinguishable from a misconfigured url
            error_txt = (
                "fwo_api: ERROR: did not receive a JWT during login"
                f", api_url: {user_management_api_base_url}{method}"
                f", user: {user}"
                f", http_status: {response.status_code}"
                f", response: {response.text[:MAX_LOGIN_ERROR_RESPONSE_LEN]}"
                f", ssl_verification: {fwo_globals.verify_certs}"
            )
            raise FwoApiLoginFailedError(error_txt)

    def call_endpoint(self, method: str, endpoint: str, params: Any = None, _retry_on_jwt_expiry: bool = True) -> Any:
        """
        Generic method to call any middleware endpoint.

        Args:
            method: HTTP method (GET, POST, PUT, DELETE, PATCH)
            endpoint: API endpoint path (e.g., "AuthenticationToken/GetTokenPair", "User", "Role/User")
            data: Request payload data

        Returns:
            Response data - could be various types based on the endpoint

        Raises:
            FwoApiLoginFailed: If authentication fails
            FwoImporterError: If request fails or returns error

        """
        # refresh ahead of time if the JWT is about to expire, so the call below has a fresh one
        self._ensure_jwt_fresh()

        url = self._build_endpoint_url(endpoint)

        with requests.Session() as session:
            self._configure_endpoint_session(session)

            try:
                response = self._dispatch_endpoint_request(session, method, url, params)

                error_result = self._handle_endpoint_error_status(
                    response, method, endpoint, params, _retry_on_jwt_expiry
                )
                if error_result is not _NO_ENDPOINT_ERROR:
                    return error_result

                response.raise_for_status()
                return self._parse_endpoint_response(response)

            except requests.exceptions.RequestException as e:
                FWOLogger.error(f"Middleware API request failed: {e!s}")
                raise FwoImporterError(f"Middleware API request failed: {e!s}")

    @staticmethod
    def _build_endpoint_url(endpoint: str) -> str:
        fwo_config = ServiceProvider().get_fwo_config()
        return fwo_config["user_management_api_base_url"] + endpoint.lstrip("/")

    def _configure_endpoint_session(self, session: requests.Session) -> None:
        session.verify = False if fwo_globals.verify_certs is None else fwo_globals.verify_certs
        session.headers.update({"Authorization": f"Bearer {self.fwo_jwt}", "Content-Type": JSON_CONTENT_TYPE})

    @staticmethod
    def _dispatch_endpoint_request(session: requests.Session, method: str, url: str, params: Any) -> requests.Response:
        request_by_method = {
            "GET": session.get,
            "POST": session.post,
            "PUT": session.put,
            "DELETE": session.delete,
            "PATCH": session.patch,
        }
        request_func = request_by_method.get(method.upper())
        if request_func is None:
            raise FwoImporterError(f"Unsupported HTTP method: {method}")
        return request_func(url, json=params, timeout=int(FWO_API_HTTP_IMPORT_TIMEOUT))

    def _handle_endpoint_error_status(
        self,
        response: requests.Response,
        method: str,
        endpoint: str,
        params: Any,
        retry_on_jwt_expiry: bool,
    ) -> Any:
        """Return the retried call's result for a handled error status, or _NO_ENDPOINT_ERROR if none applied."""
        if response.status_code == HTTP_STATUS_UNAUTHORIZED:
            if retry_on_jwt_expiry and self._try_refresh_jwt():
                FWOLogger.info("fwo_api: middleware call got 401 - refreshed JWT and retrying once")
                return self.call_endpoint(method, endpoint, params, _retry_on_jwt_expiry=False)
            raise FwoApiLoginFailedError(f"Authentication failed for endpoint: {endpoint}")
        if response.status_code == HTTP_STATUS_SERVICE_UNAVAILABLE:
            raise FwoApiServiceUnavailableError("FWO Middleware API HTTP error 503 (middleware died?)")
        if response.status_code == HTTP_STATUS_BAD_GATEWAY:
            raise FwoApiTimeoutError("FWO Middleware API HTTP error 502 (might have reached timeout)")
        return _NO_ENDPOINT_ERROR

    @staticmethod
    def _parse_endpoint_response(response: requests.Response) -> Any:
        try:
            return response.json()
        except ValueError:
            # If response is not JSON, return the text content
            return response.text

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
            if exception.response.status_code == 503:  # noqa: PLR2004
                raise FwoApiServiceUnavailableError("FWO API HTTP error 503 (FWO API died?)")
            if exception.response.status_code == 502:  # noqa: PLR2004
                raise FwoApiTimeoutError(
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

            FWOLogger.debug(
                f"Chunk {chunk_number}: Query variables updated: {self.summarize_query_variables(query_variables)}",
                9,
            )

            response = self._post_chunk_with_jwt_retry(session, query, query_variables)

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

    def _post_chunk_with_jwt_retry(
        self, session: requests.Session, query: str, query_variables: dict[str, list[Any] | Any]
    ) -> dict[str, Any]:
        """
        Posts a single chunk, refreshing the JWT and retrying this same chunk once if it expired.

        Keeping the retry local to one chunk (instead of raising _JwtExpiredResponseError out of
        _call_chunked) preserves the surrounding loop's state - remaining chunkable_variables,
        total_processed_elements and the return_object accumulated so far.
        """
        try:
            return self._post_query(session, {"query": query, "variables": query_variables})
        except _JwtExpiredResponseError:
            if not self._try_refresh_jwt():
                raise FwoImporterError("fwo_api: JWT expired during chunked call and could not be refreshed")
            FWOLogger.info(
                "fwo_api: JWT had expired during chunked call - refreshed token and retrying this chunk once"
            )
            # the session's headers were built with the now-stale JWT - refresh them before retrying
            session.headers.update(self._build_request_headers())
            return self._post_query(session, {"query": query, "variables": query_variables})

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

        FWOLogger.debug("API response received", 10)

        # Hasura may report an expired JWT as a GraphQL-level error (HTTP 200 with an "errors"
        # array) rather than an HTTP-level 401, so this has to be checked before raise_for_status().
        try:
            response_body = r.json()
        except ValueError:
            response_body = None
        if response_body is not None and self._contains_jwt_expired_error(response_body):
            raise _JwtExpiredResponseError

        r.raise_for_status()

        return r.json()

    def show_api_call_info(self, url: str, query: dict[str, Any], headers: dict[str, Any], typ: str = "debug"):
        max_query_size_to_display = 1000
        redacted_query = self._redact_graphql_payload(query)
        query_string = json.dumps(redacted_query, indent=2)
        header_string = json.dumps(self._redact_headers(headers), indent=2)
        query_size = len(query_string)

        result = "error while sending api_call to url " if typ == "error" else "successful FWO API call to url "
        result += str(url) + " with payload \n"
        if query_size < max_query_size_to_display:
            result += query_string
        else:
            result += (
                str(redacted_query)[: round(max_query_size_to_display / 2)]
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
        headers: Mapping[str, Any],
        typ: str = "debug",
        show_query_info: bool = False,
    ):
        max_query_size_to_display = 1000
        redacted_query = self._redact_graphql_payload(query)
        query_string = json.dumps(redacted_query, indent=2)
        header_string = json.dumps(self._redact_headers(headers), indent=2)
        api_url = json.dumps(api_url, indent=2)
        query_size = len(query_string)
        result = "error while sending api_call to url " if typ == "error" else "successful FWO API call to url "
        result += str(self.fwo_api_url) + " with payload \n"
        if query_size < max_query_size_to_display:
            result += query_string
        else:
            result += (
                str(redacted_query)[: round(max_query_size_to_display / 2)]
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

    @staticmethod
    def _redact_headers(headers: Mapping[str, Any]) -> dict[str, Any]:
        return {
            header_name: REDACTED_VALUE if header_name.lower() in SENSITIVE_HEADER_NAMES else header_value
            for header_name, header_value in dict(headers).items()
        }

    @staticmethod
    def _redact_graphql_payload(query_payload: dict[str, Any]) -> dict[str, Any]:
        redacted_payload = dict(query_payload)
        if "variables" in redacted_payload:
            redacted_payload["variables"] = REDACTED_VALUE
        return redacted_payload

    @staticmethod
    def summarize_query_variables(query_variables: Mapping[str, Any]) -> str:
        if not query_variables:
            return "none"
        return (
            f"{len(query_variables)} variable(s): {', '.join(sorted(query_variables.keys()))}; values {REDACTED_VALUE}"
        )

    @classmethod
    def get_graphql_code(cls, file_list: list[str]) -> str:
        code = ""

        for file in file_list:
            try:
                # read graphql code from file
                printable_chars = set(string.printable)
                with open(file, encoding="utf-8", errors="ignore") as f:
                    code += "".join(filter(printable_chars.__contains__, f.read())) + " "
            except FileNotFoundError:  # noqa: PERF203
                FWOLogger.error("fwo_api: file not found: " + file)
                raise

        return code.replace("\n", " ").replace("\r", " ")

    @staticmethod
    def _read_clean_text_from_file(file_path: str) -> str:
        printable_chars = set(string.printable)
        with open(file_path, encoding="utf-8", errors="ignore") as f:
            return "".join(filter(printable_chars.__contains__, f.read()))

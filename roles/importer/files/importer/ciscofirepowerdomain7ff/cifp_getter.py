# library for API get functions
import base64
from typing import Any
from fwo_log import getFwoLogger
import requests
import json
import fwo_globals
from fwo_exceptions import FwLoginFailed

auth_token = ""

def api_call(url: str, params: dict[str, Any] = {}, headers: dict[str, Any] = {}, json_payload: dict[str, Any] = {}, auth_token: str = '', show_progress: bool = False, method: str = 'get') -> tuple[dict[str, Any], dict[str, Any]]:
    logger = getFwoLogger()
    request_headers = {'Content-Type': 'application/json'}
    for header_key in headers:
        request_headers[header_key] = headers[header_key]
    if auth_token != '':
        request_headers["X-auth-access-token"] = auth_token

    if method == "post":
        response = requests.post(url, params=params, data=json.dumps(json_payload), headers=request_headers,
                          verify=fwo_globals.verify_certs)
    elif method == "get":
        response = requests.get(url, params=params, data=json.dumps(json_payload), headers=request_headers,
                         verify=fwo_globals.verify_certs)
    else:
        raise Exception("unknown HTTP method found in cifp_getter")

    if (len(response.content) > 0):
        body_json: dict[str, Any] = response.json()
    else:
        body_json: dict[str, Any] = {}

    if fwo_globals.debug_level > 2:
        if 'pass' in json.dumps(json_payload):
            logger.debug("api_call containing credential information to url '" +
                         str(url) + " - not logging query")
        else:
            logger.debug("api_call to url '" + str(url) + "' with payload '" + json.dumps(
                json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))

    return dict(response.headers), body_json

def login(user: str, password: str, api_host: str, api_port: int) -> tuple[str, str]:
    base_url = 'https://' + api_host + ':' + str(api_port) + '/api/'
    try:
        headers, _ = api_call(base_url + "fmc_platform/v1/auth/generatetoken", method="post", headers={"Authorization" : "Basic " + str(base64.b64encode((user + ":" + password).encode('utf-8')), 'utf-8')})
    except Exception as e:
        raise FwLoginFailed(
            "Cisco Firepower login ERROR: host=" + str(api_host) + ":" + str(api_port) + " Message: " + str(e)) from None
    access_token = headers.get("X-auth-access-token")
    if access_token is None:   # leaving out payload as it contains pwd
        raise FwLoginFailed(
            "Cisco Firepower login ERROR: host=" + str(api_host) + ":" + str(api_port)) from None
    if fwo_globals.debug_level > 2:
        logger = getFwoLogger()
        logger.debug("Login successful. Received auth token: " + headers["X-auth-access-token"])
    return access_token, headers.get("DOMAINS") or ""

# TODO Is there a logout?
def logout(v_url: str, sid: str, method: str = 'exec') -> None:
    return

def update_config_with_cisco_api_call(session_id: str, api_base_url: str, api_path: str, parameters: dict[str, Any] = {}, payload: dict[str, Any] = {}, show_progress: bool = False, limit: int = 1000, method: str = "get") -> list[dict[str, Any]]:
    offset = 0
    limit = 1000
    returned_new_data = True
    full_result: list[dict[str, Any]] = []
    while returned_new_data:
        parameters["offset"] = offset
        parameters["limit"] = limit
        result = api_call(api_base_url + "/" + api_path, auth_token=session_id, params=parameters, json_payload=payload, show_progress=show_progress, method=method)[1]
        returned_new_data = result["paging"]["count"] > 0
        if returned_new_data:
            full_result.extend(result["items"])           
            offset += limit
    return full_result
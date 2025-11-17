# library for API get functions
from typing import Any
from fwo_log import getFwoLogger
import requests
import json
import fwo_globals


def api_call(url: str, show_progress:bool=False) -> dict[str, Any]:
    logger = getFwoLogger()
    request_headers = {'Content-Type': 'application/json'}

    r = requests.get(url, headers=request_headers, verify=fwo_globals.verify_certs)
    #TYPING: check for non 200 responses
    result_json = r.json()
    if 'results' not in result_json:
        raise Exception("error while sending api_call to url '" + str(url) + "' with headers: '" + json.dumps(request_headers, indent=2) + ', results=' + json.dumps(r.json()['results'], indent=2))
    if 'status' not in result_json:
        # trying to ignore empty results as valid
        pass # logger.warning('received empty result')
    if fwo_globals.debug_level>2:
        logger.debug("api_call to url '" + str(url) + "' with headers: '" + json.dumps(request_headers, indent=2))
    return result_json


# def set_api_url(base_url, testmode, api_supported, hostname):
#     url = ''
#     if testmode == 'off':
#         url = base_url
#     else:
#         if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
#             if testmode in api_supported:
#                 url = base_url + 'v' + testmode + '/'
#             else:
#                 raise Exception("api version " + testmode +
#                              " is not supported by the manager " + hostname + " - Import is canceled")
#         else:
#             raise Exception("\"" + testmode + "\" - not a valid version")
#     return url


def update_config_with_fortiOS_api_call(config_json: dict[str, Any], api_url: str, result_name: str, show_progress: bool = False, limit: int = 150):
    limit = int(limit)
    full_result: list[Any] = []
    result = fortiOS_api_call(api_url)
    full_result.extend(result)
    # removing loop for api gets (no limit option in FortiOS API)
    # while returned_new_objects:
    #     range = [offset, limit]        
    # result = fortiOS_api_call(api_url)
    # full_result.extend(result)
    #     offset += limit
    #     if len(result)<limit:
    #         returned_new_objects = False
    if result_name in config_json:  # data already exists - extend
        config_json[result_name].extend(full_result)
    else:
        config_json.update({result_name: full_result})


def fortiOS_api_call(api_url: str) -> Any:
    result = api_call(api_url)
    if 'results' in result:
        plain_result = result["results"]
    else:
        plain_result: dict[str, Any] = {}
    return plain_result

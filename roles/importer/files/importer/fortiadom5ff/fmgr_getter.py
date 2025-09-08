# library for API get functions
from fwo_log import getFwoLogger
import requests.packages
import requests
import json
from typing import Any
import fwo_globals
from fwo_exceptions import FwLoginFailed, FwoUnknownDeviceForManager, FwApiCallFailed, FwLogoutFailed


def api_call(url, command, json_payload, sid, show_progress=False, method=''):
    logger = getFwoLogger()
    request_headers = {'Content-Type': 'application/json'}
    if sid != '':
        json_payload.update({'session': sid})
    if command != '':
        for p in json_payload['params']:
            p.update({'url': command})
    if method == '':
        method = 'get'
    json_payload.update({'method': method})

    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=fwo_globals.verify_certs)
    try:
        r
    except Exception as e:
        if 'pass' in json.dumps(json_payload):
            exception_text = f'error while sending api_call containing credential information to url {str(url)}'
        else:
            exception_text = f'error while sending api_call to url {str(url)} with payload {json.dumps(json_payload, indent=2)} and  headers: {json.dumps(request_headers, indent=2)}'
        raise FwApiCallFailed(exception_text)
    result_json = r.json()
    if 'result' not in result_json or len(result_json['result'])<1:
        if 'pass' in json.dumps(json_payload):
            raise FwApiCallFailed(f'error while sending api_call containing credential information to url {str(url)}')
        else:
            if 'status' in result_json['result'][0]:
                result_status = r.json()['result'][0]['status']
                exception_text = f'error while sending api_call to url {str(url)} with payload {json.dumps(json_payload, indent=2)}  and  headers: '
                exception_text += f'{json.dumps(request_headers, indent=2)}, result={json.dumps(result_status, indent=2)}'
            else:
                result_data = r.json()['result'][0]
                exception_text = f'error while sending api_call to url {str(url)} with payload {json.dumps(json_payload, indent=2)}'
                exception_text += f' and  headers: {json.dumps(request_headers, indent=2)}, result={json.dumps(result_data, indent=2)}'
            raise FwApiCallFailed(exception_text)
    if 'status' not in result_json['result'][0] or 'code' not in result_json['result'][0]['status'] or result_json['result'][0]['status']['code'] != 0:
        # trying to ignore empty results as valid
        pass # logger.warning('received empty result')
    if fwo_globals.debug_level>2:
        if 'pass' in json.dumps(json_payload):
            logger.debug('api_call containing credential information to url ' + str(url) + ' - not logging query')
        else:
            logger.debug('api_call to url ' + str(url) + ' with payload ' + json.dumps(
                json_payload, indent=2) + ' and  headers: ' + json.dumps(request_headers, indent=2))

    return result_json


def login(user, password, base_url) -> str:
    payload = {
        'id': 1,
        'params': [ { 'data': [ { 'user': user, 'passwd': password, } ] } ]
    }
    try:
       response = api_call(base_url, 'sys/login/user', payload, '', method='exec')
    except Exception:
        raise FwLoginFailed('FortiManager login ERROR: url=' + base_url) from None
    if 'session' not in response:   # leaving out payload as it contains pwd
        raise FwLoginFailed('FortiManager login ERROR (no sid): url=' + base_url) from None
    return response['session']


def logout(v_url, sid, method='exec'):
    logger = getFwoLogger()
    payload = {'params': [{}]}

    response = api_call(v_url, 'sys/logout', payload, sid, method=method)
    if 'result' in response and 'status' in response['result'][0] and 'code' in response['result'][0]['status'] and response['result'][0]['status']['code'] == 0:
        logger.debug('successfully logged out')
    else:
        raise FwLogoutFailed( 'fmgr_getter ERROR: did not get status code 0 when logging out, ' + 
                            'api call: url: ' + str(v_url) + ',  + payload: ' + str(payload))


def update_config_with_fortinet_api_call(config_json, sid, api_base_url, api_path, result_name, payload={}, options=[], limit=150, method='get'):
    offset = 0
    limit = int(limit)
    returned_new_objects = True
    full_result = []
    while returned_new_objects:
        range = [offset, limit]
        if payload == {}:
            payload = {'params': [{'range': range}]}
        else:
            if 'params' in payload and len(payload['params'])>0:
                payload['params'][0].update({'range': range})
        
        # adding options
        if len(options)>0:
            payload['params'][0].update({'option': options})
            # payload['params'][0].update({'filter': options})

        result = fortinet_api_call(sid, api_base_url, api_path, payload=payload, method=method)
        full_result.extend(result)
        offset += limit
        if len(result)<limit:
            returned_new_objects = False

    full_result = parse_special_fortinet_api_results(result_name, full_result)

    config_json.append({'type': result_name, 'data': full_result})


def parse_special_fortinet_api_results(result_name, full_result):
    if result_name == 'nw_obj_global_firewall/internet-service-basic':
        if len(full_result)>0 and 'response' in full_result[0] and 'results' in full_result[0]['response']: 
            full_result = full_result[0]['response']['results']
        else:
            logger = getFwoLogger()
            logger.warning(f"did not get expected results for {result_name} - setting to empty list")
            full_result = []
    return full_result


def fortinet_api_call(sid, api_base_url, api_path, payload={}, method='get'):
    if payload == {}:
        payload = {'params': [{}]}
    result = api_call(api_base_url, api_path, payload, sid, method=method)
    plain_result = result['result'][0]
    if 'data' in plain_result:
        result = plain_result['data']
        if isinstance(result, dict):  # code implicitly expects result to be a list, but some fmgr data results are dicts
            result = [result]
    else:
        result = []
    return result

def get_devices_from_manager(adom_mgm_details, sid, fm_api_url) -> dict[str, Any]:
    device_vdom_dict = {}

    device_results = fortinet_api_call(sid, fm_api_url, '/dvmdb/adom/' + adom_mgm_details.DomainName + '/device')
    for mgm_details_device in adom_mgm_details.Devices:
        if not mgm_details_device['importDisabled']:
            found_fmgr_device = False
            for fmgr_device in device_results:
                found_fmgr_device = parse_device_and_vdom(fmgr_device, mgm_details_device, device_vdom_dict, found_fmgr_device)
            if not found_fmgr_device:
                raise FwoUnknownDeviceForManager('Could not find ' + mgm_details_device['name'] + ' in Fortimanager Config') from None
        
    return device_vdom_dict

def parse_device_and_vdom(fmgr_device, mgm_details_device, device_vdom_dict, found_fmgr_device):
    if 'vdom' in fmgr_device:
        for fmgr_vdom in fmgr_device['vdom']:
            if mgm_details_device['name'] == fmgr_device['name'] + '_' + fmgr_vdom['name']:
                found_fmgr_device = True
                if fmgr_device['name'] in device_vdom_dict:
                    device_vdom_dict[fmgr_device['name']].update({fmgr_vdom['name']: ''})
                else:
                    device_vdom_dict.update({fmgr_device['name']: {fmgr_vdom['name']: ''}})
    return found_fmgr_device
            
def get_policy_packages_from_manager(adom, sid, fm_api_url):

    policy_packages_result = fortinet_api_call(sid, fm_api_url, '/pm/pkg/adom/' + adom)

    return policy_packages_result
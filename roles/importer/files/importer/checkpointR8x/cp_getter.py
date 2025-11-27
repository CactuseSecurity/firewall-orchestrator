# library for API get functions
import json
import requests
import time
from datetime import datetime
from typing import Any

from fwo_exceptions import FwLoginFailed, FwApiError, FwApiResponseDecodingError, FwoImporterError
from fwo_log import FWOLogger
import fwo_globals
from checkpointR8x import cp_const, cp_network
import fwo_const
from model_controllers.management_controller import ManagementController
from services.service_provider import ServiceProvider
from fwo_api_call import FwoApiCall, FwoApi

# Constants for status values
STATUS_IN_PROGRESS = 'in progress'
STATUS_SUCCEEDED = 'succeeded'
STATUS_FAILED = 'failed'

def cp_api_call(url: str, command: str, json_payload: dict[str, Any], sid: str | None, show_progress: bool=False):
    url += command
    request_headers = {'Content-Type' : 'application/json'}
    if sid: # only not set for login
        request_headers.update({'X-chkp-sid' : sid})

    FWOLogger.debug(f"api call '{command}'", 9)
    if command!='login':    # do not log passwords
        FWOLogger.debug("json_payload: " + str(json_payload), 10)

    try:
         r = requests.post(url, json=json_payload, headers=request_headers, verify=fwo_globals.verify_certs)
    except requests.exceptions.RequestException as _:
        if 'password' in json.dumps(json_payload):
            exception_text = "\nerror while sending api_call containing credential information to url '" + str(url)
        else:
            exception_text = "\nerror while sending api_call to url '" + str(url) + "' with payload '" + json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2)
        raise FwApiError(exception_text)
    if show_progress:
        print ('.', end='', flush=True)

    try:
        json_response = r.json()
    except Exception:
        raise FwApiResponseDecodingError(f"checkpointR8x:api_call: response is not in valid json format: {r.text}")
    return json_response


def login(mgm_details: ManagementController):
    payload = {'user': mgm_details.import_user, 'password': mgm_details.secret}
    domain = mgm_details.getDomainString()
    if domain is not None and domain != '': # type: ignore # TODO: shouldnt be None
        payload.update({'domain': domain})
    base_url = mgm_details.buildFwApiString()
    FWOLogger.debug(f"login - login to url {base_url} with user {mgm_details.import_user}", 3)
    response = cp_api_call(base_url, 'login', payload, '')
    if "sid" not in response:
        exception_text = f"getter ERROR: did not receive a sid, api call: {base_url}"
        raise FwLoginFailed(exception_text)
    return response["sid"]



def logout(url: str, sid: str):

    FWOLogger.debug("logout from url " + url, 3)
    response = cp_api_call(url, 'logout', {}, sid)
    return response


def process_single_task(task: dict[str, Any]) -> tuple[str, int]:
    """Process a single task and return status and result code."""
    FWOLogger.debug("task: " + json.dumps(task), 6)
    
    if 'status' not in task:
        FWOLogger.error("no status in task")
        return STATUS_FAILED, -1
        
    status = task['status']
    
    if STATUS_SUCCEEDED in status:
        result = check_task_details_for_changes(task)
        return status, result
    elif status == STATUS_FAILED:
        FWOLogger.debug("show-changes - status: failed -> no changes found")
        return status, 0
    elif status == STATUS_IN_PROGRESS:
        FWOLogger.debug("status: in progress")
        return status, 0
    else:
        FWOLogger.error("unknown status: " + status)
        return STATUS_FAILED, -1


def process_changes_task(base_url: str, task_id: dict[str, Any], sid: str) -> int:
    """Process the changes task and return the result."""
    sleeptime = 1
    status = STATUS_IN_PROGRESS
    
    while status == STATUS_IN_PROGRESS:
        time.sleep(sleeptime)
        tasks = cp_api_call(base_url, 'show-task', task_id, sid)
        
        if 'tasks' not in tasks:
            FWOLogger.error("no tasks in task response")
            return -1
            
        for task in tasks['tasks']:
            status, result = process_single_task(task)
            if status != STATUS_IN_PROGRESS:
                return result
                
        sleeptime += 2
        if sleeptime > 40:
            FWOLogger.error("task took too long, aborting")
            return -1
            
    return 0


def check_task_details_for_changes(task: dict[str, Any]) -> int:
    """Check task details to see if changes were found."""
    for detail in task['task-details']:
        if detail['changes']:
            FWOLogger.debug("status: succeeded -> changes found")
            return 1
        else:
            FWOLogger.debug("status: succeeded -> but no changes found")
    return 0


def get_changes(sid: str, api_host: str, api_port: str, fromdate: str) -> int:
    
    dt_object = datetime.fromisoformat(fromdate)
    dt_truncated = dt_object.replace(microsecond=0)     # Truncate microseconds
    fromdate = dt_truncated.isoformat()

    payload = {'from-date' : fromdate, 'details-level' : 'uid'}
    FWOLogger.debug ("payload: " + json.dumps(payload))
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'
    task_id = cp_api_call(base_url, 'show-changes', payload, sid)

    FWOLogger.debug ("task_id: " + json.dumps(task_id))
    return process_changes_task(base_url, task_id, sid)


def get_policy_structure(api_v_url: str, sid: str, show_params_policy_structure: dict[str, Any], manager_details: ManagementController, policy_structure: list[dict[str, Any]] | None = None) -> int:

    if policy_structure is None:
        policy_structure = []

    current = 0
    total = current + 1

    show_params_policy_structure.update({'offset': current})

    while current < total:
        packages, current, total = get_show_packages_via_api(api_v_url, sid, show_params_policy_structure)

        for package in packages['packages']:
            current_package, already_fetched_package = parse_package(package, manager_details)
            if not already_fetched_package:
                continue
            add_access_layers_to_current_package(package, current_package)

            # in future threat-layers may be fetched analog to add_access_layers_to_current_package
            policy_structure.append(current_package)

    return 0

def get_show_packages_via_api(api_v_url: str, sid: str, show_params_policy_structure: dict[str, Any]) -> tuple[dict[str, Any], int, int]:
    try:
        packages = cp_api_call(api_v_url, 'show-packages', show_params_policy_structure, sid)
    except Exception:
        raise FwApiError("could not return 'show-packages'")

    if 'total' in packages:
        total = packages['total']
    else:
        FWOLogger.error('packages do not contain total field')
        FWOLogger.warning('sid: ' + sid)
        FWOLogger.warning('api_v_url: ' + api_v_url)
        for key, value in show_params_policy_structure.items():
            FWOLogger.warning('show_params_policy_structure ' + key + ': ' + str(value))
        for key, value in packages.items():
            FWOLogger.warning('packages ' + key + ': ' + str(value))
        raise FwApiError('packages do not contain total field')
    
    if total == 0:
        current = 0
    else:
        if 'to' in packages:
            current = packages['to']
        else:
            raise FwApiError('packages do not contain to field')
    return packages, current, total

def parse_package(package: dict[str, Any], manager_details: ManagementController) -> tuple[dict[str, Any], bool]:
    already_fetched_package = False
    current_package = {}
    if 'installation-targets' in package and package['installation-targets'] == 'all':
        if not already_fetched_package:
            
            current_package: dict[str, Any] = { 'name': package['name'],
                                'uid': package['uid'],
                                'targets': [{'name': 'all', 'uid': 'all'}],
                                'access-layers': []}
            already_fetched_package = True

    elif 'installation-targets-revision' in package:
        for installation_target in package['installation-targets-revision']:
            if is_valid_installation_target(installation_target, manager_details):

                if not already_fetched_package:
                    current_package = { 'name': package['name'],
                                        'uid': package['uid'],
                                        'targets': [],
                                        'access-layers': []}
                    already_fetched_package = True

                current_package['targets'].append({ 'name': installation_target['target-name'],
                                                    'uid': installation_target['target-uid']})
            else:
                FWOLogger.warning ( 'installation target in package: ' + package['uid'] + ' is missing name or uid')
    return current_package, already_fetched_package

def is_valid_installation_target(installation_target: dict[str, Any], manager_details: ManagementController) -> bool:
    """ensures that target is defined as gateway in database"""
    if 'target-name' in installation_target and 'target-uid' in installation_target:
        for device in manager_details.devices:
            if device['name'] == installation_target['target-name'] and device['uid'] == installation_target['target-uid']:
                return True
    return False

def add_access_layers_to_current_package(package: dict[str, Any], current_package: dict[str, Any]) -> None:

    if 'access-layers' in package:
        for access_layer in package['access-layers']:
            if 'name' in access_layer and 'uid' in access_layer:
                current_package['access-layers'].append({ 'name': access_layer['name'],
                                                            'uid': access_layer['uid'],
                                                            'domain': access_layer['domain']['uid']})
            else:
                raise FwApiError('access layer in package: ' + package['uid'] + ' is missing name or uid')

def fetch_global_assignments_chunk(api_v_url: str, sid: str, show_params_policy_structure: dict[str, Any]) -> tuple[dict[str, Any], int, int]:
    """Fetch a chunk of global assignments from the API."""
    try:
        assignments = cp_api_call(api_v_url, 'show-global-assignments', show_params_policy_structure, sid)
    except Exception:
        FWOLogger.error("could not return 'show-global-assignments'")
        raise FwoImporterError('could not return "show-global-assignments"')

    if 'total' not in assignments:
        log_global_assignments_error(sid, api_v_url, show_params_policy_structure, assignments)
        raise FwoImporterError('global assignments do not contain "total" field')
        
    total = assignments['total']
    
    if total == 0:
        current = 0
    else:
        if 'to' not in assignments:
            raise FwoImporterError('global assignments do not contain "to" field')
        current = assignments['to']
        
    return assignments, current, total


def log_global_assignments_error(sid: str, api_v_url: str, show_params_policy_structure: dict[str, Any], assignments: dict[str, Any]) -> None:
    """Log error information for global assignments debugging."""
    FWOLogger.warning('sid: ' + sid)
    FWOLogger.warning('api_v_url: ' + api_v_url)
    for key, value in show_params_policy_structure.items():
        FWOLogger.warning('show_params_policy_structure ' + key + ': ' + str(value))
    for key, value in assignments.items():
        FWOLogger.warning('global assignments ' + key + ': ' + str(value))


def parse_global_assignment(assignment: dict[str, Any]) -> dict[str, Any]:
    """Parse a single global assignment object."""
    if 'type' not in assignment or assignment['type'] != 'global-assignment':
        raise FwoImporterError('global assignment with unexpected type')
        
    return {
        'uid': assignment['uid'],
        'global-domain': {
            'uid': assignment['global-domain']['uid'],
            'name': assignment['global-domain']['name']
        },
        'dependent-domain': {
            'uid': assignment['dependent-domain']['uid'],
            'name': assignment['dependent-domain']['name']
        },
        'global-access-policy': assignment['global-access-policy']
    }


def get_global_assignments(api_v_url: str, sid: str, show_params_policy_structure: dict[str, Any]) -> list[Any]:
    current = 0
    total = current + 1
    show_params_policy_structure.update({'offset': current})
    global_assignments: list[dict[str, Any]] = []

    while current < total:
        assignments, current, total = fetch_global_assignments_chunk(api_v_url, sid, show_params_policy_structure)
        
        # parse global assignments
        for assignment in assignments['objects']:
            global_assignment = parse_global_assignment(assignment)
            global_assignments.append(global_assignment)

    return global_assignments
                        

def get_rulebases(api_v_url: str, sid: str | None, show_params_rules: dict[str, Any], native_config_domain: dict[str, Any] | None,
                  device_config: dict[str, Any] | None, policy_rulebases_uid_list: list[str], is_global: bool = False,
                  access_type: str = 'access', rulebase_uid: str | None = None, rulebase_name: str | None = None) -> list[str]:
    
    # access_type: access / nat
    native_config_rulebase_key = 'rulebases'
    current_rulebase = {}

    if native_config_domain is None:
        native_config_domain = {'rulebases': [], 'nat_rulebases': []}
    if device_config is None:
        device_config = {'rulebase_links': []}

    if access_type == 'access':
        native_config_rulebase_key = 'rulebases'
    elif access_type == 'nat':
        native_config_rulebase_key = 'nat_rulebases'
    else:
        FWOLogger.error('access_type is neither "access" nor "nat", but ' + access_type)

    # get uid of rulebase
    if rulebase_name is not None:
        rulebase_uid = get_uid_of_rulebase(rulebase_name, api_v_url, access_type, sid)
    else:
        FWOLogger.error('must provide either rulebaseUid or rulebaseName')
    policy_rulebases_uid_list.append(rulebase_uid) #type: ignore # TODO: get_uid_of_rulebase can return None but in theory should not
    
    # search all rulebases in nativeConfigDomain and import if rulebase is not already fetched
    fetched_rulebase_list: list[str] = []
    for fetched_rulebase in native_config_domain[native_config_rulebase_key]:
        fetched_rulebase_list.append(fetched_rulebase['uid'])
        if fetched_rulebase['uid'] == rulebase_uid:
            current_rulebase = fetched_rulebase
            break

    # get rulebase in chunks
    if rulebase_uid not in fetched_rulebase_list:
        current_rulebase = get_rulebases_in_chunks(rulebase_uid, show_params_rules, api_v_url, access_type, sid, native_config_domain) #type: ignore # TODO: rulebaseUid can be None but in theory should not
        native_config_domain[native_config_rulebase_key].append(current_rulebase)

    # use recursion to get inline layers
    policy_rulebases_uid_list = get_inline_layers_recursively(current_rulebase, device_config, native_config_domain, api_v_url, sid,
                                                              show_params_rules, is_global, policy_rulebases_uid_list)    
    
    return policy_rulebases_uid_list


def get_uid_of_rulebase(rulebase_name: str, api_v_url: str, access_type: str, sid: str | None) -> str | None: # TODO: what happens if rulebaseUid None? Error?
    rulebase_uid = None
    get_rulebase_uid_params: dict[str, Any] = {
        'name': rulebase_name,
        'limit': 1,
        'use-object-dictionary': False,
        'details-level': 'uid',
        'show-hits': False
    }
    try:
        rulebase_for_uid = cp_api_call(api_v_url, 'show-' + access_type + '-rulebase', get_rulebase_uid_params, sid)
        rulebase_uid = rulebase_for_uid['uid']
    except Exception:
        FWOLogger.error("could not find uid for rulebase name=" + rulebase_name)

    return rulebase_uid


def get_rulebases_in_chunks(rulebase_uid: str, show_params_rules: dict[str, Any], api_v_url: str, access_type: str, sid: str, native_config_domain: dict[str, Any]) -> dict[str, Any]:

    current_rulebase: dict[str, Any] = {'uid': rulebase_uid, 'name': '', 'chunks': []}
    show_params_rules.update({'uid': rulebase_uid})
    current=0
    total=current+1

    while (current<total):

        show_params_rules.update({'offset': current})
    
        try:
            rulebase = cp_api_call(api_v_url, 'show-' + access_type + '-rulebase', show_params_rules, sid)               
            if current_rulebase['name'] == '' and 'name' in rulebase:
                current_rulebase.update({'name': rulebase['name']})
        except Exception:
            FWOLogger.error("could not find rulebase uid=" + rulebase_uid)

            service_provider = ServiceProvider()
            global_state = service_provider.get_global_state()
            api_call = FwoApiCall(FwoApi(ApiUri=global_state.import_state.fwo_config.fwo_api_url, Jwt=global_state.import_state.Jwt))
            description = f"failed to get show-access-rulebase  {rulebase_uid}"
            api_call.create_data_issue(severity=2, description=description)
            raise FwApiError('')

        resolve_checkpoint_uids_via_object_dict(rulebase, native_config_domain,
                                                current_rulebase,
                                                rulebase_uid, show_params_rules)
        total, current = control_while_loop_in_get_rulebases_in_chunks(current_rulebase, rulebase, sid, api_v_url, show_params_rules)

    return current_rulebase

def resolve_checkpoint_uids_via_object_dict(rulebase: dict[str, Any], native_config_domain: dict[str, Any],
                                            current_rulebase: dict[str, Any],
                                            rulebase_uid: str, show_params_rules: dict[str, Any]) -> None:
    """
    Checkpoint stores some rulefields as uids, function translates them to names
    """
    try:
        for rule_field in ['source', 'destination', 'service', 'action',
                          'track', 'install-on', 'time']:
            resolve_ref_list_from_object_dictionary(rulebase, rule_field,
                                               native_config_domain=native_config_domain)
        current_rulebase['chunks'].append(rulebase)
    except Exception:
        
        FWOLogger.error("error while getting a field of layer "
                     + rulebase_uid + ", params: " + str(show_params_rules))
        

def control_while_loop_in_get_rulebases_in_chunks(current_rulebase: dict[str, Any], rulebase: dict[str, Any], sid: str, api_v_url: str, show_params_rules: dict[str, Any]) -> tuple[int, int]:
    total=0
    if 'total' in rulebase:
        total=rulebase['total']
    else:
        FWOLogger.error ( "rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " + str(current_rulebase))
        FWOLogger.warning ( "sid: " + sid)
        FWOLogger.warning ( "api_v_url: " + api_v_url)
        for key, value in show_params_rules.items():
            FWOLogger.warning("show_params_rules " + key + ": " + str(value))
        for key, value in rulebase.items():
            FWOLogger.warning("rulebase " + key + ": " + str(value))
    
    if total==0:
        current=0
    else:
        if 'to' in rulebase:
            current=rulebase['to']
        else:
            raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(rulebase))
    return total, current


def get_inline_layers_recursively(current_rulebase: dict[str, Any], device_config: dict[str, Any], native_config_domain: dict[str, Any], api_v_url: str, sid: str | None, show_params_rules: dict[str, Any], is_global: bool, policy_rulebases_uid_list: list[str]) -> list[str]:
    """Takes current_rulebase, splits sections into sub-rulebases and searches for layerguards to fetch
    """
    current_rulebase_uid = current_rulebase['uid']
    for rulebase_chunk in current_rulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebase_chunk:
            for section in rulebase_chunk['rulebase']:

                section, current_rulebase_uid = section_traversal_and_links(section, current_rulebase_uid, device_config, is_global)

                for rule in section['rulebase']:
                    if 'inline-layer' in rule:
                        # add link to inline layer for current device
                        device_config['rulebase_links'].append({
                            'from_rulebase_uid': current_rulebase_uid,
                            'from_rule_uid': rule['uid'],
                            'to_rulebase_uid': rule['inline-layer'],
                            'type': 'inline',
                            'is_initial': False,
                            'is_global': is_global,
                            'is_section': False
                        })
                        
                        # get inline layer
                        policy_rulebases_uid_list = get_rulebases(api_v_url, sid, show_params_rules,
                                                                  native_config_domain, device_config,
                                                                  policy_rulebases_uid_list,
                                                                  is_global=is_global,
                                                                  access_type='access',
                                                                  rulebase_uid=rule['inline-layer'])
                                                    
    return policy_rulebases_uid_list


def section_traversal_and_links(section: dict[str, Any], current_rulebase_uid: str, device_config: dict[str, Any], is_global: bool) -> tuple[dict[str, Any], str]:
    """If section is actually rule, fake it to be section and link sections as self-contained rulebases
    """

    # if no section is used, create dummy section
    dummy_section = False
    is_section = True
    if section['type'] != 'access-section':
        section = {
            'type': 'access-section',
            'uid': section['uid'],
            'rulebase': [section]
            }
        dummy_section = True

    # define placeholder rules as concatenated rulebase
    if dummy_section and section['rulebase'][0]['type'] == 'place-holder':
        dummy_section = False
        is_section = False

    # define section chain
    if not dummy_section:
        device_config['rulebase_links'].append({
            'from_rulebase_uid': current_rulebase_uid,
            'from_rule_uid': None,
            'to_rulebase_uid': section['uid'],
            'type': 'concatenated',
            'is_global': is_global,
            'is_initial': False,
            'is_section': is_section
        })
        current_rulebase_uid = section['uid']

    return section, current_rulebase_uid


def get_placeholder_in_rulebase(rulebase: dict[str, Any]) -> tuple[str | None, str | None]:
    placeholder_rule_uid = None
    placeholder_rulebase_uid = None
    for rulebase_chunk in rulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebase_chunk:
            for section in rulebase_chunk['rulebase']:

                # if no section is used, use dummy section
                if section['type'] != 'access-section':
                    section: dict[str, Any] = {
                        'type': 'access-section',
                        'rulebase': [section]
                        }

                for rule in section['rulebase']:
                    placeholder_rule_uid, placeholder_rulebase_uid = assign_placeholder_uids(
                        rulebase, section, rule, placeholder_rule_uid, placeholder_rulebase_uid)


    return placeholder_rule_uid, placeholder_rulebase_uid

def assign_placeholder_uids(rulebase: dict[str, Any], section: dict[str, Any], rule: dict[str, Any], placeholder_rule_uid: str | None, placeholder_rulebase_uid: str | None) -> tuple[str | None, str | None]:
    if rule['type'] == 'place-holder':
        placeholder_rule_uid = rule['uid']
        if 'uid' in section:
            placeholder_rulebase_uid = section['uid']
        else:
            placeholder_rulebase_uid = rulebase['uid']
    return placeholder_rule_uid, placeholder_rulebase_uid
    
                            
def get_nat_rules_from_api_as_dict (api_v_url: str, sid: str, show_params_rules: dict[str, Any], native_config_domain: dict[str, Any]={}):
    nat_rules: dict[str, list[Any]] = { "nat_rule_chunks": [] }
    current=0
    total=current+1
    while (current<total) :
        show_params_rules['offset']=current
        FWOLogger.debug ("params: " + str(show_params_rules))
        rulebase = cp_api_call(api_v_url, 'show-nat-rulebase', show_params_rules, sid)

        for rule_field in ['original-source', 'original-destination', 'original-service', 'translated-source',
                          'translated-destination', 'translated-service', 'action', 'track', 'install-on', 'time']:
            resolve_ref_list_from_object_dictionary(rulebase, rule_field, native_config_domain=native_config_domain)

        nat_rules['nat_rule_chunks'].append(rulebase)
        if 'total' in rulebase:
            total=rulebase['total']
        else:
            FWOLogger.error ( "get_nat_rules_from_api - rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " 
                + str(nat_rules))
        if total==0:
            current=0
        else:
            if 'to' in rulebase:
                current=rulebase['to']
            else:
                raise FwApiError("get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(nat_rules))
    return nat_rules


def find_element_by_uid(array: list[dict[str, Any]], uid: str | None) -> dict[str, Any] | None:
    for el in array:
        if 'uid' in el and el['uid']==uid:
            return el
    return None


def resolve_ref_from_object_dictionary(uid: str | None, obj_dict: list[dict[str, Any]], native_config_domain: dict[str, Any]={}, field_name: str | None=None) -> dict[str, Any] | None:

    matched_obj = find_element_by_uid(obj_dict, uid)
        
    if matched_obj is None: # object not in dict - need to fetch it from API
        if field_name != 'track' and uid != '29e53e3d-23bf-48fe-b6b1-d59bd88036f9':
            # 29e53e3d-23bf-48fe-b6b1-d59bd88036f9 is a track object uid (track None) which is not in the object dictionary, but used in some rules
            if field_name is None:
                field_name = 'unknown'
            if uid is None:
                uid = 'unknown'
            FWOLogger.warning(f"object of type {field_name} with uid {uid} not found in object dictionary")
        return None
    else:
        # there are some objects (at least CpmiVoipSipDomain) which are not API-gettable with show-objects (only with show-object "UID")
        # these must be added to the (network) objects tables
        if matched_obj['type'] in ['CpmiVoipSipDomain', 'CpmiVoipMgcpDomain', 'gsn_handover_group']:
            FWOLogger.info(f"adding {matched_obj['type']} '{matched_obj['name']}' object manually, because it is not retrieved by show objects API command")
            color = matched_obj.get('color', 'black')
            native_config_domain['objects'].append({ 
                "type": matched_obj['type'], "chunks": [ {
                "objects": [ {
                'uid': matched_obj['uid'], 'name': matched_obj['name'], 'color': color,
                'type': matched_obj['type'], 'domain': matched_obj['domain']
            } ] } ] } )

        return matched_obj


# resolving all uid references using the object dictionary
# dealing with a single chunk
def resolve_ref_list_from_object_dictionary(rulebase: list[dict[str, Any]] | dict[str, Any], value: str, obj_dicts: list[dict[str, Any]]=[], native_config_domain: dict[str, Any]={}): # TODO: what is objDict: I think it should be a list of dicts
    if isinstance(rulebase, dict) and 'objects-dictionary' in rulebase:
        obj_dicts = rulebase['objects-dictionary']
    if isinstance(rulebase, list): # found a list of rules
        for rule in rulebase:
            if value in rule:
                categorize_value_for_resolve_ref(rule, value, obj_dicts, native_config_domain)
            if 'rulebase' in rule:
                resolve_ref_list_from_object_dictionary(rule['rulebase'], value, obj_dicts=obj_dicts, native_config_domain=native_config_domain)
    elif 'rulebase' in rulebase:
        resolve_ref_list_from_object_dictionary(rulebase['rulebase'], value, obj_dicts=obj_dicts, native_config_domain=native_config_domain)


def categorize_value_for_resolve_ref(rule: dict[str, Any], value: str, obj_dict: list[dict[str, Any]], native_config_domain: dict[str, Any]):
    value_list: list[Any] = []
    if isinstance(rule[value], str): # assuming single uid
        rule[value] = resolve_ref_from_object_dictionary(rule[value], obj_dict, native_config_domain=native_config_domain, field_name=value)
    else:
        if 'type' in rule[value]:   # e.g. track
            rule[value] = resolve_ref_from_object_dictionary(rule[value]['type'], obj_dict, native_config_domain=native_config_domain, field_name=value)
        else:   # assuming list of rules
            for id in rule[value]:
                value_list.append(resolve_ref_from_object_dictionary(id, obj_dict, native_config_domain=native_config_domain, field_name=value))
            rule[value] = value_list # replace ref list with object list


def handle_cpmi_any_object(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle CpmiAnyObject type objects."""
    color = obj.get('color', 'black')
    
    if obj['name'] == 'Any':
        return {
            "type": "hosts", "chunks": [{
                "objects": [{
                    'uid': obj['uid'], 'name': obj['name'], 'color': color,
                    'comments': 'any nw object checkpoint (hard coded)',
                    'type': 'network', 'ipv4-address': fwo_const.ANY_IP_IPV4,
                    'domain': obj['domain']
                }]
            }]
        }
    elif obj['name'] == 'None':  # None service or network object
        return {
            "type": "hosts", "chunks": [{
                "objects": [{
                    'uid': obj['uid'], 'name': obj['name'], 'color': color,
                    'comments': 'none nw object checkpoint (hard coded)',
                    'type': 'group', 'domain': obj['domain']
                }]
            }]
        }
    return {}


def handle_gateway_objects(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle various gateway and network member objects."""
    color = obj.get('color', 'black')
    return {
        "type": "hosts", "chunks": [{
            "objects": [{
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                'domain': obj['domain']
            }]
        }]
    }


def handle_global_object(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle Global type objects."""
    color = obj.get('color', 'black')
    return {
        "type": "hosts", "chunks": [{
            "objects": [{
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': fwo_const.ANY_IP_IPV4,
                'domain': obj['domain']
            }]
        }]
    }


def handle_updatable_objects(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle updatable objects and VoIP domains."""
    color = obj.get('color', 'black')
    return {
        "type": "hosts", "chunks": [{
            "objects": [{
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host',
                'domain': obj['domain']
            }]
        }]
    }


def handle_network_zone_objects(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle Internet and security-zone objects."""
    color = obj.get('color', 'black')
    return {
        "type": "hosts", "chunks": [{
            "objects": [{
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'network', 'ipv4-address': fwo_const.ANY_IP_IPV4,
                'domain': obj['domain']
            }]
        }]
    }


def get_object_details_from_api(uid_missing_obj: str, sid: str='', apiurl: str='') ->  dict[str, Any]:
    FWOLogger.debug(f"getting {uid_missing_obj} from API", 6)

    show_params_host = {'details-level':'full','uid':uid_missing_obj}   # need to get the full object here
    try:
        obj = cp_api_call(apiurl, 'show-object', show_params_host, sid)
    except Exception as e:
        raise FwoImporterError(f"error while trying to get details for object with uid {uid_missing_obj}: {e}")
    
    if obj is None:
        raise FwoImporterError(f"None received while trying to get details for object with uid {uid_missing_obj}")

    if 'object' not in obj:
        if 'code' in obj:
            FWOLogger.warning("broken ref in CP DB uid=" + uid_missing_obj + ": " + obj['code'])
        else:
            FWOLogger.warning("broken ref in CP DB uid=" + uid_missing_obj)
        return {}
        
    obj = obj['object']
    obj_type = obj['type']
    
    # Handle different object types
    if obj_type == 'CpmiAnyObject':
        return handle_cpmi_any_object(obj)
    elif obj_type in ['simple-gateway', 'CpmiGatewayPlain', 'interop', 'multicast-address-range',
                      'CpmiVsClusterMember', 'CpmiVsxClusterMember', 'CpmiVsxNetobj']:
        return handle_gateway_objects(obj)
    elif obj_type == 'Global':
        return handle_global_object(obj)
    elif obj_type in ['updatable-object', 'CpmiVoipSipDomain', 'CpmiVoipMgcpDomain', 'gsn_handover_group']:
        return handle_updatable_objects(obj)
    elif obj_type in ['Internet', 'security-zone']:
        return handle_network_zone_objects(obj)
    elif obj_type == 'access-role':
        return obj
    elif obj_type in cp_const.api_obj_types:    # standard objects with proper ip
        return obj
    else:
        FWOLogger.warning(f"missing nw obj of unexpected type '{obj_type}': {uid_missing_obj}")
        return {}

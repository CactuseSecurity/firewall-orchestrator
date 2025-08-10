# library for API get functions
from asyncio.log import logger
import json
import requests
import time
from datetime import datetime
from typing import Any

from fwo_exceptions import FwLoginFailed, FwApiError, FwApiResponseDecodingError, FwoImporterError
from fwo_log import getFwoLogger
import fwo_globals
import cp_network
import cp_const
import fwo_const
from model_controllers.management_controller import ManagementController
from services.service_provider import ServiceProvider
from services.enums import Services
from fwo_api_call import FwoApiCall, FwoApi

def cp_api_call(url, command, json_payload, sid, show_progress=False):
    url += command
    request_headers = {'Content-Type' : 'application/json'}
    if sid != '': # only not set for login
        request_headers.update({'X-chkp-sid' : sid})

    if fwo_globals.debug_level>8:
        logger.debug(f"api call '{command}'")
        if fwo_globals.debug_level>9 and command!='login':    # do not log passwords
                logger.debug("json_payload: " + str(json_payload) )

    try:
         r = requests.post(url, json=json_payload, headers=request_headers, verify=fwo_globals.verify_certs)
    except requests.exceptions.RequestException as e:
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
    logger = getFwoLogger()
    payload = {'user': mgm_details.ImportUser, 'password': mgm_details.Secret}
    domain = mgm_details.getDomainString()
    if domain is not None and domain != '':
        payload.update({'domain': domain})
    base_url = mgm_details.buildFwApiString()
    if int(fwo_globals.debug_level)>2:
        logger.debug(f"login - login to url {base_url} with user {mgm_details.ImportUser}")
    response = cp_api_call(base_url, 'login', payload, '')
    if "sid" not in response:
        exception_text = f"getter ERROR: did not receive a sid, api call: {base_url}"
        raise FwLoginFailed(exception_text)
    return response["sid"]


def logout(url, sid):
    logger = getFwoLogger()
    if int(fwo_globals.debug_level)>2:
        logger.debug("logout from url " + url)
    response = cp_api_call(url, 'logout', {}, sid)
    return response


def get_changes(sid,api_host,api_port,fromdate):
    logger = getFwoLogger()
    
    dt_object = datetime.fromisoformat(fromdate)
    dt_truncated = dt_object.replace(microsecond=0)     # Truncate microseconds
    fromdate = dt_truncated.isoformat()

    payload = {'from-date' : fromdate, 'details-level' : 'uid'}
    logger.debug ("payload: " + json.dumps(payload))
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'
    task_id = cp_api_call(base_url, 'show-changes', payload, sid)

    logger.debug ("task_id: " + json.dumps(task_id))
    sleeptime = 1
    status = 'in progress'
    while (status == 'in progress'):
        time.sleep(sleeptime)
        tasks = cp_api_call(base_url, 'show-task', task_id, sid)
        if 'tasks' in tasks:
            for task in tasks['tasks']:
                if fwo_globals.debug_level>5:
                    logger.debug ("task: " + json.dumps(task))
                if 'status' in task:
                    status = task['status']
                    if 'succeeded' in status:
                        for detail in task['task-details']:
                            if detail['changes']:
                                logger.debug ("status: " + status + " -> changes found")
                                return 1
                            else:
                                logger.debug ("status: " + status + " -> but no changes found")
                    elif status == 'failed':
                        logger.debug ("show-changes - status: failed -> no changes found")
                    elif status == 'in progress':
                        logger.debug ("status: in progress")
                    else:
                        logger.error ("unknown status: " + status)
                        return -1
                else:
                    logger.error ("no status in task")
                    return -1
        sleeptime += 2
        if sleeptime > 40:
            logger.error ("task took too long, aborting")
            return -1
    return 0


def getPolicyStructure(api_v_url: str, sid: str, show_params_policy_structure: dict[str,Any], policyStructure:list[dict[str,Any]]|None = None) -> None:

    logger = getFwoLogger()
    current=0
    total=current+1
    show_params_policy_structure.update({'offset': current})

    if policyStructure is None:
        policyStructure = []

    while (current<total):

        try:
            packages = cp_api_call(api_v_url, 'show-packages', show_params_policy_structure, sid)
        except Exception:
            raise FwoImporterError("error while running 'show-packages'")

        if 'total' in packages:
            total=packages['total']
        else:
            _log_no_total_case(show_params_policy_structure, packages, api_v_url)
            return
        
        if total==0:
            current=0
        else:
            if 'to' in packages:
                current=packages['to']
            else:
                logger.error ( 'packages do not contain to field')
                return
        
        # parse devices with ordered layers
        for package in packages['packages']:
            _parse_package(package, policyStructure)

    return


def _log_no_total_case(show_params_policy_structure: dict[str,Any], packages: dict[str,Any], api_v_url: str) -> None:
    logger.error ( f'packages do not contain total field, api_v_url: {api_v_url}')
    for key, value in show_params_policy_structure.items():
        logger.warning('show_params_policy_structure ' + key + ': ' + str(value))
    for key, value in packages.items():
        logger.warning('packages ' + key + ': ' + str(value))


def _parse_package(package: dict[str, Any], policyStructure:list[dict[str,Any]]) -> None:
    alreadyFetchedPackage = False
    currentPackage = {}
    # parse package in case of supermanager
    if 'installation-targets' in package \
        and package['installation-targets'] == 'all' \
        and  not alreadyFetchedPackage:
            
        currentPackage = { 'name': package['name'],
                            'uid': package['uid'],
                            'targets': [{'name': 'all', 'uid': 'all'}],
                            'access-layers': []}
        alreadyFetchedPackage = True

    # parse package if at least one installation target exists for sub- or stand-alone-manager
    elif 'installation-targets-revision' in package:
        for installationTarget in package['installation-targets-revision']:
            alreadyFetchedPackage = _parse_package_handle_revisions(installationTarget, alreadyFetchedPackage, currentPackage, package['uid'])
                
    # add access-layers to current package, if at least one installation target was found
    if not alreadyFetchedPackage:
        return
    if 'access-layers' in package:
        _parse_access_layers(package['access-layers'], currentPackage, package)
    # in future threat-layers may be fetched the same way as access-layers
    
    policyStructure.append(currentPackage)


def _parse_package_handle_revisions(installationTarget: dict[str, Any], alreadyFetchedPackage: bool, 
                                    currentPackage: dict[str,Any], package) -> bool:
    if 'target-name' in installationTarget and 'target-uid' in installationTarget:

        if not alreadyFetchedPackage:
            currentPackage = { 'name': package['name'],
                                'uid': package['uid'],
                                'targets': [],
                                'access-layers': []}
            alreadyFetchedPackage = True

        currentPackage['targets'].append({ 'name': installationTarget['target-name'],
                                            'uid': installationTarget['target-uid']})
    else:
        logger.warning ( 'installation target in package: ' + package['uid'] + ' is missing name or uid')
    return alreadyFetchedPackage


def _parse_access_layers(access_layers_in_package, currentPackage, package_uid) -> None:

    for accessLayer in access_layers_in_package:
        if 'name' in accessLayer and 'uid' in accessLayer:
            currentPackage['access-layers'].append({ 'name': accessLayer['name'],
                                                        'uid': accessLayer['uid'],
                                                        'domain': accessLayer['domain']['uid']})
        else:
            logger.warning ( f'access layer in package: {package_uid} is missing name or uid')


def get_global_assignments(api_v_url, sid, show_params_policy_structure) -> list[Any]:
    logger = getFwoLogger()
    current=0
    total=current+1
    show_params_policy_structure.update({'offset': current})
    global_assignments = []

    while (current<total):
        try:
            assignments = cp_api_call(api_v_url, 'show-global-assignments', show_params_policy_structure, sid)
        except Exception:
            logger.error("could not return 'show-global-assignments'")
            raise FwoImporterError( 'could not return "show-global-assignments"')

        if 'total' in assignments:
            total=assignments['total']
        else:
            raise FwoImporterError(_get_global_assignment_warning_log(show_params_policy_structure, assignments, api_v_url))
        
        if total==0:
            current=0
        else:
            if 'to' in assignments:
                current=assignments['to']
            else:
                raise FwoImporterError( 'global assignments do not contain "to" field')

        _parse_global_assignments(assignments, global_assignments)

    return global_assignments


def _get_global_assignment_warning_log(show_params_policy_structure, assignments, api_v_url) -> str:
    warning = f'global assignments do not contain "total" field\napi_v_url: {api_v_url}\n'
    for key, value in show_params_policy_structure.items():
        warning += f'show_params_policy_structure {key}: {str(value)}\n'
    for key, value in assignments.items():
        warning += f'global assignments {key}: {str(value)}\n'
    return warning


def _parse_global_assignments(assignments: dict[str,Any], global_assignments: list[dict[str,Any]]) -> None:
    # parse global assignment
    for assignment in assignments['objects']:
        if 'type' not in assignment and assignment['type'] != 'global-assignment':
            raise FwoImporterError ('global assignment with unexpected type')
        global_assignment = {
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
        global_assignments.append(global_assignment)


def get_rulebases(api_v_url, sid, show_params_rules, nativeConfigDomain, deviceConfig, policy_rulebases_uid_list, is_global=False, access_type='access', rulebaseUid=None, rulebaseName=None):
    
    # access_type: access / nat
    logger = getFwoLogger()
    nativeConfigRulebaseKey = 'rulebases'
    current_rulebase = {}

    if nativeConfigDomain is None:
        nativeConfigDomain = {'rulebases': [], 'nat_rulebases': []}
    if deviceConfig is None:
        deviceConfig = {'rulebase_links': []}

    if access_type == 'access':
        nativeConfigRulebaseKey = 'rulebases'
    elif access_type == 'nat':
        nativeConfigRulebaseKey = 'nat_rulebases'
    else:
        logger.error('access_type is neither "access" nor "nat", but ' + access_type)

    # get uid of rulebase
    if rulebaseUid is not None:
        pass
    elif rulebaseName is not None:
        rulebaseUid = get_uid_of_rulebase(rulebaseName, api_v_url, access_type, sid)
    else:
        logger.error('must provide either rulebaseUid or rulebaseName')
    policy_rulebases_uid_list.append(rulebaseUid)
    
    # search all rulebases in nativeConfigDomain and import if rulebase is not already fetched
    fetchedRulebaseList = []
    for fetchedRulebase in nativeConfigDomain[nativeConfigRulebaseKey]:
        fetchedRulebaseList.append(fetchedRulebase['uid'])
        if fetchedRulebase['uid'] == rulebaseUid:
            current_rulebase = fetchedRulebase
            break

    # get rulebase in chunks
    if rulebaseUid not in fetchedRulebaseList:
        current_rulebase = get_rulebases_in_chunks(rulebaseUid, show_params_rules, api_v_url, access_type, sid, nativeConfigDomain)
        nativeConfigDomain[nativeConfigRulebaseKey].append(current_rulebase)

    # use recursion to get inline layers
    policy_rulebases_uid_list = get_inline_layers_recursively(current_rulebase, deviceConfig, nativeConfigDomain, api_v_url, sid,
                                                              show_params_rules, is_global, policy_rulebases_uid_list)    
    
    return policy_rulebases_uid_list


def get_uid_of_rulebase(rulebaseName, api_v_url, access_type, sid):
    rulebaseUid = None
    get_rulebase_uid_params = {
        'name': rulebaseName,
        'limit': 1,
        'use-object-dictionary': False,
        'details-level': 'uid',
        'show-hits': False
    }
    try:
        rulebaseForUid = cp_api_call(api_v_url, 'show-' + access_type + '-rulebase', get_rulebase_uid_params, sid)
        rulebaseUid = rulebaseForUid['uid']
    except Exception:
        logger.error("could not find uid for rulebase name=" + rulebaseName)

    return rulebaseUid


def get_rulebases_in_chunks(rulebaseUid, show_params_rules, api_v_url, access_type, sid, nativeConfigDomain):

    current_rulebase = {'uid': rulebaseUid, 'name': '', 'chunks': []}
    show_params_rules.update({'uid': rulebaseUid})
    current=0
    total=current+1

    while (current<total):

        show_params_rules.update({'offset': current})
    
        try:
            rulebase = cp_api_call(api_v_url, 'show-' + access_type + '-rulebase', show_params_rules, sid)               
            if current_rulebase['name'] == '' and 'name' in rulebase:
                current_rulebase.update({'name': rulebase['name']})
        except Exception:
            logger.error("could not find rulebase uid=" + rulebaseUid)

            service_provider = ServiceProvider()
            global_state = service_provider.get_service(Services.GLOBAL_STATE)
            api_call = FwoApiCall(FwoApi(ApiUri=global_state.import_state.FwoConfig.FwoApiUri, Jwt=global_state.import_state.Jwt))
            description = f"failed to get show-access-rulebase  {rulebaseUid}"
            api_call.create_data_issue(severity=2, description=description)
            raise FwApiError('')

        resolve_checkpoint_uids_via_object_dict(rulebase, nativeConfigDomain,
                                                current_rulebase,
                                                rulebaseUid, show_params_rules)
        total, current = control_while_loop_in_get_rulebases_in_chunks(current_rulebase, rulebase, sid, api_v_url, show_params_rules)

    return current_rulebase

def resolve_checkpoint_uids_via_object_dict(rulebase, nativeConfigDomain,
                                            current_rulebase,
                                            rulebaseUid, show_params_rules):
    """
    Checkpoint stores some rulefields as uids, function translates them to names
    """
    try:
        for ruleField in ['source', 'destination', 'service', 'action',
                          'track', 'install-on', 'time']:
            resolve_ref_list_from_object_dictionary(rulebase, ruleField,
                                               nativeConfigDomain=nativeConfigDomain)
        current_rulebase['chunks'].append(rulebase)
    except Exception:
        
        logger.error("error while getting a field of layer "
                     + rulebaseUid + ", params: " + str(show_params_rules))
        

def control_while_loop_in_get_rulebases_in_chunks(current_rulebase, rulebase, sid, api_v_url, show_params_rules):
    total=0
    if 'total' in rulebase:
        total=rulebase['total']
    else:
        logger.error ( "rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " + str(current_rulebase))
        logger.warning ( "sid: " + sid)
        logger.warning ( "api_v_url: " + api_v_url)
        for key, value in show_params_rules.items():
            logger.warning("show_params_rules " + key + ": " + str(value))
        for key, value in rulebase.items():
            logger.warning("rulebase " + key + ": " + str(value))
    
    if total==0:
        current=0
    else:
        if 'to' in rulebase:
            current=rulebase['to']
        else:
            raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(rulebase))
    return total, current


def get_inline_layers_recursively(current_rulebase, deviceConfig, nativeConfigDomain, api_v_url, sid, show_params_rules, is_global, policy_rulebases_uid_list):
    """Takes current_rulebase, splits sections into sub-rulebases and searches for layerguards to fetch
    """
    current_rulebase_uid = current_rulebase['uid']
    for rulebase_chunk in current_rulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebase_chunk:
            for section in rulebase_chunk['rulebase']:

                section, current_rulebase_uid = section_traversal_and_links(section, current_rulebase_uid, deviceConfig, is_global)

                for rule in section['rulebase']:
                    if 'inline-layer' in rule:
                        # add link to inline layer for current device
                        deviceConfig['rulebase_links'].append({
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
                                                                  nativeConfigDomain, deviceConfig,
                                                                  policy_rulebases_uid_list,
                                                                  is_global=is_global,
                                                                  access_type='access',
                                                                  rulebaseUid=rule['inline-layer'])
                                                    
    return policy_rulebases_uid_list


def section_traversal_and_links(section, current_rulebase_uid, deviceConfig, is_global):
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
        deviceConfig['rulebase_links'].append({
            'from_rulebase_uid': current_rulebase_uid,
            'from_rule_uid': '',
            'to_rulebase_uid': section['uid'],
            'type': 'concatenated',
            'is_global': is_global,
            'is_initial': False,
            'is_section': is_section
        })
        current_rulebase_uid = section['uid']

    return section, current_rulebase_uid


def get_placeholder_in_rulebase(rulebase):

    placeholder_rule_uid = ''
    placeholder_rulebase_uid = ''
    for rulebase_chunk in rulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebase_chunk:
            for section in rulebase_chunk['rulebase']:

                # if no section is used, use dummy section
                if section['type'] != 'access-section':
                    section = {
                        'type': 'access-section',
                        'rulebase': [section]
                        }

                for rule in section['rulebase']:
                    placeholder_rule_uid, placeholder_rulebase_uid = assign_placeholder_uids(
                        rulebase, section, rule, placeholder_rule_uid, placeholder_rulebase_uid)


    return placeholder_rule_uid, placeholder_rulebase_uid

def assign_placeholder_uids(rulebase, section, rule, placeholder_rule_uid, placeholder_rulebase_uid):
    if rule['type'] == 'place-holder':
        placeholder_rule_uid = rule['uid']
        if 'uid' in section:
            placeholder_rulebase_uid = section['uid']
        else:
            placeholder_rulebase_uid = rulebase['uid']
    return placeholder_rule_uid, placeholder_rulebase_uid
    
                            
def get_nat_rules_from_api_as_dict (api_v_url, sid, show_params_rules, nativeConfigDomain={}):
    logger = getFwoLogger()
    nat_rules = { "nat_rule_chunks": [] }
    current=0
    total=current+1
    while (current<total) :
        show_params_rules['offset']=current
        logger.debug ("params: " + str(show_params_rules))
        rulebase = cp_api_call(api_v_url, 'show-nat-rulebase', show_params_rules, sid)

        for ruleField in ['original-source', 'original-destination', 'original-service', 'translated-source',
                          'translated-destination', 'translated-service', 'action', 'track', 'install-on', 'time']:
            resolve_ref_list_from_object_dictionary(rulebase, ruleField, nativeConfigDomain=nativeConfigDomain)

        nat_rules['nat_rule_chunks'].append(rulebase)
        if 'total' in rulebase:
            total=rulebase['total']
        else:
            logger.error ( "get_nat_rules_from_api - rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " 
                + str(nat_rules))
        if total==0:
            current=0
        else:
            if 'to' in rulebase:
                current=rulebase['to']
            else:
                raise FwApiError("get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(nat_rules))
    return nat_rules


def find_element_by_uid(array, uid):
    for el in array:
        if 'uid' in el and el['uid']==uid:
            return el
    return None


def resolve_ref_from_object_dictionary(uid, objDict, native_config_domain={}, field_name=None):

    matched_obj = find_element_by_uid(objDict, uid)
        
    if matched_obj is None: # object not in dict - need to fetch it from API
        if field_name != 'track' and uid != '29e53e3d-23bf-48fe-b6b1-d59bd88036f9':
            # 29e53e3d-23bf-48fe-b6b1-d59bd88036f9 is a track object uid (track None) which is not in the object dictionary, but used in some rules
            if field_name is None:
                field_name = 'unknown'
            if uid is None:
                uid = 'unknown'
            logger.warning(f"object of type {field_name} with uid {uid} not found in object dictionary")
        return None
    else:
        # there are some objects (at least CpmiVoipSipDomain) which are not API-gettable with show-objects (only with show-object "UID")
        # these must be added to the (network) objects tables
        if matched_obj['type'] in ['CpmiVoipSipDomain', 'CpmiVoipMgcpDomain', 'gsn_handover_group']:
            logger.info(f"adding {matched_obj['type']} '{matched_obj['name']}' object manually, because it is not retrieved by show objects API command")
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
def resolve_ref_list_from_object_dictionary(rulebase, value, objDict={}, nativeConfigDomain={}):
    if 'objects-dictionary' in rulebase:
        objDict = rulebase['objects-dictionary']
    if isinstance(rulebase, list): # found a list of rules
        for rule in rulebase:
            if value in rule:
                categorize_value_for_resolve_ref(rule, value, objDict, nativeConfigDomain)
            if 'rulebase' in rule:
                resolve_ref_list_from_object_dictionary(rule['rulebase'], value, objDict=objDict, nativeConfigDomain=nativeConfigDomain)
    elif 'rulebase' in rulebase:
        resolve_ref_list_from_object_dictionary(rulebase['rulebase'], value, objDict=objDict, nativeConfigDomain=nativeConfigDomain)


def categorize_value_for_resolve_ref(rule, value, objDict, nativeConfigDomain):
    value_list = []
    if isinstance(rule[value], str): # assuming single uid
        rule[value] = resolve_ref_from_object_dictionary(rule[value], objDict, native_config_domain=nativeConfigDomain, field_name=value)
    else:
        if 'type' in rule[value]:   # e.g. track
            rule[value] = resolve_ref_from_object_dictionary(rule[value]['type'], objDict, native_config_domain=nativeConfigDomain, field_name=value)
        else:   # assuming list of rules
            for id in rule[value]:
                value_list.append(resolve_ref_from_object_dictionary(id, objDict, native_config_domain=nativeConfigDomain, field_name=value))
            rule[value] = value_list # replace ref list with object list


def getObjectDetailsFromApi(uid_missing_obj, sid='', apiurl='') ->  dict[str, Any]:
    logger = getFwoLogger()
    if fwo_globals.debug_level>5:
        logger.debug(f"getting {uid_missing_obj} from API")

    show_params_host = {'details-level':'full','uid':uid_missing_obj}   # need to get the full object here
    try:
        obj = cp_api_call(apiurl, 'show-object', show_params_host, sid)
    except Exception as e:
        raise FwoImporterError(f"error while trying to get details for object with uid {uid_missing_obj}: {e}")
    
    if obj is None:
        raise FwoImporterError(f"None received while trying to get details for object with uid {uid_missing_obj}")

    if 'object' in obj:
        obj = obj['object']
        color = obj.get('color', 'black')
        if (obj['type'] == 'CpmiAnyObject'):
            if (obj['name'] == 'Any'):
                return  { "type": "hosts", "chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': color,
                    'comments': 'any nw object checkpoint (hard coded)',
                    'type': 'network', 'ipv4-address': fwo_const.any_ip_ipv4,
                    'domain': obj['domain']
                    } ] } ] }
            elif (obj['name'] == 'None'): # None service or network object
                return  { "type": "hosts", "chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': color,
                    'comments': 'none nw object checkpoint (hard coded)',
                    'type': 'group', 'domain': obj['domain']
                    } ] } ] }
        elif (obj['type'] in [ 'simple-gateway', 'CpmiGatewayPlain', 'interop', 'multicast-address-range',
                                'CpmiVsClusterMember', 'CpmiVsxClusterMember', 'CpmiVsxNetobj' ]):
            return { "type": "hosts", "chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                'domain': obj['domain']
                } ] } ] }
        elif (obj['type'] == 'Global'):
            return {"type": "hosts", "chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': fwo_const.any_ip_ipv4,
                'domain': obj['domain']
                } ] } ] }
        elif (obj['type'] in [ 'updatable-object', 'CpmiVoipSipDomain', 'CpmiVoipMgcpDomain', 'gsn_handover_group' ]):
            return {"type": "hosts", "chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host',
                'domain': obj['domain']
                } ] } ] }
        elif (obj['type'] in ['Internet', 'security-zone']):
            return {"type": "hosts", "chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'network', 'ipv4-address': fwo_const.any_ip_ipv4,
                'domain': obj['domain']
                } ] } ] }
        elif (obj['type'] == 'access-role'):
            return obj
        elif obj['type'] in cp_const.api_obj_types:    # standard objects with proper ip
            return obj
        else:
            logger.warning ( "missing nw obj of unexpected type '" + obj['type'] + "': " + uid_missing_obj )
    else:
        if 'code' in obj:
            logger.warning("broken ref in CP DB uid=" + uid_missing_obj + ": " + obj['code'])
        else:
            logger.warning("broken ref in CP DB uid=" + uid_missing_obj)
    return {}

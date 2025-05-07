# library for API get functions
from asyncio.log import logger
import json
import requests, requests.packages
import time
from datetime import datetime

from fwo_exceptions import FwLoginFailed, FwApiError, FwApiResponseDecodingError
from fwo_log import getFwoLogger
import fwo_globals
import cp_network
import cp_const
import fwo_const
from fwo_exceptions import ImportInterruption
from model_controllers.management_details_controller import ManagementDetailsController


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


def login(mgm_details: ManagementDetailsController):
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


def getPolicyStructure(api_v_url, sid, show_params_policy_structure, policyStructure = []):

    logger = getFwoLogger()

    current=0
    total=current+1

    show_params_policy_structure.update({'offset': current})

    while (current<total):

        try:
            packages = cp_api_call(api_v_url, 'show-packages', show_params_policy_structure, sid)
        except Exception:
            logger.error("could not return 'show-packages'")
            return 1

        if 'total' in packages:
            total=packages['total']
        else:
            logger.error ( 'packages do not contain total field')
            logger.warning ( 'sid: ' + sid)
            logger.warning ( 'api_v_url: ' + api_v_url)
            for key, value in show_params_policy_structure.items():
                logger.warning('show_params_policy_structure ' + key + ': ' + str(value))
            for key, value in packages.items():
                logger.warning('packages ' + key + ': ' + str(value))
            return 1
        
        if total==0:
            current=0
        else:
            if 'to' in packages:
                current=packages['to']
            else:
                logger.error ( 'packages do not contain to field')
                return 1
        
        # parse devices with ordered layers
        for package in packages['packages']:
            alreadyFetchedPackage = False

            # parse package in case of supermanager
            if 'installation-targets' in package and package['installation-targets'] == 'all':
                if not alreadyFetchedPackage:
                    
                    currentPacakage = { 'name': package['name'],
                                        'uid': package['uid'],
                                        'targets': [{'name': 'all', 'uid': 'all'}],
                                        'access-layers': []}
                    alreadyFetchedPackage = True

            # parse package if at least one installation target exists for sub- or stand-alone-manager
            elif 'installation-targets-revision' in package:
                for installationTarget in package['installation-targets-revision']:
                    if 'target-name' in installationTarget and 'target-uid' in installationTarget:

                        if not alreadyFetchedPackage:
                            currentPacakage = { 'name': package['name'],
                                                'uid': package['uid'],
                                                'targets': [],
                                                'access-layers': []}
                            alreadyFetchedPackage = True

                        currentPacakage['targets'].append({ 'name': installationTarget['target-name'],
                                                            'uid': installationTarget['target-uid']})
                    else:
                        logger.warning ( 'installation target in package: ' + package['uid'] + ' is missing name or uid')
                
            # add access-layers to current package, if at least one installation target was found
            if alreadyFetchedPackage:
                if 'access-layers' in package:
                    for accessLayer in package['access-layers']:
                        if 'name' in accessLayer and 'uid' in accessLayer:
                            currentPacakage['access-layers'].append({ 'name': accessLayer['name'],
                                                                        'uid': accessLayer['uid'],
                                                                        'domain': accessLayer['domain']['uid']})
                        else:
                            logger.warning ( 'access layer in package: ' + package['uid'] + ' is missing name or uid')
                # in future threat-layers may be fetched the same way as access-layers
                
                policyStructure.append(currentPacakage)

    return 0


def getGlobalAssignments(api_v_url, sid, show_params_policy_structure, globalAssignments = []):
    logger = getFwoLogger()
    current=0
    total=current+1
    show_params_policy_structure.update({'offset': current})

    while (current<total):
        try:
            assignments = cp_api_call(api_v_url, 'show-global-assignments', show_params_policy_structure, sid)
        except Exception:
            logger.error("could not return 'show-global-assignments'")
            return 1

        if 'total' in assignments:
            total=assignments['total']
        else:
            logger.error ( 'global assignments do not contain total field')
            logger.warning ( 'sid: ' + sid)
            logger.warning ( 'api_v_url: ' + api_v_url)
            for key, value in show_params_policy_structure.items():
                logger.warning('show_params_policy_structure ' + key + ': ' + str(value))
            for key, value in assignments.items():
                logger.warning('global assignments ' + key + ': ' + str(value))
            return 1
        
        if total==0:
            current=0
        else:
            if 'to' in assignments:
                current=assignments['to']
            else:
                logger.error ( 'global assignments do not contain to field')
                return 1

        # parse global assignment
        for assignment in assignments['objects']:
            if 'type' in assignment and assignment['type'] == 'global-assignment':
                globalAssignment = {
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

                globalAssignments.append(globalAssignment)

            else:
                logger.error ('global assignment with unexpected type')

    return 0
                        

def get_rulebases(api_v_url, sid, show_params_rules,
                 rulebaseUid=None,
                 rulebaseName=None,
                 access_type='access',
                 nativeConfig=None,
                 deviceConfig=None):
    if nativeConfig is None:
        nativeConfig = {'rulebases': [], 'nat_rulebases': []}
    if deviceConfig is None:
        deviceConfig = {'rulebase_links': []}
    
    # access_type: access / nat
    logger = getFwoLogger()

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
            return 1
    else:
        logger.error('must provide either rulebaseUid or rulebaseName')
        return 1
    
    # search all rulebases in nativeConfig and import if rulebase is not already fetched
    fetchedRulebaseList = []
    for fetchedRulebase in nativeConfig[nativeConfigRulebaseKey]:
        fetchedRulebaseList.append(fetchedRulebase['uid'])
        if fetchedRulebase['uid'] == rulebaseUid:
            currentRulebase = fetchedRulebase
            break

    if rulebaseUid not in fetchedRulebaseList:
        currentRulebase = {'uid': rulebaseUid, 'name': '', 'chunks': []}
        show_params_rules.update({'uid': rulebaseUid})
        current=0
        total=current+1

        # get rulebase in chunks
        while (current<total):

            show_params_rules.update({'offset': current})
        
            try:
                rulebase = cp_api_call(api_v_url, 'show-' + access_type + '-rulebase', show_params_rules, sid)
                if fwo_globals.shutdown_requested:
                    raise ImportInterruption("Shutdown requested during rulebase retrieval.")                
                if currentRulebase['name'] == '' and 'name' in rulebase:
                    currentRulebase.update({'name': rulebase['name']})
            except Exception:
                logger.error("could not find rulebase uid=" + rulebaseUid)
                # todo: need to get FWO API jwt here somehow:
                # create_data_issue(fwo_api_base_url, jwt, severity=2, description="failed to get show-access-rulebase  " + rulebaseUid)
                return 1

            # resolve checkpoint uids via object dictionary
            try:
                for ruleField in ['source', 'destination', 'service', 'action', 'track', 'install-on', 'time']:
                    resolveRefListFromObjectDictionary(rulebase, ruleField, nativeConfig=nativeConfig, sid=sid, base_url=api_v_url)
                currentRulebase['chunks'].append(rulebase)
            except Exception:
                logger.error("error while getting field " + ruleField + " of layer " + rulebaseUid + ", params: " + str(show_params_rules))
                return 1

            if 'total' in rulebase:
                total=rulebase['total']
            else:
                logger.error ( "rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " + str(currentRulebase))
                logger.warning ( "sid: " + sid)
                logger.warning ( "api_v_url: " + api_v_url)
                logger.warning ( "access_type: " + access_type)
                for key, value in show_params_rules.items():
                    logger.warning("show_params_rules " + key + ": " + str(value))
                for key, value in rulebase.items():
                    logger.warning("rulebase " + key + ": " + str(value))
                return 1
            
            if total==0:
                current=0
            else:
                if 'to' in rulebase:
                    current=rulebase['to']
                else:
                    raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(rulebase))

        nativeConfig[nativeConfigRulebaseKey].append(currentRulebase)

    # use recursion to get inline layers
    for rulebase_chunk in currentRulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebase_chunk:
            for section in rulebase_chunk['rulebase']:

                # if no section is used, create dummy section
                if section['type'] != 'access-section':
                    section = {
                        'type': 'access-section',
                        'rulebase': [section]
                        }

                for rule in section['rulebase']:
                    if 'inline-layer' in rule:
                        # add link to inline layer for current device
                        deviceConfig['rulebase_links'].append({
                            'from_rulebase_uid': currentRulebase['uid'],
                            'from_rule_uid': rule['uid'],
                            'to_rulebase_uid': rule['inline-layer'],
                            'type': 'inline',
                            'is_initial': False,
                            'is_global': False  # TODO: check if we are also importing global rulebases here, if so, we need to add an is_global parameter
                        })
                        
                        # get inline layer
                        get_rulebases(api_v_url, sid, show_params_rules,
                                        rulebaseUid=rule['inline-layer'],
                                        access_type='access',
                                        nativeConfig=nativeConfig,
                                        deviceConfig=deviceConfig)
    
    return 0


def getRuleUid(rulebase, mode):
    # mode: last/place-holder

    return_uid = ''
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
                    if mode == 'last':
                        return_uid = rule['uid']
                    elif mode == 'place-holder':
                        if rule['type'] == 'place-holder':
                            return_uid = rule['uid']

    return return_uid
                            

def get_nat_rules_from_api_as_dict (api_v_url, sid, show_params_rules, nativeConfig={}):
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
            resolveRefListFromObjectDictionary(rulebase, ruleField, sid=sid, base_url=api_v_url, nativeConfig=nativeConfig)

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


def resolveRefFromObjectDictionary(id, objDict, nativeConfig={}, sid='', base_url='', rule4debug={}):

    matched_obj = find_element_by_uid(objDict, id)

    if matched_obj is None: # object not in dict - neet to fetch it from API
        logger.warning(f"did not find object with uid {id} in object dictionary")
        return None
    else:
        # there are some objects (at least CpmiVoipSipDomain) which are not API-gettable with show-objects (only with show-object "UID")
        # these must be added to the (network) objects tables
        if matched_obj['type'] in ['CpmiVoipSipDomain', 'CpmiVoipMgcpDomain']:
            logger.info(f"adding voip domain '{matched_obj['name']}' object manually, because it is not retrieved by show objects API command")
            if 'object_tables' in nativeConfig:
                color = matched_obj.get('color', 'black')
                nativeConfig['object_tables'].append({ 
                        "object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': matched_obj['uid'], 'name': matched_obj['name'], 'color': color,
                        'type': matched_obj['type']
                    } ] } ] } )
            else:
                logger.warning(f"found no existing object_tables while adding voip domain '{matched_obj['name']}' object")

        return matched_obj


# resolving all uid references using the object dictionary
# dealing with a single chunk
def resolveRefListFromObjectDictionary(rulebase, value, objDict={}, nativeConfig={}, location='network', sid='', base_url=''):
    if 'objects-dictionary' in rulebase:
        objDict = rulebase['objects-dictionary']
    if isinstance(rulebase, list): # found a list of rules
        for rule in rulebase:
            if value in rule:
                value_list = []
                if isinstance(rule[value], str): # assuming single uid
                    rule[value] = resolveRefFromObjectDictionary(rule[value], objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url, rule4debug=rule)
                else:
                    if 'type' in rule[value]:   # e.g. track
                        rule[value] = resolveRefFromObjectDictionary(rule[value]['type'], objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url, rule4debug=rule)
                    else:   # assuming list of rules
                        for id in rule[value]:
                            value_list.append(resolveRefFromObjectDictionary(id, objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url, rule4debug=rule))
                        rule[value] = value_list # replace ref list with object list
            if 'rulebase' in rule:
                resolveRefListFromObjectDictionary(rule['rulebase'], value, objDict=objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url)
    elif 'rulebase' in rulebase:
        resolveRefListFromObjectDictionary(rulebase['rulebase'], value, objDict=objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url)


def getObjectDetailsFromApi(uid_missing_obj, sid='', apiurl='', debug_level=0):
    logger = getFwoLogger()
    if debug_level>5:
        logger.debug(f"getting {uid_missing_obj} from API")

    show_params_host = {'details-level':'full','uid':uid_missing_obj}   # need to get the full object here
    try:
        obj = cp_api_call(apiurl, 'show-object', show_params_host, sid)
    except Exception as e:
        logger.exception(f"error while trying to get details for object with uid {uid_missing_obj}: {e}")
        return None
    
    if obj is None:
        logger.warning("got 'None' from CP API for uid=" + uid_missing_obj)
        return None
    if 'object' in obj:
        obj = obj['object']
        color = obj.get('color', 'black')

        if (obj['type'] == 'CpmiAnyObject'):
            if (obj['name'] == 'Any'):
                return  { "object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': color,
                    'comments': 'any nw object checkpoint (hard coded)',
                    'type': 'network', 'ipv4-address': fwo_const.any_ip_ipv4
                    } ] } ] }
            elif (obj['name'] == 'None'):
                return  { "object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': color,
                    'comments': 'any nw object checkpoint (hard coded)',
                    'type': 'group'
                    } ] } ] }
        elif (obj['type'] in [ 'simple-gateway', 'CpmiGatewayPlain', 'interop', 'multicast-address-range', 'CpmiVsClusterMember', 'CpmiVsxClusterMember', 'CpmiVsxNetobj' ]):
            return { "object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                } ] } ] }
        elif (obj['type'] == 'Global'):
            return {"object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': fwo_const.any_ip_ipv4,
                } ] } ] }
        elif (obj['type'] in [ 'updatable-object', 'CpmiVoipSipDomain', 'CpmiVoipMgcpDomain' ]):
            return {"object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'group'
                } ] } ] }
        elif (obj['type'] in ['Internet', 'security-zone']):
            return {"object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': color,
                'comments': obj['comments'], 'type': 'network', 'ipv4-address': fwo_const.any_ip_ipv4,
                } ] } ] }
        elif (obj['type'] == 'access-role'):
            return obj
        elif obj['type'] in cp_const.api_obj_types:    # standard objects with proper ip
            return obj
        else:
            logger.warning ( "missing nw obj of unexpected type '" + obj['type'] + "': " + uid_missing_obj )
    else:
        if 'code' in obj:
            logger.warning(f"broken ref in CP DB uid={uid_missing_obj}: {obj['code']}")
        else:
            logger.warning(f"broken ref in CP DB uid={uid_missing_obj}")

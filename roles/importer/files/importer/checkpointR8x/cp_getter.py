# library for API get functions
from asyncio.log import logger
import json
import re
import requests, requests.packages
import time
from common import FwLoginFailed
from fwo_log import getFwoLogger
import fwo_globals
import cp_network
import cp_const


def cp_api_call(url, command, json_payload, sid, show_progress=False):
    url += command
    request_headers = {'Content-Type' : 'application/json'}
    if sid != '': # only not set for login
        request_headers.update({'X-chkp-sid' : sid})

    if fwo_globals.debug_level>8:
        logger.debug(f"api call '{command}'")
        if fwo_globals.debug_level>9:
            if command!='login':    # do not log passwords
                logger.debug("json_payload: " + str(json_payload) )

    try:
         r = requests.post(url, json=json_payload, headers=request_headers, verify=fwo_globals.verify_certs)
    except requests.exceptions.RequestException as e:
        raise Exception("error, url: " + str(url))
        
    if r is None:
        if 'password' in json.dumps(json_payload):
            exception_text = "\nerror while sending api_call containing credential information to url '" + str(url)
        else:
            exception_text = "\nerror while sending api_call to url '" + str(url) + "' with payload '" + json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2)
        raise Exception (exception_text)
    if show_progress:
        print ('.', end='', flush=True)

    try:
        json_response = r.json()
    except:
        raise Exception("checkpointR8x:api_call: response is not in valid json format: " + r.text)
    return json_response


def login(user, password, api_host, api_port, domain):
    logger = getFwoLogger()
    payload = {'user': user, 'password': password}
    if domain is not None and domain != '':
        payload.update({'domain': domain})
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'
    if int(fwo_globals.debug_level)>2:
        logger.debug("login - login to url " + base_url + " with user " + user)
    response = cp_api_call(base_url, 'login', payload, '')
    if "sid" not in response:
        exception_text = "\ngetter ERROR: did not receive a sid during login, " + \
            "api call: api_host: " + str(api_host) + ", api_port: " + str(api_port) + ", base_url: " + str(base_url) + \
            ", ssl_verification: " + str(fwo_globals.verify_certs)
        raise  FwLoginFailed(exception_text)
    return response["sid"]


def logout(url, sid):
    logger = getFwoLogger()
    if int(fwo_globals.debug_level)>2:
        logger.debug("logout from url " + url)
    response = cp_api_call(url, 'logout', {}, sid)
    return response


def get_api_url(sid, api_host, api_port, user, base_url, limit, test_version, ssl_verification, debug_level=0):
    logger = getFwoLogger()

    v_url = ''
    if test_version == 'off':
        v_url = base_url
    else:
        api_versions = cp_api_call(base_url, 'show-api-versions', {}, sid)
        api_version = api_versions["current-version"]
        api_supported = api_versions["supported-versions"]

        if debug_level>3:
            logger.debug ("current version: " + api_version + "; supported versions: "+ ', '.join(api_supported) + "; limit:"+ str(limit) )
            logger.debug ("getter - login:" + user + "; sid:" + sid )
        if re.search(r'^\d+[\.\d+]+$', test_version) or re.search(r'^\d+$', test_version):
            if test_version in api_supported :
                v_url = base_url + 'v' + test_version + '/'
            else:
                raise Exception("api version " + test_version + " not supported")
        else:
            logger.debug ("not a valid version")
            raise Exception("\"" + test_version +"\" - not a valid version")
    logger.debug ("test_version: " + test_version + " - url: "+ v_url)
    return v_url


def set_api_url(base_url,testmode,api_supported,hostname, debug_level=0):
    logger = getFwoLogger()
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported :
                url = base_url + 'v' + testmode + '/'
            else:
                raise Exception("api version " + testmode + " is not supported by the manager " + hostname + " - Import is canceled")
        else:
            logger.debug ("not a valid version")
            raise Exception("\"" + testmode +"\" - not a valid version")
    logger.debug ("testmode: " + testmode + " - url: "+ url)
    return url


def get_changes(sid,api_host,api_port,fromdate):
    logger = getFwoLogger()
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
    # delete_v: return:
    # [{'name':'policy1', 'uid':'bla', 'targets':[{'name':'gateway1', 'uid':'bla'}], 'access-layers':[{'name':'ord1', 'uid':'ord1'}]}]
    logger = getFwoLogger()

    current=0
    total=current+1

    show_params_policy_structure.update({'offset': current})

    while (current<total):

        try:
            packages = cp_api_call(api_v_url, 'show-packages', show_params_policy_structure, sid)
        except:
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
                raise Exception ( 'packages do not contain to field')
                return 1
        
# [{'name':'policy1', 'uid':'bla', 'targets':[{'name':'gateway1', 'uid':'bla'}], 'access-layers':[{'name':'ord1', 'uid':'ord1'}]}]
        # parse devices with ordered layers
        for package in packages['packages']:
            alreadyFetchedPackage = False

            # parse package if at least one installation target exists
            if 'installation-targets-revision' in package:
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
                
                if alreadyFetchedPackage:
                    if 'access-layers' in package:
                        for accessLayer in package['access-layers']:
                            if 'name' in accessLayer and 'uid' in accessLayer:
                                currentPacakage['access-layers'].append({ 'name': accessLayer['name'],
                                                                          'uid': accessLayer['uid']})
                            else:
                                logger.warning ( 'access layer in package: ' + package['uid'] + ' is missing name or uid')
                    # in future threat-layers may be fetched the same way as access-layers
                    
                    policyStructure.append(currentPacakage)

    return 0
                        

def getRulebases (api_v_url, sid, show_params_rules,
                  rulebaseUid=None,
                  rulebaseName=None,
                  access_type='access',
                  nativeConfig={'rulebases':[],'nat_rulebases':[]},
                  deviceConfig={'rulebae_links': []}):
    
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
            # delete_v hier nochmal genau das return format ansehen
            rulebaseUid = rulebaseForUid['uid']
        except:
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
                if currentRulebase['name'] == '' and 'name' in rulebase:
                    currentRulebase.update({'name': rulebase['name']})
            except:
                logger.error("could not find rulebase uid=" + rulebaseUid)
                # todo: need to get FWO API jwt here somehow:
                # create_data_issue(fwo_api_base_url, jwt, severity=2, description="failed to get show-access-rulebase  " + rulebaseUid)
                return 1

            # resolve checkpoint uids via object dictionary
            try:
                for ruleField in ['source', 'destination', 'service', 'action', 'track', 'install-on', 'time']:
                    resolveRefListFromObjectDictionary(rulebase, ruleField, nativeConfig=nativeConfig, sid=sid, base_url=api_v_url)
                currentRulebase['chunks'].append(rulebase)
            except:
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
    for rulebaseChunk in currentRulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebaseChunk:
            for section in rulebaseChunk['rulebase']:

                # if no section is used, use dummy section
                if section['type'] == 'access-rule':
                    section = {
                        'type': 'access-section',
                        'rulebase': [section]
                        }

                if section['type'] == 'access-section':
                    for rule in section['rulebase']:
                        if 'inline-layer' in rule:
                            # add link to inline layer for current device
                            deviceConfig['rulebase_links'].append({
                                'from_rulebase_uid': currentRulebase['uid'],
                                'from_rule_uid': rule['uid'],
                                'to_rulebase_uid': rule['inline-layer'],
                                'type': 'inline'})
                            
                            # get inline layer
                            getRulebases(api_v_url, sid, show_params_rules,
                                         rulebaseUid=rule['inline-layer'],
                                         access_type='access',
                                         nativeConfig=nativeConfig,
                                         deviceConfig=deviceConfig)
    
    return 0


def getRuleUid(rulebase, mode):
    # mode: last/place-holder

    for rulebaseChunk in rulebase['chunks']:
        # search in case of access rulebase only
        if 'rulebase' in rulebaseChunk:
            for section in rulebaseChunk['rulebase']:

                # if no section is used, use dummy section
                if section['type'] == 'access-rule':
                    section = {
                        'type': 'access-section',
                        'rulebase': [section]
                        }

                if section['type'] == 'access-section':
                    for rule in section['rulebase']:
                        if mode == 'last':
                            returnUid = rule['uid']
                        elif mode == 'place-holder':
                            if rule['type'] == 'place-holder':
                                returnUid = rule['uid']

    return returnUid
                            


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
                current = total # assuming we do not have any NAT rules, so skipping this step
                # logger.warning ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(nat_rules))
                raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(nat_rules))
    return nat_rules


# insert domain rule layer after rule_idx within top_ruleset
def insert_layer_after_place_holder (top_ruleset_json, domain_ruleset_json, placeholder_uid, nativeConfig={}):
    logger = getFwoLogger()
    # serialize domain rule chunks
    domain_rules_serialized = []
    for chunk in domain_ruleset_json['rulebase_chunks']:
        domain_rules_serialized.extend(chunk['rulebase'])

    # set the upper (parent) rule uid for all domain rules:
    for rule in domain_rules_serialized:
        rule['parent_rule_uid'] = placeholder_uid
        logger.debug ("domain_rules_serialized, added parent_rule_uid for rule with uid " + rule['uid'])

    # find the reference (place-holder rule) and insert the domain rules behind it:
    chunk_idx = 0
    while chunk_idx<len(top_ruleset_json['rulebase_chunks']):
        rules = top_ruleset_json['rulebase_chunks'][chunk_idx]['rulebase']
        rule_idx = 0
        while rule_idx<len(rules):
            if rules[rule_idx]['uid'] == placeholder_uid:
                logger.debug ("found matching rule uid, "  + placeholder_uid + " == " + rules[rule_idx]['uid'])
                rules[rule_idx+1:rule_idx+1] = domain_rules_serialized
                top_ruleset_json['rulebase_chunks'][chunk_idx]['rulebase'] = rules
            rule_idx += 1
        chunk_idx += 1
    if fwo_globals.debug_level>5:
        logger.debug("result:\n" + json.dumps(top_ruleset_json, indent=2))
    return top_ruleset_json


def findElementByUid(array, uid):
    for el in array:
        if 'uid' in el and el['uid']==uid:
            return el
    return None


def resolveRefFromObjectDictionary(id, objDict, nativeConfig={}, sid='', base_url='', rule4debug={}):

    matchedObj = findElementByUid(objDict, id)

    if matchedObj is None: # object not in dict - neet to fetch it from API
        logger.warning(f"did not find object with uid {id} in object dictionary")
        return None
    else:
        # there are some objects (at least CpmiVoipSipDomain) which are not API-gettable with show-objects (only with show-object "UID")
        # these must be added to the (network) objects tables
        if matchedObj['type'] in ['CpmiVoipSipDomain', 'CpmiVoipMgcpDomain']:
            logger.info(f"adding voip domain '{matchedObj['name']}' object manually, because it is not retrieved by show objects API command")
            if 'object_tables' in nativeConfig:
                nativeConfig['object_tables'].append({ 
                        "object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': matchedObj['uid'], 'name': matchedObj['name'], 'color': matchedObj['color'],
                        'type': matchedObj['type']
                    } ] } ] } )
            else:
                logger.warning(f"found no existing object_tables while adding voip domain '{matchedObj['name']}' object")

        return matchedObj


# resolving all uid references using the object dictionary
# dealing with a single chunk
def resolveRefListFromObjectDictionary(rulebase, value, objDict={}, nativeConfig={}, location='network', sid='', base_url=''):
    if 'objects-dictionary' in rulebase:
        objDict = rulebase['objects-dictionary']
    if isinstance(rulebase, list): # found a list of rules
        for rule in rulebase:
            if value in rule:
                valueList = []
                if isinstance(rule[value], str): # assuming single uid
                    rule[value] = resolveRefFromObjectDictionary(rule[value], objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url, rule4debug=rule)
                else:
                    if 'type' in rule[value]:   # e.g. track
                        rule[value] = resolveRefFromObjectDictionary(rule[value]['type'], objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url, rule4debug=rule)
                    else:   # assuming list of rules
                        for id in rule[value]:
                            valueList.append(resolveRefFromObjectDictionary(id, objDict, nativeConfig=nativeConfig, sid=sid, base_url=base_url, rule4debug=rule))
                        rule[value] = valueList # replace ref list with object list
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
    else:
        if obj is not None:
            if 'object' in obj:
                obj = obj['object']
                if (obj['type'] == 'CpmiAnyObject'):
                    if (obj['name'] == 'Any'):
                        return  { "object_type": "hosts", "object_chunks": [ {
                            "objects": [ {
                            'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                            'comments': 'any nw object checkpoint (hard coded)',
                            'type': 'network', 'ipv4-address': '0.0.0.0/0'
                            } ] } ] }
                    elif (obj['name'] == 'None'):
                        return  { "object_type": "hosts", "object_chunks": [ {
                            "objects": [ {
                            'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                            'comments': 'any nw object checkpoint (hard coded)',
                            'type': 'group'
                            } ] } ] }
                elif (obj['type'] in [ 'simple-gateway', obj['type'], 'CpmiGatewayPlain', obj['type'] == 'interop' ]):
                    return { "object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                        } ] } ] }
                elif obj['type'] == 'multicast-address-range':
                    return {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                        } ] } ] }
                elif (obj['type'] in ['CpmiVsClusterMember', 'CpmiVsxClusterMember', 'CpmiVsxNetobj']):
                    return {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                        } ] } ] }
                elif (obj['type'] == 'Global'):
                    return {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
                elif (obj['type'] in [ 'updatable-object', 'CpmiVoipSipDomain', 'CpmiVoipMgcpDomain' ]):
                    return {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'group'
                        } ] } ] }
                elif (obj['type'] in ['Internet', 'security-zone']):
                    return {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'network', 'ipv4-address': '0.0.0.0/0',
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
        else:
            logger.warning("got 'None' from CP API for uid=" + uid_missing_obj)

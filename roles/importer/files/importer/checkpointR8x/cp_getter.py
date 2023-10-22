# library for API get functions
from asyncio.log import logger
from distutils.log import debug
import json
import re
import requests, requests.packages
import time
from common import FwLoginFailed
from fwo_log import getFwoLogger
import fwo_globals


def cp_api_call(url, command, json_payload, sid, show_progress=False):
    url += command
    request_headers = {'Content-Type' : 'application/json'}
    if sid != '': # only not set for login
        request_headers.update({'X-chkp-sid' : sid})

    if fwo_globals.debug_level>4:
        logger.debug("using sid: " + sid )

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
        logger.debug("auto-discover - login to url " + base_url + " with user " + user)
    response = cp_api_call(base_url, 'login', payload, '')
    if "sid" not in response:
        exception_text = "\ngetter ERROR: did not receive a sid during login, " + \
            "api call: api_host: " + str(api_host) + ", api_port: " + str(api_port) + ", base_url: " + str(base_url) + \
            ", ssl_verification: " + str(fwo_globals.verify_certs)
        raise  FwLoginFailed(exception_text)
    return response["sid"]


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


def collect_uids_from_rule(rule, nw_uids_found, svc_uids_found):
    # just a guard:
    if 'rule-number' in rule and 'type' in rule and rule['type'] != 'place-holder':
        logger = getFwoLogger()

        if rule['type']=='access-rule': # normal rule (no nat) - merging lists
            lsources = rule["source"]
            ldestinations = rule["destination"]
            lservices = rule["service"]

        elif rule['type']=='nat-rule':
            lsources = [rule["translated-source"], rule["original-source"]]
            ldestinations = [rule["translated-destination"], rule["original-destination"]]
            lservices = [rule["translated-service"], rule["original-service"]]

        for src in lsources:
            if 'type' in src:
                if src['type'] == 'LegacyUserAtLocation':
                    nw_uids_found.append(src["location"])
                elif src['type'] == 'access-role':
                    if isinstance(src['networks'], str):  # just a single source
                        if src['networks'] != 'any':   # ignore any objects as they do not contain a uid
                            nw_uids_found.append(src['networks'])
                    else:  # more than one source
                        for nw in src['networks']:
                            nw_uids_found.append(nw)
                else:  # standard network objects as source, only here we have an uid value
                    nw_uids_found.append(src['uid'])
            else:
                #logger.warning ("found src without type field: " + json.dumps(src))                
                if 'uid' in src:
                    nw_uids_found.append(src['uid'])

        for dst in ldestinations:
            nw_uids_found.append(dst['uid'])
        for svc in lservices:
            svc_uids_found.append(svc['uid'])
    return


def collect_uids_from_rulebase(rulebase, nw_uids_found, svc_uids_found, debug_text):
    logger = getFwoLogger()
    chunk_name = ''
    if 'layerchunks' in rulebase:
        chunk_name = 'layerchunks'
    elif 'nat_rule_chunks' in rulebase:
        chunk_name = 'nat_rule_chunks'
    else:
        for rule in rulebase:
            if 'rulebase' in rule:
                collect_uids_from_rulebase(rule['rulebase'], nw_uids_found, svc_uids_found, debug_text + '.')
            else:
                collect_uids_from_rule(rule, nw_uids_found, svc_uids_found)
        return
    for layer_chunk in rulebase[chunk_name]:
        if 'rulebase' in layer_chunk:
            if fwo_globals.debug_level>5:
                debug_layer_str = "handling layer with uid " + layer_chunk['uid']
                if 'name' in layer_chunk:
                    debug_layer_str += '(' + layer_chunk['name'] + ')'
                logger.debug ( debug_layer_str )
            for rule in layer_chunk['rulebase']:
                if 'rule-number' in rule and 'type' in rule and rule['type'] != 'place-holder':
                    collect_uids_from_rule(rule, nw_uids_found, svc_uids_found)
                else:
                    if 'rulebase' in rule and rule['rulebase'] != []: # found a layer within a rulebase, recursing
                        if fwo_globals.debug_level>8:
                            logger.debug ("found embedded rulebase - recursing")
                        collect_uids_from_rulebase(rule['rulebase'], nw_uids_found, svc_uids_found, debug_text + '.')
    return


def get_all_uids_of_a_type(object_table, obj_table_names):
    all_uids = []

    if object_table['object_type'] in obj_table_names:
        for chunk in object_table['object_chunks']:
            if 'objects' in chunk:
                for obj in chunk['objects']:
                    if 'uid' in obj:
                        all_uids.append(obj['uid'])  # add non-group (simple) refs
                    elif 'uid-in-updatable-objects-repository' in obj:
                        all_uids.append(obj['uid-in-updatable-objects-repository'])  # add updatable obj uid
                    else:
                        logger.warning ("found nw obj without UID: " + str(obj))

    all_uids = list(set(all_uids)) # remove duplicates
    return all_uids


def get_broken_object_uids(all_uids_from_obj_tables, all_uids_from_rules):
    broken_uids = []
    for uid in all_uids_from_rules:
        if not uid in all_uids_from_obj_tables:
            broken_uids.append(uid)
    return list(set(broken_uids))


def get_layer_from_api_as_dict (api_v_url, sid, show_params_rules, layername, access_type='access', collection_type='rulebase'):
    # access_type: access / nat
    # collection_type: rulebase / layer
    logger = getFwoLogger()
    current_layer_json = { "layername": layername, "layerchunks": [] }
    current=0
    total=current+1
    while (current<total) :

        show_params_rules['offset']=current
        if collection_type=='layer':
            show_params_rules['name']=layername

        try:
            rulebase = cp_api_call(api_v_url, 'show-' + access_type + '-rulebase', show_params_rules, sid)
            current_layer_json['layerchunks'].append(rulebase)
        except:
            logger.error("could not find layer " + layername)
            # todo: need to get FWO API jwt here somehow:
            # create_data_issue(fwo_api_base_url, jwt, severity=2, description="failed to get show-access-rulebase  " + layername)
            return None

        if 'total' in rulebase:
            total=rulebase['total']
        if 'total' in rulebase:
            total=rulebase['total']
        else:
            logger.error ( "rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " + str(current_layer_json))
            logger.warning ( "sid: " + sid)
            logger.warning ( "api_v_url: " + api_v_url)
            logger.warning ( "access_type: " + access_type)
            for key, value in show_params_rules.items():
                logger.warning("show_params_rules " + key + ": " + str(value))
            for key, value in rulebase.items():
                logger.warning("rulebase " + key + ": " + str(value))
            return None
        
        if total==0:
            current=0
        else:
            if 'to' in rulebase:
                current=rulebase['to']
            else:
                raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(rulebase))

    #################################################################################
    # adding inline and domain layers (if they exist)
    add_inline_layers (current_layer_json, api_v_url, sid, show_params_rules)    

    return current_layer_json


def add_inline_layers (rulebase, api_v_url, sid, show_params_rules, access_type='access', collection_type='layer'):

    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    add_inline_layers(rules_chunk, api_v_url, sid, show_params_rules)
    else:
        if 'rulebase' in rulebase:
            rulebase_idx = 0
            for rule in rulebase['rulebase']:
                if 'inline-layer' in rule:
                    inline_layer_name = rule['inline-layer']['name']
                    if fwo_globals.debug_level>5:
                        logger.debug ( "found inline layer " + inline_layer_name )
                    inline_layer = get_layer_from_api_as_dict (api_v_url, sid, show_params_rules, inline_layer_name, access_type=access_type, collection_type=collection_type)
                    rulebase['rulebase'][rulebase_idx+1:rulebase_idx+1] = inline_layer['layerchunks']  #### insert inline layer here
                    rulebase_idx += len(inline_layer['layerchunks'])

                if 'name' in rule and rule['name'] == "Placeholder for domain rules":
                    logger.debug ("getter - found domain rules reference with uid " + rule["uid"])
                rulebase_idx += 1


def get_nat_rules_from_api_as_dict (api_v_url, sid, show_params_rules):
    logger = getFwoLogger()
    nat_rules = { "nat_rule_chunks": [] }
    current=0
    total=current+1
    while (current<total) :
        show_params_rules['offset']=current
        logger.debug ("get_nat_rules_from_api_as_dict params: " + str(show_params_rules))
        rulebase = cp_api_call(api_v_url, 'show-nat-rulebase', show_params_rules, sid)
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
                raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(nat_rules))
    return nat_rules


# insert domain rule layer after rule_idx within top_ruleset
def insert_layer_after_place_holder (top_ruleset_json, domain_ruleset_json, placeholder_uid):
    logger = getFwoLogger()
    # serialize domain rule chunks
    domain_rules_serialized = []
    for chunk in domain_ruleset_json['layerchunks']:
        domain_rules_serialized.extend(chunk['rulebase'])

    # set the upper (parent) rule uid for all domain rules:
    for rule in domain_rules_serialized:
        rule['parent_rule_uid'] = placeholder_uid
        logger.debug ("domain_rules_serialized, added parent_rule_uid for rule with uid " + rule['uid'])

    # find the reference (place-holder rule) and insert the domain rules behind it:
    chunk_idx = 0
    while chunk_idx<len(top_ruleset_json['layerchunks']):
        rules = top_ruleset_json['layerchunks'][chunk_idx]['rulebase']
        rule_idx = 0
        while rule_idx<len(rules):
            if rules[rule_idx]['uid'] == placeholder_uid:
                logger.debug ("found matching rule uid, "  + placeholder_uid + " == " + rules[rule_idx]['uid'])
                rules[rule_idx+1:rule_idx+1] = domain_rules_serialized
                top_ruleset_json['layerchunks'][chunk_idx]['rulebase'] = rules
            rule_idx += 1
        chunk_idx += 1
    if fwo_globals.debug_level>5:
        logger.debug("result:\n" + json.dumps(top_ruleset_json, indent=2))
    return top_ruleset_json

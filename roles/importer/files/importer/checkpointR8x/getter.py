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

details_level = "full"    # 'standard'
use_object_dictionary = 'false'

# all obj table names to look at:
api_obj_types = [
    'hosts', 'networks', 'groups', 'address-ranges', 'multicast-address-ranges', 'groups-with-exclusion', 'gateways-and-servers',
    'security-zones', 'dynamic-objects', 'dns-domains', # 'trusted-clients',
    'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-icmp', 'services-icmp6' ]

svc_obj_table_names = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-other', 'services-icmp', 'services-icmp6']
# usr_obj_table_names : do not exist yet - not fetchable via API


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
    api_versions = cp_api_call(base_url, 'show-api-versions', {}, sid)
    api_version = api_versions["current-version"]
    api_supported = api_versions["supported-versions"]

    if debug_level>3:
        logger.debug ("current version: " + api_version + "; supported versions: "+ ', '.join(api_supported) + "; limit:"+ str(limit) )
        logger.debug ("getter - login:" + user + "; sid:" + sid )
    v_url = ''
    if test_version == 'off':
        v_url = base_url
    else:
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
        for src in rule["source"]:
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
        for dst in rule["destination"]:
            nw_uids_found.append(dst['uid'])
        for svc in rule["service"]:
            svc_uids_found.append(svc['uid'])
    return


def collect_uids_from_rulebase(rulebase, nw_uids_found, svc_uids_found, debug_text):
    logger = getFwoLogger()
    if 'layerchunks' in rulebase:
        for layer_chunk in rulebase['layerchunks']:
            if 'rulebase' in layer_chunk:
                if fwo_globals.debug_level>5:
                    logger.debug ( "handling layer " + layer_chunk['name'] + " with uid " + layer_chunk['uid'] )
                for rule in layer_chunk['rulebase']:
                    if 'rule-number' in rule and 'type' in rule and rule['type'] != 'place-holder':
                        collect_uids_from_rule(rule, nw_uids_found, svc_uids_found)
                    else:
                        if 'rulebase' in rule: # found a layer within a rulebase, recursing
                            if fwo_globals.debug_level>8:
                                logger.debug ("found embedded rulebase - recursing")
                            collect_uids_from_rulebase(rule['rulebase'], nw_uids_found, svc_uids_found, debug_text + '.')
    else:
        for rule in rulebase:
            collect_uids_from_rule(rule, nw_uids_found, svc_uids_found)
    return


def get_all_uids_of_a_type(object_table, obj_table_names):
    all_uids = []

    if object_table['object_type'] in obj_table_names:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                all_uids.append(obj['uid'])  # add non-group (simple) refs
    all_uids = list(set(all_uids)) # remove duplicates
    return all_uids


def get_broken_object_uids(all_uids_from_obj_tables, all_uids_from_rules):
    broken_uids = []
    for uid in all_uids_from_rules:
        if not uid in all_uids_from_obj_tables:
            broken_uids.append(uid)
    return list(set(broken_uids))


def get_inline_layer_names_from_rulebase(rulebase, inline_layers):
    logger = getFwoLogger()
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    get_inline_layer_names_from_rulebase(rules_chunk, inline_layers)
    else:
        if 'rulebase' in rulebase:
            # add section header, but only if it does not exist yet (can happen by chunking a section)
            for rule in rulebase['rulebase']:
                if 'inline-layer' in rule:
                    inline_layers.append(rule['inline-layer']['name'])
                if 'name' in rule and rule['name'] == "Placeholder for domain rules":
                    logger.debug ("getter - found domain rules reference with uid " + rule["uid"])

        if 'rule-number' in rulebase:   # not a rulebase but a single rule
            if 'inline-layer' in rulebase:
                inline_layers.append(rulebase['inline-layer']['name'])
                # get_inline_layer_names_from_rulebase(rulebase, inline_layers)


def get_layer_from_api_as_dict (api_host, api_port, api_v_url, sid, show_params_rules, layername):
    logger = getFwoLogger()
    current_layer_json = { "layername": layername, "layerchunks": [] }
    current=0
    total=current+1
    while (current<total) :
        show_params_rules['offset']=current
        try:
            rulebase = cp_api_call(api_v_url, 'show-access-rulebase', show_params_rules, sid)
            current_layer_json['layerchunks'].append(rulebase)
        except:
            logger.error("could not find layer " + layername)
            # todo: need to get FWO API jwt here somehow:
            # create_data_issue(fwo_api_base_url, jwt, severity=2, description="failed to get show-access-rulebase  " + layername)
            return None

        if 'total' in rulebase:
            total=rulebase['total']
        else:
            logger.error ( "rulebase does not contain total field, get_rulebase_chunk_from_api found garbled json " 
                + str(current_layer_json))
            return None

        if total==0:
            current=0
        else:
            if 'to' in rulebase:
                current=rulebase['to']
            else:
                raise Exception ( "get_nat_rules_from_api - rulebase does not contain to field, get_rulebase_chunk_from_api found garbled json " + str(rulebase))
        if fwo_globals.debug_level>6:
            logger.debug ( "get_layer_from_api_as_dict current offset: "+ str(current) )
    return current_layer_json


def get_nat_rules_from_api_as_dict (api_host, api_port, api_v_url, sid, show_params_rules):
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

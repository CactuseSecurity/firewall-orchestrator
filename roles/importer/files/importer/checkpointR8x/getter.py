#!/usr/bin/python3
# library for API get functions
import requests, json, argparse, pdb
import requests.packages.urllib3, time, logging, re, sys
import os
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

details_level = "full"    # 'standard'
use_object_dictionary = 'false'

# all obj table names to look at:
api_obj_types = [
    'hosts', 'networks', 'groups', 'address-ranges', 'groups-with-exclusion', 'gateways-and-servers',
    'security-zones', 'dynamic-objects', 'trusted-clients', 'dns-domains',
    'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-icmp', 'services-icmp6' ]

nw_obj_table_names = ['hosts', 'networks', 'address-ranges', 'groups', 'gateways-and-servers', 'simple-gateways']  
# do not consider: CpmiAnyObject, CpmiGatewayPlain, external 
svc_obj_table_names = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-other', 'services-icmp', 'services-icmp6']
# usr_obj_table_names : do not exist yet - not fetchable via API


def api_call(ip_addr, port, url, command, json_payload, sid, ssl_verification, proxy_string):
    url = url + command
    if sid == '':
        request_headers = {'Content-Type' : 'application/json'}
    else:
        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
    return r.json()


def login(user,password,api_host,api_port,domain, ssl_verification, proxy_string):
    if domain == '':
       payload = {'user':user, 'password' : password}
    else:
        payload = {'user':user, 'password' : password, 'domain' :  domain}
    base_url = 'https://' + api_host + ':' + api_port + '/web_api/'
    response = api_call(api_host, api_port, base_url, 'login', payload, '', ssl_verification, proxy_string)
    if "sid" not in response:
        print ("getter ERROR: did not receive a sid during login")
        print ("api call: api_host: " + str(api_host) + ", api_port: " + str(api_port) + ", base_url: " + str(base_url) + ", payload: " + str(payload) +
            ", ssl_verification: " + str(ssl_verification) + ", proxy_string: " + str(proxy_string))
        sys.exit(1)
    return response["sid"]


def set_ssl_verification(ssl_verification_mode):
    logger = logging.getLogger(__name__)
    if ssl_verification_mode == '' or ssl_verification_mode == 'off':
        ssl_verification = False
        logger.debug ("ssl_verification: False")
    else:
        ssl_verification = ssl_verification_mode
        logger.debug ("ssl_verification: [ca]certfile="+ ssl_verification )
    return ssl_verification


def get_api_url(sid, api_host, api_port, user, base_url, limit, test_version, ssl_verification, proxy_string):
    logger = logging.getLogger(__name__)
    api_versions = api_call(api_host, api_port, base_url, 'show-api-versions', {}, sid, ssl_verification, proxy_string)
    api_version = api_versions["current-version"]
    api_supported = api_versions["supported-versions"]

    logging.debug ("getter - current version: "+ api_version )
    logging.debug ("getter - supported versions: "+ ', '.join(api_supported) )
    logging.debug ("getter - limit:"+ limit )
    logging.debug ("getter - login:" + user )
    logging.debug ("getter - sid:"+ sid )
    v_url = ''
    if test_version == 'off':
        v_url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', test_version) or re.search(r'^\d+$', test_version):
            if test_version in api_supported :
                v_url = base_url + 'v' + test_version + '/'
            else:
                logging.debug ("getter - api version " + test_version + " is not supported by the manager " + api_host + " - Import is canceled")
                #v_url = base_url
                sys.exit("api version " + test_version + " not supported")
        else:
            logging.debug ("getter.py::get_api_url - not a valid version")
            sys.exit("\"" + test_version +"\" - not a valid version")
    logging.debug ("getter.py::get_api_url  - test_version: " + test_version + " - url: "+ v_url)
    return v_url


def set_api_url(base_url,testmode,api_supported,hostname):
    logger = logging.getLogger(__name__)
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported :
                url = base_url + 'v' + testmode + '/'
            else:
                logger.debug ("api version " + testmode + " is not supported by the manager " + hostname + " - Import is canceled")
                sys.exit("api version " + testmode +" not supported")
        else:
            logger.debug ("not a valid version")
            sys.exit("\"" + testmode +"\" - not a valid version")
    logger.debug ("testmode: " + testmode + " - url: "+ url)
    return url


def collect_uids_from_rule(rule, debug_text):
    nw_uids_found = []
    svc_uids_found = []
 
    if 'rule-number' in rule:  # standard rule, no section header (layered rules)
        if 'type' in rule and rule['type'] != 'place-holder':
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
            #logging.debug ("getter::collect_uids_from_rule nw_uids_found: " + str(nw_uids_found))
            #logging.debug ("getter::collect_uids_from_rule svc_uids_found: " + str(svc_uids_found))
        return (nw_uids_found, svc_uids_found)
    else: # recurse into rulebase within rule
        return collect_uids_from_rulebase(rule["rulebase"], debug_text + ", recursion")


def collect_uids_from_rulebase(rulebase, debug_text):
    nw_uids_found = []
    svc_uids_found = []

    if 'layerchunks' in rulebase:
        logging.debug ("getter::collect_uids_from_rulebase found layerchunks " + debug_text )
        for layer_chunk in rulebase['layerchunks']:
            logging.debug ("getter::collect_uids_from_rulebase found chunk " + layer_chunk['name'] + " with uid " + layer_chunk['uid'] )
            for rule in layer_chunk['rulebase']:
                (nw_uids_found_in_rule, svc_uids_found_in_rule) = collect_uids_from_rule(rule, debug_text + "calling collect_uids_from_rule - if")
                if nw_uids_found_in_rule is not None:
                    nw_uids_found.extend(nw_uids_found_in_rule)
                if svc_uids_found_in_rule is not None:
                    svc_uids_found.extend(svc_uids_found_in_rule)
    else:
        for rule in rulebase:
            (nw_uids_found, svc_uids_found) = collect_uids_from_rule(rule, debug_text)

    #logging.debug ("getter::collect_uids_from_rulebase nw_uids_found: " + str(nw_uids_found))
    #logging.debug ("getter::collect_uids_from_rulebase svc_uids_found: " + str(svc_uids_found))
    return (nw_uids_found, svc_uids_found)


def get_all_uids_of_a_type(object_table, obj_table_names):
    all_uids = []

    if object_table['object_type'] in obj_table_names:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                all_uids.append(obj['uid'])  # add non-group (simple) refs
    all_uids = list(set(all_uids)) # remove duplicates
    return all_uids


def get_broken_object_uids(all_uids_from_obj_tables, all_uids_from_rules):
    logger = logging.getLogger(__name__)
    logging.debug ("getter - entering get_broken_object_uids" )
    broken_uids = []
    for uid in all_uids_from_rules:
        # logging.debug ("getter - uid from rules: " + uid )
        if not uid in all_uids_from_obj_tables:
            broken_uids.append(uid)
            logging.debug ("getter - found missing uid from obj_tables: " + uid )
    return list(set(broken_uids))


def get_inline_layer_names_from_rulebase(rulebase, inline_layers):
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rules_chunk in chunk['rulebase']:
                get_inline_layer_names_from_rulebase(rules_chunk, inline_layers)
    else:
        if 'rulebase' in rulebase:
            # logging.debug ( "enrich_config - searching for inline layers in layer " + rulebase['layername'] )
            # add section header, but only if it does not exist yet (can happen by chunking a section)
            for rule in rulebase['rulebase']:
                if 'inline-layer' in rule:
                    inline_layers.append(rule['inline-layer']['name'])
                if rule['name'] == "Placeholder for domain rules":
                    logging.debug ("getter - found domain rules reference with uid " + rule["uid"])

        if 'rule-number' in rulebase:   # not a rulebase but a single rule
            if 'inline-layer' in rulebase:
                inline_layers.append(rulebase['inline-layer']['name'])
                # get_inline_layer_names_from_rulebase(rulebase, inline_layers)

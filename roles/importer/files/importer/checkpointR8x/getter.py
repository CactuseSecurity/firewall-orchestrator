#!/usr/bin/python3
# library for API get functions

import requests, json, argparse, pdb
import requests.packages.urllib3, time, logging, re, sys
import os
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

offset = 0
details_level = "full"    # 'standard'
json_indent=2
use_object_dictionary = 'false'
#limit="25"

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
    return response["sid"]


def collect_uids_from_rule(rule, debug_text):
    nw_uids_found = []
    svc_uids_found = []
 
    if 'rule-number' in rule:  # standard rule, no section header (layered rules)
        for src in rule["source"]:
            if src['type'] == 'LegacyUserAtLocation':
                # user_objects.append(src["userGroup"])
                #print ("Legacy found user uid: " + src["userGroup"] + ", " + debug_text)
                nw_uids_found.append(src["location"])
                #print ("Legacy found nw uid: " + src["location"] + ", " + debug_text)
            elif src['type'] == 'access-role':
                # user_objects.append(src['uid'])
                if isinstance(src['networks'], str):  # just a single source
                    if src['networks'] != 'any':   # ignore any objects as they do not contain a uid
                       nw_uids_found.append(src['networks'])
                else:  # more than one source
                    for nw in src['networks']:
                        nw_uids_found.append(nw)
            else:  # standard network objects as source
                #print ("found nw uid (standard, no usr rule): " + src["uid"] + ", " + debug_text)
                nw_uids_found.append(src['uid'])
        for dst in rule["destination"]:
            nw_uids_found.append(dst['uid'])
        for svc in rule["service"]:
            svc_uids_found.append(svc['uid'])
    else: # recurse into rulebase within rule
        #print ("rule - else zweig - collect_uids_from_rule: " + debug_text)
        (nw_uids_from_sub_rulebase, svc_uids_from_sub_rulebase) = collect_uids_from_rulebase(rule["rulebase"], debug_text + ", recursion")
    return (nw_uids_found.extend(nw_uids_from_sub_rulebase), svc_uids_found.extend(svc_uids_from_sub_rulebase))


def collect_uids_from_rulebase(rulebase, debug_text):
    nw_uids_found = []
    svc_uids_found = []

    if 'layerchunks' in rulebase:
        #print (debug_text + ", found layerchanks in layered rulebase , " + debug_text)
        for layer_chunk in rulebase['layerchunks']:
            #print ("found chunk in layerchanks with name " + layer_chunk['name'] + ' , '+ debug_text)
            for rule in layer_chunk['rulebase']:
                #print ("found rules_chunk in rulebase with uid " + layer_chunk['uid'] + ', ' + debug_text)
                (nw_uids_from_sub_rulebase, svc_uids_from_sub_rulebase) = collect_uids_from_rule(rule, debug_text + "calling collect_uids_from_rule - if")
                nw_uids_found.extend(nw_uids_from_sub_rulebase)
                svc_uids_found.extend(svc_uids_from_sub_rulebase)
    else:
        #print ("else: found no layerchunks in rulebase")
        for rule in rulebase:
            (nw_uids_from_sub_rulebase, svc_uids_from_sub_rulebase) = collect_uids_from_rule(rule, debug_text)
            # print ("rule found: " + str(rule))
            nw_uids_found.extend(nw_uids_from_sub_rulebase)
            svc_uids_found.extend(svc_uids_from_sub_rulebase)
    return (nw_uids_found, svc_uids_found)


def get_all_uids_of_a_type(object_table, obj_table_names):
    all_uids = []

    if object_table['object_type'] in obj_table_names:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                # if 'members' in obj:   # add group member refs
                #     for member in obj['members']:
                #         all_uids.append(member)
                all_uids.append(obj['uid'])  # add non-group (simple) refs
    all_uids = list(set(all_uids)) # remove duplicates
    return all_uids

    
def get_broken_object_uids(all_uids_from_obj_tables, all_uids_from_rules):
    broken_uids = []
    for uid in all_uids_from_rules:
        if not uid in all_uids_from_obj_tables:
            broken_uids.append(uid)
    return list(set(broken_uids))


def get_ip_of_obj(obj):
    if 'ipv4-address' in obj:
        ip_addr = obj['ipv4-address']
    elif 'ipv6-address' in obj:
        ip_addr = obj['ipv6-address']
    elif 'subnet4' in obj:
        ip_addr = obj['subnet4'] + '/' + str(obj['mask-length4'])
    elif 'subnet6' in obj:
        ip_addr = obj['subnet6'] + '/' + str(obj['mask-length6'])
    elif 'obj_typ' in obj and obj['obj_typ'] == 'group':
        ip_addr = ''
    else:
        ip_addr = '0.0.0.0/0'
    return ip_addr


def get_api_url(sid, api_host, api_port, user, base_url, limit, test_version, ssl_verification, proxy_string):
    api_versions = api_call(api_host, api_port, base_url, 'show-api-versions', {}, sid, ssl_verification, proxy_string)
    api_version = api_versions["current-version"]
    api_supported = api_versions["supported-versions"]

    logging.debug ("get_config_cp_r8x_api - current version: "+ api_version )
    logging.debug ("get_config_cp_r8x_api - supported versions: "+ ', '.join(api_supported) )
    logging.debug ("get_config_cp_r8x_api - limit:"+ limit )
    logging.debug ("get_config_cp_r8x_api - login:" + user )
    logging.debug ("get_config_cp_r8x_api - sid:"+ sid )

    #test_version = '1.5'
    # v_url definiton - version dependent
    v_url = ''
    if test_version == 'off':
        v_url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', test_version) or re.search(r'^\d+$', test_version):
            if test_version in api_supported :
                v_url = base_url + 'v' + test_version + '/'
            else:
                logging.debug ("get_config_cp_r8x_api - api version " + test_version + " is not supported by the manager " + api_host + " - Import is canceled")
                #v_url = base_url
                sys.exit("api version " + test_version + " not supported")
        else:
            logging.debug ("getter.py::get_api_url - not a valid version")
            sys.exit("\"" + test_version +"\" - not a valid version")
    logging.debug ("getter.py::get_api_url  - test_version: " + test_version + " - url: "+ v_url)
    return v_url

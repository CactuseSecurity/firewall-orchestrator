#!/usr/bin/python3

import common, getter
import json, argparse, pdb, sys, time, logging
import requests, requests.packages.urllib3
import os
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password_file', default='id_rsa', help='name of the file to read the password for management server from')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
parser.add_argument('-c', '--configfile', metavar='config_file', required=True, help='filename to read and write config in json format from/to')
parser.add_argument('-n', '--noapi', metavar='mode', default='false', help='if set to true (only in combination with mode=enrich), no api connections are made. Useful for testing only.')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

api_host = args.apihost
api_port = args.port
config_filename = args.configfile

with open(args.password, "r") as password_file:
    api_password = password_file.read().rstrip()
print("pwd:" + api_password +".")
api_domain = args.domain
test_version = args.testing
proxy_string = { "http" : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
testmode = args.testing
base_url = 'https://' + api_host + ':' + api_port + '/web_api/'
json_indent=2
use_object_dictionary = 'false'
svc_objects = []
nw_objects = []
nw_objs_from_obj_tables = []
svc_objs_from_obj_tables = []

# logging config
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)

ssl_verification = getter.set_ssl_verification(args.ssl)
starttime = int(time.time())

# read json config data
with open(config_filename, "r") as json_data:
    config = json.load(json_data)

#################################################################################
# adding inline and domain layers 
#################################################################################

found_new_inline_layers = True
old_inline_layers = []
while found_new_inline_layers is True:
    # sweep existing rules for inline layer links
    inline_layers = []
    for rulebase in config['rulebases']:
        getter.get_inline_layer_names_from_rulebase(rulebase, inline_layers)

    if len(inline_layers) == len(old_inline_layers):
        found_new_inline_layers = False
    else:
        old_inline_layers = inline_layers
        for layer in inline_layers:
            logging.debug ( "enrich_config - found inline layer " + layer )
            # enrich config --> get additional layers referenced in top level layers by name
            # also handle possible recursion (inline layer containing inline layer(s))
            # get layer rules from api
            # add layer rules to config

# next phase: how to logically link layer guard with rules in layer? --> AND of src, dst & svc between layer guard and each rule in layer?

# for rulebase in config['rulebases']:
#     for rule in rulebase:
#         if 'type' in rule and rule['type'] == 'place-holder':
#             logging.debug("enrich_config: found domain rule ref: " + rule["uid"])

#################################################################################
# get object data which is only contained as uid in config by making addtional api calls
#################################################################################


# get all object uids (together with type) from all rules in fields src, dst, svc
nw_uids_from_rulebase = []
svc_uids_from_rulebase = []

for rulebase in config['rulebases']:
    logging.debug ( "enrich_config - searching for all uids in rulebase: " + rulebase['layername'] )
    getter.collect_uids_from_rulebase(rulebase, nw_uids_from_rulebase, svc_uids_from_rulebase, "top_level")
    #nw_uids_from_rulebase.extend(nw_uids_from_rulebase)
    #svc_uids_from_rulebase.extend(svc_uids_from_rulebase)

    # if common.debug_new_uid in nw_uids_from_rulebase:
    #     logging.debug("found " + common.debug_new_uid + " in enrich_config")


# remove duplicates from uid lists
nw_uids_from_rulebase = list(set(nw_uids_from_rulebase))
svc_uids_from_rulebase = list(set(svc_uids_from_rulebase))
# logging.debug ("enrich_config - found (unique) nw_objects in rulebase:\n" + str(nw_uids_from_rulebase))

# if common.debug_new_uid in nw_uids_from_rulebase:
#     logging.debug("enrich_config: found " + common.debug_new_uid + " in enrich_config after cleanup")

# get all uids in objects tables
for obj_table in config['object_tables']:
    nw_objs_from_obj_tables.extend(getter.get_all_uids_of_a_type(obj_table, common.nw_obj_table_names))
    svc_objs_from_obj_tables.extend(getter.get_all_uids_of_a_type(obj_table, getter.svc_obj_table_names))
#logging.debug ("enrich_config - already parsed nw_objects in obj tables:\n" + str(nw_objs_from_obj_tables))


# identify all objects (by type) that are missing in objects tables but present in rulebase
missing_nw_object_uids  = getter.get_broken_object_uids(nw_objs_from_obj_tables, nw_uids_from_rulebase)
missing_svc_object_uids = getter.get_broken_object_uids(svc_objs_from_obj_tables, svc_uids_from_rulebase)

logging.debug ( "enrich_config - found missing nw objects: '" + ",".join(missing_nw_object_uids) + "'" )
logging.debug ( "enrich_config - found missing svc objects: '" + ",".join(missing_svc_object_uids) + "'" )

if args.noapi == 'false':
    sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string)
    v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)
    logging.debug ( "enrich_config - logged into api" )

# if an object is not there:
#   make api call: show object details-level full uid "<uid>" and add object to respective json
for missing_obj in missing_nw_object_uids:
    if args.noapi == 'false':
        show_params_host = {'details-level':details_level,'uid':missing_obj}
        logging.debug ( "checkpointR8x/enrich_config - fetching obj with uid: " + missing_obj)
        obj = getter.api_call(api_host, args.port, v_url, 'show-object', show_params_host, sid, ssl_verification, proxy_string)
        obj = obj['object']
        if (obj['type'] == 'CpmiAnyObject'):
            json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': 'any nw object checkpoint (hard coded)',
                        'type': 'CpmiAnyObject', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
            config['object_tables'].append(json_obj)
        elif (obj['type'] == 'simple-gateway' or obj['type'] == 'CpmiGatewayPlain' or obj['type'] == 'interop'):
            json_obj = {"object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': common.get_ip_of_obj(obj),
                } ] } ] }
            config['object_tables'].append(json_obj)
        elif obj['type'] == 'multicast-address-range':
            logging.debug("enrich_config - found multicast-address-range: " + obj['name'] + " (uid:" + obj['uid']+ ")")
            json_obj = {"object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': common.get_ip_of_obj(obj),
                } ] } ] }
            config['object_tables'].append(json_obj)
        elif (obj['type'] == 'CpmiVsClusterMember' or obj['type'] == 'CpmiVsxClusterMember'):
            json_obj = {"object_type": "hosts", "object_chunks": [ {
                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': common.get_ip_of_obj(obj),
                } ] } ] }
            config['object_tables'].append(json_obj)
            logging.debug ('missing obj: ' + obj['name'] + obj['type'])
        else:
            logging.debug ( "WARNING - checkpointR8x/enrich_config - missing nw obj of unexpected type '" + obj['type'] + "': " + missing_obj )
            #print ("missing nw obj: " + missing_obj)

    logging.debug ( "enrich_config - missing nw obj: " + missing_obj )
    print ("INFO: adding nw  obj missing from standard api call results: " + missing_obj)

for missing_obj in missing_svc_object_uids:
    if args.noapi == 'false':
        show_params_host = {'details-level':details_level,'uid':missing_obj}
        obj = getter.api_call(api_host, args.port, v_url, 'show-object', show_params_host, sid, ssl_verification, proxy_string)
        obj = obj['object']
        # print(json.dumps(obj))
        # currently no svc objects are found missing, not even the any obj?
        if (obj['type'] == 'CpmiAnyObject'):
            json_obj = {"object_type": "services-other", "object_chunks": [ {
                    "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': 'any svc object checkpoint (hard coded)',
                        'type': 'service-other', 'ip-protocol': '0'
                        } ] } ] }
            config['object_tables'].append(json_obj)
        else:
            logging.debug ( "WARNING - enrich_config - missing svc obj of unexpected type: " + missing_obj )
            print ("WARNING - enrich_config - missing svc obj of unexpected type: " + missing_obj)
    logging.debug ( "enrich_config - missing svc obj: " + missing_obj )
    print ("INFO: adding svc obj missing from standard api call results: " + missing_obj)

# dump new json file
if args.noapi == 'false':
    if os.path.exists(config_filename): # delete json file (to enabiling re-write)
        os.remove(config_filename)
    with open(config_filename, "w") as json_data:
        json_data.write(json.dumps(config))
        # json_data.write(json.dumps(config,indent=json_indent))

if args.noapi == 'false':
    logout_result = getter.api_call(api_host, args.port, v_url, 'logout', '', sid, ssl_verification, proxy_string)
    #logout_result = api_call(api_host, args.port, base_url, 'logout', {}, sid)
duration = int(time.time()) - starttime
logging.debug ( "checkpointR8x/enrich_config - duration: " + str(duration) + "s" )

sys.exit(0)

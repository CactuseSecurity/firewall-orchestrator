#!/usr/bin/python3

import checkpointR8x.common
import checkpointR8x.getter
import requests, json, argparse, pdb
import requests.packages.urllib3, time, logging, re, sys
import os
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password', required=True, help='password for management server')
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
api_host = args.apihost
api_port = args.port
config_filename = args.out
api_password = args.password
api_domain = args.domain
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
testmode = args.testing
base_url = 'https://' + api_host + ':' + api_port + '/web_api/'
json_indent=2
use_object_dictionary = 'false'
#limit="25"

# logging config
debug_level = int(args.debug)
# todo: save the initial value, reset initial value at the end
# todo: switch to native syslog

if debug_level == 1:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 2:
    logging.basicConfig(filename='/var/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
if debug_level == 3:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    logging.basicConfig(filename='/var/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')

# ssl_verification mode
ssl_verification_mode = args.ssl
if ssl_verification_mode == '':
    ssl_verification = False
else:
    ssl_verification = ssl_verification_mode
    # todo: supplement error handling: redable file, etc

starttime = int(time.time())
logging.debug ("get_config_cp_r8x_api - starting in " + mode + " mode" )

svc_objects = []
nw_objects = []
nw_objs_from_obj_tables = []
svc_objs_from_obj_tables = []

# read json config data
with open(config_filename, "r") as json_data:
    config = json.load(json_data)

# get all object uids (together with type) from all rules in fields src, dst, svc
for rulebase in config['rulebases']:
    #print ("\n\nsearching for all uids in rulebase: " + rulebase['layername'])
    collect_uids_from_rulebase(rulebase, "top_level")

# remove duplicates from uid lists
svc_objects = list(set(svc_objects))
nw_objects = list(set(nw_objects))

# get all uids in objects tables
for obj_table in config['object_tables']:
    nw_objs_from_obj_tables.extend(get_all_uids_of_a_type(obj_table, nw_obj_table_names))
    svc_objs_from_obj_tables.extend(get_all_uids_of_a_type(obj_table, svc_obj_table_names))

# identify all objects (by type) that are missing in objects tables but present in rulebase
missing_nw_object_uids  = get_broken_object_uids(nw_objs_from_obj_tables, nw_objects)
missing_svc_object_uids = get_broken_object_uids(svc_objs_from_obj_tables, svc_objects)

if args.noapi == 'false':
    sid = login(args.user,api_password,api_host,args.port,api_domain)
    v_url = get_api_url (sid)

# if an object is not there:
#   make api call: show object details-level full uid "<uid>" and add object to respective json
for missing_obj in missing_nw_object_uids:
    if args.noapi == 'false':
        show_params_host = {'details-level':details_level,'uid':missing_obj}
        obj = api_call(api_host, args.port, v_url, 'show-object', show_params_host, sid)
        obj = obj['object']
        #print(json.dumps(obj, indent=json_indent))
        if (obj['type'] == 'CpmiAnyObject'):
            json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': 'any nw object checkpoint (hard coded)',
                        'type': 'CpmiAnyObject', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
            config['object_tables'].append(json_obj)
        elif (obj['type'] == 'simple-gateway' or obj['type'] == 'CpmiGatewayPlain'):
            json_obj = {"object_type": "hosts", "object_chunks": [ {

                "objects": [ {
                'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                'comments': obj['comments'], 'type': 'host', 'ipv4-address': get_ip_of_obj(obj),
                } ] } ] }
            config['object_tables'].append(json_obj)
        else:
            logging.debug ( "WARNING - get_config_cp_r8x_api - missing nw obj of unexpected type: " + missing_obj )
            #print ("missing nw obj: " + missing_obj)

    logging.debug ( "get_config_cp_r8x_api - missing nw obj: " + missing_obj )
    print ("INFO: adding nw  obj missing from standard api call results: " + missing_obj)

for missing_obj in missing_svc_object_uids:
    if args.noapi == 'false':
        show_params_host = {'details-level':details_level,'uid':missing_obj}
        obj = api_call(api_host, args.port, v_url, 'show-object', show_params_host, sid)
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
            logging.debug ( "WARNING - get_config_cp_r8x_api - missing svc obj of unexpected type: " + missing_obj )
            print ("WARNING - get_config_cp_r8x_api - missing svc obj of unexpected type: " + missing_obj)
    logging.debug ( "get_config_cp_r8x_api - missing svc obj: " + missing_obj )
    print ("INFO: adding svc obj missing from standard api call results: " + missing_obj)

# dump new json file
if args.noapi == 'false':
    if os.path.exists(config_filename): # delete json file (to enabiling re-write)
        os.remove(config_filename)
    with open(config_filename, "w") as json_data:
        json_data.write(json.dumps(config,indent=json_indent))

if args.noapi == 'false':
    logout_result = api_call(api_host, args.port, base_url, 'logout', {}, sid)
duration = int(time.time()) - starttime
logging.debug ( "checkpointR8x/enrich_config - duration: " + str(duration) + "s" )

sys.exit(0)

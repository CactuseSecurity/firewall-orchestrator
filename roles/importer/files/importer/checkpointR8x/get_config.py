#!/usr/bin/python3
# first connect to api should result in the following:
# tim@acantha:~$ wget --no-check-certificate https://192.168.100.110/web_api/ 
# --2020-06-03 13:22:19--  https://192.168.100.110/web_api/
# Connecting to 192.168.100.110:443... connected.
# WARNING: cannot verify 192.168.100.110's certificate, issued by ‘unstructuredName=An optional company name,emailAddress=Email Address,CN=192.168.100.110,L=Locality Name (eg\\, city)’:
#   Self-signed certificate encountered.
# HTTP request sent, awaiting response... 401 Unauthorized
# Username/Password Authentication Failed.
#
# if you get the following:
#    tim@acantha:~$ wget --no-check-certificate https://192.168.100.110/web_api/ 
#    HTTP request sent, awaiting response... 403 Forbidden
#    2020-06-03 12:56:12 ERROR 403: Forbidden.
# 
# make sure the api server is up and running and accepting connections from your ip address:
# (taken from https://community.checkpoint.com/t5/API-CLI-Discussion-and-Samples/Enabling-web-api/td-p/32641)
# mgmt_cli -r true --domain MDS set api-settings accepted-api-calls-from "All IP addresses"
# api restart

import common
import getter
import requests, json, argparse, pdb
import requests.packages.urllib3, time, logging, re, sys
import os
#import fworch_session_cp_r8x_api
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
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')

# TODO: fix ugly use of --out for input file def of enrich mode

args = parser.parse_args()
api_host = args.apihost
api_port = args.port
config_filename = args.out
api_password = args.password
api_domain = args.domain
proxy_string = { "http" : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
test_version = args.testing
base_url = 'https://' + api_host + ':' + api_port + '/web_api/'
json_indent=2
use_object_dictionary = 'false'
#limit="25"

# logging config
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)

# todo: save the initial value, reset initial value at the end
# todo: switch to native syslog
# if debug_level == 1:
#     logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
# elif debug_level == 2:
#     logging.basicConfig(filename='/var/tmp/get_config.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
# if debug_level == 3:
#     logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
#     logging.basicConfig(filename='/var/tmp/get_config.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')

# ssl_verification mode
ssl_verification_mode = args.ssl
if ssl_verification_mode == '':
    ssl_verification = False
else:
    ssl_verification = ssl_verification_mode
    # todo: supplement error handling: redable file, etc

starttime = int(time.time())
# top level dict start
sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string)
v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)

config_json = "{\n"
config_json += "\"rulebases\": [\n"
show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level}
# read all rulebases:
for layer in args.layer.split(','):
    show_params_rules['name'] = layer
    config_json += "{\n\"layername\": \"" + layer + "\",\n"
    config_json +=  "\"layerchunks\": [\n"
    current=0
    total=current+1
    logging.debug ( "get_config - layer:"+ layer )
    while (current<total) :
#        show_params_rules = {'name':layer,'offset':current,'limit':limit,'use-object-dictionary':'false','details-level':'full'}
        show_params_rules['offset']=current
        rulebase = getter.api_call(api_host, args.port, v_url, 'show-access-rulebase', show_params_rules, sid, ssl_verification, proxy_string)
        config_json +=  json.dumps(rulebase, indent=json_indent)
        config_json +=  ",\n"
        total=rulebase['total']
        current=rulebase['to']
        logging.debug ( "get_config - rulebase current:"+ str(current) )
    config_json = config_json[:-2]
    config_json +=  "]\n},\n"
config_json = config_json[:-2]
config_json += "],\n"  # 'level': 'rulebases'
logging.debug ( "get_config - rulebase total:"+ str(total) )

config_json += "\"object_tables\": [\n"
show_params_objs = {'limit':limit,'details-level': details_level}

for obj_type in getter.api_obj_types:
    config_json += "{\n\"object_type\": \"" + obj_type + "\",\n"
    config_json += "\"object_chunks\": [\n"
    current=0
    total=current+1
    show_cmd = 'show-' + obj_type
    logging.debug ( "get_config - obj_type: "+ obj_type )
    while (current<total) :
        show_params_objs['offset']=current
        objects = getter.api_call(api_host, args.port, v_url, show_cmd, show_params_objs, sid, ssl_verification, proxy_string)
        config_json += json.dumps(objects, indent=json_indent)
        config_json += ",\n"
        if 'total' in objects  and 'to' in objects:
            total=objects['total']
            current=objects['to']
            logging.debug ( "get_config - "+ obj_type +" current:"+ str(current) )
            logging.debug ( "get_config - "+ obj_type +" total:"+ str(total) )
        else :
            current = total
            logging.debug ( "get_config - "+ obj_type +" total:"+ str(total) )
    config_json = config_json[:-2]
    config_json += "]\n},\n" # 'level': 'top::object'\n"
config_json = config_json[:-2]
config_json += "]\n" # 'level': 'objects'\n"
config_json += "}\n" # 'level': 'top'"
with open(config_filename, "w") as configfile_json:
    configfile_json.write(config_json)

#logout_result = getter.api_call(api_host, args.port, base_url, 'logout', {}, sid)
logout_result = getter.api_call(api_host, args.port, v_url, 'logout', '', sid, ssl_verification, proxy_string)
duration = int(time.time()) - starttime
logging.debug ( "checkpointR8x/get_config - duration: " + str(duration) + "s" )

sys.exit(0)

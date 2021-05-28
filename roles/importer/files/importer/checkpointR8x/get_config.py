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
import json, argparse, pdb
import requests, requests.packages.urllib3
import time, logging, re, sys
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
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

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
ssl_verification = getter.set_ssl_verification(args.ssl)

starttime = int(time.time())
# top level dict start
sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string)
v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)


config_json = "{\n"
config_json += "\"rulebases\": [\n"
show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level}

# read all rulebases:
for layer in args.layer.split(','):
    logging.debug ( "get_config - layer: " + layer )
    domain_layer_name = ""
    current_layer_json = ""
    show_params_rules['name'] = layer

    if '/' in layer:
        logging.debug ( "get_config - layer contains global and domain part separated by slash, parsing individually: " + layer )
        (global_layer_name, domain_layer_name) = layer.split('/')
        show_params_rules['name'] = global_layer_name

    current_layer_json = getter.get_layer_from_api (api_host, args.port, v_url, sid, ssl_verification, proxy_string, show_params_rules)

    # now handling possible reference to domain rules within global rules
    # if we find the reference, replace it with the domain rules
    if domain_layer_name != "":
        show_params_rules['name'] = domain_layer_name
        current_layer = json.loads(current_layer_json)
        if 'layerchunks' in current_layer:
            for chunk in current_layer["layerchunks"]:
                for rule in chunk['rulebase']:
                    if "type" in rule and rule["type"] == "place-holder":
                        #logging.debug ("found domain rules place-holder: " + str(rule) + "\n\n")
                        domain_rules = getter.get_layer_from_api (api_host, args.port, v_url, sid, ssl_verification, proxy_string, show_params_rules)
                        #logging.debug ("found domain rules: " + str(domain_rules) + "\n\n")
                        current_layer_json = getter.insert_layer_after_place_holder(current_layer_json, domain_rules, rule['uid'])
                        # logging.debug ("substituted domain rules: " + json.dumps(current_layer_json, indent=2) + "\n\n")

    logging.debug ("get_config current_layer:\n" + json.dumps(json.loads(current_layer_json), indent=2) + "\n\n")
    config_json += current_layer_json + ",\n"

config_json = config_json[:-2]  # remove final comma layer from loop and add closing bracket for rules:
config_json += "],\n"

# leaving rules, moving on to objects

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
        config_json += json.dumps(objects)
        # config_json += json.dumps(objects, indent=json_indent)
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

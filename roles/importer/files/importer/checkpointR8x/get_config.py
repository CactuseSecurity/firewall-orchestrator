#!/usr/bin/python3

import json, argparse
import requests, requests.packages
import time, logging
import sys
sys.path.append(r"/usr/local/fworch/importer")
import common, getter

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password_file', default='import_user_secret', help='name of the file to read the password for management server from')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-k', '--package', metavar='policy package name', required=True, help='name of policy package (needed for nat rule retrieval)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='150', help='The maximal number of returned results per HTTPS Connection; default=150')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')
parser.add_argument('-f', '--fromdate', metavar='from_date', default='', help='date to start from, e.g. last successful import; default=2000-01-01T00:00:00')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

api_host = args.apihost
api_port = args.port
config_filename = args.out
with open(args.password, "r") as password_file:
    api_password = password_file.read().rstrip()
api_domain = args.domain
proxy_string = { "http" : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
test_version = args.testing
base_url = 'https://' + api_host + ':' + api_port + '/web_api/'
json_indent=2
use_object_dictionary = 'false'

# logging config
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)
ssl_verification = getter.set_ssl_verification(args.ssl)

starttime = int(time.time())
# top level dict start
sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string)
v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)

if args.fromdate == "":
    changes = 1
else:
    changes = getter.get_changes(sid, api_host,args.port,args.fromdate,ssl_verification, proxy_string)

if changes < 0:
    logging.debug ( "get_changes: error getting changes")
    sys.exit(1)
elif changes == 0:
    logging.debug ( "get_changes: no new changes found")
else:
    logging.debug ( "get_changes: changes found -> go ahead with getting config")

    config_json = { 'rulebases': [], 'nat_rulebases': [] }
    show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level}

    # read all rulebases:
    for layer in args.layer.split(','):
        logging.debug ( "get_config - dealing with layer: " + layer )
        domain_layer_name = ""
        current_layer_json = ""
        show_params_rules['name'] = layer

        if '/' in layer:
            logging.debug ( "get_config - layer contains global and domain part separated by slash, parsing individually: " + layer )
            (global_layer_name, domain_layer_name) = layer.split('/')
            show_params_rules['name'] = global_layer_name

        # either get complete rulebase or global layer rulebase if domain rules are present
        logging.debug ( "get_config - getting layer: " + show_params_rules['name'] )
        # current_layer_json = getter.get_layer_from_api (api_host, args.port, v_url, sid, ssl_verification, proxy_string, show_params_rules)
        current_layer_json = getter.get_layer_from_api_as_dict (api_host, args.port, v_url, sid, ssl_verification, proxy_string, show_params_rules, layername=layer)

        # now handling possible reference to domain rules within global rules
        # if we find the reference, replace it with the domain rules
        if domain_layer_name != "":
            show_params_rules['name'] = domain_layer_name
            # current_layer = json.loads(current_layer_json)
            
            # changing layer name to individual combination of global and domain rule
            # this is necessary for multiple references to global layer
            current_layer_json['layername'] = layer
            logging.debug ( "get_config - getting domain rule layer: " + show_params_rules['name'] )
            domain_rules = getter.get_layer_from_api_as_dict (api_host, args.port, v_url, sid, ssl_verification, proxy_string, show_params_rules, layername=layer)
            # logging.debug ("found domain rules: " + str(domain_rules) + "\n\n")

            if 'layerchunks' in current_layer_json:
                for chunk in current_layer_json["layerchunks"]:
                    for rule in chunk['rulebase']:
                        if "type" in rule and rule["type"] == "place-holder":
                            logging.debug ("found domain rules place-holder: " + str(rule) + "\n\n")
                            current_layer_json = getter.insert_layer_after_place_holder(current_layer_json, domain_rules, rule['uid'])
                            # logging.debug ("substituted domain rules with chunks: " + json.dumps(current_layer_json, indent=2) + "\n\n")
        # logging.debug ("get_config current_layer:\n" + json.dumps(json.loads(current_layer_json), indent=2) + "\n\n")
        config_json['rulebases'].append(current_layer_json)
        # getting NAT rules
        show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level, 'package': args.package }
        logging.debug ( "get_config - getting nat rules for package: " + args.package )
        config_json['nat_rulebases'].append(getter.get_nat_rules_from_api_as_dict (api_host, args.port, v_url, sid, ssl_verification, proxy_string, show_params_rules))

    # leaving rules, moving on to objects
    config_json["object_tables"] = []
    show_params_objs = {'limit':limit,'details-level': details_level}

    for obj_type in getter.api_obj_types:
        object_table = { "object_type": obj_type, "object_chunks": [] }
        current=0
        total=current+1
        show_cmd = 'show-' + obj_type
        logging.debug ( "get_config - obj_type: "+ obj_type )
        while (current<total) :
            show_params_objs['offset']=current
            objects = getter.api_call(api_host, args.port, v_url, show_cmd, show_params_objs, sid, ssl_verification, proxy_string)
            object_table["object_chunks"].append(objects)
            if 'total' in objects  and 'to' in objects:
                total=objects['total']
                current=objects['to']
                logging.debug ( "get_config - "+ obj_type +" current:"+ str(current) )
                logging.debug ( "get_config - "+ obj_type +" total:"+ str(total) )
            else :
                current = total
                logging.debug ( "get_config - "+ obj_type +" total:"+ str(total) )
        config_json["object_tables"].append(object_table)
    with open(config_filename, "w") as configfile_json:
        configfile_json.write(json.dumps(config_json))

logout_result = getter.api_call(api_host, args.port, v_url, 'logout', '', sid, ssl_verification, proxy_string)
duration = int(time.time()) - starttime
logging.debug ( "checkpointR8x/get_config - duration: " + str(duration) + "s" )

if changes == 0:
    sys.exit(2)
sys.exit(0)

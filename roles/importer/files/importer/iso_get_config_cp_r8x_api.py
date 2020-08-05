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

import requests, json, argparse, pdb
import requests.packages.urllib3, time, logging
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('hostname', metavar='api_host', help='Check Point R8x management server')
parser.add_argument('password', metavar='api_password', help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='itsecorg', help='user for connecting to Check Point R8x management server, default=itsecorg')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='verification_mode', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 

args = parser.parse_args()

api_host=args.hostname
api_password=args.password
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset=0
limit = args.limit
details_level="full"    # 'standard'

# logging config
debug_level = int(args.debug)
if debug_level == 4:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 41:
    logging.basicConfig(filename='/var/tmp/fworch_get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')

#ssl_verification mode
verification_mode = args.ssl
if verification_mode == '':
    ssl_verification = False
else:
    ssl_verification = verification_mode

# import pdb; pdb.set_trace() # debug python

use_object_dictionary='false'

def api_call(ip_addr, port, command, json_payload, sid):
    url = 'https://' + ip_addr + ':' + port + '/web_api/' + command
    if sid == '':
        request_headers = {'Content-Type' : 'application/json'}
    else:
        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
    return r.json()

def login(user,password,api_host,api_port):
    payload = {'user':user, 'password' : password}
    response = api_call(api_host, api_port, 'login', payload, '')
    return response["sid"]

sid = login(args.user,api_password,api_host,args.port)
logging.debug ("fworch_get_config_cp_r8x_api - limit:"+ limit )
logging.debug ("fworch_get_config_cp_r8x_api - login:"+ args.user )
logging.debug ("fworch_get_config_cp_r8x_api - sid:"+ sid )


# top level dict start
starttime = int(time.time())
logging.debug ("fworch_get_config_cp_r8x_api - top level dict start" )
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
    logging.debug ( "fworch_get_config_cp_r8x_api - layer:"+ layer )
    while (current<total) :
#        show_params_rules = {'name':layer,'offset':current,'limit':limit,'use-object-dictionary':'false','details-level':'full'}
        show_params_rules['offset']=current
        rulebase = api_call(api_host, args.port, 'show-access-rulebase', show_params_rules, sid)
        config_json +=  json.dumps(rulebase, indent=4)
        config_json +=  ",\n"
        total=rulebase['total']
        current=rulebase['to']
        logging.debug ( "fworch_get_config_cp_r8x_api - rulebase current:"+ str(current) )
    config_json = config_json[:-2]
    config_json +=  "]\n},\n"
config_json = config_json[:-2]
config_json += "],\n"  # 'level': 'rulebases'
logging.debug ( "fworch_get_config_cp_r8x_api - rulebase total:"+ str(total) )

# read all objects:
obj_types = [
    'hosts', 'networks', 'groups', 'address-ranges', 'groups-with-exclusion', 'simple-gateways', 
    'security-zones', 'dynamic-objects', 'trusted-clients', 'dns-domains', 
    'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc',
#    'application-sites', 'application-site-categories', 'application-site-groups' 
]

config_json += "\"object_tables\": [\n"
show_params_objs = {'limit':limit,'details-level': details_level}
for obj_type in obj_types:
    config_json += "{\n\"object_type\": \"" + obj_type + "\",\n"
    config_json += "\"object_chunks\": [\n"
    current=0
    total=current+1
    show_cmd = 'show-' + obj_type
    logging.debug ( "fworch_get_config_cp_r8x_api - obj_type: "+ obj_type )
    while (current<total) :
#        show_params_objs = {'offset':current,'limit':limit,'details-level':'full'}
        show_params_objs['offset']=current
        objects = api_call(api_host, args.port, show_cmd, show_params_objs, sid)
        config_json += json.dumps(objects, indent=4)
        config_json += ",\n"
        if 'total' in objects  and 'to' in objects :
            total=objects['total']
            current=objects['to']
            logging.debug ( "fworch_get_config_cp_r8x_api - "+ obj_type +" current:"+ str(current) )
            logging.debug ( "fworch_get_config_cp_r8x_api - "+ obj_type +" total:"+ str(total) )
        else :
            current = total
            logging.debug ( "fworch_get_config_cp_r8x_api - "+ obj_type +" total:"+ str(total) )
    config_json = config_json[:-2]
    config_json += "]\n},\n" # 'level': 'top::object'\n"
config_json = config_json[:-2]
config_json += "]\n" # 'level': 'objects'\n"
config_json += "}\n" # 'level': 'top'"

logout_result = api_call(api_host, args.port, 'logout', {}, sid)
endtime = int(time.time())
duration = endtime - starttime
logging.debug ( "fworch_get_config_cp_r8x_api - duration: "+ str(duration) )
print(config_json)

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
import requests.packages.urllib3, time, logging, re
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
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|all|<version number>]; default=off') 

args = parser.parse_args()

api_host = args.hostname
api_port = args.port
api_password = args.password
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
testmode = args.testing
base_url = 'https://' + api_host + ':' + api_port + '/web_api/'

# import pdb; pdb.set_trace() # debug python

use_object_dictionary = 'false'
#def api_call(ip_addr, port, command, json_payload, sid):
#    if testmode == 'off':
#        url = 'https://' + ip_addr + ':' + port + '/web_api/' + command
#    elif testmode == 'all':
#        url = 'https://' + ip_addr + ':' + port + '/web_api/' + command # for later use
#    else:
#        url = 'https://' + ip_addr + ':' + port + '/web_api/' + testmode + '/' + command # todo: supplement error handling: 'd.d'
#
#    if sid == '':
#        request_headers = {'Content-Type' : 'application/json'}
#    else:
#        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
#    if json_payload == '':
#        r = requests.post(url, data='{}', headers=request_headers, verify=ssl_verification, proxies=proxy_string)
#    else:
#        r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
#    return r.json()

def api_call(ip_addr, port, url, command, json_payload, sid):
    url = url + command
    if sid == '':
        request_headers = {'Content-Type' : 'application/json'}
    else:
        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
    return r.json()

def login(user,password,api_host,api_port):
    payload = {'user':user, 'password' : password}
    response = api_call(api_host, api_port, base_url, 'login', payload, '')
    return response["sid"]

# logging config
debug_level = int(args.debug)
# todo: save the initial value, reset initial value at the end
if debug_level == 4:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 41:
    logging.basicConfig(filename='/var/tmp/iso_get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')

# ssl_verification mode
verification_mode = args.ssl
if verification_mode == '':
    ssl_verification = False
else:
    ssl_verification = verification_mode
    # todo: supplement error handling: redable file, etc

sid = login(args.user,api_password,api_host,args.port)

api_versions = api_call(api_host, args.port, base_url, 'show-api-versions', {}, sid)
api_version = api_versions["current-version"]
api_supported = api_versions["supported-versions"]
logging.debug ("iso_get_config_cp_r8x_api - current version: "+ api_version )
logging.debug ("iso_get_config_cp_r8x_api - supported versions: "+ ', '.join(api_supported) )
logging.debug ("iso_get_config_cp_r8x_api - limit:"+ limit )
logging.debug ("iso_get_config_cp_r8x_api - login:"+ args.user )
logging.debug ("iso_get_config_cp_r8x_api - sid:"+ sid )

# v_url definiton - version dependent
v_url = ''
if testmode == 'off':
    v_url = base_url
elif testmode == 'all':  # for future use
    v_url = base_url
else:
    if re.search(r'\d+\.\d+', testmode):  # todo: supplement error handling: if testmode in api_versions -> api_versions is unknown yet -> use v_url_mode as an indicator - anywhere
        if testmode in api_supported :
            v_url = base_url + testmode + '/'
        else:
            logging.debug ("iso_get_config_cp_r8x_api - api version " + testmode + "is not supported by the manager " + api_host)
            v_url = base_url
    else:
        v_url = base_url
        logging.debug ("iso_get_config_cp_r8x_api - not a valid version")
logging.debug ("iso_get_config_cp_r8x_api - testmode: " + testmode + " url: "+ v_url)

# top level dict start
starttime = int(time.time())
logging.debug ("iso_get_config_cp_r8x_api - top level dict start" )
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
    logging.debug ( "iso_get_config_cp_r8x_api - layer:"+ layer )
    while (current<total) :
#        show_params_rules = {'name':layer,'offset':current,'limit':limit,'use-object-dictionary':'false','details-level':'full'}
        show_params_rules['offset']=current
        rulebase = api_call(api_host, args.port, v_url, 'show-access-rulebase', show_params_rules, sid)
        config_json +=  json.dumps(rulebase, indent=4)
        config_json +=  ",\n"
        total=rulebase['total']
        current=rulebase['to']
        logging.debug ( "iso_get_config_cp_r8x_api - rulebase current:"+ str(current) )
    config_json = config_json[:-2]
    config_json +=  "]\n},\n"
config_json = config_json[:-2]
config_json += "],\n"  # 'level': 'rulebases'
logging.debug ( "iso_get_config_cp_r8x_api - rulebase total:"+ str(total) )

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
    logging.debug ( "iso_get_config_cp_r8x_api - obj_type: "+ obj_type )
    while (current<total) :
#        show_params_objs = {'offset':current,'limit':limit,'details-level':'full'}
        show_params_objs['offset']=current
        objects = api_call(api_host, args.port, v_url, show_cmd, show_params_objs, sid)
        config_json += json.dumps(objects, indent=4)
        config_json += ",\n"
        if 'total' in objects  and 'to' in objects :
            total=objects['total']
            current=objects['to']
            logging.debug ( "iso_get_config_cp_r8x_api - "+ obj_type +" current:"+ str(current) )
            logging.debug ( "iso_get_config_cp_r8x_api - "+ obj_type +" total:"+ str(total) )
        else :
            current = total
            logging.debug ( "iso_get_config_cp_r8x_api - "+ obj_type +" total:"+ str(total) )
    config_json = config_json[:-2]
    config_json += "]\n},\n" # 'level': 'top::object'\n"
config_json = config_json[:-2]
config_json += "]\n" # 'level': 'objects'\n"
config_json += "}\n" # 'level': 'top'"

logout_result = api_call(api_host, args.port, base_url, 'logout', {}, sid)
endtime = int(time.time())
duration = endtime - starttime
logging.debug ( "iso_get_config_cp_r8x_api - duration: "+ str(duration) )
print(config_json)

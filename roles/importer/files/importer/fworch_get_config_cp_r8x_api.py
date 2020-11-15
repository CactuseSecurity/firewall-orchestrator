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
import requests.packages.urllib3, time, logging, re, sys
import os
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password', required=True, help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='verification_mode', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to') 

args = parser.parse_args()

api_host = args.apihost
api_port = args.port
config_out_filename = args.out
api_password = args.password
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
testmode = args.testing
base_url = 'https://' + api_host + ':' + api_port + '/web_api/'

# all obj table names to look at:
api_obj_types = [
    'hosts', 'networks', 'groups', 'address-ranges', 'groups-with-exclusion', 'gateways-and-servers',
    'security-zones', 'dynamic-objects', 'trusted-clients', 'dns-domains',
    'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-icmp', 'services-icmp6' ]

nw_obj_table_names = ['hosts', 'networks', 'address-ranges', 'groups', 'gateways-and-servers', 'simple-gateways']  
# does not consider: CpmiAnyObject, CpmiGatewayPlain, external 
svc_obj_table_names = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-other', 'services-icmp', 'services-icmp6']
# usr_obj_table_names : do net exist yet - not fetchable via API

use_object_dictionary = 'false'


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


def add_uids(rule):
    global svc_objects
    global nw_objects
    #global user_objects
 
    if 'rule-number' in rule:  # standard rule, no section header
        for src in rule["source"]:
            if src['type'] == 'LegacyUserAtLocation':
                user_objects.append(src["userGroup"])
                nw_objects.append(src["location"])
            elif src['type'] == 'access-role':
                user_objects.append(src['uid'])
                if isinstance(src['networks'], str):  # just a single source
                    if src['networks'] != 'any':   # ignore any objects as they do not contain a uid
                       nw_objects.append(src['networks'])
                else:  # more than one source
                    for nw in src['networks']:
                        nw_objects.append(nw)
            else:  # standard network objects as source
                nw_objects.append(src['uid'])

        for dst in rule["destination"]:
            nw_objects.append(dst['uid'])

        for svc in rule["service"]:
            svc_objects.append(svc['uid'])


def get_all_uids_of_a_type(object_table, obj_table_names):
    all_uids = []

    if object_table['object_type'] in obj_table_names:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                if 'members' in obj:   # add group member refs
                    for member in obj['members']:
                        all_uids.append(member)
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


# logging config
debug_level = int(args.debug)
# todo: save the initial value, reset initial value at the end
if debug_level == 4:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 41:
    logging.basicConfig(filename='/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')

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

logging.debug ("get_config_cp_r8x_api - current version: "+ api_version )
logging.debug ("get_config_cp_r8x_api - supported versions: "+ ', '.join(api_supported) )
logging.debug ("get_config_cp_r8x_api - limit:"+ limit )
logging.debug ("get_config_cp_r8x_api - login:"+ args.user )
logging.debug ("get_config_cp_r8x_api - sid:"+ sid )

#testmode = '1.5'
# v_url definiton - version dependent
v_url = ''
if testmode == 'off':
    v_url = base_url
else:
    if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
        if testmode in api_supported :
            v_url = base_url + 'v' + testmode + '/'
        else:
            logging.debug ("iso_get_config_cp_r8x_api - api version " + testmode + " is not supported by the manager " + api_host + " - Import is canceled")
            #v_url = base_url
            sys.exit("api version " + testmode +" not supported")
    else:
        logging.debug ("iso_get_config_cp_r8x_api - not a valid version")
        sys.exit("\"" + testmode +"\" - not a valid version")
logging.debug ("iso_get_config_cp_r8x_api - testmode: " + testmode + " - url: "+ v_url)

# top level dict start
starttime = int(time.time())
logging.debug ("get_config_cp_r8x_api - top level dict start" )
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
    logging.debug ( "get_config_cp_r8x_api - layer:"+ layer )
    while (current<total) :
#        show_params_rules = {'name':layer,'offset':current,'limit':limit,'use-object-dictionary':'false','details-level':'full'}
        show_params_rules['offset']=current
        rulebase = api_call(api_host, args.port, v_url, 'show-access-rulebase', show_params_rules, sid)
        config_json +=  json.dumps(rulebase, indent=4)
        config_json +=  ",\n"
        total=rulebase['total']
        current=rulebase['to']
        logging.debug ( "get_config_cp_r8x_api - rulebase current:"+ str(current) )
    config_json = config_json[:-2]
    config_json +=  "]\n},\n"
config_json = config_json[:-2]
config_json += "],\n"  # 'level': 'rulebases'
logging.debug ( "get_config_cp_r8x_api - rulebase total:"+ str(total) )

config_json += "\"object_tables\": [\n"
show_params_objs = {'limit':limit,'details-level': details_level}

for obj_type in api_obj_types:
    config_json += "{\n\"object_type\": \"" + obj_type + "\",\n"
    config_json += "\"object_chunks\": [\n"
    current=0
    total=current+1
    show_cmd = 'show-' + obj_type
    logging.debug ( "get_config_cp_r8x_api - obj_type: "+ obj_type )
    while (current<total) :
        show_params_objs['offset']=current
        objects = api_call(api_host, args.port, v_url, show_cmd, show_params_objs, sid)
        config_json += json.dumps(objects, indent=4)
        config_json += ",\n"
        if 'total' in objects  and 'to' in objects:
            total=objects['total']
            current=objects['to']
            logging.debug ( "get_config_cp_r8x_api - "+ obj_type +" current:"+ str(current) )
            logging.debug ( "get_config_cp_r8x_api - "+ obj_type +" total:"+ str(total) )
        else :
            current = total
            logging.debug ( "get_config_cp_r8x_api - "+ obj_type +" total:"+ str(total) )
    config_json = config_json[:-2]
    config_json += "]\n},\n" # 'level': 'top::object'\n"
config_json = config_json[:-2]
config_json += "]\n" # 'level': 'objects'\n"
config_json += "}\n" # 'level': 'top'"


######################################################
## fixing missing object refs in rulebases          ##
## deals with "ungettable" objects like             ##
## CpmiAnyObject, CpmiGatewayPlain, simple-gateway  ##
######################################################
svc_objects = []
nw_objects = []
user_objects = []
missing_nw_object_uids = []
missing_svc_object_uids = []
missing_user_object_uids = []
nw_objs_from_obj_tables = []
svc_objs_from_obj_tables = []
user_objs_from_obj_tables = []

# converting from text to structered data ...
# write first version to disk in order to read json code (Todo: clean this up):
configfile_json = open(config_out_filename + ".tmp", "w")  
configfile_json.write(config_json)
configfile_json.close()
with open(config_out_filename + ".tmp", "r") as json_data:
    config = json.load(json_data)

#delete tmp json file
if os.path.exists(config_out_filename + ".tmp"):
    os.remove(config_out_filename + ".tmp")

# get all object uids (together with type) from all rules in fields src, dst, svc
for rulebase in config['rulebases']:
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rules_chunk in chunk['rulebase']:
                add_uids(rules_chunk)
    else:
        if 'rulebase' in rulebase or 'rule-number' in rulebase:
            for rule in rulebase['rulebase']:
                add_uids(rule)

# remove duplicates from uid lists
svc_objects = list(set(svc_objects))
nw_objects = list(set(nw_objects))

# check if all objects are in their respective section of the config file (svc, nw_obj)
for obj_table in config['object_tables']:
    nw_objs_from_obj_tables.extend(get_all_uids_of_a_type(obj_table, nw_obj_table_names))
    svc_objs_from_obj_tables.extend(get_all_uids_of_a_type(obj_table, svc_obj_table_names))

missing_nw_object_uids.extend(get_broken_object_uids(nw_objs_from_obj_tables, nw_objects))
missing_svc_object_uids.extend(get_broken_object_uids(svc_objs_from_obj_tables, svc_objects))

# if an object is not there:
#     make api call: show object details-level full uid "<uid>"
#     add object to their respective section (if "Any": add ip addr 0.0.0.0/0 for nw_obj and tcp1-65535 for svc)

for missing_obj in missing_nw_object_uids:
    show_params_host = {'details-level':details_level,'uid':missing_obj}
    obj = api_call(api_host, args.port, v_url, 'show-object', show_params_host, sid)
    obj = obj['object']
    #print(json.dumps(obj))
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
        print ("missing nw obj: " + missing_obj)

    logging.debug ( "get_config_cp_r8x_api - missing nw obj: " + missing_obj )
    print ("missing nw obj: " + missing_obj)

for missing_obj in missing_svc_object_uids:
    show_params_host = {'details-level':details_level,'uid':missing_obj}
    obj = api_call(api_host, args.port, v_url, 'show-object', show_params_host, sid)
    obj = obj['object']
    print(json.dumps(obj))
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
    print ("missing svc obj: " + missing_obj)


# dump new json file
with open(config_out_filename, "w") as json_data:
    json_data.write(json.dumps(config,indent=2))

logout_result = api_call(api_host, args.port, base_url, 'logout', {}, sid)
endtime = int(time.time())
duration = endtime - starttime
logging.debug ( "get_config_cp_r8x_api - duration: "+ str(duration) )

sys.exit(1)

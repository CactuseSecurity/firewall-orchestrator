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
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')
parser.add_argument('-m', '--mode', metavar='mode', default='get', help='either get (default) or enrich. Get simple gets the config from the API, enrich adds some missing refs.')
parser.add_argument('-n', '--noapi', metavar='mode', default='false', help='if set to true (only in combination with mode=enrich), no api connections are made. Useful for testing only.')

# TODO: fix ugly use of --out for input file def of enrich mode

args = parser.parse_args()

mode = args.mode
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

# all obj table names to look at:
api_obj_types = [
    'hosts', 'networks', 'groups', 'address-ranges', 'multicast-address-ranges', 'groups-with-exclusion', 'gateways-and-servers',
    'security-zones', 'dynamic-objects', 'trusted-clients', 'dns-domains',
    'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-icmp', 'services-icmp6' ]

nw_obj_table_names = ['hosts', 'networks', 'address-ranges', 'multicast-address-ranges', 'groups', 'gateways-and-servers', 'simple-gateways']  
# do not consider: CpmiAnyObject, CpmiGatewayPlain, external 
svc_obj_table_names = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-other', 'services-icmp', 'services-icmp6']

# usr_obj_table_names : do not exist yet - not fetchable via API


def api_call(ip_addr, port, url, command, json_payload, sid):
    url = url + command
    if sid == '':
        request_headers = {'Content-Type' : 'application/json'}
    else:
        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
    return r.json()


def login(user,password,api_host,api_port,domain):
    if domain == '':
       payload = {'user':user, 'password' : password}
    else:
        payload = {'user':user, 'password' : password, 'domain' :  domain}
    response = api_call(api_host, api_port, base_url, 'login', payload, '')
    return response["sid"]


def collect_uids_from_rule(rule, debug_text):
    global svc_objects
    global nw_objects
 
    if 'rule-number' in rule:  # standard rule, no section header (layered rules)
        for src in rule["source"]:
            if src['type'] == 'LegacyUserAtLocation':
                # user_objects.append(src["userGroup"])
                #print ("Legacy found user uid: " + src["userGroup"] + ", " + debug_text)
                nw_objects.append(src["location"])
                #print ("Legacy found nw uid: " + src["location"] + ", " + debug_text)
            elif src['type'] == 'access-role':
                # user_objects.append(src['uid'])
                if isinstance(src['networks'], str):  # just a single source
                    if src['networks'] != 'any':   # ignore any objects as they do not contain a uid
                       nw_objects.append(src['networks'])
                else:  # more than one source
                    for nw in src['networks']:
                        nw_objects.append(nw)
            else:  # standard network objects as source
                #print ("found nw uid (standard, no usr rule): " + src["uid"] + ", " + debug_text)
                nw_objects.append(src['uid'])
        for dst in rule["destination"]:
            nw_objects.append(dst['uid'])
        for svc in rule["service"]:
            svc_objects.append(svc['uid'])
    else: # recurse into rulebase within rule
        #print ("rule - else zweig - collect_uids_from_rule: " + debug_text)
        collect_uids_from_rulebase(rule["rulebase"], debug_text + ", recursion")


def collect_uids_from_rulebase(rulebase, debug_text):
    global nw_objects
    global svc_objects

    #print ("entering RULEBASE parsing: " + debug_text)
    if 'layerchunks' in rulebase:
        #print (debug_text + ", found layerchanks in layered rulebase , " + debug_text)
        for layer_chunk in rulebase['layerchunks']:
            #print ("found chunk in layerchanks with name " + layer_chunk['name'] + ' , '+ debug_text)
            for rule in layer_chunk['rulebase']:
                #print ("found rules_chunk in rulebase with uid " + layer_chunk['uid'] + ', ' + debug_text)
                collect_uids_from_rule(rule, debug_text + "calling collect_uids_from_rule - if")
    else:
        #print ("else: found no layerchunks in rulebase")
        for rule in rulebase:
            collect_uids_from_rule(rule, debug_text)
            # print ("rule found: " + str(rule))


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


def get_api_url(sid):
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
                logging.debug ("get_config_cp_r8x_api - api version " + testmode + " is not supported by the manager " + api_host + " - Import is canceled")
                #v_url = base_url
                sys.exit("api version " + testmode +" not supported")
        else:
            logging.debug ("get_config_cp_r8x_api - not a valid version")
            sys.exit("\"" + testmode +"\" - not a valid version")
    logging.debug ("get_config_cp_r8x_api - testmode: " + testmode + " - url: "+ v_url)
    return v_url


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

### get mode ##############################################################
if (mode=='get'):
    # top level dict start
    sid = login(args.user,api_password,api_host,args.port,api_domain)
    v_url = get_api_url (sid)
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
            config_json +=  json.dumps(rulebase, indent=json_indent)
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
            config_json += json.dumps(objects, indent=json_indent)
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
    with open(config_filename, "w") as configfile_json:
        configfile_json.write(config_json)

### enrich mode ##################################################################
### fixing missing object refs in rulebases;  deals with "ungettable" objects like
### CpmiAnyObject, CpmiGatewayPlain, simple-gateway  

elif (mode=='enrich'):
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
            logging.debug ('missing obj:\n')
            logging.debug (json.dumps(obj, indent=json_indent) )
            if (obj['type'] == 'CpmiAnyObject'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                            'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                            'comments': 'any nw object checkpoint (hard coded)',
                            'type': 'CpmiAnyObject', 'ipv4-address': '0.0.0.0/0',
                            } ] } ] }
                logging.debug ('missing obj: ' + obj['name'] + obj['type'])
                config['object_tables'].append(json_obj)
            elif (obj['type'] == 'simple-gateway' or obj['type'] == 'CpmiGatewayPlain'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                    'comments': obj['comments'], 'type': 'host', 'ipv4-address': get_ip_of_obj(obj),
                    } ] } ] }
                config['object_tables'].append(json_obj)
                logging.debug ('missing obj: ' + obj['name'] + obj['type'])
            elif (obj['type'] == 'CpmiVsClusterMember'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {

                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                    'comments': obj['comments'], 'type': 'host', 'ipv4-address': get_ip_of_obj(obj),
                    } ] } ] }
                config['object_tables'].append(json_obj)
                logging.debug ('missing obj: ' + obj['name'] + obj['type'])
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

else:
    logging.debug ( "get_config_cp_r8x_api - called with wrong mode parameter: " + mode )
    sys.exit(1)

if args.noapi == 'false':
    logout_result = api_call(api_host, args.port, base_url, 'logout', {}, sid)
duration = int(time.time()) - starttime
logging.debug ( "get_config_cp_r8x_api - duration: " + str(duration) + "s" )

sys.exit(0)

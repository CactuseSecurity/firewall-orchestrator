import requests, json, argparse

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('hostname', metavar='api_host', help='Check Point R8x management server')
parser.add_argument('password', metavar='api_password', help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='itsecorg', help='user for connecting to Check Point R8x management server, default=itsecorg')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')

args = parser.parse_args()

api_host=args.hostname
api_password=args.password
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset=0
limit=100
details_level="full"    # 'standard'
ssl_verification=False
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

# top level dict start
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
    while (current<total) :
#        show_params_rules = {'name':layer,'offset':current,'limit':limit,'use-object-dictionary':'false','details-level':'full'}
        show_params_rules['offset']=current
        rulebase = api_call(api_host, args.port, 'show-access-rulebase', show_params_rules, sid)
        config_json +=  json.dumps(rulebase, indent=4)
        config_json +=  ",\n"
        total=rulebase['total']
        current=rulebase['to']
    config_json = config_json[:-2]
    config_json +=  "]\n},\n"
config_json = config_json[:-2]
config_json += "],\n"  # 'level': 'rulebases'

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
    while (current<total) :
#        show_params_objs = {'offset':current,'limit':limit,'details-level':'full'}
        show_params_objs['offset']=current
        objects = api_call(api_host, args.port, show_cmd, show_params_objs, sid)
        config_json += json.dumps(objects, indent=4)
        config_json += ",\n"
        if 'total' in objects  and 'to' in objects :
            total=objects['total']
            current=objects['to']
        else :
            current = total
    config_json = config_json[:-2]
    config_json += "]\n},\n" # 'level': 'top::object'\n"
config_json = config_json[:-2]
config_json += "]\n" # 'level': 'objects'\n"
config_json += "}\n" # 'level': 'top'"

logout_result = api_call(api_host, args.port, 'logout', {}, sid)

print config_json

#!/usr/bin/python3
import requests, json, argparse

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('hostname', metavar='api_host', help='Check Point R8x management server')
parser.add_argument('password', metavar='api_password', help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
args = parser.parse_args()

api_host=args.hostname
api_password=args.password
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
details_level="full"    # 'standard'
ssl_verification=False
use_object_dictionary='false'

# show package name "New_Standard_Package_1" --format json
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
packages = api_call(api_host, args.port, 'show-packages', {'details-level': details_level}, sid)
print ("the following layers exist on management server:")
for p in packages['packages']:
    print ("package: " + p['name'])
    for l in p['access-layers']:
        print ("    layer: " + l['name'])

logout_result = api_call(api_host, args.port, 'logout', {}, sid)

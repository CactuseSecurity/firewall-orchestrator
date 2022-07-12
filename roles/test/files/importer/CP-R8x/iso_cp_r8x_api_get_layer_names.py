#!/usr/bin/python3

# synopsis:

# tim@deb10-test:~$ python3 read_layer.py -u itsecorg -p 443 192.168.100.110 "password"
# the following layers exist on management server:
# package: Cactus_New
#     layer: cactus_Security_neu
#     layer: cactus_Application
# package: TestPolicy1
#     layer: Testlayer no. 123
#     layer: cactus_Security
#     layer: TestPolicy1 Security
# tim@deb10-test:~$ 


import requests, json, argparse

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('hostname', metavar='api_host', help='Check Point R8x management server')
parser.add_argument('password', metavar='api_password', help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='itsecorg', help='user for connecting to Check Point R8x management server, default=itsecorg')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-v', '--version', metavar='api_version', default='', help='api version to use for connecting to Check Point R8x management server, default=empty, meaning latest api version, valid values: 1.0, 1.1, 1.2, ... ')
# parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
args = parser.parse_args()

api_host=args.hostname
api_password=args.password
if args.version == '':
    api_version=''
else:
    api_version='v' + args.version

# proxy_string = { "http"  : args.proxy, "https" : args.proxy }
details_level="full"    # 'standard'
ssl_verification=True
use_object_dictionary='false'

# show package name "New_Standard_Package_1" --format json
def api_call(ip_addr, port, command, json_payload, sid):
    url = 'https://' + ip_addr + ':' + port + '/web_api/' + api_version + '/' + command
    if sid == '':
        request_headers = {'Content-Type' : 'application/json'}
    else:
        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification) # , proxies=proxy_string)
    return r.json()

def login(user,password,api_host,api_port):
    payload = {'user':user, 'password' : password}
    response = api_call(api_host, api_port, 'login', payload, '')
    return response["sid"]

sid = login(args.user,api_password,api_host,args.port)
packages = api_call(api_host, args.port, 'show-packages', {'details-level': details_level}, sid)

print ("dump of packages:\n" + json.dumps(packages, indent=4) )
print ("the following layers exist on management server:")
for p in packages['packages']:
    print ("package: " + p['name'])
    for l in p['access-layers']:
        print ("    layer: " + l['name'])

logout_result = api_call(api_host, args.port, 'logout', {}, sid)

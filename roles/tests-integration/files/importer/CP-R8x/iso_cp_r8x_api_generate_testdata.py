import requests
import json
import argparse


parser = argparse.ArgumentParser(description='Create configuration from Check Point R8x management via API calls')
parser.add_argument('hostname', metavar='api_host', help='Check Point R8x management server')
parser.add_argument('password', metavar='api_password', help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch',
                    help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443',
                    help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True,
                    help='name of policy layer to create rules in')
parser.add_argument('-n', '--number_of_test_objs', metavar='number_of_objs_to_create', default=3,
                    help='How many objects and rules to generate; default=3')
parser.add_argument('-d', '--delete_all_test_data', action="store_true",
                    help='delete all test data (all objects and rules with name fworch_test_*')
args = parser.parse_args()

api_host = args.hostname
api_password = args.password
offset = 0
limit = 100
details_level = "full"  # 'standard'
ssl_verification = False
use_object_dictionary = 'false'
name_prefix = 'fworch_test_'
obj_types = ['hosts', 'networks', 'services-tcp']
base_ip = '10.88.99.'


def api_call(ip_addr, port, command, json_payload, sid_a):
    url = 'https://' + ip_addr + ':' + port + '/web_api/' + command
    if sid_a == '':
        request_headers = {'Content-Type': 'application/json'}
    else:
        request_headers = {'Content-Type': 'application/json', 'X-chkp-sid': sid_a}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification)
    return r.json()


def login(user, password, api_host_a, api_port):
    payload = {'user': user, 'password': password}
    response = api_call(api_host_a, api_port, 'login', payload, '')
    return response["sid"]


sid = login(args.user, api_password, api_host, args.port)

if args.delete_all_test_data:  # delete
    for obj_type in obj_types:
        current = 0
        while current < args.number_of_test_objs:
            del_cmd = "error, not defined"
            del_req = "error, not defined"
            if obj_type == 'networks':
                del_cmd = 'delete-network'
                del_req = {'name': name_prefix + 'net_' + str(current)}
            if obj_type == 'hosts':
                del_cmd = 'delete-host'
                del_req = {'name': name_prefix + 'host_' + str(current)}
            if obj_type == 'services-tcp':
                del_cmd = 'delete-service-tcp'
                del_req = {'name': name_prefix + 'svc_tcp_' + str(current)}
            ret = api_call(api_host, args.port, del_cmd, del_req, sid)
            print("del return value: " + str(ret))
            current += 1
    current = 0
    cmd = 'delete-access-rule'
    while current < args.number_of_test_objs:
        req = {'layer': args.layer, 'name': name_prefix + str(current)}
        ret = api_call(api_host, args.port, cmd, req, sid)
        print("rule del return value: " + str(ret))
        current += 1
else:  # create
    for obj_type in obj_types:
        current = 0
        while current < args.number_of_test_objs:
            create_cmd = "error, not defined"
            create_req = "error, not defined"
            if obj_type == 'networks':
                create_cmd = 'add-network'
                create_req = {'name': name_prefix + 'net_' + str(current),
                              'subnet': base_ip + str(current % 255 + 1),
                              'mask-length': 32}
            if obj_type == 'hosts':
                create_cmd = 'add-host'
                create_req = {'name': name_prefix + 'host_' + str(current),
                              'ip-address': base_ip + str(current % 255 + 1)}
            if obj_type == 'services-tcp':
                create_cmd = 'add-service-tcp'
                create_req = {'name': name_prefix + 'svc_tcp_' + str(current),
                              'port': str(current % 65535 + 1)}
            ret = api_call(api_host, args.port, create_cmd, create_req, sid)
            print("create return value: " + str(ret))
            current += 1
        cmd = 'add-access-rule'
    # create test rules:
    current = 0
    cmd = 'add-access-rule'
    while current < args.number_of_test_objs:
        req = {
            'layer': args.layer,
            'position': 'top',
            'name': name_prefix + 'rule_' + str(current),
            'source': name_prefix + 'net_' + str(current),
            'destination': name_prefix + 'host_' + str(current),
            'service': name_prefix + 'svc_tcp_' + str(current),
            'action': 'Accept',
            'track': 'Log'
        }
        ret = api_call(api_host, args.port, cmd, req, sid)
        print("rule add return value: " + str(ret))
        current += 1
# publish and logout
ret = api_call(api_host, args.port, 'publish', {}, sid)
print("publish return value: " + str(ret))
logout_result = api_call(api_host, args.port, 'logout', {}, sid)

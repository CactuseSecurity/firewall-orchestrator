#!/usr/bin/python3

import fworch_session_cp_r8x_api.py as ses
import fworch_logging_cp_r8x_api.py as log
import json, argparse

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--hostname', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password', required=True, help='password for management server')
parser.add_argument('-m', '--mode', metavar='mode', required=True, help='[domains|packages|layers]')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0')
parser.add_argument('-V', '--version', metavar='api_version', default='off', help='alternate API version [off|<version number>]; default=off')

args = parser.parse_args()
domain = args.domain
if args.mode == 'packages':
    api_command='show-packages'
    api_details_level="standard"    # 'standard'|full
elif args.mode == 'domains':
    api_command='show-domains'
    api_details_level="standard"
    domain = ''
elif args.mode == 'layers':
    api_command='show-access-layers'
    api_details_level="standard"
else:
    sys.exit("\"" + args.mode +"\" - unknown mode")

proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
details_level = "full"    # 'standard'
use_object_dictionary = 'false'
base_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'

log.set_log_level(0,int(args.debug))
ssl_verification = ses.set_ssl_verification(args.ssl)


# show package name "New_Standard_Package_1" --format json
xsid = ses.login(args.user, args.password, args.hostname, args.port, domain, base_url, ssl_verification, proxy_string)
api_versions = ses.api_call(args.hostname, args.port, base_url, 'show-api-versions', {}, ssl_verification, proxy_string, xsid)

api_version = api_versions["current-version"]
api_supported = api_versions["supported-versions"]
v_url = ses.set_api_url(base_url,args.version,api_supported,args.hostname)

log.logging.debug ("fworch_get_dom_pkg_layer_cp_r8x_api - current version: "+ api_version )
log.logging.debug ("fworch_get_dom_pkg_layer_cp_r8x_api - supported versions: "+ ', '.join(api_supported) )
log.logging.debug ("fworch_get_dom_pkg_layer_cp_r8x_api - limit:"+ args.limit )
log.logging.debug ("fworch_get_dom_pkg_layer_cp_r8x_api - Domain:"+ args.domain )
log.logging.debug ("fworch_get_dom_pkg_layer_cp_r8x_api - login:"+ args.user )
log.logging.debug ("fworch_get_dom_pkg_layer_cp_r8x_api - sid:"+ xsid )

    #packages = ses.api_call(args.hostname, args.port, 'show-access-layers', {"limit" : 50, "offset" : 0, "details-level" : "standard", "domains-to-process" : "ALL_DOMAINS_ON_THIS_SERVER"}, sid)
result = ses.api_call(args.hostname, args.port,  v_url, api_command, {"limit" : args.limit, "offset" : offset, "details-level" : api_details_level}, ssl_verification, proxy_string, xsid)
if args.debug == "1" or args.debug == "3":
    print ("\ndump of result:\n" + json.dumps(result, indent=4))
if args.mode == 'packages':
    print ("\nthe following packages exist on management server:")
    for p in result['packages']:
        print ("    package: " + p['name'])
    if "access-layers" in result:
        print ("the following layers exist on management server:")
        for p in result['packages']:
            print ("    package: " + p['name'])
            for l in p['access-layers']:
                print ("        layer: " + l['name'])
if args.mode == 'domains':
    print ("\nthe following domains exist on management server:")
    for d in result['objects']:
        print ("    domain: " + d['name'])
if args.mode == 'layers':
    print ("\nthe following access-layers exist on management server:")
    for l in result['access-layers']:
        print ("    access-layer: " + l['name'])
print()
logout_result = ses.api_call(args.hostname, args.port, v_url, 'logout', {}, ssl_verification, proxy_string, xsid)

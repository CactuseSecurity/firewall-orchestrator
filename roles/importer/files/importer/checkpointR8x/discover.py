#!/usr/bin/python3
import logging
import logging.config
import getter
import common
import json, argparse, os, sys

logging.config.fileConfig(fname='discovery_logging.conf', disable_existing_loggers=False)

logger = logging.getLogger(__name__)

logger.info("START")
parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--hostname', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password', required=True, help='password for management server')
parser.add_argument('-m', '--mode', metavar='mode', required=True, help='[domains|packages|layers|generic]')
parser.add_argument('-c', '--command', metavar='command', required=False, help='generic command to send to the api (needs -m generic)')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Environment')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0')
parser.add_argument('-V', '--version', metavar='api_version', default='off', help='alternate API version [off|<version number>]; default=off')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

domain = args.domain

if args.mode == 'packages':
    api_command='show-packages'
    api_details_level="standard"
elif args.mode == 'domains':
    api_command='show-domains'
    api_details_level="standard"
    domain = ''
elif args.mode == 'layers':
    api_command='show-access-layers'
    api_details_level="standard"
elif args.mode == 'generic':
    api_command=args.command
    api_details_level="details"
else:
    sys.exit("\"" + args.mode +"\" - unknown mode")

proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
details_level = "full"
use_object_dictionary = 'false'
base_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'
ssl_verification = getter.set_ssl_verification(args.ssl)
logger = logging.getLogger(__name__)

xsid = getter.login(args.user, args.password, args.hostname, args.port, domain, ssl_verification, proxy_string)
api_versions = getter.api_call(args.hostname, args.port, base_url, 'show-api-versions', {}, xsid, ssl_verification, proxy_string)

api_version = api_versions["current-version"]
api_supported = api_versions["supported-versions"]
v_url = getter.set_api_url(base_url,args.version,api_supported,args.hostname)
logger.debug ("current version: "+ api_version )
logger.debug ("supported versions: "+ ', '.join(api_supported) )
logger.debug ("limit:"+ args.limit )
logger.debug ("Domain:"+ args.domain )
logger.debug ("login:"+ args.user )
logger.debug ("sid:"+ xsid )

result = getter.api_call(args.hostname, args.port, v_url, api_command, {"limit" : args.limit, "offset" : offset, "details-level" : api_details_level}, xsid, ssl_verification, proxy_string)
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
        print ("    domain: " + d['name'] + ", uid: " + d['uid'])
if args.mode == 'layers':
    print ("\nthe following access-layers exist on management server:")
    for l in result['access-layers']:
        print ("    access-layer: " + l['name'] + ", uid: " + l['uid'] )
if args.mode == 'generic':
    print ("running api command " + api_command)
    print (str(result))
print()

logout_result = getter.api_call(args.hostname, args.port, v_url, 'logout', {}, xsid, ssl_verification, proxy_string)

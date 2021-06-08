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
parser.add_argument('-m', '--mode', metavar='mode', required=True, help='[domains|packages|layers|generic|devices]')
parser.add_argument('-c', '--command', metavar='command', required=False, help='generic command to send to the api (needs -m generic). ' +
                            'Please note that the command must be written as one word (e.g. show-access-layer instead of show acess-layers).')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Environment')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-l', '--level', metavar='level_of_detail', default='standard', help='[standard|full]')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-n', '--nolimit', metavar='nolimit', default='off', help='[on|off] Set to on if (generic) command does not understand limit switch')
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
elif args.mode == 'domains' or args.mode == 'devices':
    api_command='show-domains'
    api_details_level="standard"
    domain = ''
elif args.mode == 'layers':
    api_command='show-access-layers'
    api_details_level="standard"
elif args.mode == 'generic':
    api_command=args.command
    api_details_level=args.level
else:
    sys.exit("\"" + args.mode +"\" - unknown mode")

proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
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

payload = { "details-level" : api_details_level }
if args.nolimit == 'off':
    payload.update( { "limit" : args.limit, "offset" : offset } )

if args.mode == 'generic': # need to divide command string into command and payload (i.e. parameters)
    cmd_parts = api_command.split(" ")
    api_command = cmd_parts[0]
    idx = 1
    if len(cmd_parts)>1:
        payload.pop('limit')
        payload.pop('offset')
    while idx < len(cmd_parts):
        payload.update({cmd_parts[idx]: cmd_parts[idx+1]})
        idx += 2

result = getter.api_call(args.hostname, args.port, v_url, api_command, payload, xsid, ssl_verification, proxy_string)

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
    print (json.dumps(result, indent=3))

# get complete device information (auto-discovery) and display it
if args.mode == 'devices':
    domains = result
    if domains['total']== 0:
        print("no domains found, adding dummy domain.")
        domains['objects'].append ({ "name": "", "uid": "" }) 
    for obj in domains['objects']:
        # fetching gateways:
        obj['gateways'] = getter.api_call(args.hostname, args.port, v_url, 
            "show-gateways-and-servers", { "details-level" : "full" }, 
            xsid, ssl_verification, proxy_string)
        for gw in obj['gateways']['objects']:
            print ("gw or server: " + gw['name'] + ", (uid=" + gw['uid'] + ")")
            if 'type' in gw and gw['type'] in ['simple-gateway', 'others'] and 'policy' in gw:
                print ("found gateway: " + gw['name'] + ", (uid=" + gw['uid'] + ")")
                if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                    print ("found firewall gateway with policy: " + gw['name'] + ", (uid=" + gw['uid'] + ")")
                    gw['package'] = getter.api_call(args.hostname, args.port, v_url, 
                        "show-package", 
                        { "name" : gw['policy']['access-policy-name'], "details-level": "full" }, 
                        xsid, ssl_verification, proxy_string)

                    for layer in gw['package']['access-layers']:
                        print ("access-layer: " + layer['name'] + ", (uid=" + layer['uid'] + ")")
                        if 'firewall' in layer and layer['firewall']:
                            print ("found firewall layer: " + layer['name'] + ", (uid=" + layer['uid'] + ")")
                        # else:
                        #     gw['package']['access-layers'].remove(layer)
            #     else: # remove non-fw gw
            #         obj['gateways']['objects'].remove(gw)
            # else: # remove non-gw object
            #     obj['gateways']['objects'].remove(gw)

    # copy only the relevant data to new dict
    domains_essential = []
    for obj in domains['objects']:
        domain = {
                'name':  obj['name'],
                'uid': obj['uid']
            }
        gateways = []
        for gw in obj['gateways']['objects']:
            if 'policy' in gw:
                gateway = { "name": gw['name'], "uid": gw['uid'], "access-policy-name": gw['policy']["access-policy-name"] }
                layers = []
                if 'package' in gw:
                    for ly in gw['package']['access-layers']:
                        layer = { "name": ly['name'], "uid": ly['uid'] }
                        layers.append(layer)
                gateway['layers'] = layers
                gateways.append(gateway)
            domain['gateways'] = gateways
        domains_essential.append(domain)
    devices = {"domains": domains_essential }
    print (json.dumps(devices, indent=3))

print()

logout_result = getter.api_call(args.hostname, args.port, v_url, 'logout', {}, xsid, ssl_verification, proxy_string)

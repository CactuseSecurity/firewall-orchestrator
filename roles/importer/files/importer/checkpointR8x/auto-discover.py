#!/usr/bin/python3
import sys
# from .. common import importer_base_dir
sys.path.append('..')
import logging, logging.config
import getter
import json, argparse, sys
import fwo_log
logging.config.fileConfig(fname='discovery_logging.conf', disable_existing_loggers=False)

logger = logging.getLogger(__name__)

logger.info("START")
parser = argparse.ArgumentParser(description='Discover all devices, policies starting from a single server (MDS or stand-alone) from Check Point R8x management via API calls')
parser.add_argument('-a', '--hostname', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password_file', metavar='api_password_file', required=True, help='name of file containing the password for API of the management server')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0')
parser.add_argument('-V', '--version', metavar='api_version', default='off', help='alternate API version [off|<version number>]; default=off')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Environment')
parser.add_argument('-f', '--format', metavar='output_format', default='table', help='[json|table]]')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

offset = 0
use_object_dictionary = 'false'
base_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'
ssl_verification = fwo_log.set_ssl_verification(args.ssl, debug_level=args.debug)

with open(args.password_file, 'r') as file:
    apiuser_pwd = file.read().replace('\n', '')

xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, args.domain, ssl_verification=ssl_verification, debug=args.debug)

api_versions = getter.cp_api_call(base_url, 'show-api-versions', {}, xsid, ssl_verification=ssl_verification)
api_version = api_versions["current-version"]
api_supported = api_versions["supported-versions"]
v_url = getter.set_api_url(base_url,args.version,api_supported,args.hostname)

v_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'
if args.version != "off":
    v_url += 'v' + args.version + '/'

logger = logging.getLogger(__name__)

xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, '', ssl_verification=ssl_verification)

if args.debug == "1" or args.debug == "3":
    debug = True
else:
    debug = False

# todo: only show active devices (optionally with a switch)
domains = getter.cp_api_call (v_url, 'show-domains', {}, xsid, ssl_verification=ssl_verification)
gw_types = ['simple-gateway', 'simple-cluster', 'CpmiVsClusterNetobj', 'CpmiGatewayPlain', 'CpmiGatewayCluster', 'CpmiVsxClusterNetobj']
parameters =  { "details-level" : "full" }

if domains['total']== 0:
    logging.debug ("no domains found, adding dummy domain.")
    domains['objects'].append ({ "name": "", "uid": "" }) 

    # fetching gateways for non-MDS management:
    obj = domains['objects'][0]
    obj['gateways'] = getter.cp_api_call(v_url, 'show-gateways-and-servers', parameters, xsid, ssl_verification=ssl_verification)

    if 'objects' in obj['gateways']:
        for gw in obj['gateways']['objects']:
            if 'type' in gw and gw['type'] in gw_types and 'policy' in gw:
                if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                    logging.debug ("standalone mgmt: found gateway " + gw['name'] + " with policy" + gw['policy']['access-policy-name'])
                    gw['package'] = getter.cp_api_call(v_url, 
                        "show-package", 
                        { "name" : gw['policy']['access-policy-name'], "details-level": "full" }, 
                        xsid, ssl_verification)
    else:
        logging.warning ("Standalone WARNING: did not find any gateways in stand-alone management")
    logout_result = getter.cp_api_call(v_url, 'logout', {}, xsid, ssl_verification=ssl_verification)

else: # visit each domain and fetch layers
    for obj in domains['objects']:
        domain_name = obj['name']
        logging.debug ("MDS: searchig in domain " + domain_name)
        xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, domain_name, ssl_verification=ssl_verification)
        obj['gateways'] = getter.cp_api_call(v_url, 'show-gateways-and-servers', parameters, xsid, ssl_verification)
        if 'objects' in obj['gateways']:
            for gw in obj['gateways']['objects']:
                if 'type' in gw and gw['type'] in gw_types and 'policy' in gw:
                    if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                        api_call_str = "show-package name " + gw['policy']['access-policy-name'] + ", logged in to domain " + domain_name
                        logging.debug ("MDS: found gateway " + gw['name'] + " with policy: " + gw['policy']['access-policy-name'])
                        logging.debug ("api call: " + api_call_str)
                        try:
                            tmp_pkg_name = getter.cp_api_call(v_url, 'show-package', { "name" : gw['policy']['access-policy-name'], "details-level": "full" },
                                xsid, ssl_verification=ssl_verification)
                        except:
                            tmp_pkg_name = "ERROR while trying to get package " + gw['policy']['access-policy-name']
                        gw['package'] = tmp_pkg_name
        else:
            logging.warning ("Domain-WARNING: did not find any gateways in domain " + obj['name'])
        logout_result = getter.cp_api_call(v_url, 'logout', {}, xsid, ssl_verification=ssl_verification)

# now collect only relevant data and copy to new dict
domains_essential = []
for obj in domains['objects']:
    domain = { 'name':  obj['name'], 'uid': obj['uid'] }
    gateways = []
    domain['gateways'] = gateways
    if 'objects' in obj['gateways']:
        for gw in obj['gateways']['objects']:
            if 'policy' in gw and 'access-policy-name' in  gw['policy']:
                gateway = { "name": gw['name'], "uid": gw['uid'], "access-policy-name": gw['policy']['access-policy-name'] }
                layers = []
                if 'package' in gw:
                    if 'access-layers' in gw['package']:
                        found_domain_layer = False
                        for ly in gw['package']['access-layers']:
                            if 'firewall' in ly and ly['firewall']:
                                if 'parent-layer' in ly:
                                    found_domain_layer = True 
                        for ly in gw['package']['access-layers']:
                            if 'firewall' in ly and ly['firewall']:
                                if 'parent-layer' in ly:
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "domain-layer", "parent-layer": ly['parent-layer'] }
                                elif domains['total']==0:
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "local-layer" }
                                elif found_domain_layer:
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "global-layer" }
                                else:   # in domain context, but no global layer exists
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "stand-alone-layer" }
                                layers.append(layer)
                gateway['layers'] = layers
                gateways.append(gateway)
            domain['gateways'] = gateways
    domains_essential.append(domain)
devices = {"domains": domains_essential }


##### output ########
if args.format == 'json':
    print (json.dumps(devices, indent=3))

elif args.format == 'table':
    # compact print in FWO UI input format
    colsize_number = 35
    colsize = "{:"+str(colsize_number)+"}"
    table = ""
    heading_list = ["Domain/Management", "Gateway", "Policy String"]

    # add table header:
    for heading in heading_list:
        table += colsize.format(heading)
    table += "\n"
    x = 0
    while x <  len(heading_list) * colsize_number:
        table += '-'
        x += 1
    table += "\n"

    # print one gateway/policy per line
    for dom in devices['domains']:
        if 'gateways' in dom:
            for gw in dom['gateways']:
                table += colsize.format(dom["name"])
                table += colsize.format(gw['name'])
                if 'layers' in gw:
                    found_domain_layer = False
                    layer_string = '<undefined>'
                    for ly in gw['layers']:
                        if 'parent-layer' in ly:
                            found_domain_layer = True 
                    for ly in gw['layers']:
                        if ly['type'] == 'stand-alone-layer' or ly['type'] == 'local-layer':
                            layer_string = ly["name"]
                        elif found_domain_layer and ly['type'] == 'domain-layer':
                            domain_layer = ly['name']
                        elif found_domain_layer and ly['type'] == 'global-layer':
                            global_layer = ly['name']
                        else:
                            logging.warning ("found unknown layer type")
                    if found_domain_layer:
                        layer_string = global_layer + '/' + domain_layer
                    table += colsize.format(layer_string)
                table += "\n"
        else:
            table += colsize.format(dom["name"])
        table += "\n"  # empty line between domains for readability

    print (table)

else:
    logging.error("You specified a wrong output format: " + args.format )
    parser.print_help(sys.stderr)
    sys.exit(1)

#!/usr/bin/python3
import logging
import logging.config
import getter
import common
import json, argparse, os, sys

logging.config.fileConfig(fname='discovery_logging.conf', disable_existing_loggers=False)

logger = logging.getLogger(__name__)

logger.info("START")
parser = argparse.ArgumentParser(description='Discover all devices, policies starting from a single server (MDS or stand-alone) from Check Point R8x management via API calls')
parser.add_argument('-a', '--hostname', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password', required=True, help='password for management server')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0')
parser.add_argument('-V', '--version', metavar='api_version', default='off', help='alternate API version [off|<version number>]; default=off')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
use_object_dictionary = 'false'
v_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'
if args.version != "off":
    v_url += 'v' + args.version + '/'
ssl_verification = getter.set_ssl_verification(args.ssl)
logger = logging.getLogger(__name__)

xsid = getter.login(args.user, args.password, args.hostname, args.port, '', ssl_verification, proxy_string)

if args.debug == "1" or args.debug == "3":
    debug = True
else:
    debug = False

# todo: 
# - only show active devices (optionally with a switch)

domains = getter.api_call(args.hostname, args.port, v_url, "show-domains", {}, xsid, ssl_verification, proxy_string)

gw_types = ['simple-gateway', 'simple-cluster', 'CpmiVsClusterNetobj', 'CpmiGatewayPlain', 'CpmiGatewayCluster']
if domains['total']== 0:
    logging.debug ("no domains found, adding dummy domain.")
    domains['objects'].append ({ "name": "", "uid": "" }) 

# fetch gws on MDS level first
for obj in domains['objects']:
    # fetching gateways:
    parameters =  { "details-level" : "full" }
    obj['gateways'] = getter.api_call(args.hostname, args.port, v_url, "show-gateways-and-servers", parameters, xsid, ssl_verification, proxy_string)
    if 'objects' in obj['gateways']:
        for gw in obj['gateways']['objects']:
            logging.debug ("gw or server: " + gw['name'] + ", (uid=" + gw['uid'] + ")")
            if 'type' in gw and gw['type'] in gw_types and 'policy' in gw:
                logging.debug ("found gateway: " + gw['name'] + ", (uid=" + gw['uid'] + ")")
                if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                    logging.debug ("found firewall gateway with policy: " + gw['name'] + ", (uid=" + gw['uid'] + ")")

                    if domains['total']== 0:  # stand-alone mgmt (no MDS)
                        gw['package'] = getter.api_call(args.hostname, args.port, v_url, 
                            "show-package", 
                            { "name" : gw['policy']['access-policy-name'], "details-level": "full" }, 
                            xsid, ssl_verification, proxy_string)
                        if 'access-layers' in gw['package']:
                            for layer in gw['package']['access-layers']:
                                logging.debug ("access-layer: " + layer['name'] + ", (uid=" + layer['uid'] + ")")
                                if 'firewall' in layer and layer['firewall']:
                                    logging.debug ("found firewall layer: " + layer['name'] + ", (uid=" + layer['uid'] + ")")
    else:
        logging.warning ("MDS-WARNING: did not find any gateways in domain " + obj['name'])

logout_result = getter.api_call(args.hostname, args.port, v_url, 'logout', {}, xsid, ssl_verification, proxy_string)

# now visit each domain and fetch layers
if domains['total']> 0:  # MDS
    for obj in domains['objects']:
        xsid = getter.login(args.user, args.password, args.hostname, args.port, obj['name'], ssl_verification, proxy_string)
        obj['gateways'] = getter.api_call(args.hostname, args.port, v_url, "show-gateways-and-servers", parameters, xsid, ssl_verification, proxy_string)
        if 'objects' in obj['gateways']:
            for gw in obj['gateways']['objects']:
                if 'type' in gw and gw['type'] in gw_types and 'policy' in gw:
                    if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                        # make api call to domain
                        gw['package'] = getter.api_call(args.hostname, args.port, v_url, "show-package", 
                            { "name" : gw['policy']['access-policy-name'], "details-level": "full" }, 
                            xsid, ssl_verification, proxy_string)
                        # if 'access-layers' in gw['package']:
                        #     for layer in gw['package']['access-layers']:
                        #         logging.debug  ("access-layer: " + layer['name'] + ", (uid=" + layer['uid'] + ")")
                        #         if 'firewall' in layer and layer['firewall']:
                        #             logging.debug  ("found firewall layer: " + layer['name'] + ", (uid=" + layer['uid'] + ")")
        else:
            logging.warning ("Domain-WARNING: did not find any gateways in domain " + obj['name'])
        logout_result = getter.api_call(args.hostname, args.port, v_url, 'logout', {}, xsid, ssl_verification, proxy_string)


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

print (json.dumps(devices, indent=3))

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

#!/usr/bin/python3
import sys
from common import importer_base_dir
sys.path.append(importer_base_dir)
import common 
import parse_network, parse_service, parse_user # parse_rule, 
import parse_network_csv, parse_rule_csv, parse_service_csv, parse_user_csv
import argparse
import json
import sys
import fwo_log


parser = argparse.ArgumentParser(description='parse json configuration file from Check Point R8x management')
parser.add_argument('-f', '--config_file', required=True, help='name of config file to parse (json format)')
parser.add_argument('-i', '--import_id', default='0', help='unique import id')
parser.add_argument('-m', '--management_name', default='<mgmt-name>', help='name of management system to import')
parser.add_argument('-r', '--rulebase', default='', help='name of rulebase to import')
parser.add_argument('-n', '--network_objects', action="store_true", help='import network objects')
parser.add_argument('-s', '--service_objects', action="store_true", help='import service objects')
parser.add_argument('-u', '--users', action="store_true", help='import users')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 1(DEBUG Console) 2(DEBUG File)i 2(DEBUG Console&File); default=0')
args = parser.parse_args()

found_rulebase = False
number_of_section_headers_so_far = 0
rule_num = 0
nw_objects = [] 
svc_objects = []
section_header_uids=[]
result = ""

# log config
debug_level = int(args.debug)
logger = fwo_log.getFwoLogger()

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

config_filename = args.config_file
use_object_dictionary = 'false'

with open(args.config_file, "r") as json_data:
    config = json.load(json_data)

logger.debug ("parse_config - args"+ "\nf:" +args.config_file +"\ni: "+ args.import_id +"\nm: "+ args.management_name +"\nr: "+ args.rulebase +"\nn: "+ str(args.network_objects) +"\ns: "+ str(args.service_objects) +"\nu: "+ str(args.users) +"\nd: "+ str(args.debug))

if args.rulebase != '':
    for rulebase in config['rulebases']:
        current_layer_name = rulebase['layername']
        if current_layer_name == args.rulebase:
            logger.debug("parse_config: found layer to parse: " + current_layer_name)
            found_rulebase = True
            rule_num, result = parse_rule_csv.csv_dump_rules(rulebase, args.rulebase, args.import_id, rule_num=0, section_header_uids=[], parent_uid="", debug_level=debug_level)

if args.network_objects:
    result = ''
    nw_objects = []

    if args.network_objects != '':
        for obj_table in config['object_tables']:
            parse_network.collect_nw_objects(obj_table, nw_objects, debug_level=debug_level)
        for idx in range(0, len(nw_objects)-1):
            if nw_objects[idx]['obj_typ'] == 'group':
                parse_network.add_member_names_for_nw_group(idx, nw_objects)
    
    for nw_obj in nw_objects:
        result += parse_network_csv.csv_dump_nw_obj(nw_obj, args.import_id)

if args.service_objects:
    result = ''
    service_objects = []
    if args.service_objects != '':
        for obj_table in config['object_tables']:
            parse_service.collect_svc_objects(obj_table, service_objects)
        # resolving group members:
        for idx in range(0, len(service_objects)-1):
            if service_objects[idx]['svc_typ'] == 'group':
                parse_service.add_member_names_for_svc_group(idx, service_objects)

    for svc_obj in service_objects:
        result += parse_service_csv.csv_dump_svc_obj(svc_obj, args.import_id)

if args.users:
    users = {}
    result = ''
    for rulebase in config['rulebases']:
        parse_user.collect_users_from_rulebase(rulebase, users)

    for user_name in users.keys():
        user_dict = users[user_name]
        result += parse_user_csv.csv_dump_user(user_name, user_dict, args.import_id)

if args.rulebase != '' and not found_rulebase:
    logger.exception("PARSE ERROR: rulebase '" + args.rulebase + "' not found.")
else:
    result = result[:-1]  # strip off final line break to avoid empty last line
    print(result)

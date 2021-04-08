import common 
import parse_network, parse_rule, parse_service, parse_user
import argparse
import json
import re
import logging

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

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
found_rulebase = False
number_of_section_headers_so_far = 0
rule_num = 0
nw_objects = [] 
svc_objects = []
section_header_uids=[]
result = ""

# logging config
debug_level = int(args.debug)
# todo: save the initial value, reset initial value at the end
# todo: switch to native syslog

if debug_level == 1:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 2:
    logging.basicConfig(filename='/var/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 3:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    logging.basicConfig(filename='/var/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')

args = parser.parse_args()
config_filename = args.config_file
#test_version = args.testing
json_indent=2
use_object_dictionary = 'false'


with open(args.config_file, "r") as json_data:
    config = json.load(json_data)

logging.debug ("parse_config - args"+ "\nf:" +args.config_file +"\ni: "+ args.import_id +"\nm: "+ args.management_name +"\nr: "+ args.rulebase +"\nn: "+ str(args.network_objects) +"\ns: "+ str(args.service_objects) +"\nu: "+ str(args.users) +"\nd: "+ str(args.debug))


if args.rulebase != '':
    for rulebase in config['rulebases']:
        current_layer_name = rulebase['layername']
        if current_layer_name == args.rulebase:
            found_rulebase = True
            result = parse_rule.csv_dump_rules(rulebase, args.rulebase, rule_num=1, header_uids=[], number_of_section_headers_so_far=0)

if args.network_objects:
    result = ''
    if args.network_objects != '':
        for obj_table in config['object_tables']:
            parse_network.collect_nw_objects(obj_table)
        for idx in range(0, len(nw_objects)-1):
            if nw_objects[idx]['obj_typ'] == 'group':
                parse_network.add_member_names_for_nw_group(idx)
    
    for nw_obj in nw_objects:
        result += csv_dump_nw_obj(nw_obj)

if args.service_objects:
    result = ''
    if args.service_objects != '':
        for obj_table in config['object_tables']:
            parse_service.collect_svc_objects(obj_table)
        for idx in range(0, len(svc_objects)-1):
            if svc_objects[idx]['svc_typ'] == 'group':
                parse_service.add_member_names_for_svc_group(idx)

    for svc_obj in svc_objects:
        result += parse_service.csv_dump_svc_obj(svc_obj)

if args.users:
    users = {}
    result = ''
    for rulebase in config['rulebases']:
        parse_user.collect_users_from_rulebase(rulebase)
    for user in users.keys():
        result += parse_user.csv_dump_user(user)

if args.rulebase != '' and not found_rulebase:
    print("PARSE ERROR: rulebase '" + args.rulebase + "' not found.")
else:
    result = result[:-1]  # strip off final line break to avoid empty last line
    print(result)

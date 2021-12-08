import sys
base_dir = "/usr/local/fworch"
sys.path.append(base_dir + '/importer')
sys.path.append(base_dir + '/importer/checkpointR8x')
import os 
import parse_network, parse_rule, parse_service, parse_user
import json
import sys
import logging
import copy
import parse_rule
import parse_user
import parse_service
import parse_network

nw_obj_table_names = ['hosts', 'networks', 'address-ranges', 'multicast-address-ranges', 'groups', 'gateways-and-servers', 'simple-gateways', 'CpmiGatewayPlain', 'CpmiAnyObject']  
# now test to also get: CpmiAnyObject, external 

svc_obj_table_names = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-other', 'services-icmp', 'services-icmp6', 'CpmiAnyObject']

# the following is the static across all installations unique any obj uid 
# cannot fetch the Any object via API (<=1.7) at the moment
# therefore we have a workaround adding the object manually (as svc and nw)
any_obj_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"
# todo: read this from config (from API 1.6 on it is fetched)

original_obj_uid = "85c0f50f-6d8a-4528-88ab-5fb11d8fe16c"
# used for nat only (both svc and nw obj)

# this is just a test UID for debugging a single rule
debug_new_uid = "90f749ec-5331-477d-89e5-a58990f7271d"


def get_config(config2import, current_import_id, base_dir, mgm_details, secret_filename, rulebase_string, config_filename, debug_level, package, proxy_string='', limit=150, force=False):
    logging.info("found Check Point R8x management")

    apihost = ' -a ' + mgm_details['hostname']
    apiuser = ' -u ' + mgm_details['user']
    starttime = ''
    if 'import_controls' in mgm_details:
        for importctl in mgm_details['import_controls']: 
            if 'starttime' in importctl:
                starttime = ' -f "' + importctl['starttime'] + '"'
    secret_file = ' -w "' + secret_filename + '"'
    rulebase = ' -l "' + rulebase_string + '"'
    output_file = ' -o "' + config_filename + '"'
    debug = ' -d ' + str(debug_level)
    api_limit = ' -i ' + str(limit)
    if proxy_string != '':
        proxy_string = ' -x ' + proxy_string
    if package == None or package == '' or package == [None]:
        package_string = ''
    else:
        package_string = ' -k "' + ','.join(package) + '"'
    force_string = ''
    if force is True:
        force_string = ' -F '
    config_file = ' -c ' + config_filename

    get_config_cmd = "cd " + base_dir + "/importer/checkpointR8x && ./get_config.py" + \
        apihost + apiuser + starttime + secret_file + rulebase + output_file + debug + api_limit + proxy_string + package_string + force_string

    get_config_cmd += " && ./enrich_config.py " + apihost + apiuser + secret_file + rulebase + config_file + debug + api_limit + proxy_string
    logging.debug("get_config_cmd: " + get_config_cmd)

    result = os.system(get_config_cmd)
    if result != 0:
        return result
    else:
        with open(config_filename, "r") as json_data:
            full_config_json = json.load(json_data)
            if len(full_config_json) == 0:
                return 0
        parse_network.parse_network_objects_to_json(
            full_config_json, config2import, current_import_id)
        parse_service.parse_service_objects_to_json(
            full_config_json, config2import, current_import_id)
        if 'users' not in full_config_json:
            full_config_json.update({'users': {}})
        rb_range = range(len(rulebase_string.split(',')))
        target_rulebase = []
        rule_num = 0
        parent_uid=""
        section_header_uids=[]
        for rb_id in rb_range:
            parse_user.parse_user_objects_from_rulebase(
                full_config_json['rulebases'][rb_id], full_config_json['users'], current_import_id)
            # if current_layer_name == args.rulebase:
            logging.debug("parsing layer " + full_config_json['rulebases'][rb_id]['layername'])
            rule_num = parse_rule.parse_rulebase_json(
                full_config_json['rulebases'][rb_id], target_rulebase, full_config_json['rulebases'][rb_id]['layername'], current_import_id, rule_num, section_header_uids, parent_uid)
            # now parse the nat rulebase
            # rule_num = parse_rule.parse_nat_rulebase_json(
            #     full_config_json['nat_rulebases'][rb_id], target_rulebase, package[rb_id], current_import_id, rule_num, section_header_uids, parent_uid)
            if len(full_config_json['nat_rulebases'])>0:
                rule_num = parse_rule.parse_nat_rulebase_json(
                    full_config_json['nat_rulebases'][rb_id], target_rulebase, full_config_json['rulebases'][rb_id]['layername'], current_import_id, rule_num, section_header_uids, parent_uid)
        config2import.update({'rules': target_rulebase})

        # copy users from full_config to config2import
        # also converting users from dict to array:
        config2import.update({'user_objects': []})
        for user_name in full_config_json['users'].keys():
            user = copy.deepcopy(full_config_json['users'][user_name])
            user.update({'user_name': user_name})
            config2import['user_objects'].append(user)

    return 0


def get_ip_of_obj(obj):
    if 'ipv4-address' in obj:
        ip_addr = obj['ipv4-address']
    elif 'ipv6-address' in obj:
        ip_addr = obj['ipv6-address']
    elif 'subnet4' in obj:
        ip_addr = obj['subnet4'] + '/' + str(obj['mask-length4'])
    elif 'subnet6' in obj:
        ip_addr = obj['subnet6'] + '/' + str(obj['mask-length6'])
    elif 'ipv4-address-first' in obj and 'ipv4-address-last' in obj:
        ip_addr = obj['ipv4-address-first'] + '-' + str(obj['ipv4-address-last'])
    elif 'ipv6-address-first' in obj and 'ipv6-address-last' in obj:
        ip_addr = obj['ipv6-address-first'] + '-' + str(obj['ipv6-address-last'])
    elif 'obj_typ' in obj and obj['obj_typ'] == 'group':
        ip_addr = ''
    else:
        ip_addr = '0.0.0.0/0'
    return ip_addr

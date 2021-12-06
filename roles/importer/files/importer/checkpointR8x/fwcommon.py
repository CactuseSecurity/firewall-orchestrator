import sys
base_dir = "/usr/local/fworch"
sys.path.append(base_dir + '/importer')
sys.path.append(base_dir + '/importer/checkpointR8x')
import os 
import json
import logging
import copy, time
import parse_network, parse_rule, parse_service, parse_user
import common, getter

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

    last_change_time = ''
    if 'import_controls' in mgm_details:
        for importctl in mgm_details['import_controls']: 
            if 'starttime' in importctl:
                last_change_time = importctl['starttime']
    if package == None or package == '' or package == [None]:
        package_string = ''
    else:
        package_string = '"' + ','.join(package) + '"'
    full_config_json = {}

    common.set_log_level(log_level=debug_level, debug_level=debug_level)

    with open(secret_filename, "r") as password_file:
        api_password = password_file.read().rstrip()

    # todo: ssl verification mode needs to be gotten from somewhere
    # todo: test if proxy is handled properly
    # todo: test if debug_level is handled properly
    # todo: test get_config.py & enrich_config.py

    starttime = int(time.time())
    
    result_simple_get_config = simple_get_config (full_config_json, mgm_details, mgm_details['hostname'], mgm_details['user'], None, api_password, rulebase_string, package_string, mgm_details['configPath'], last_change_time,
        force, mgm_details['port'], { "http" : proxy_string, "https" : proxy_string }, str(limit), details_level='full', test_version='off', debug_level=debug_level, ssl_verification=getter.set_ssl_verification(''))

    if result_simple_get_config>0:
        return result_simple_get_config

    result_enrich_config = enrich_config (full_config_json, mgm_details['hostname'], mgm_details['user'], None, api_password, rulebase_string, package_string, mgm_details['configPath'],
        mgm_details['port'], { "http" : proxy_string, "https" : proxy_string }, str(limit), details_level='full', test_version='off', debug_level=debug_level, ssl_verification=getter.set_ssl_verification(''))

    if result_enrich_config>0:
        return result_enrich_config

    duration = int(time.time()) - starttime
    logging.debug ( "checkpointR8x/get_config - duration: " + str(duration) + "s" )


    # result = os.system(get_config_cmd)

    if full_config_json == {}: # no changes
        return 0
    else:
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

##################### 2nd-level functions ###################################

def simple_get_config (config_json, mgm_details, api_host, api_user, config_filename, api_password, layers, package='', api_domain='', last_import_time=None,
    force=False, api_port=443, proxy_string='{ "http": "", "https": "" }', limit=150, details_level='full', test_version='off', debug_level=0, ssl_verification=''):

    if mgm_details!=None:
        api_host = mgm_details['hostname']
        api_user =  mgm_details['user']
        api_domain = mgm_details['configPath']
        api_port = str(mgm_details['port'])
        api_password = mgm_details['secret']
        # proxy_string = mgm_details['proxy']
        # fetch_limit = mgm_details['fetch_limit']
        # ssl_verification = mgm_details['ssl_verification']
        # debug_level = mgm_details['debug']

    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'
    use_object_dictionary = 'false'

    # top level dict start, sid contains the domain information, so only sending domain during login
    sid = getter.login(api_user,api_password,api_host,api_port,api_domain,ssl_verification, proxy_string)
    v_url = getter.get_api_url (sid, api_host, api_port, api_user, base_url, limit, test_version, ssl_verification, proxy_string)

    if last_import_time==None or last_import_time=='' or force:
        # if no last import time found or given or if force flag is set, do full import
        changes = 1
    else:
        # otherwise search for any changes since last import
        changes = getter.get_changes(sid, api_host,api_port,last_import_time,ssl_verification, proxy_string)

    if changes < 0: # changes = -1 is the error state
        logging.debug ( "get_changes: error getting changes")
        return 1
    elif changes == 0:
        logging.debug ( "get_changes: no new changes found")
    else:
        logging.debug ( "get_changes: changes found or forced mode -> go ahead with getting config, Force = " + str(force))

        config_json.update({'rulebases': [], 'nat_rulebases': [] })
        show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level}

        # read all rulebases:
        # handle per device details
        for device in mgm_details['devices']:
            if device['global_rulebase_name'] != None and device['global_rulebase_name']!='':
                # logging.debug ( "get_config - layer contains global and domain part separated by slash, parsing individually: " + layer )
                show_params_rules['name'] = device['global_rulebase_name']
                # get global layer rulebase
                logging.debug ( "get_config - getting layer: " + show_params_rules['name'] )
                current_layer_json = getter.get_layer_from_api_as_dict (api_host, api_port, v_url, sid, ssl_verification, proxy_string, show_params_rules, layername=global_layer_name)
                # now also get domain rules 
                show_params_rules['name'] = device['local_rulebase_name']
                current_layer_json['layername'] = device['local_rulebase_name']
                logging.debug ( "get_config - getting domain rule layer: " + show_params_rules['name'] )
                domain_rules = getter.get_layer_from_api_as_dict (api_host, api_port, v_url, sid, ssl_verification, proxy_string, show_params_rules, layername=device['local_rulebase_name'])
                # logging.debug ("found domain rules: " + str(domain_rules) + "\n\n")
                # now handling possible reference to domain rules within global rules
                # if we find the reference, replace it with the domain rules
                if 'layerchunks' in current_layer_json:
                    for chunk in current_layer_json["layerchunks"]:
                        for rule in chunk['rulebase']:
                            if "type" in rule and rule["type"] == "place-holder":
                                logging.debug ("found domain rules place-holder: " + str(rule) + "\n\n")
                                current_layer_json = getter.insert_layer_after_place_holder(current_layer_json, domain_rules, rule['uid'])
                                # logging.debug ("substituted domain rules with chunks: " + json.dumps(current_layer_json, indent=2) + "\n\n")
            else:   # no global rules, just get local ones
                show_params_rules['name'] = device['local_rulebase_name']
                logging.debug ( "get_config - getting layer: " + show_params_rules['name'] )
                current_layer_json = getter.get_layer_from_api_as_dict (api_host, api_port, v_url, sid, ssl_verification, proxy_string, show_params_rules, layername=device['local_rulebase_name'])

            config_json['rulebases'].append(current_layer_json)

            # getting NAT rules - need package name for nat rule retrieval
            # todo: each gateway/layer should have its own package name (pass management details instead of single data?)
            if device['package_name'] != None and device['package_name'] != '':
                show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level, 'package': device['package_name'] }
                logging.debug ( "get_config - getting nat rules for package: " + package )
                config_json['nat_rulebases'].append(getter.get_nat_rules_from_api_as_dict (api_host, api_port, v_url, sid, ssl_verification, proxy_string, show_params_rules))

        # leaving rules, moving on to objects
        config_json["object_tables"] = []
        show_params_objs = {'limit':limit,'details-level': details_level}

        for obj_type in getter.api_obj_types:
            object_table = { "object_type": obj_type, "object_chunks": [] }
            current=0
            total=current+1
            show_cmd = 'show-' + obj_type
            logging.debug ( "get_config - obj_type: "+ obj_type )
            while (current<total) :
                show_params_objs['offset']=current
                objects = getter.api_call(api_host, api_port, v_url, show_cmd, show_params_objs, sid, ssl_verification, proxy_string)
                object_table["object_chunks"].append(objects)
                if 'total' in objects  and 'to' in objects:
                    total=objects['total']
                    current=objects['to']
                    logging.debug ( "get_config - "+ obj_type +" current:"+ str(current) )
                    logging.debug ( "get_config - "+ obj_type +" total:"+ str(total) )
                else :
                    current = total
                    logging.debug ( "get_config - "+ obj_type +" total:"+ str(total) )
            config_json["object_tables"].append(object_table)
    logout_result = getter.api_call(api_host, api_port, v_url, 'logout', '', sid, ssl_verification, proxy_string)

    # only write config to file if config_filename is given
    if config_filename != None and len(config_filename)>1:
        with open(config_filename, "w") as configfile_json:
            configfile_json.write(json.dumps(config_json))

    if changes == 0:
        return 2
    return 0


################# enrich #######################

def enrich_config (config, api_host, api_user, config_filename, api_password, layer, package='', api_domain='',
    api_port=443, proxy_string='{ "http": "", "https": "" }', limit=150, details_level='full', test_version='off', debug_level=0, ssl_verification='', noapi=False):

    # requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

    if config_filename != None and len(config_filename)>1:
        time.sleep(1)
        # if filename is passed, read config from file instead of config parameter
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'
    nw_objs_from_obj_tables = []
    svc_objs_from_obj_tables = []

    # logging config
    common.set_log_level(log_level=debug_level, debug_level=debug_level)

    starttime = int(time.time())

    # do nothing for empty configs
    if config == {}:
        return 0

    #################################################################################
    # adding inline and domain layers 
    #################################################################################

    found_new_inline_layers = True
    old_inline_layers = []
    while found_new_inline_layers is True:
        # sweep existing rules for inline layer links
        inline_layers = []
        for rulebase in config['rulebases']:
            getter.get_inline_layer_names_from_rulebase(rulebase, inline_layers)

        if len(inline_layers) == len(old_inline_layers):
            found_new_inline_layers = False
        else:
            old_inline_layers = inline_layers
            for layer in inline_layers:
                logging.debug ( "enrich_config - found inline layer " + layer )
                # enrich config --> get additional layers referenced in top level layers by name
                # also handle possible recursion (inline layer containing inline layer(s))
                # get layer rules from api
                # add layer rules to config

    # next phase: how to logically link layer guard with rules in layer? --> AND of src, dst & svc between layer guard and each rule in layer?

    #################################################################################
    # get object data which is only contained as uid in config by making addtional api calls
    #################################################################################

    # get all object uids (together with type) from all rules in fields src, dst, svc
    nw_uids_from_rulebase = []
    svc_uids_from_rulebase = []

    for rulebase in config['rulebases']:
        logging.debug ( "enrich_config - searching for all uids in rulebase: " + rulebase['layername'] )
        getter.collect_uids_from_rulebase(rulebase, nw_uids_from_rulebase, svc_uids_from_rulebase, "top_level")

    # remove duplicates from uid lists
    nw_uids_from_rulebase = list(set(nw_uids_from_rulebase))
    svc_uids_from_rulebase = list(set(svc_uids_from_rulebase))

    # get all uids in objects tables
    for obj_table in config['object_tables']:
        nw_objs_from_obj_tables.extend(getter.get_all_uids_of_a_type(obj_table, nw_obj_table_names))
        svc_objs_from_obj_tables.extend(getter.get_all_uids_of_a_type(obj_table, getter.svc_obj_table_names))

    # identify all objects (by type) that are missing in objects tables but present in rulebase
    missing_nw_object_uids  = getter.get_broken_object_uids(nw_objs_from_obj_tables, nw_uids_from_rulebase)
    missing_svc_object_uids = getter.get_broken_object_uids(svc_objs_from_obj_tables, svc_uids_from_rulebase)

    # adding the uid of the Original object for natting:
    missing_nw_object_uids.append(original_obj_uid)
    missing_svc_object_uids.append(original_obj_uid)

    logging.debug ( "enrich_config - found missing nw objects: '" + ",".join(missing_nw_object_uids) + "'" )
    logging.debug ( "enrich_config - found missing svc objects: '" + ",".join(missing_svc_object_uids) + "'" )

    if noapi == False:
        sid = getter.login(api_user,api_password,api_host,api_port,api_domain,ssl_verification, proxy_string)
        v_url = getter.get_api_url (sid, api_host, api_port, api_user, base_url, limit, test_version,ssl_verification, proxy_string)
        logging.debug ( "enrich_config - logged into api" )

    # if an object is not there:
    #   make api call: show object details-level full uid "<uid>" and add object to respective json
    for missing_obj in missing_nw_object_uids:
        if noapi == False:
            show_params_host = {'details-level':details_level,'uid':missing_obj}
            logging.debug ( "checkpointR8x/enrich_config - fetching obj with uid: " + missing_obj)
            obj = getter.api_call(api_host, api_port, v_url, 'show-object', show_params_host, sid, ssl_verification, proxy_string)
            obj = obj['object']
            if (obj['type'] == 'CpmiAnyObject'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                            'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                            'comments': 'any nw object checkpoint (hard coded)',
                            'type': 'CpmiAnyObject', 'ipv4-address': '0.0.0.0/0',
                            } ] } ] }
                config['object_tables'].append(json_obj)
            elif (obj['type'] == 'simple-gateway' or obj['type'] == 'CpmiGatewayPlain' or obj['type'] == 'interop'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                    'comments': obj['comments'], 'type': 'host', 'ipv4-address': get_ip_of_obj(obj),
                    } ] } ] }
                config['object_tables'].append(json_obj)
            elif obj['type'] == 'multicast-address-range':
                logging.debug("enrich_config - found multicast-address-range: " + obj['name'] + " (uid:" + obj['uid']+ ")")
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                    'comments': obj['comments'], 'type': 'host', 'ipv4-address': get_ip_of_obj(obj),
                    } ] } ] }
                config['object_tables'].append(json_obj)
            elif (obj['type'] == 'CpmiVsClusterMember' or obj['type'] == 'CpmiVsxClusterMember'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                    'comments': obj['comments'], 'type': 'host', 'ipv4-address': get_ip_of_obj(obj),
                    } ] } ] }
                config['object_tables'].append(json_obj)
                logging.debug ('missing obj: ' + obj['name'] + obj['type'])
            elif (obj['type'] == 'Global'):
                json_obj = {"object_type": "hosts", "object_chunks": [ {
                    "objects": [ {
                    'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                    'comments': obj['comments'], 'type': 'host', 'ipv4-address': '0.0.0.0/0',
                    } ] } ] }
                config['object_tables'].append(json_obj)
                logging.debug ('missing obj: ' + obj['name'] + obj['type'])
            else:
                logging.warning ( "checkpointR8x/enrich_config - missing nw obj of unexpected type '" + obj['type'] + "': " + missing_obj )
                print ("WARNING - enrich_config - missing nw obj of unexpected type: '" + obj['type'] + "': " + missing_obj)

        logging.debug ( "enrich_config - missing nw obj: " + missing_obj )
        print ("INFO: adding nw  obj missing from standard api call results: " + missing_obj)

    for missing_obj in missing_svc_object_uids:
        if noapi == False:
            show_params_host = {'details-level':details_level,'uid':missing_obj}
            obj = getter.api_call(api_host, api_port, v_url, 'show-object', show_params_host, sid, ssl_verification, proxy_string)
            obj = obj['object']
            # print(json.dumps(obj))
            # currently no svc objects are found missing, not even the any obj?
            if (obj['type'] == 'CpmiAnyObject'):
                json_obj = {"object_type": "services-other", "object_chunks": [ {
                        "objects": [ {
                            'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                            'comments': 'any svc object checkpoint (hard coded)',
                            'type': 'service-other', 'ip-protocol': '0'
                            } ] } ] }
                config['object_tables'].append(json_obj)
            elif (obj['type'] == 'Global'):
                json_obj = {"object_type": "services-other", "object_chunks": [ {
                        "objects": [ {
                            'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                            'comments': 'Original svc object checkpoint (hard coded)',
                            'type': 'service-other', 'ip-protocol': '0'
                            } ] } ] }
                config['object_tables'].append(json_obj)
            else:
                logging.warning ( "checkpointR8x/enrich_config - missing svc obj of unexpected type: " + missing_obj )
                print ("WARNING - enrich_config - missing svc obj of unexpected type: '" + obj['type'] + "': " + missing_obj)
        logging.debug ( "enrich_config - missing svc obj: " + missing_obj )
        print ("INFO: adding svc obj missing from standard api call results: " + missing_obj)

    # dump new json file if config_filename is set
    if config_filename != None and len(config_filename)>1:
        if os.path.exists(config_filename): # delete json file (to enabiling re-write)
            os.remove(config_filename)
        with open(config_filename, "w") as json_data:
            json_data.write(json.dumps(config))

    if noapi == False:
        logout_result = getter.api_call(api_host, api_port, v_url, 'logout', '', sid, ssl_verification, proxy_string)
    logging.debug ( "checkpointR8x/enrich_config - duration: " + str(int(time.time()) - starttime) + "s" )

    return 0

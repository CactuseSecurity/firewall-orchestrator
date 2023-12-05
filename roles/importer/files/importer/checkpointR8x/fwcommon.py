import sys
import json
import copy
from common import importer_base_dir
from fwo_log import getFwoLogger
sys.path.append(importer_base_dir + '/checkpointR8x')
import time
import fwo_globals
import cp_rule
import cp_const, cp_network, cp_service
import cp_getter
from cp_enrich import enrich_config
from fwo_exception import FwLoginFailed, FwLogoutFailed
from cp_user import parse_user_objects_from_rulebase


def has_config_changed (full_config, mgm_details, force=False):

    if full_config != {}:   # a native config was passed in, so we assume that an import has to be done (simulating changes here)
        return 1

    domain, _ = prepare_get_vars(mgm_details)

    try: # top level dict start, sid contains the domain information, so only sending domain during login
        session_id = login_cp(mgm_details, domain)
    except:
        raise FwLoginFailed     # maybe 2Temporary failure in name resolution"

    last_change_time = ''
    if 'import_controls' in mgm_details:
        for importctl in mgm_details['import_controls']: 
            if 'starttime' in importctl:
                last_change_time = importctl['starttime']

    if last_change_time==None or last_change_time=='' or force:
        # if no last import time found or given or if force flag is set, do full import
        result = 1
    else: # otherwise search for any changes since last import
        result = (cp_getter.get_changes(session_id, mgm_details['hostname'], str(mgm_details['port']),last_change_time) != 0)

    try: # top level dict start, sid contains the domain information, so only sending domain during login
        logout_result = cp_getter.cp_api_call("https://" + mgm_details['hostname'] + ":" + str(mgm_details['port']) + "/web_api/", 'logout', {}, session_id)
    except:
        raise FwLogoutFailed     # maybe temporary failure in name resolution"
    return result


def get_config(config2import, full_config, current_import_id, mgm_details, limit=150, force=False, jwt=None):
    logger = getFwoLogger()
    if full_config == {}:   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())

        if 'users' not in full_config:
            full_config.update({'users': {}})

        domain, base_url = prepare_get_vars(mgm_details)

        sid = login_cp(mgm_details, domain)

        result_get_rules = get_rules (full_config, mgm_details, base_url, sid, force=force, limit=str(limit), details_level=cp_const.details_level, test_version='off')
        if result_get_rules>0:
            return result_get_rules

        result_get_objects = get_objects (full_config, mgm_details, base_url, sid, force=force, limit=str(limit), details_level=cp_const.details_level, test_version='off')
        if result_get_objects>0:
            return result_get_objects

        result_enrich_config = enrich_config (full_config, mgm_details, limit=str(limit), details_level=cp_const.details_level, sid=sid)

        if result_enrich_config>0:
            return result_enrich_config

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - duration: " + str(duration) + "s" )

    cp_network.normalize_network_objects(full_config, config2import, current_import_id, mgm_id=mgm_details['id'])
    cp_service.normalize_service_objects(full_config, config2import, current_import_id)
    parse_users_from_rulebases(full_config, full_config['rulebases'], full_config['users'], config2import, current_import_id)
    config2import.update({'rules':  cp_rule.normalize_rulebases_top_level(full_config, current_import_id, config2import) })
    if not parsing_config_only: # get config from cp fw mgr
        try: # logout
            logout_result = cp_getter.cp_api_call("https://" + mgm_details['hostname'] + ":" + str(mgm_details['port']) + "/web_api/", 'logout', {}, sid)
        except:
            raise FwLogoutFailed     # maybe emporary failure in name resolution"
    return 0


def prepare_get_vars(mgm_details):

    # from 5.8 onwards: preferably use domain uid instead of domain name due to CP R81 bug with certain installations
    if mgm_details['domainUid'] != None:
        domain = mgm_details['domainUid']
    else:
        domain = mgm_details['configPath']
    api_host = mgm_details['hostname']
    api_user =  mgm_details['import_credential']['user']
    if mgm_details['domainUid'] != None:
        api_domain = mgm_details['domainUid']
    else:
        api_domain = mgm_details['configPath']
    api_port = str(mgm_details['port'])
    api_password = mgm_details['import_credential']['secret']
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'

    return domain, base_url


def login_cp(mgm_details, domain, ssl_verification=True):
    return cp_getter.login(mgm_details['import_credential']['user'], mgm_details['import_credential']['secret'], mgm_details['hostname'], str(mgm_details['port']), domain)


def get_rules (config_json, mgm_details, v_url, sid, force=False, config_filename=None,
    limit=150, details_level=cp_const.details_level, test_version='off', debug_level=0, ssl_verification=True):

    logger = getFwoLogger()
    config_json.update({'rulebases': [], 'nat_rulebases': [] })
    with_hits = True
    show_params_rules = {'limit':limit,'use-object-dictionary':cp_const.use_object_dictionary,'details-level':cp_const.details_level, 'show-hits' : with_hits}

    # read all rulebases: handle per device details
    for device in mgm_details['devices']:
        if device['global_rulebase_name'] != None and device['global_rulebase_name']!='':
            show_params_rules['name'] = device['global_rulebase_name']
            # get global layer rulebase
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            current_layer_json = cp_getter.get_layer_from_api_as_dict (v_url, sid, show_params_rules, layername=device['global_rulebase_name'])
            if current_layer_json is None:
                return 1
            # now also get domain rules 
            show_params_rules['name'] = device['local_rulebase_name']
            current_layer_json['layername'] = device['local_rulebase_name']
            logger.debug ( "getting domain rule layer: " + show_params_rules['name'] )
            domain_rules = cp_getter.get_layer_from_api_as_dict (v_url, sid, show_params_rules, layername=device['local_rulebase_name'])
            if current_layer_json is None:
                return 1

            # now handling possible reference to domain rules within global rules
            # if we find the reference, replace it with the domain rules
            if 'layerchunks' in current_layer_json:
                for chunk in current_layer_json["layerchunks"]:
                    for rule in chunk['rulebase']:
                        if "type" in rule and rule["type"] == "place-holder":
                            logger.debug ("found domain rules place-holder: " + str(rule) + "\n\n")
                            current_layer_json = cp_getter.insert_layer_after_place_holder(current_layer_json, domain_rules, rule['uid'])
        else:   # no global rules, just get local ones
            show_params_rules['name'] = device['local_rulebase_name']
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            current_layer_json = cp_getter.get_layer_from_api_as_dict (v_url, sid, show_params_rules, layername=device['local_rulebase_name'])
            if current_layer_json is None:
                return 1

        config_json['rulebases'].append(current_layer_json)

        # getting NAT rules - need package name for nat rule retrieval
        # todo: each gateway/layer should have its own package name (pass management details instead of single data?)
        if device['package_name'] != None and device['package_name'] != '':
            show_params_rules = {'limit':limit,'use-object-dictionary':cp_const.use_object_dictionary,'details-level':cp_const.details_level, 'package': device['package_name'] }
            if debug_level>3:
                logger.debug ( "getting nat rules for package: " + device['package_name'] )
            nat_rules = cp_getter.get_nat_rules_from_api_as_dict (v_url, sid, show_params_rules)
            if len(nat_rules)>0:
                config_json['nat_rulebases'].append(nat_rules)
            else:
                config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
        else: # always making sure we have an (even empty) nat rulebase per device 
            config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
    return 0
    

def get_objects(config_json, mgm_details, v_url, sid, force=False, config_filename=None,
    limit=150, details_level=cp_const.details_level, test_version='off', debug_level=0, ssl_verification=True):

    logger = getFwoLogger()

    config_json["object_tables"] = []
    show_params_objs = {'limit':limit,'details-level': cp_const.details_level}

    for obj_type in cp_const.api_obj_types:
        object_table = { "object_type": obj_type, "object_chunks": [] }
        current=0
        total=current+1
        show_cmd = 'show-' + obj_type    
        if debug_level>5:
            logger.debug ( "obj_type: "+ obj_type )
        while (current<total) :
            show_params_objs['offset']=current
            objects = cp_getter.cp_api_call(v_url, show_cmd, show_params_objs, sid)
            object_table["object_chunks"].append(objects)
            if 'total' in objects  and 'to' in objects:
                total=objects['total']
                current=objects['to']
                if debug_level>5:
                    logger.debug ( obj_type +" current:"+ str(current) + " of a total " + str(total) )
            else :
                current = total
                if debug_level>5:
                    logger.debug ( obj_type +" total:"+ str(total) )
        config_json["object_tables"].append(object_table)
    # logout_result = cp_getter.cp_api_call(v_url, 'logout', {}, sid)

    # only write config to file if config_filename is given
    if config_filename != None and len(config_filename)>1:
        with open(config_filename, "w") as configfile_json:
            configfile_json.write(json.dumps(config_json))
    return 0


def parse_users_from_rulebases (full_config, rulebase, users, config2import, current_import_id):
    if 'users' not in full_config:
        full_config.update({'users': {}})

    rb_range = range(len(full_config['rulebases']))
    for rb_id in rb_range:
        parse_user_objects_from_rulebase (full_config['rulebases'][rb_id], full_config['users'], current_import_id)

    # copy users from full_config to config2import
    # also converting users from dict to array:
    config2import.update({'user_objects': []})
    for user_name in full_config['users'].keys():
        user = copy.deepcopy(full_config['users'][user_name])
        user.update({'user_name': user_name})
        config2import['user_objects'].append(user)

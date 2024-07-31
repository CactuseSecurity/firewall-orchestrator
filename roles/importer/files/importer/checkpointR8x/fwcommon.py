import sys
import json
import copy
from common import importer_base_dir
from fwo_log import getFwoLogger
sys.path.append(importer_base_dir + '/checkpointR8x')
import time
import cp_rule
import cp_const, cp_network, cp_service
import cp_getter
from fwo_exception import FwLoginFailed, FwLogoutFailed
from cp_user import parse_user_objects_from_rulebase
from fwconfig import FwConfigManager, ConfFormat, ConfigAction, FwConfigManagerList, FwConfigNormalized
from fwoBaseImport import ImportState
from fwo_base import calcManagerUidHash


def has_config_changed (full_config, mgm_details, force=False):

    if full_config != {}:   # a config was passed in (read from file), so we assume that an import has to be done (simulating changes here)
        return 1

    domain, _ = prepare_get_vars(mgm_details)

    session_id = login_cp(mgm_details, domain)

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

    logout_cp("https://" + mgm_details['hostname'] + ":" + str(mgm_details['port']) + "/web_api/", session_id)

    return result


def get_config(full_config: json, importState: ImportState) -> tuple[int, FwConfigManagerList]: # current_import_id, mgm_details, limit=150, force=False, jwt=None) 
    logger = getFwoLogger()
    config2import = {}
    logger.debug ( "starting checkpointR8x/get_config" )

    if full_config == {}:   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())

        if 'users' not in full_config:
            full_config.update({'users': {}})

        domain, base_url = prepare_get_vars(importState.FullMgmDetails)

        sid = login_cp(importState.FullMgmDetails, domain)

        starttimeTemp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting objects ...")

        result_get_objects = get_objects (full_config, importState.FullMgmDetails, base_url, sid, force=importState.ForceImport, limit=str(importState.FwoConfig.ApiFetchSize), details_level=cp_const.details_level_objects, test_version='off')
        if result_get_objects>0:
            return result_get_objects
        logger.debug ( "checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - starttimeTemp) + "s")

        starttimeTemp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting rules ...")
        result_get_rules = get_rules (full_config, importState.FullMgmDetails, base_url, sid, force=importState.ForceImport, limit=str(importState.FwoConfig.ApiFetchSize), details_level=cp_const.details_level, test_version='off')
        if result_get_rules>0:
            return result_get_rules
        logger.debug ( "checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - starttimeTemp) + "s")

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - fetch duration: " + str(duration) + "s" )

    cp_network.normalize_network_objects(full_config, config2import, importState.ImportId, mgm_id=importState.MgmDetails.Id)
    cp_service.normalize_service_objects(full_config, config2import, importState.ImportId)
    #parse_users_from_rulebases(full_config, full_config['rulebases'], full_config['users'], config2import, current_import_id)
    config2import.update({'rules':  cp_rule.normalize_rulebases_top_level(full_config, importState.ImportId, config2import) })
    if not parsing_config_only: # get config from cp fw mgr
        logout_cp("https://" + importState.MgmDetails.Hostname + ":" + str(importState.FullMgmDetails['port']) + "/web_api/", sid)
    
    # make sure all keys are present:
    if not 'network_objects' in config2import:
        config2import.update({'network_objects': []})
    if not 'service_objects' in config2import:
        config2import.update({'service_objects': []})
    if not 'users' in config2import:
        config2import.update({'users': []})
    if not 'zone_objects' in config2import:
        config2import.update({'zone_objects': []})
    if not 'rules' in config2import:
        config2import.update({'rules': []})

    # put dicts into object of class FwConfigManager
    normalizedConfig = FwConfigNormalized(ConfigAction.INSERT, 
                            config2import['network_objects'],
                            config2import['service_objects'],
                            config2import['users'],
                            config2import['zone_objects'],
                            config2import['rules']
                            )
    manager = FwConfigManager(ManagerUid=calcManagerUidHash(importState.FullMgmDetails), 
                              IsGlobal=False, 
                              DependantManagerUids=[], 
                              Configs=[normalizedConfig])
    listOfManagers = FwConfigManagerList()

    listOfManagers.addManager(manager)
    
    return 0, listOfManagers


def prepare_get_vars(mgm_details):
    # from 5.8 onwards: preferably use domain uid instead of domain name due to CP R81 bug with certain installations
    if mgm_details['domainUid'] != None:
        domain = mgm_details['domainUid']
    else:
        domain = mgm_details['configPath']
    api_host = mgm_details['hostname']
    api_port = str(mgm_details['port'])
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'

    return domain, base_url


def login_cp(mgm_details, domain, ssl_verification=True):
    try: # top level dict start, sid contains the domain information, so only sending domain during login
        login_result = cp_getter.login(mgm_details['import_credential']['user'], mgm_details['import_credential']['secret'], mgm_details['hostname'], str(mgm_details['port']), domain)
        return login_result
    except:
        raise FwLoginFailed


def logout_cp(url, sid):
    try:
        logout_result = cp_getter.logout(url, sid)
        return logout_result
    except:
        raise FwLogoutFailed

def get_rules (config_json, mgm_details, v_url, sid, force=False, config_filename=None,
    limit=150, details_level=cp_const.details_level, test_version='off', debug_level=0, ssl_verification=True):

    logger = getFwoLogger()
    config_json.update({'rulebases': [], 'nat_rulebases': [] })
    show_params_rules = {'limit':limit,'use-object-dictionary':cp_const.use_object_dictionary, 'details-level': details_level, 'show-hits': cp_const.with_hits }

    # read all rulebases: handle per device details
    for device in mgm_details['devices']:
        if device['global_rulebase_name'] != None and device['global_rulebase_name']!='':
            show_params_rules.update({'name': device['global_rulebase_name']})
            # get global layer rulebase
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            current_layer_json = cp_getter.get_layer_from_api_as_dict (v_url, sid, show_params_rules, layername=device['global_rulebase_name'], nativeConfig=config_json)
            if current_layer_json is None:
                return 1
            # now also get domain rules 
            show_params_rules.update({'name': device['local_rulebase_name']})
            current_layer_json({'layername': device['local_rulebase_name']})
            logger.debug ( "getting domain rule layer: " + show_params_rules['name'] )
            domain_rules = cp_getter.get_layer_from_api_as_dict (v_url, sid, show_params_rules, layername=device['local_rulebase_name'], nativeConfig=config_json)
            if current_layer_json is None:
                return 1

            # now handling possible reference to domain rules within global rules
            # if we find the reference, replace it with the domain rules
            if 'layerchunks' in current_layer_json:
                for chunk in current_layer_json["layerchunks"]:
                    for rule in chunk['rulebase']:
                        if "type" in rule and rule["type"] == "place-holder":
                            logger.debug ("found domain rules place-holder: " + str(rule) + "\n\n")
                            current_layer_json = cp_getter.insert_layer_after_place_holder(current_layer_json, domain_rules, rule['uid'], nativeConfig=config_json)
        else:   # no global rules, just get local ones
            show_params_rules.update({'name': device['local_rulebase_name']})
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            current_layer_json = cp_getter.get_layer_from_api_as_dict (v_url, sid, show_params_rules, layerName=device['local_rulebase_name'], nativeConfig=config_json)
            if current_layer_json is None:
                return 1

        config_json['rulebases'].append(current_layer_json)

        # getting NAT rules - need package name for nat rule retrieval
        # todo: each gateway/layer should have its own package name (pass management details instead of single data?)
        if device['package_name'] != None and device['package_name'] != '':
            show_params_rules = {'limit':limit,'use-object-dictionary':cp_const.use_object_dictionary,'details-level':details_level, 'package': device['package_name'], 'show-hits': cp_const.with_hits }
            if debug_level>3:
                logger.debug ( "getting nat rules for package: " + device['package_name'] )
            nat_rules = cp_getter.get_nat_rules_from_api_as_dict (v_url, sid, show_params_rules, nativeConfig=config_json)
            if len(nat_rules)>0:
                config_json['nat_rulebases'].append(nat_rules)
            else:
                config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
        else: # always making sure we have an (even empty) nat rulebase per device 
            config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
    return 0


def addRulebaseIfNew(rulebaseToAdd, url, sid, packageName, rulebaseNamesCollected=[], limit=500, nativeConfig={}):
    if rulebaseToAdd in rulebaseNamesCollected:
        return None
    else:
        rulebaseNamesCollected.append(rulebaseToAdd)

        show_params_rules = {
            'limit': limit,
            'use-object-dictionary': cp_const.use_object_dictionary,
            'details-level': 'standard',
            'package': packageName, 
            'show-hits': cp_const.with_hits 
        }

        logger = getFwoLogger()
        rulebaseNamesCollected.append(rulebaseToAdd)
        show_params_rules.update({'name': rulebaseToAdd})
        logger.debug ( "getting layer: " + show_params_rules['name'] )
        return cp_getter.get_layer_from_api_as_dict (url, sid, show_params_rules, layerName=rulebaseToAdd, nativeConfig=nativeConfig)
    

def getRules (config_json, mgm_details, v_url, sid, force=False, config_filename=None,
    limit=150, details_level=cp_const.details_level, test_version='off', debug_level=0, ssl_verification=True):

    logger = getFwoLogger()
    config_json.update({'rulebases': [], 'nat_rulebases': [] })
    show_params_rules = {'limit':limit,'use-object-dictionary':cp_const.use_object_dictionary, 'details-level': details_level, 'show-hits': cp_const.with_hits }

    rulebaseNamesCollected = []
    rulebases = []
    # read all rulebases: handle per device details
    for device in mgm_details['devices']:
        if device['global_rulebase_name'] != None and device['global_rulebase_name']!='':
            rulebases.append(addRulebaseIfNew(device['global_rulebase_name'], rulebaseNamesCollected, limit=limit, nativeConfig=config_json))
        if device['local_rulebase_name'] not in rulebaseNamesCollected:
            rulebases.append(addRulebaseIfNew(device['local_rulebase_name'], rulebaseNamesCollected, limit=limit, nativeConfig=config_json))


        # TODO: add ref to nat rulebase per gateway
        # getting NAT rules - we need the package name for nat rule retrieval

        if device['package_name'] != None and device['package_name'] != '':
            if device['local_rulebase_name'] not in rulebaseNamesCollected:
                rulebases.append(addRulebaseIfNew(device['local_rulebase_name'], rulebaseNamesCollected, limit=limit, nativeConfig=config_json))
                show_params_rules = {'limit':limit,'use-object-dictionary':cp_const.use_object_dictionary,'details-level':details_level, 'package': device['package_name'], 'show-hits': cp_const.with_hits }
                                
                nat_rules = cp_getter.get_nat_rules_from_api_as_dict (v_url, sid, show_params_rules, nativeConfig=config_json)
                if len(nat_rules)>0:
                    config_json['nat_rulebases'].append(nat_rules)
                else:
                    config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
            else: # always making sure we have an (even empty) nat rulebase per device 
                config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })

    config_json.update({ 'rulebases': rulebases })

    return 0
    

def get_objects(config_json, mgm_details, v_url, sid, force=False, config_filename=None,
    limit=150, details_level=cp_const.details_level_objects, test_version='off', debug_level=0, ssl_verification=True):

    logger = getFwoLogger()

    config_json["object_tables"] = []
    show_params_objs = {'limit':limit,'details-level': details_level }

    # getting Original (NAT) object (both for networks and services)
    origObj = cp_getter.getObjectDetailsFromApi(cp_const.original_obj_uid, sid=sid, apiurl=v_url)['object_chunks'][0]
    anyObj = cp_getter.getObjectDetailsFromApi(cp_const.any_obj_uid, sid=sid, apiurl=v_url)['object_chunks'][0]

    for obj_type in cp_const.api_obj_types:
        if obj_type in cp_const.obj_group_types:
            show_params_objs.update({'details-level': cp_const.details_level_group_objects})
        else:
            show_params_objs.update({'details-level': cp_const.details_level_objects})
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

        # adding the uid of the Original and Any objects (as separate chunks):
        if obj_type == 'networks':
            object_table['object_chunks'].append(origObj)
            object_table['object_chunks'].append(anyObj)
        if obj_type == 'services-other':
            object_table['object_chunks'].append(origObj)
            object_table['object_chunks'].append(anyObj)

        config_json["object_tables"].append(object_table)

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


def ParseUidToName(myUid, myObjectDictList):
    """Help function finds name to given UID in object dict 
    
    Parameters
    ----------
    myUid : str
        Checkpoint UID
    myObjectDictList : list[dict]
        Each dict represents a checkpoint object
        Notation of CP API return to 'show-access-rulebase'
        with 'details-level' as 'standard'

    Returns
    -------
    myReturnObject : str
        Name of object with matching UID to input parameter myUid
    """

    logger = getFwoLogger()
    myReturnObject = ''
    for myObject in myObjectDictList:
        if myUid == myObject['uid']:
            myReturnObject = myObject['name']

    if myReturnObject == '':
        logger.warning('The UID: ' + myUid + ' was not found in Object Dict')

    return myReturnObject
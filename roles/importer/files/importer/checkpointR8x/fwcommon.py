import sys
import json
import copy
from common import importer_base_dir
from fwo_log import getFwoLogger
sys.path.append(importer_base_dir + '/checkpointR8x')
import time
import cp_rule
import cp_gateway
import cp_const, cp_network, cp_service
import cp_getter
from fwo_exception import FwLoginFailed
from cp_user import parse_user_objects_from_rulebase
from fwconfig_base import calcManagerUidHash
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction
import fwo_const
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController


# objects as well as rules can now be either from super-amanager or from local manager!
# TODO: decide if we still support importing native config from file
#   might replace this with json config file (in case it is not deserializable into classes)
#def getConfig(nativeConfig:json, importState:ImportStateController, managerSet:FwConfigManagerList) -> tuple[int, FwConfigManagerList]:
#    logger = getFwoLogger()
#    logger.debug ( "starting checkpointR8x/get_config" )
#
#    # get list of managers for import
#    managers = FwConfigManagerList()
#    managers.ManagerSet.append(FwConfigManager(ManagerUid=importState.MgmDetails.Name, ManagerName=importState.MgmDetails.Name))#
#
#    for manager in managers:
#        
#
#    policies = []
#    if importState.MgmDetails.IsSuperManager:
#        # parse all global objects and policies
#        getObjects(importState, normalizedConfig)
#        getPolicies(importState, normalizedConfig)
#    else:
#        # get all details needed to import necessary policies via CP API
#        getGatewayDetails(importState, normalizedConfig)
#
#        # for local managers - only get and parse policies that are used by gateways which are marked as "do import" 
#        for device in importState.FullMgmDetails['devices']:
#            if not device.do_not_import:
#
#                for rulebase in package:
#                    if rulebase not in policies:
#                        normalizedConfig.rulebases.append(getRulebase(rulebase))
#                    normalizedConfig.ManagerSet[mgrSet].Configs.gateways[device].append(rulebase.name )


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


def getRules (nativeConfig: dict, importState: ImportStateController) -> int:
    # delete_v: Schnittstellen die zum Rest passen müssen
    # 1. domain wird durch prepare_get_vars aus importState.FullMgmDetails ausgelesen,
    #    was muss für mds vs standalone beachtet werden
    # 2. importState.FullMgmDetails['devices'][x]['global_rulebase_name'] entscheidet ob mds oder nicht
    # 3. ich mache nirgends logout
    # 4. NAT noch nicht getestet

    logger = getFwoLogger()
    nativeConfig.update({'rulebases': [], 'nat_rulebases': [], 'gateways': [] })
    show_params_policy_structure = {
        'limit': importState.FwoConfig.ApiFetchSize,
        'details-level': 'full'
    }

    domain, cpManagerApiBaseUrl = prepare_get_vars(importState.FullMgmDetails)
    sid = login_cp(importState.FullMgmDetails, domain)

    # get all access (ordered) layers for each policy
    policyStructure = []
    cp_getter.getPolicyStructure(cpManagerApiBaseUrl,
                                 sid,
                                 show_params_policy_structure,
                                 policyStructure = policyStructure)

    show_params_rules = {
        'limit': importState.FwoConfig.ApiFetchSize,
        'use-object-dictionary': cp_const.use_object_dictionary,
        'details-level': 'standard',
        'show-hits': cp_const.with_hits 
    }

    # read all rulebases: handle per device details
    for device in importState.FullMgmDetails['devices']:
        if 'name' in device:

            # find device uid in policy structure
            deviceConfigUid = ''
            for policy in policyStructure:
                for target in policy['targets']:
                    if device['name'] == target['name']:
                        deviceConfigUid = target['uid']
            
            if deviceConfigUid != '':
                deviceConfig = {'name': device['name'],
                                'uid': deviceConfigUid,
                                'rulebase_links': []}
            else:
                logger.error ( "found device without active policy: " + str(device) )
                return 1

        else:
            logger.error ( "found device without name: " + str(device) )
            return 1

        # get ordered layer uids for current device
        orderedLayerUids = []
        for policy in policyStructure:
            foundTargetInPolciy = False
            for target in policy['targets']:
                if target['uid'] == deviceConfig['uid']:
                    foundTargetInPolciy = True
            if foundTargetInPolciy:
                for accessLayer in policy['access-layers']:
                    orderedLayerUids.append(accessLayer['uid'])

        if len(orderedLayerUids) == 0:
            logger.warning ( "found no ordered layers for device: " + deviceConfig['name'] )
            continue

        # decide if mds or stand alone manager
        if device['global_rulebase_name'] != None and device['global_rulebase_name']!='':
            # delete_v: ACHTUNG hier werden namen in show_params_rules benutzt
            show_params_rules.update({'name': device['global_rulebase_name']})

            # get global rulebase
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            cp_getter.getRulebases (cpManagerApiBaseUrl,
                                    sid,
                                    show_params_rules,
                                    rulebaseName=device['global_rulebase_name'],
                                    access_type='access',
                                    nativeConfig=nativeConfig,
                                    deviceConfig=deviceConfig)
            
            # get uid of global rulebase
            for rulebase in nativeConfig['rulebases']:
                if rulebase['name'] == device['global_rulebase_name']:
                    globalRulebaseUid = rulebase['uid']
            
            # define initial rulebase for device in case of mds
            deviceConfig['rulebase_links'].append({
                'from_rulebase_uid': '',
                'from_rule_uid': '',
                'to_rulebase_uid': globalRulebaseUid,
                'type': 'initial'})
            
            # parse global rulebase, find place-holder and link local rulebase (first ordered layer)
            for rulebase in nativeConfig['rulebases']:
                if rulebase['uid'] == globalRulebaseUid:
                    placeholderRuleUid = cp_getter.getRuleUid(rulebase, 'place-holder')
                    break
            deviceConfig['rulebase_links'].append({
                'from_rulebase_uid': globalRulebaseUid,
                'from_rule_uid': placeholderRuleUid,
                'to_rulebase_uid': orderedLayerUids[0],
                'type': 'local'})

        else:
            # define initial rulebase for device in case of stand alone manager
            deviceConfig['rulebase_links'].append({
                'from_rulebase_uid': '',
                'from_rule_uid': '',
                'to_rulebase_uid': orderedLayerUids[0],
                'type': 'initial'})

        # get local rulebases (ordered layers)
        orderedLayerIndex = 0
        for orderedLayerUid in orderedLayerUids:

            # get sid for local domain
            #sid = login_cp(importState.FullMgmDetails, domain) # delete_v wie bekomme ich local domain

            show_params_rules.update({'uid': orderedLayerUid})
            show_params_rules.pop('name', None) # delete_v das kann weg sobald show_params_rules nur noch uids verwendet

            logger.debug ( "getting domain rule layer: " + show_params_rules['uid'] )
            cp_getter.getRulebases (cpManagerApiBaseUrl, 
                                    sid, 
                                    show_params_rules, 
                                    rulebaseUid=orderedLayerUid,
                                    access_type='access',
                                    nativeConfig=nativeConfig,
                                    deviceConfig=deviceConfig)
            
            # parse ordered layer and get last rule uid
            for rulebase in nativeConfig['rulebases']:
                if rulebase['uid'] == orderedLayerUid:
                    lastRuleUid = cp_getter.getRuleUid(rulebase, 'last')
                    break
            
            # link to next ordered layer
            if orderedLayerIndex < len(orderedLayerUids) - 1:
                deviceConfig['rulebase_links'].append({
                    'from_rulebase_uid': orderedLayerUid,
                    'from_rule_uid': lastRuleUid,
                    'to_rulebase_uid': orderedLayerUids[orderedLayerIndex + 1],
                    'type': 'ordered'})
            
            orderedLayerIndex += 1

        # getting NAT rules - need package name for nat rule retrieval
        # todo: each gateway/layer should have its own package name (pass management details instead of single data?)
        if device['package_name'] != None and device['package_name'] != '':
            show_params_rules = {
                'limit': importState.FwoConfig.ApiFetchSize,
                'use-object-dictionary':cp_const.use_object_dictionary,
                'details-level': 'standard', 
                'package': device['package_name'] } #  'show-hits': cp_const.with_hits
            if importState.DebugLevel>3:
                logger.debug ( "getting nat rules for package: " + device['package_name'] )
            nat_rules = cp_getter.get_nat_rules_from_api_as_dict (cpManagerApiBaseUrl, 
                                                                  sid, 
                                                                  show_params_rules, 
                                                                  nativeConfig=nativeConfig)
            if len(nat_rules)>0:
                nativeConfig['nat_rulebases'].append(nat_rules)
            else:
                nativeConfig['nat_rulebases'].append({ "nat_rule_chunks": [] })
        else: # always making sure we have an (even empty) nat rulebase per device 
            nativeConfig['nat_rulebases'].append({ "nat_rule_chunks": [] })

        nativeConfig['gateways'].append(deviceConfig)
    return 0


def get_config(nativeConfig: json, importState: ImportStateController) -> tuple[int, FwConfigManagerList]:
    logger = getFwoLogger()
    normalizedConfig = fwo_const.emptyNormalizedFwConfigJsonDict
    logger.debug ( "starting checkpointR8x/get_config" )

    if nativeConfig == {}:   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())

        if 'users' not in nativeConfig:
            nativeConfig.update({'users': {}})

        domain, cpManagerApiBaseUrl = prepare_get_vars(importState.FullMgmDetails)

        sid = login_cp(importState.FullMgmDetails, domain)

        starttimeTemp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting objects ...")

        result_get_objects = get_objects (nativeConfig, importState.FullMgmDetails, cpManagerApiBaseUrl, sid, force=importState.ForceImport, limit=str(importState.FwoConfig.ApiFetchSize), details_level=cp_const.details_level_objects, test_version='off')
        if result_get_objects>0:
            logger.warning ( "checkpointR8x/get_config/error while gettings objects")
            return result_get_objects
        logger.debug ( "checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - starttimeTemp) + "s")

        starttimeTemp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting rules ...")
        result_get_rules = getRules (nativeConfig, importState)
        if result_get_rules>0:
            logger.warning ( "checkpointR8x/get_config/error while gettings rules")
            return result_get_rules
        logger.debug ( "checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - starttimeTemp) + "s")

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - fetch duration: " + str(duration) + "s" )

    cp_network.normalize_network_objects(nativeConfig, normalizedConfig, importState.ImportId, mgm_id=importState.MgmDetails.Id)
    logger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(nativeConfig, normalizedConfig, importState.ImportId)
    logger.info("completed normalizing service objects")

    # TODO: re-add user import
    # parse_users_from_rulebases(full_config, full_config['rulebases'], full_config['users'], config2import, current_import_id)
    # parseUsersFromRulebases(nativeConfig, normalizedConfig, importState.ImportId)

    if importState.ImportVersion>8:
        cp_rule.normalizeRulebases(nativeConfig, importState, normalizedConfig)
        cp_gateway.normalizeGateways(nativeConfig, importState, normalizedConfig)
    else:
        normalizedConfig.update({'rules':  cp_rule.normalize_rulebases_top_level(nativeConfig, importState.ImportId, normalizedConfig) })
    if not parsing_config_only: # get config from cp fw mgr
        logout_cp("https://" + importState.MgmDetails.Hostname + ":" + str(importState.FullMgmDetails['port']) + "/web_api/", sid)
    logger.info("completed normalizing rulebases")
    
    # put dicts into object of class FwConfigManager
    normalizedConfig2 = FwConfigNormalized(action=ConfigAction.INSERT, 
                            network_objects=FwConfigNormalizedController.convertListToDict(normalizedConfig['network_objects'], 'obj_uid'),
                            service_objects=FwConfigNormalizedController.convertListToDict(normalizedConfig['service_objects'], 'svc_uid'),
                            users=normalizedConfig['users'],
                            zone_objects=normalizedConfig['zone_objects'],
                            # decide between old (rules) and new (policies) format
                            # rules=normalizedConfig['rules'] if len(normalizedConfig['rules'])>0 else normalizedConfig['policies'],    
                            rulebases=normalizedConfig['policies'],
                            gateways=normalizedConfig['gateways']
                            )
    manager = FwConfigManager(ManagerUid=calcManagerUidHash(importState.FullMgmDetails),
                              ManagerName=importState.MgmDetails.Name,
                              IsGlobal=False, 
                              DependantManagerUids=[], 
                              Configs=[normalizedConfig2])
    listOfManagers = FwConfigManagerListController()

    listOfManagers.addManager(manager)
    logger.info("completed getting config")
    
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
        logger = getFwoLogger()
        logger.warning("logout from CP management failed")


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
    

def get_objects(config_json, mgm_details, v_url, sid, force=False, config_filename=None,
    limit=150, details_level=cp_const.details_level_objects, test_version='off', debug_level=0, ssl_verification=True):

    logger = getFwoLogger()

    config_json["object_tables"] = []
    show_params_objs = {'limit':limit,'details-level': details_level }

    # getting Original (NAT) object (both for networks and services)
    origObj = cp_getter.getObjectDetailsFromApi(cp_const.original_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['object_chunks'][0]
    anyObj = cp_getter.getObjectDetailsFromApi(cp_const.any_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['object_chunks'][0]
    noneObj = cp_getter.getObjectDetailsFromApi(cp_const.none_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['object_chunks'][0]
    internetObj = cp_getter.getObjectDetailsFromApi(cp_const.internet_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['object_chunks'][0]

    for obj_type in cp_const.api_obj_types:
        if obj_type in cp_const.obj_types_full_fetch_needed:
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

        # adding the uid of the Original, Any and None objects (as separate chunks):
        if obj_type == 'networks':
            object_table['object_chunks'].append(origObj)
            object_table['object_chunks'].append(anyObj)
            object_table['object_chunks'].append(noneObj)
            object_table['object_chunks'].append(internetObj)
        if obj_type == 'services-other':
            object_table['object_chunks'].append(origObj)
            object_table['object_chunks'].append(anyObj)
            object_table['object_chunks'].append(noneObj)

        config_json["object_tables"].append(object_table)

    # only write config to file if config_filename is given
    if config_filename != None and len(config_filename)>1:
        with open(config_filename, "w") as configfile_json:
            configfile_json.write(json.dumps(config_json))
    return 0


# def parse_users_from_rulebases (full_config, rulebase, users, config2import, current_import_id):
#     if 'users' not in full_config:
#         full_config.update({'users': {}})

#     rb_range = range(len(full_config['rulebases']))
#     for rb_id in rb_range:
#         parse_user_objects_from_rulebase (full_config['rulebases'][rb_id], full_config['users'], current_import_id)

#     # copy users from full_config to config2import
#     # also converting users from dict to array:
#     config2import.update({'user_objects': []})
#     for user_name in full_config['users'].keys():
#         user = copy.deepcopy(full_config['users'][user_name])
#         user.update({'user_name': user_name})
#         config2import['user_objects'].append(user)

def parseUsersFromRulebases(nativeConfig, normalizedConfig, current_import_id):
        """
        Extracts user objects from rulebases and normalizes them.

        This function ensures that the `users` key exists in `nativeConfig`, 
        extracts user objects from all rulebases, and stores them in `normalizedConfig`.

        Args:
            nativeConfig (dict): The raw configuration containing rulebases.
            normalizedConfig (dict): The dictionary where normalized user objects will be stored.
            current_import_id (int): The ID of the current import process.

        Returns:
            None: The function modifies `nativeConfig` and `normalizedConfig` in place.
        """
        # initialize nativeConfig.users if it does not exist
        if 'users' not in nativeConfig:
            nativeConfig.update({'users': {}})

        # parse user objects
        rb_range = range(len(nativeConfig['rulebases']))
        for rb_id in rb_range:
            parse_user_objects_from_rulebase (nativeConfig['rulebases'][rb_id], nativeConfig['users'], current_import_id)

        # copy user objects to normalized config
        normalizedConfig.update({'user_objects': []})
        for user_name in nativeConfig['users'].keys():
            user = copy.deepcopy(nativeConfig['users'][user_name])
            user.update({'user_name': user_name})
            normalizedConfig['user_objects'].append(user)

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
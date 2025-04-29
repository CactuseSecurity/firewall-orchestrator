import json
from fwo_log import getFwoLogger
import time

import cp_rule
import cp_const, cp_network, cp_service
import cp_getter
import cp_gateway
import fwo_exceptions
from fwconfig_base import calcManagerUidHash
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction
import fwo_const
import fwo_globals
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from fwo_exceptions import ImportInterruption


def has_config_changed (full_config, importState, force=False):

    if full_config != {}:   # a config was passed in (read from file), so we assume that an import has to be done (simulating changes here)
        return 1

    domain, _ = prepare_get_vars(importState.FullMgmDetails)
    session_id = login_cp(importState.FullMgmDetails, domain)
    last_change_time = ''

    if importState.LastSuccessfulImport==None or importState.LastSuccessfulImport=='' or force:
        # if no last import time found or given or if force flag is set, do full import
        result = 1
    else: # otherwise search for any changes since last import
        result = (cp_getter.get_changes(session_id, importState.FullMgmDetails['hostname'], str(importState.FullMgmDetails['port']),importState.LastSuccessfulImport) != 0)

    logout_cp("https://" + importState.FullMgmDetails['hostname'] + ":" + str(importState.FullMgmDetails['port']) + "/web_api/", session_id)

    return result


def get_config(nativeConfig: json, importState: ImportStateController) -> tuple[int, FwConfigManagerList]:

    logger = getFwoLogger()
    normalizedConfigDict = fwo_const.emptyNormalizedFwConfigJsonDict
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

    normalizedConfig = normalizeConfig(nativeConfig, normalizedConfigDict, importState, parsing_config_only, sid)

    manager = FwConfigManager(ManagerUid=calcManagerUidHash(importState.FullMgmDetails),
                              ManagerName=importState.MgmDetails.Name,
                              IsGlobal=False, 
                              DependantManagerUids=[], 
                              Configs=[normalizedConfig])
    
    listOfManagers = FwConfigManagerListController()
    listOfManagers.addManager(manager)
    logger.info("completed getting config")
    
    return 0, listOfManagers

def normalizeConfig(nativeConfig: json, normalizedConfigDict, importState: ImportStateController, parsing_config_only: bool, sid: str) -> tuple[int, FwConfigManagerList]:
    logger = getFwoLogger()
    cp_network.normalize_network_objects(nativeConfig, normalizedConfigDict, importState.ImportId, mgm_id=importState.MgmDetails.Id)
    logger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(nativeConfig, normalizedConfigDict, importState.ImportId)
    logger.info("completed normalizing service objects")
    cp_rule.normalizeRulebases(nativeConfig, importState, normalizedConfigDict)
    cp_gateway.normalizeGateways(nativeConfig, importState, normalizedConfigDict)
    if not parsing_config_only: # get config from cp fw mgr
        logout_cp("https://" + importState.MgmDetails.Hostname + ":" + str(importState.FullMgmDetails['port']) + "/web_api/", sid)
    logger.info("completed normalizing rulebases")
    
    # put dicts into object of class FwConfigManager
    return FwConfigNormalized(
        action=ConfigAction.INSERT, 
        network_objects=FwConfigNormalizedController.convertListToDict(normalizedConfigDict['network_objects'], 'obj_uid'),
        service_objects=FwConfigNormalizedController.convertListToDict(normalizedConfigDict['service_objects'], 'svc_uid'),
        zone_objects=normalizedConfigDict['zone_objects'],
        rulebases=normalizedConfigDict['policies'],
        gateways=normalizedConfigDict['gateways']
    )

def getRules (nativeConfig: dict, importState: ImportStateController) -> int:
    '''
    Implicit premises for mds:
    - all devices are attached to a submanager, not to the super manager
    - global rulebase is the same for every device
    - only db rulebase stored per device is its local rulebase
    '''
    # delete_v: Noch offen Todo
    # 1. ich mache nirgends logout
    # 2. NAT noch nicht getestet

    logger = getFwoLogger()
    nativeConfig.update({'rulebases': [], 'nat_rulebases': [], 'gateways': [] })
    show_params_policy_structure = {
        'limit': importState.FwoConfig.ApiFetchSize,
        'details-level': 'full'
    }

    # control standalone vs mds
    managerDetailsList = [importState.MgmDetails]
    if importState.MgmDetails.IsSuperManager:
        topLevelMgmDetails = importState.MgmDetails
        for subManager in importState.MgmDetails.SubManagers:
            managerDetailsList.append(subManager)
            
    # loop over toplevel- and sub-managers in case of mds
    for managerDetails in managerDetailsList:

        # delete_v: kann prepare_get_vars gelöscht werden? Nein noch nicht
        domain, cpManagerApiBaseUrl = prepareGetVars(managerDetails)

        # in case of mds get global assignments via mds sid and then change to global domain and sid for all further operations
        if managerDetails.IsSuperManager and managerDetails.Uid == topLevelMgmDetails.Uid:
            mdsSid = loginCp(managerDetails, domain)
            globalAssignments = []
            cp_getter.getGlobalAssignments(cpManagerApiBaseUrl,
                                           mdsSid,
                                           show_params_policy_structure,
                                           globalAssignments = globalAssignments)

            # delete_v: man könnte Global domain uid aus globalAssignments[0]['global-domain]['uid']
            # delete_v: auslesen. Wenn es kein einziges Assignment gibt, könnte man global wahrscheinlich weglassen
            domain = '1e294ce0-367a-11e3-aa6e-0800200c9a66' # delete_v: muss Global uid sein

        # delete_v: kann login_cp weg? Nein noch nicht
        sid = loginCp(managerDetails, domain)
        
        # get all access (ordered) layers for each policy
        policyStructure = []
        cp_getter.getPolicyStructure(cpManagerApiBaseUrl,
                                    sid,
                                    show_params_policy_structure,
                                    policyStructure = policyStructure)

        # store toplevel domain, api-url, sid and policy structure, we need them in the submanager iterations
        if managerDetails.IsSuperManager and managerDetails.Uid == topLevelMgmDetails.Uid:
            globalDomain = domain
            globalApiUrl = cpManagerApiBaseUrl
            globalSid = sid
            globalPolicyStructure = policyStructure

        show_params_rules = {
            'limit': importState.FwoConfig.ApiFetchSize,
            'use-object-dictionary': cp_const.use_object_dictionary,
            'details-level': 'standard',
            'show-hits': cp_const.with_hits 
        }

        # read all rulebases: handle per device details
        for device in managerDetails.Devices:

            # initialize device config
            if 'name' and 'uid' in device:
                deviceConfig = {'name': device['name'],
                                'uid': device['uid'],
                                'rulebase_links': []}

            else:
                logger.error ( "found device without name or uid: " + str(device) )
                return 1

            # get ordered layer uids for current device
            orderedLayerUids = getOrderedLayerUids(policyStructure, deviceConfig, domain)
            if len(orderedLayerUids) == 0:
                logger.warning ( "found no ordered layers for device: " + deviceConfig['name'] )
                continue

            # decide if architecture is mds or stand alone manager
            if importState.MgmDetails.IsSuperManager:

                # get global policy from globalPolicyStructure via globalAssignments
                for globalAssignment in globalAssignments:
                    if globalAssignment['dependent-domain']['uid'] == domain:
                        for globalPolicy in globalPolicyStructure:
                            if globalPolicy['name'] == globalAssignment['global-access-policy']:

                                # get ordered layers for global policy
                                globalOrderedLayerUids = getOrderedLayerUids([globalPolicy], deviceConfig, globalDomain)
                                if len(globalOrderedLayerUids) == 0:
                                    logger.warning ( "No access layer for global policy: " +  globalPolicy['name'])
                                    break
                                logger.debug ( "getting global rule layers" )
                                addOrderedLayersToNativeConfig(globalOrderedLayerUids, show_params_rules, globalApiUrl, globalSid, nativeConfig, deviceConfig)

                                # define initial rulebase for device in case of mds
                                deviceConfig['rulebase_links'].append({
                                    'from_rulebase_uid': '',
                                    'from_rule_uid': '',
                                    'to_rulebase_uid': globalOrderedLayerUids[0],
                                    'type': 'initial'})
                
                                # parse global rulebase, find place-holder and link local rulebase (first ordered layer)
                                for globalOrderedLayerUid in globalOrderedLayerUids:
                                    placeholderRuleUid = ''
                                    for rulebase in nativeConfig['rulebases']:
                                        if rulebase['uid'] == globalOrderedLayerUid:
                                            placeholderRuleUid = cp_getter.getRuleUid(rulebase, 'place-holder')
                                            if placeholderRuleUid != '':
                                                break

                                    if placeholderRuleUid != '':
                                        deviceConfig['rulebase_links'].append({
                                            'from_rulebase_uid': globalOrderedLayerUid,
                                            'from_rule_uid': placeholderRuleUid,
                                            'to_rulebase_uid': orderedLayerUids[0],
                                            'type': 'local'})
                                        
                # define initial rulebase for device in case of mds without global rulebase
                if deviceConfig['rulebase_links'] == []:
                    logger.info ( "No global rulebases for device : " +  deviceConfig['name'])
                    deviceConfig['rulebase_links'].append({
                        'from_rulebase_uid': '',
                        'from_rule_uid': '',
                        'to_rulebase_uid': orderedLayerUids[0],
                        'type': 'initial'})

            else:
                # define initial rulebase for device in case of stand alone manager
                deviceConfig['rulebase_links'].append({
                    'from_rulebase_uid': '',
                    'from_rule_uid': '',
                    'to_rulebase_uid': orderedLayerUids[0],
                    'type': 'initial'})

            # get local rulebases (ordered layers)
            logger.debug ( "getting domain rule layers" )
            addOrderedLayersToNativeConfig(orderedLayerUids, show_params_rules, cpManagerApiBaseUrl, sid, nativeConfig, deviceConfig)

            # getting NAT rules - need package name for nat rule retrieval
            # todo: each gateway/layer should have its own package name (pass management details instead of single data?)
            if 'package_name' in device and device['package_name'] != None and device['package_name'] != '':
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


def addOrderedLayersToNativeConfig(orderedLayerUids, show_params_rules, cpManagerApiBaseUrl, sid, nativeConfig, deviceConfig):

    orderedLayerIndex = 0
    for orderedLayerUid in orderedLayerUids:

        show_params_rules.update({'uid': orderedLayerUid})

        cp_getter.getRulebases (cpManagerApiBaseUrl, 
                                sid, 
                                show_params_rules, 
                                rulebaseUid=orderedLayerUid,
                                access_type='access',
                                nativeConfig=nativeConfig,
                                deviceConfig=deviceConfig)
        if fwo_globals.shutdown_requested:
            raise ImportInterruption("Shutdown requested during rulebase retrieval.")
                    
        lastRuleUid = None
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

    return 0


def getOrderedLayerUids(policyStructure, deviceConfig, domain):

    orderedLayerUids = []
    for policy in policyStructure:
        foundTargetInPolciy = False
        for target in policy['targets']:
            if target['uid'] == deviceConfig['uid'] or target['uid'] == 'all':
                foundTargetInPolciy = True
        if foundTargetInPolciy:
            for accessLayer in policy['access-layers']:
                if accessLayer['domain'] == domain:
                    orderedLayerUids.append(accessLayer['uid'])

    return orderedLayerUids


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

def prepareGetVars(mgm_details):
    # from 5.8 onwards: preferably use domain uid instead of domain name due to CP R81 bug with certain installations
    if mgm_details.DomainUid != None:
        domain = mgm_details.DomainUid
    else:
        domain = mgm_details.DomainName
    api_host = mgm_details.Hostname
    api_port = str(mgm_details.Port)
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'

    return domain, base_url

def loginCp(mgm_details, domain, ssl_verification=True):
    try: # top level dict start, sid contains the domain information, so only sending domain during login
        login_result = cp_getter.login(mgm_details.ImportUser, mgm_details.Secret, mgm_details.Hostname, str(mgm_details.Port), domain)
        return login_result
    except Exception:
        raise FwLoginFailed

def login_cp(mgm_details, domain, ssl_verification=True):
    try: # top level dict start, sid contains the domain information, so only sending domain during login
        login_result = cp_getter.login(mgm_details['import_credential']['user'], mgm_details['import_credential']['secret'], mgm_details['hostname'], str(mgm_details['port']), domain)
        return login_result
    except Exception:
        raise fwo_exceptions.FwLoginFailed


def logout_cp(url, sid):
    try:
        logout_result = cp_getter.logout(url, sid)
        return logout_result
    except Exception:
        logger = getFwoLogger()
        logger.warning("logout from CP management failed")


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
        if fwo_globals.shutdown_requested:
            raise ImportInterruption("Shutdown requested during object retrieval.")
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
            if fwo_globals.shutdown_requested:
                raise ImportInterruption("Shutdown requested during object retrieval.")

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

# delete_v soll das weg, wird bisher nirgends benutzt
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

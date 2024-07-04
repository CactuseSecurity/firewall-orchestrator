import sys
from common import importer_base_dir
from fwo_log import getFwoLogger
sys.path.append(importer_base_dir + '/checkpointR8x')
import time
import cp_getter
import fwo_globals
import cp_const
import cp_network


################# enrich #######################
def enrich_config (config, mgm_details, limit=150, details_level=cp_const.details_level, noapi=False, sid=None):

    logger = getFwoLogger()
    base_url = 'https://' + mgm_details['hostname'] + ':' + str(mgm_details['port']) + '/web_api/'
    nw_objs_from_obj_tables = []
    svc_objs_from_obj_tables = []
    starttime = int(time.time())

    # do nothing for empty configs
    if config == {}:
        return 0

    #################################################################################
    # get object data which is only contained as uid in config by making additional api calls
    # get all object uids (together with type) from all rules in fields src, dst, svc
    nw_uids_from_rulebase = []
    svc_uids_from_rulebase = []

    for rulebase in config['rulebases'] + config['nat_rulebases']:
        if fwo_globals.debug_level>5:
            if 'layername' in rulebase:
                logger.debug ( "Searching for all uids in rulebase: " + rulebase['layername'] )
        cp_getter.collect_uids_from_rulebase(rulebase, nw_uids_from_rulebase, svc_uids_from_rulebase, "top_level")

    # remove duplicates from uid lists
    nw_uids_from_rulebase = list(set(nw_uids_from_rulebase))
    svc_uids_from_rulebase = list(set(svc_uids_from_rulebase))

    # get all uids in objects tables
    for obj_table in config['object_tables']:
        nw_objs_from_obj_tables.extend(cp_getter.get_all_uids_of_a_type(obj_table, cp_const.nw_obj_table_names))
        svc_objs_from_obj_tables.extend(cp_getter.get_all_uids_of_a_type(obj_table, cp_const.svc_obj_table_names))

    # identify all objects (by type) that are missing in objects tables but present in rulebase
    missing_nw_object_uids  = cp_getter.get_broken_object_uids(nw_objs_from_obj_tables, nw_uids_from_rulebase)
    missing_svc_object_uids = cp_getter.get_broken_object_uids(svc_objs_from_obj_tables, svc_uids_from_rulebase)

    # adding the uid of the Original object for natting:
    missing_nw_object_uids.append(cp_const.original_obj_uid)
    missing_svc_object_uids.append(cp_const.original_obj_uid)

    if fwo_globals.debug_level>4:
        logger.debug ( "found missing nw objects: '" + ",".join(missing_nw_object_uids) + "'" )
        logger.debug ( "found missing svc objects: '" + ",".join(missing_svc_object_uids) + "'" )

    if noapi == False:
        # if an object is not there:
        #   make api call: show object details-level full uid "<uid>" and add object to respective json
        for missing_obj in missing_nw_object_uids:
            show_params_host = {'details-level':cp_const.details_level,'uid':missing_obj}
            logger.debug ( "fetching obj with uid: " + missing_obj)
            obj = cp_getter.cp_api_call(base_url, 'show-object', show_params_host, sid)
            if 'object' in obj:
                obj = obj['object']
                if (obj['type'] == 'CpmiAnyObject'):
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': 'any nw object checkpoint (hard coded)',
                        'type': 'network', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
                    )
                elif (obj['type'] in [ 'simple-gateway', obj['type'], 'CpmiGatewayPlain', obj['type'] == 'interop' ]):
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                        } ] } ] }
                    )
                elif obj['type'] == 'multicast-address-range':
                    logger.debug("found multicast-address-range: " + obj['name'] + " (uid:" + obj['uid']+ ")")
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                        } ] } ] }
                    )
                elif (obj['type'] in ['CpmiVsClusterMember', 'CpmiVsxClusterMember', 'CpmiVsxNetobj']):
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': cp_network.get_ip_of_obj(obj),
                        } ] } ] }
                    )
                elif (obj['type'] == 'Global'):
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
                    )
                elif (obj['type'] in [ 'updatable-object', 'CpmiVoipSipDomain' ]):
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'group' #, 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
                    )
                    logger.debug ('missing obj: ' + obj['name'] + obj['type'])
                elif (obj['type'] == 'Internet'):
                    config['object_tables'].append(
                        {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'network', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
                    )
                elif (obj['type'] == 'access-role'):
                    pass # ignorning user objects
                else:
                    logger.warning ( "missing nw obj of unexpected type '" + obj['type'] + "': " + missing_obj )
                logger.debug ( "missing nw obj: " + missing_obj + " added" )
            else:
                logger.warning("could not get the missing object with uid=" + missing_obj + " from CP API")

        for missing_obj in missing_svc_object_uids:
            show_params_host = {'details-level':cp_const.details_level,'uid':missing_obj}
            obj = cp_getter.cp_api_call(base_url, 'show-object', show_params_host, sid)
            if 'object' in obj:
                obj = obj['object']
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
                    logger.warning ( "missing svc obj (uid=" + missing_obj + ") of unexpected type \"" + obj['type'] +"\"" )
                logger.debug ( "missing svc obj: " + missing_obj + " added")

        # logout_result = cp_getter.cp_api_call(base_url, 'logout', {}, sid)
    
    logger.debug ( "checkpointR8x/enrich_config - duration: " + str(int(time.time()) - starttime) + "s" )

    return 0

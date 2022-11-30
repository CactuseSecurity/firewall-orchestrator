from distutils.log import debug
import sys
from common import importer_base_dir
from fwo_log import getFwoLogger
sys.path.append(importer_base_dir + '/checkpointR8x')
import copy, time
import parse_network, parse_rule, parse_service, parse_user
import getter
from cpcommon import get_basic_config, enrich_config
import fwo_globals
from fwo_exception import FwLoginFailed
from cpcommon import details_level


def has_config_changed (full_config, mgm_details, force=False):

    if full_config != {}:   # a native config was passed in, so we assume that an import has to be done (simulating changes here)
        return 1
        # from 5.8 onwards: preferably use domain uid instead of domain name due to CP R81 bug with certain installations
    if mgm_details['domainUid'] != None:
        domain = mgm_details['domainUid']
    else:
        domain = mgm_details['configPath']

    try: # top level dict start, sid contains the domain information, so only sending domain during login
        session_id = getter.login(mgm_details['import_credential']['user'], mgm_details['import_credential']['secret'], mgm_details['hostname'], str(mgm_details['port']), domain)
    except:
        raise FwLoginFailed     # maybe 2Temporary failure in name resolution"

    last_change_time = ''
    if 'import_controls' in mgm_details:
        for importctl in mgm_details['import_controls']: 
            if 'starttime' in importctl:
                last_change_time = importctl['starttime']

    if last_change_time==None or last_change_time=='' or force:
        # if no last import time found or given or if force flag is set, do full import
        return 1
    else:
        # otherwise search for any changes since last import
        return (getter.get_changes(session_id, mgm_details['hostname'], str(mgm_details['port']),last_change_time) != 0)


def get_config(config2import, full_config, current_import_id, mgm_details, limit=150, force=False, jwt=None):
    logger = getFwoLogger()
    if full_config == {}:   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())

        # from 5.8 onwards: preferably use domain uid instead of domain name due to CP R81 bug with certain installations
        if mgm_details['domainUid'] != None:
            domain = mgm_details['domainUid']
        else:
            domain = mgm_details['configPath']

        sid = getter.login(mgm_details['import_credential']['user'], mgm_details['import_credential']['secret'], mgm_details['hostname'], str(mgm_details['port']), domain)

        result_get_basic_config = get_basic_config (full_config, mgm_details, force=force, sid=sid, limit=str(limit), details_level=details_level, test_version='off')

        if result_get_basic_config>0:
            return result_get_basic_config

        result_enrich_config = enrich_config (full_config, mgm_details, limit=str(limit), details_level=details_level, sid=sid)

        if result_enrich_config>0:
            return result_enrich_config

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - duration: " + str(duration) + "s" )

    if full_config == {}: # no changes
        return 0
    else:
        parse_network.parse_network_objects_to_json(full_config, config2import, current_import_id, mgm_id=mgm_details['id'])
        parse_service.parse_service_objects_to_json(full_config, config2import, current_import_id)
        if 'users' not in full_config:
            full_config.update({'users': {}})
        target_rulebase = []
        rule_num = 0
        parent_uid=""
        section_header_uids=[]
        rb_range = range(len(full_config['rulebases']))
        for rb_id in rb_range:
            parse_user.parse_user_objects_from_rulebase(
                full_config['rulebases'][rb_id], full_config['users'], current_import_id)
            # if current_layer_name == args.rulebase:
            if fwo_globals.debug_level>3:
                logger.debug("parsing layer " + full_config['rulebases'][rb_id]['layername'])

            # parse access rules
            rule_num = parse_rule.parse_rulebase_json(
                full_config['rulebases'][rb_id], target_rulebase, full_config['rulebases'][rb_id]['layername'], 
                current_import_id, rule_num, section_header_uids, parent_uid, config2import)
            # now parse the nat rulebase

            # parse nat rules
            if len(full_config['nat_rulebases'])>0:
                if len(full_config['nat_rulebases']) != len(rb_range):
                    logger.warning('get_config - found ' + str(len(full_config['nat_rulebases'])) +
                        ' nat rulebases and ' +  str(len(rb_range)) + ' access rulebases')
                else:
                    rule_num = parse_rule.parse_nat_rulebase_json(
                        full_config['nat_rulebases'][rb_id], target_rulebase, full_config['rulebases'][rb_id]['layername'], 
                        current_import_id, rule_num, section_header_uids, parent_uid, config2import)
        config2import.update({'rules': target_rulebase})

        # copy users from full_config to config2import
        # also converting users from dict to array:
        config2import.update({'user_objects': []})
        for user_name in full_config['users'].keys():
            user = copy.deepcopy(full_config['users'][user_name])
            user.update({'user_name': user_name})
            config2import['user_objects'].append(user)
    return 0

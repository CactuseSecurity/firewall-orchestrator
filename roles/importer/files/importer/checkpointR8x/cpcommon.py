from distutils.log import debug
import sys
from common import importer_base_dir
from fwo_log import getFwoLogger
sys.path.append(importer_base_dir + '/checkpointR8x')
import json
import time
import getter
import fwo_alert, fwo_api
import ipaddress 
import fwo_globals

def validate_ip_address(address):
    try:
        # ipaddress.ip_address(address)
        ipaddress.ip_network(address)
        return True
        # print("IP address {} is valid. The object returned is {}".format(address, ip))
    except ValueError:
        return False
        # print("IP address {} is not valid".format(address)) 


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


def get_ip_of_obj(obj, mgm_id=None):
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
    else:
        ip_addr = None

    ## fix malformed ip addresses (should not regularly occur and constitutes a data issue in CP database)
    if ip_addr is None or ('type' in obj and (obj['type'] == 'address-range' or obj['type'] == 'multicast-address-range')):
        pass   # ignore None and ranges here
    elif not validate_ip_address(ip_addr):
        alerter = fwo_alert.getFwoAlerter()
        alert_description = "object is not a valid ip address (" + str(ip_addr) + ")"
        fwo_api.create_data_issue(alerter['fwo_api_base_url'], alerter['jwt'], severity=2, obj_name=obj['name'], object_type=obj['type'], description=alert_description, mgm_id=mgm_id) 
        alert_description = "object '" + obj['name'] + "' (type=" + obj['type'] + ") is not a valid ip address (" + str(ip_addr) + ")"
        fwo_api.setAlert(alerter['fwo_api_base_url'], alerter['jwt'], title="import error", severity=2, role='importer', \
            description=alert_description, source='import', alertCode=17, mgm_id=mgm_id)
        ip_addr = '0.0.0.0/32'  # setting syntactically correct dummy ip
    return ip_addr

##################### 2nd-level functions ###################################

def get_basic_config (config_json, mgm_details, force=False, config_filename=None,
    limit=150, details_level='full', test_version='off', debug_level=0, ssl_verification=True, sid=None):
    logger = getFwoLogger()

    api_host = mgm_details['hostname']
    api_user =  mgm_details['import_credential']['user']
    api_domain = mgm_details['configPath']
    api_port = str(mgm_details['port'])
    api_password = mgm_details['import_credential']['secret']
    base_url = 'https://' + api_host + ':' + str(api_port) + '/web_api/'
    use_object_dictionary = 'false'

    # top level dict start, sid contains the domain information, so only sending domain during login
    if sid is None:  # if sid was not passed, login and get it
        sid = getter.login(api_user,api_password,api_host,api_port,api_domain,ssl_verification)
    v_url = getter.get_api_url (sid, api_host, api_port, api_user, base_url, limit, test_version, ssl_verification, debug_level=debug_level)

    config_json.update({'rulebases': [], 'nat_rulebases': [] })
    show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level}

    # read all rulebases: handle per device details
    for device in mgm_details['devices']:
        if device['global_rulebase_name'] != None and device['global_rulebase_name']!='':
            show_params_rules['name'] = device['global_rulebase_name']
            # get global layer rulebase
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            current_layer_json = getter.get_layer_from_api_as_dict (api_host, api_port, v_url, sid, show_params_rules, layername=device['global_rulebase_name'])
            if current_layer_json is None:
                return 1
            # now also get domain rules 
            show_params_rules['name'] = device['local_rulebase_name']
            current_layer_json['layername'] = device['local_rulebase_name']
            logger.debug ( "getting domain rule layer: " + show_params_rules['name'] )
            domain_rules = getter.get_layer_from_api_as_dict (api_host, api_port, v_url, sid, show_params_rules, layername=device['local_rulebase_name'])
            if current_layer_json is None:
                return 1

            # now handling possible reference to domain rules within global rules
            # if we find the reference, replace it with the domain rules
            if 'layerchunks' in current_layer_json:
                for chunk in current_layer_json["layerchunks"]:
                    for rule in chunk['rulebase']:
                        if "type" in rule and rule["type"] == "place-holder":
                            logger.debug ("found domain rules place-holder: " + str(rule) + "\n\n")
                            current_layer_json = getter.insert_layer_after_place_holder(current_layer_json, domain_rules, rule['uid'])
        else:   # no global rules, just get local ones
            show_params_rules['name'] = device['local_rulebase_name']
            logger.debug ( "getting layer: " + show_params_rules['name'] )
            current_layer_json = getter.get_layer_from_api_as_dict (api_host, api_port, v_url, sid, show_params_rules, layername=device['local_rulebase_name'])
            if current_layer_json is None:
                return 1

        config_json['rulebases'].append(current_layer_json)

        # getting NAT rules - need package name for nat rule retrieval
        # todo: each gateway/layer should have its own package name (pass management details instead of single data?)
        if device['package_name'] != None and device['package_name'] != '':
            show_params_rules = {'limit':limit,'use-object-dictionary':use_object_dictionary,'details-level':details_level, 'package': device['package_name'] }
            if debug_level>3:
                logger.debug ( "getting nat rules for package: " + device['package_name'] )
            nat_rules = getter.get_nat_rules_from_api_as_dict (api_host, api_port, v_url, sid, show_params_rules)
            if len(nat_rules)>0:
                config_json['nat_rulebases'].append(nat_rules)
            else:
                config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
        else: # always making sure we have an (even empty) nat rulebase per device 
            config_json['nat_rulebases'].append({ "nat_rule_chunks": [] })
    
    # leaving rules, moving on to objects
    config_json["object_tables"] = []
    show_params_objs = {'limit':limit,'details-level': details_level}

    for obj_type in getter.api_obj_types:
        object_table = { "object_type": obj_type, "object_chunks": [] }
        current=0
        total=current+1
        show_cmd = 'show-' + obj_type    
        if debug_level>5:
            logger.debug ( "obj_type: "+ obj_type )
        while (current<total) :
            show_params_objs['offset']=current
            objects = getter.cp_api_call(v_url, show_cmd, show_params_objs, sid)
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
    logout_result = getter.cp_api_call(v_url, 'logout', {}, sid)

    # only write config to file if config_filename is given
    if config_filename != None and len(config_filename)>1:
        with open(config_filename, "w") as configfile_json:
            configfile_json.write(json.dumps(config_json))
    return 0


################# enrich #######################
def enrich_config (config, mgm_details, limit=150, details_level='full', noapi=False, sid=None):

    logger = getFwoLogger()
    base_url = 'https://' + mgm_details['hostname'] + ':' + str(mgm_details['port']) + '/web_api/'
    nw_objs_from_obj_tables = []
    svc_objs_from_obj_tables = []
    starttime = int(time.time())

    # do nothing for empty configs
    if config == {}:
        return 0

    #################################################################################
    # adding inline and domain layers 
    found_new_inline_layers = True
    old_inline_layers = []
    while found_new_inline_layers is True:
        # sweep existing rules for inline layer links
        inline_layers = []
        for rulebase in config['rulebases'] + config['nat_rulebases']:
            getter.get_inline_layer_names_from_rulebase(rulebase, inline_layers)

        if len(inline_layers) == len(old_inline_layers):
            found_new_inline_layers = False
        else:
            old_inline_layers = inline_layers
            for layer in inline_layers:
                if fwo_globals.debug_level>5:
                    logger.debug ( "found inline layer " + layer )
                # enrich config --> get additional layers referenced in top level layers by name
                # also handle possible recursion (inline layer containing inline layer(s))
                # get layer rules from api
                # add layer rules to config

    # next phase: how to logically link layer guard with rules in layer? --> AND of src, dst & svc between layer guard and each rule in layer?

    #################################################################################
    # get object data which is only contained as uid in config by making additional api calls
    # get all object uids (together with type) from all rules in fields src, dst, svc
    nw_uids_from_rulebase = []
    svc_uids_from_rulebase = []

    for rulebase in config['rulebases'] + config['nat_rulebases']:
        if fwo_globals.debug_level>5:
            if 'layername' in rulebase:
                logger.debug ( "Searching for all uids in rulebase: " + rulebase['layername'] )
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

    if fwo_globals.debug_level>4:
        logger.debug ( "found missing nw objects: '" + ",".join(missing_nw_object_uids) + "'" )
        logger.debug ( "found missing svc objects: '" + ",".join(missing_svc_object_uids) + "'" )

    if noapi == False:
        # if sid is None:
        # TODO: why is the re-genereation of a new sid necessary here?
        sid = getter.login(mgm_details['import_credential']['user'],mgm_details['import_credential']['secret'],mgm_details['hostname'],mgm_details['port'],mgm_details['configPath'])
        logger.debug ( "re-logged into api" )

        # if an object is not there:
        #   make api call: show object details-level full uid "<uid>" and add object to respective json
        for missing_obj in missing_nw_object_uids:
            show_params_host = {'details-level':details_level,'uid':missing_obj}
            logger.debug ( "fetching obj with uid: " + missing_obj)
            obj = getter.cp_api_call(base_url, 'show-object', show_params_host, sid)
            if 'object' in obj:
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
                    logger.debug("found multicast-address-range: " + obj['name'] + " (uid:" + obj['uid']+ ")")
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
                    logger.debug ('missing obj: ' + obj['name'] + obj['type'])
                elif (obj['type'] == 'Global'):
                    json_obj = {"object_type": "hosts", "object_chunks": [ {
                        "objects": [ {
                        'uid': obj['uid'], 'name': obj['name'], 'color': obj['color'],
                        'comments': obj['comments'], 'type': 'host', 'ipv4-address': '0.0.0.0/0',
                        } ] } ] }
                    config['object_tables'].append(json_obj)
                    logger.debug ('missing obj: ' + obj['name'] + obj['type'])
                elif (obj['type'] == 'access-role'):
                    pass # ignorning user objects
                else:
                    logger.warning ( "missing nw obj of unexpected type '" + obj['type'] + "': " + missing_obj )
                logger.debug ( "missing nw obj: " + missing_obj + " added" )
            else:
                logger.warning("could not get the missing object with uid=" + missing_obj + " from CP API")

        for missing_obj in missing_svc_object_uids:
            show_params_host = {'details-level':details_level,'uid':missing_obj}
            obj = getter.cp_api_call(base_url, 'show-object', show_params_host, sid)
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
                logger.warning ( "missing svc obj of unexpected type: " + missing_obj )
                # print ("WARNING - enrich_config - missing svc obj of unexpected type: '" + obj['type'] + "': " + missing_obj)
            logger.debug ( "missing svc obj: " + missing_obj + " added")

        logout_result = getter.cp_api_call(base_url, 'logout', {}, sid)
    
    logger.debug ( "checkpointR8x/enrich_config - duration: " + str(int(time.time()) - starttime) + "s" )

    return 0

import sys
from common import importer_base_dir
sys.path.append(importer_base_dir + "/paloaltomanagement2023ff")
from palo_service import normalize_svcobjects
from palo_application import normalize_application_objects
from palo_rule import normalize_access_rules
from palo_network import normalize_nwobjects
from palo_zone import normalize_zones
from palo_getter import login, update_config_with_palofw_api_call
from fwo_log import getFwoLogger
from palo_base import api_version_str

def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, limit=1000, force=False, jwt=''):
    logger = getFwoLogger()
    if full_config == {}:   # no native config was passed in, so getting it from Azzure
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # no native config was passed in, so getting it from Palo Firewall
        apipwd = mgm_details["import_credential"]['secret']
        apiuser = mgm_details["import_credential"]['user']
        apihost = mgm_details["hostname"]

        vsys_objects   = ["/Network/Zones", "/Objects/Addresses", "/Objects/Services", "/Objects/AddressGroups", "/Objects/ServiceGroups"]
        predef_objects = ["/Objects/Applications"]
        rulebase_names = ["/Policies/SecurityRules", "/Policies/NATRules"]

        for obj_path in vsys_objects:
            full_config[obj_path] = []

        for obj_path in predef_objects:
            full_config[obj_path] = []
        
        # login
        key = login(apiuser, apipwd, apihost)
        if key == None or key == "":
            logger.error('Did not succeed in logging in to Palo API, no key returned.')
            return 1

        ## get objects:
        base_url =  "https://{apihost}/restapi/v{api_version_str}".format(apihost=apihost, api_version_str=api_version_str)

        vsys_name = "vsys1" # TODO - automate this hard-coded name
        location = "vsys"       # alternative: panorama-pushed

        for obj_path in vsys_objects:
            update_config_with_palofw_api_call(key, base_url, full_config, obj_path + "?location={location}&vsys={vsys_name}".format(location=location, vsys_name=vsys_name), obj_type=obj_path)

        for obj_path in predef_objects:
            update_config_with_palofw_api_call(key, base_url, full_config, obj_path + "?location={location}".format(location="predefined"), obj_type=obj_path)

        # users
        
        # get rules
        full_config.update({'devices': {}})
        for device in mgm_details["devices"]:
            dev_id = device['id']
            dev_name = device['local_rulebase_name']
            full_config['devices'].update({ dev_id: {} })

            for obj_path in rulebase_names:
                update_config_with_palofw_api_call(
                        key, base_url, full_config['devices'][device['id']], 
                        obj_path + "?location={location}&vsys={vsys_name}".format(location="vsys", vsys_name=dev_name),
                        obj_type=obj_path)

    ##################
    # now we normalize relevant parts of the raw config and write the results to config2import dict

    normalize_nwobjects(full_config, config2import, current_import_id, jwt=jwt, mgm_id=mgm_details['id'])
    normalize_svcobjects(full_config, config2import, current_import_id)
    normalize_application_objects(full_config, config2import, current_import_id)
    # normalize_users(full_config, config2import, current_import_id, user_scope)

    # adding default any and predefined objects
    any_nw_svc = {"svc_uid": "any_svc_placeholder", "svc_name": "any", "svc_comment": "Placeholder service.", 
        "svc_typ": "simple", "ip_proto": -1, "svc_port": 0, "svc_port_end": 65535, "control_id": current_import_id}
    http_svc = {"svc_uid": "http_predefined_svc", "svc_name": "service-http", "svc_comment": "Predefined service", 
        "svc_typ": "simple", "ip_proto": 6, "svc_port": 80, "control_id": current_import_id}
    https_svc = {"svc_uid": "https_predefined_svc", "svc_name": "service-https", "svc_comment": "Predefined service", 
        "svc_typ": "simple", "ip_proto": 6, "svc_port": 443, "control_id": current_import_id}

    config2import["service_objects"].append(any_nw_svc)
    config2import["service_objects"].append(http_svc)
    config2import["service_objects"].append(https_svc)

    any_nw_object = {"obj_uid": "any_obj_placeholder", "obj_name": "any", "obj_comment": "Placeholder object.",
        "obj_typ": "network", "obj_ip": "0.0.0.0/0", "control_id": current_import_id}
    config2import["network_objects"].append(any_nw_object)

    normalize_zones(full_config, config2import, current_import_id)
    normalize_access_rules(full_config, config2import, current_import_id, mgm_details=mgm_details)
    # normalize_nat_rules(full_config, config2import, current_import_id, jwt=jwt)

    return 0

import sys
from common import importer_base_dir
sys.path.append(importer_base_dir + '/azure2022ff')
from azure_service import normalize_svcobjects
from azure_rule import normalize_access_rules
from azure_network import normalize_nwobjects
from azure_getter import login, update_config_with_azure_api_call
from fwo_log import getFwoLogger
from azure_base import azure_api_version_str

def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, limit=1000, force=False, jwt=''):
    logger = getFwoLogger()
    if full_config == {}:   # no native config was passed in, so getting it from Azzure
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # no native config was passed in, so getting it from Azure
        azure_client_id = mgm_details["import_credential"]['cloudClientId']
        azure_client_secret = mgm_details["import_credential"]['cloudClientSecret']
        azure_password = mgm_details["import_credential"]['secret']
        azure_user = mgm_details["import_credential"]['user']
        azure_tenant_id = mgm_details['cloudTenantId']
        azure_subscription_id = mgm_details['cloudSubscriptionId']
        azure_resource_group = mgm_details['configPath']
        azure_api_worker_base_url = 'https://management.azure.com/subscriptions/{subscription_id}/'.format(subscription_id=azure_subscription_id)

        full_config["networkObjects"] = []
        full_config["networkObjectGroups"] = []

        full_config["serviceObjects"] = []
        full_config["serviceObjectGroups"] = []

        full_config["userObjects"] = []
        full_config["userObjectGroups"] = []
        
        # login
        azure_jwt = login(azure_user, azure_password, azure_tenant_id, azure_client_id, azure_client_secret)
        if azure_jwt == None or azure_jwt == "":
            logger.error('Did not succeed in logging in to Azure API, no jwt returned.')
            return 1

        # get objects:
        # network objects
        # network groups
        api_path = 'resourceGroups/{resourceGroupName}/providers/Microsoft.Network/ipGroups{azure_api_version_str}'.format(
                resourceGroupName=azure_resource_group, azure_api_version_str=azure_api_version_str)
        update_config_with_azure_api_call(azure_jwt, azure_api_worker_base_url, full_config,
            api_path, "networkObjectGroups")

        # network services
        # network serivce groups

        # users
        
        # get rules
        full_config.update({'devices': {}})
        for device in mgm_details["devices"]:
            azure_policy_name = device['name']
            full_config['devices'].update({ device['name']: {} })

            api_path = 'resourceGroups/{resourceGroupName}/providers/Microsoft.Network/firewallPolicies/{firewallPolicyName}/ruleCollectionGroups{azure_api_version_str}'.format(
                resourceGroupName=azure_resource_group, firewallPolicyName=azure_policy_name, azure_api_version_str=azure_api_version_str)

            update_config_with_azure_api_call(azure_jwt, azure_api_worker_base_url, full_config['devices'][device['name']], api_path, "rules")
            ##azure_rule.getNatPolicy(sessionId, azure_api_worker_base_url, full_config, domain, device, limit) TODO

        # extract objects from rules
        for device in mgm_details["devices"]:
            azure_policy_name = device['name']
            for policy_name in full_config['devices'].keys():
                extract_nw_objects(policy_name, full_config)
                extract_svc_objects(policy_name, full_config)
                extract_user_objects(policy_name, full_config)

    # now we normalize relevant parts of the raw config and write the results to config2import dict

    # normalize_network_data(full_config, config2import, mgm_details)

    # azure_user.normalize_users(
    #     full_config, config2import, current_import_id, user_scope)
    normalize_nwobjects(full_config, config2import, current_import_id, jwt=jwt, mgm_id=mgm_details['id'])
    normalize_svcobjects(full_config, config2import, current_import_id)

    any_nw_svc = {"svc_uid": "any_svc_placeholder", "svc_name": "Any", "svc_comment": "Placeholder service.", 
    "svc_typ": "simple", "ip_proto": -1, "svc_port": 0, "svc_port_end": 65535, "control_id": current_import_id}
    any_nw_object = {"obj_uid": "any_obj_placeholder", "obj_name": "Any", "obj_comment": "Placeholder object.",
    "obj_typ": "network", "obj_ip": "0.0.0.0/0", "control_id": current_import_id}
    config2import["service_objects"].append(any_nw_svc)
    config2import["network_objects"].append(any_nw_object)

    normalize_access_rules(full_config, config2import, current_import_id, mgm_details=mgm_details)
    # azure_rule.normalize_nat_rules(
    #     full_config, config2import, current_import_id, jwt=jwt)
    # azure_network.remove_nat_ip_entries(config2import)
    return 0


def extract_nw_objects(rule, config):
    pass


def extract_svc_objects(rule, config):
    pass


def extract_user_objects(rule, config):
    pass


# def getDevices(azure_jwt, api_url, config, limit, devices):
# https://management.azure.com/subscriptions/{{ _.subscriptionId }}/providers/Microsoft.Network/applicationGateways?api-version=2022-05-01

# but this does not return firewalls!

#     logger = getFwoLogger()
#     # get all devices
#     config["devices"] = update_config_with_azure_api_call(azure_jwt, api_url, "fmc_config/v1/domain/" + "/devices/devicerecords", parameters={"expanded": True}, limit=limit)
#     # filter for existent devices
#     for cisco_api_device in config["devices"]:
#         found = False
#         for device in devices:
#             if device["name"] == cisco_api_device["name"] or device["name"] == cisco_api_device["id"]:
#                 found = True
#                 break
#         # remove device if not in fwo api
#         if found == False:
#             config["devices"].remove(cisco_api_device)
#             logger.info("Device \"" + cisco_api_device["name"] + "\" was found but it is not registered in FWO. Ignoring it.")

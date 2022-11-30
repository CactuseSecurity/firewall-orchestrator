
from cifp_service import parse_svc_group
from cifp_network import parse_obj_group
import cifp_getter
from fwo_log import getFwoLogger

rule_access_scope_v4 = ['rules_global_header_v4',
                        'rules_adom_v4', 'rules_global_footer_v4']
rule_access_scope_v6 = ['rules_global_header_v6',
                        'rules_adom_v6', 'rules_global_footer_v6']
rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
rule_scope = rule_access_scope + rule_nat_scope

def getAccessPolicy(sessionId, api_url, config, device, limit):
    access_policy = device["accessPolicy"]["id"]
    domain = device["domain"]
    logger = getFwoLogger()

    device["rules"] = cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
        "fmc_config/v1/domain/" + domain + "/policy/accesspolicies/" + access_policy + "/accessrules", parameters={"expanded": True}, limit=limit)

    return

def normalize_access_rules(full_config, config2import, import_id, mgm_details={}, jwt=None):
    any_nw_svc = {"svc_uid": "any_svc_placeholder", "svc_name": "Any", "svc_comment": "Placeholder service.", 
    "svc_typ": "simple", "ip_proto": -1, "svc_port": 0, "svc_port_end": 65535, "control_id": import_id}
    any_nw_object = {"obj_uid": "any_obj_placeholder", "obj_name": "Any", "obj_comment": "Placeholder object.",
    "obj_typ": "network", "obj_ip": "0.0.0.0/0", "control_id": import_id}
    config2import["service_objects"].append(any_nw_svc)
    config2import["network_objects"].append(any_nw_object)

    rules = []
    for device in full_config["devices"]:
        access_policy = device["accessPolicy"]
        rule_number = 0
        for rule_orig in device["rules"]:
            rule = {'rule_src': 'any', 'rule_dst': 'any', 'rule_svc': 'any',
            'rule_src_refs': 'any_obj_placeholder', 'rule_dst_refs': 'any_obj_placeholder',
            'rule_svc_refs': 'any_svc_placeholder'}
            rule['control_id'] = import_id
            rule['rulebase_name'] = access_policy["name"]
            rule["rule_uid"] = rule_orig["id"]
            rule["rule_name"] = rule_orig["name"]
            rule['rule_type'] = "access"
            rule['rule_num'] = rule_number
            rule['rule_installon'] = None
            rule['parent_rule_id'] = None
            rule['rule_time'] = None
            rule['rule_implied'] = False

            if 'description' in rule_orig:
                rule['rule_comment'] = rule_orig['description']
            else:
                rule["rule_comment"] = None
            if rule_orig["action"] == "ALLOW":
                rule["rule_action"] = "Accept"
            elif rule_orig["action"] == "BLOCK":
                rule["rule_action"] = "Drop"
            elif rule_orig["action"] == "TRUST":
                 rule["rule_action"] = "Accept" #TODO More specific?            
            elif rule_orig["action"] == "MONITOR":
                continue #TODO No access rule (just tracking and logging)
            if rule_orig["enableSyslog"]:
                rule["rule_track"] = "Log"
            else:
                rule["rule_track"] = "None"
            rule["rule_disabled"] = not rule_orig["enabled"]

            if "sourceNetworks" in rule_orig:
                rule['rule_src_refs'], rule["rule_src"] = parse_obj_group(rule_orig["sourceNetworks"], import_id, config2import['network_objects'], rule["rule_uid"])
            if "destinationNetworks" in rule_orig:
                rule['rule_dst_refs'], rule["rule_dst"] = parse_obj_group(rule_orig["destinationNetworks"], import_id, config2import['network_objects'], rule["rule_uid"])
            # TODO source ports
            if "destinationPorts" in rule_orig:
                rule["rule_svc_refs"], rule["rule_svc"] = parse_svc_group(rule_orig["destinationPorts"], import_id, config2import['service_objects'], rule["rule_uid"])

            rule["rule_src_neg"] = False
            rule["rule_dst_neg"] = False
            rule["rule_svc_neg"] = False

            rule_number += 1
            rules.append(rule)

    config2import['rules'] = rules

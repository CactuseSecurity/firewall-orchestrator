
from typing import Any
from cifp_service import parse_svc_group
from cifp_network import parse_obj_group
import cifp_getter

rule_access_scope_v4 = ['rules_global_header_v4',
                        'rules_adom_v4', 'rules_global_footer_v4']
rule_access_scope_v6 = ['rules_global_header_v6',
                        'rules_adom_v6', 'rules_global_footer_v6']
rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
rule_scope = rule_access_scope + rule_nat_scope

def get_access_policy(session_id: str, api_url: str, device: dict[str, Any]) -> None:
    access_policy = device["accessPolicy"]["id"]
    domain = device["domain"]

    device["rules"] = cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
        "fmc_config/v1/domain/" + domain + "/policy/accesspolicies/" + access_policy + "/accessrules", parameters={"expanded": True})

def create_placeholder_objects(config2import: dict[str, Any], import_id: str) -> None:
    """Create placeholder 'Any' objects for network and service references."""
    any_nw_svc: dict[str, Any] = {"svc_uid": "any_svc_placeholder", "svc_name": "Any", "svc_comment": "Placeholder service.", 
    "svc_typ": "simple", "ip_proto": -1, "svc_port": 0, "svc_port_end": 65535, "control_id": import_id}
    any_nw_object: dict[str, Any] = {"obj_uid": "any_obj_placeholder", "obj_name": "Any", "obj_comment": "Placeholder object.",
    "obj_typ": "network", "obj_ip": "0.0.0.0/0", "control_id": import_id}
    config2import["service_objects"].append(any_nw_svc)
    config2import["network_objects"].append(any_nw_object)

def create_base_rule(rule_orig: dict[str, Any], import_id: str, access_policy: dict[str, Any], rule_number: int) -> dict[str, Any]:
    """Create a base rule structure with default values."""
    rule: dict[str, Any] = {
        'rule_src': 'any', 'rule_dst': 'any', 'rule_svc': 'any',
        'rule_src_refs': 'any_obj_placeholder', 'rule_dst_refs': 'any_obj_placeholder',
        'rule_svc_refs': 'any_svc_placeholder'
    }
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
        
    return rule

def set_rule_action(rule: dict[str, Any], rule_orig: dict[str, Any]) -> bool:
    """Set the rule action based on the original rule action. Returns True if rule should be skipped."""
    if rule_orig["action"] == "ALLOW":
        rule["rule_action"] = "Accept"
    elif rule_orig["action"] == "BLOCK":
        rule["rule_action"] = "Drop"
    elif rule_orig["action"] == "TRUST":
        rule["rule_action"] = "Accept"  # TODO More specific?
    elif rule_orig["action"] == "MONITOR":
        return True  # TODO No access rule (just tracking and logging)
    return False

def set_rule_tracking(rule: dict[str, Any], rule_orig: dict[str, Any]) -> None:
    """Set rule tracking and disabled status."""
    if rule_orig["enableSyslog"]:
        rule["rule_track"] = "Log"
    else:
        rule["rule_track"] = "None"
    rule["rule_disabled"] = not rule_orig["enabled"]

def process_rule_networks(rule: dict[str, Any], rule_orig: dict[str, Any], import_id: str, config2import: dict[str, Any]) -> None:
    """Process source and destination network objects for the rule."""
    if "sourceNetworks" in rule_orig:
        rule['rule_src_refs'], rule["rule_src"] = parse_obj_group(rule_orig["sourceNetworks"], import_id, config2import['network_objects'], rule["rule_uid"])
    if "destinationNetworks" in rule_orig:
        rule['rule_dst_refs'], rule["rule_dst"] = parse_obj_group(rule_orig["destinationNetworks"], import_id, config2import['network_objects'], rule["rule_uid"])

def process_rule_services(rule: dict[str, Any], rule_orig: dict[str, Any], import_id: str, config2import: dict[str, Any]) -> None:
    """Process service objects for the rule."""
    # TODO source ports
    if "destinationPorts" in rule_orig:
        rule["rule_svc_refs"], rule["rule_svc"] = parse_svc_group(rule_orig["destinationPorts"], import_id, config2import['service_objects'], rule["rule_uid"])

def finalize_rule(rule: dict[str, Any]) -> None:
    """Set final rule properties."""
    rule["rule_src_neg"] = False
    rule["rule_dst_neg"] = False
    rule["rule_svc_neg"] = False

def normalize_access_rules(full_config: dict[str, Any], config2import: dict[str, Any], import_id: str) -> None:
    create_placeholder_objects(config2import, import_id)

    rules: list[dict[str, Any]] = []
    for device in full_config["devices"]:
        access_policy = device["accessPolicy"]
        rule_number = 0
        for rule_orig in device["rules"]:
            rule = create_base_rule(rule_orig, import_id, access_policy, rule_number)
            
            if set_rule_action(rule, rule_orig):
                continue
                
            set_rule_tracking(rule, rule_orig)
            process_rule_networks(rule, rule_orig, import_id, config2import)
            process_rule_services(rule, rule_orig, import_id, config2import)
            finalize_rule(rule)

            rule_number += 1
            rules.append(rule)

    config2import['rules'] = rules

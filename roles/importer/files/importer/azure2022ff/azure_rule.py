from azure_service import parse_svc_list
from azure_network import parse_obj_list
from fwo_log import getFwoLogger
import hashlib
import base64


def make_hash_sha256(o):
    hasher = hashlib.sha256()
    hasher.update(repr(make_hashable(o)).encode())
    return base64.b64encode(hasher.digest()).decode()


def make_hashable(o):
    if isinstance(o, (tuple, list)):
        return tuple((make_hashable(e) for e in o))

    if isinstance(o, dict):
        return tuple(sorted((k,make_hashable(v)) for k,v in o.items()))

    if isinstance(o, (set, frozenset)):
        return tuple(sorted(make_hashable(e) for e in o))

    return o

# rule_access_scope_v4 = ['rules_global_header_v4',
#                         'rules_adom_v4', 'rules_global_footer_v4']
# rule_access_scope_v6 = ['rules_global_header_v6',
#                         'rules_adom_v6', 'rules_global_footer_v6']
# rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
# rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
# rule_scope = rule_access_scope + rule_nat_scope


def normalize_access_rules(full_config, config2import, import_id, mgm_details={}):
    rules = []
    for device in full_config["devices"]:     
        rule_number = 0
        for policy_name in full_config['devices'].keys():
            for rule_prop in full_config['devices'][policy_name]['rules']:
                rule_coll_container = rule_prop['properties']
                if 'ruleCollections' in rule_coll_container:
                    for rule_coll in rule_coll_container['ruleCollections']:
                        if 'ruleCollectionType' in rule_coll and rule_coll['ruleCollectionType'] == 'FirewallPolicyFilterRuleCollection':
                            rule_action = "accept"
                            if rule_coll['action']['type'] == 'Deny':
                                rule_action = "deny"
                                
                        for rule_orig in rule_coll['rules']:
                            rule = {'rule_src': 'any', 'rule_dst': 'any', 'rule_svc': 'any',
                            'rule_src_refs': 'any_obj_placeholder', 'rule_dst_refs': 'any_obj_placeholder',
                            'rule_svc_refs': 'any_svc_placeholder'}
                            rule['rulebase_name'] = policy_name
                            rule["rule_name"] = rule_orig["name"]
                            rule['rule_type'] = "access"
                            rule['rule_num'] = rule_number
                            rule['rule_installon'] = None
                            rule['parent_rule_id'] = None
                            rule['rule_time'] = None
                            rule['rule_implied'] = False
                            rule["rule_comment"] = None
                            rule["rule_action"] = rule_action
                            rule["rule_track"] = "None"
                            rule["rule_disabled"] = False
                            rule["rule_uid"] = make_hash_sha256(rule)   # generate uid from invariable rule parts without import_id
                            rule['control_id'] = import_id

                            if "sourceAddresses" in rule_orig:
                                rule['rule_src_refs'], rule["rule_src"] = parse_obj_list(rule_orig["sourceAddresses"], import_id, config2import['network_objects'], rule["rule_uid"])
                            if "destinationAddresses" in rule_orig:
                                rule['rule_dst_refs'], rule["rule_dst"] = parse_obj_list(rule_orig["destinationAddresses"], import_id, config2import['network_objects'], rule["rule_uid"])
                            if "destinationPorts" in rule_orig:
                                rule['rule_svc_refs'], rule['rule_svc'] = parse_svc_list(rule_orig["destinationPorts"], rule_orig["ipProtocols"], import_id, config2import['service_objects'], rule["rule_uid"])
                            # TODO: 
                                # ipProtocols!!
                                # sourceIpGroups
                                # destinationIpGroups
                                # destinationFqdns
                            rule["rule_src_neg"] = False
                            rule["rule_dst_neg"] = False
                            rule["rule_svc_neg"] = False

                            rule_number += 1
                            rules.append(rule)

    config2import['rules'] = rules

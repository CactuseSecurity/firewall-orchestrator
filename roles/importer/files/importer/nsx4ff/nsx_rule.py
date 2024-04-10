from nsx_service import parse_svc_list
from nsx_network import parse_obj_list
from fwo_log import getFwoLogger
from fwo_const import list_delimiter
import hashlib
import base64
import os.path


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


def normalize_access_rules(full_config, config2import, import_id, mgm_details={}):
    rules = []
    logger = getFwoLogger()

    nw_obj_names = []
    for o in config2import['network_objects']:
        nw_obj_names.append(o["obj_name"])

    for device in full_config["devices"]:     
        rule_number = 0
        for dev_id in full_config['devices'].keys():
            for rulebase in list(full_config['devices'][dev_id].keys()):
                for rule_orig in full_config['devices'][dev_id][rulebase]['rules']:

                    # set some default values first
                    rule = {'rule_src': 'any', 'rule_dst': 'any', 'rule_svc': 'any',
                    'rule_src_refs': 'any_obj_placeholder', 'rule_dst_refs': 'any_obj_placeholder',
                    'rule_src_neg': False, 'rule_dst_neg': False,
                    'rule_svc_refs': 'any_svc_placeholder'}

                    if 'sources_excluded' in rule_orig and rule_orig['sources_excluded']:
                        rule["rule_src_neg"] = True
                    if 'destinations_excluded' in rule_orig and rule_orig['destinations_excluded']:
                        rule["rule_dst_neg"] = True
                    rule.update({
                        "rule_svc_neg": False,     # not possible to negate the svc field on NSX
                        "rulebase_name": os.path.basename(rule_orig['parent_path']),
                        "rule_name": rule_orig['relative_path'],
                        'rule_type': 'access',
                        'rule_num': rule_number,
                        'parent_rule_id': None,
                        'rule_time': None,
                        'rule_implied': False,
                        'rule_comment': None,
                        'rule_track': 'None',
                        'rule_uid': rule_orig['unique_id'],
                        'rule_disabled': rule_orig['disabled'],
                        'control_id': import_id
                    })

                    if "action" in rule_orig:
                        if rule_orig['action']=='ALLOW':
                            rule['rule_action'] = 'accept'
                        elif rule_orig['action']=='drop':
                            rule['rule_action'] = 'drop'
                        elif rule_orig['action']=='deny':
                            rule['rule_action'] = 'deny'
                        elif rule_orig['action']=='REJECT':
                            rule['rule_action'] = 'reject'
                        else:
                            logger.warning("found undefined action:" + str(rule_orig))
                    else:   # NAT rules
                        rule['rule_action'] = "accept"
                        rule['rule_type'] = 'nat'

                    if 'logged' in rule_orig and rule_orig['logged']:
                        rule['rule_track'] = 'log'
                    else:
                        rule['rule_track'] = 'none'

                    if "source_groups" in rule_orig:
                        rule['rule_src_refs'], rule["rule_src"] = parse_obj_list(rule_orig["source_groups"], import_id, config2import['network_objects'], rule["rule_uid"])
                    else:
                        logger.warning("found undefined source in rule: " + str(rule_orig))

                    if "destination_groups" in rule_orig:
                        rule['rule_dst_refs'], rule["rule_dst"] = parse_obj_list(rule_orig["destination_groups"], import_id, config2import['network_objects'], rule["rule_uid"])
                    else:
                        logger.warning("found undefined destination in rule: " + str(rule_orig))

                    services = []
                    if "services" in rule_orig:
                        services = rule_orig["services"]
                    
                    if services != [ "ANY" ]:
                        rule['rule_svc_refs'], rule["rule_svc"] = parse_svc_list(services, import_id, config2import['service_objects'], rule["rule_uid"], type='service')

                    rule_number += 1
                    rules.append(rule)

    config2import['rules'] += rules

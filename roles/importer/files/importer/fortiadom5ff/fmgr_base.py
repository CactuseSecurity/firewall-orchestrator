
def resolve_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None, jwt=None, import_id=None, mgm_id=None):
    # guessing ipv4 and adom (to also search global objects)
    return resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type='v4_adom', obj_type='network', jwt=jwt, import_id=import_id, mgm_id=mgm_id)


def resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None, obj_type='network', jwt=None, import_id=None, rule_uid=None, object_type=None, mgm_id=None):
    logger = getFwoLogger()
    fwo_config = readConfig(fwo_config_filename)

    ref_list = []
    objects_not_found = []
    for el in obj_name_string_list.split(delimiter):
        found = False
        if rule_type is None:
            continue
        if obj_type == 'network':
            if 'v4' in rule_type and 'global' in rule_type:
                object_tables = [obj_dict[obj_type] for obj_type in v4_object_types]
                object_tables.append(obj_dict['nw_obj_global_firewall/internet-service-basic'][0]['response']['results'])
            elif 'v6' in rule_type and 'global' in rule_type:
                object_tables = [obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
            elif 'v4' in rule_type and 'adom' in rule_type:
                object_tables = [obj_dict[obj_type] for obj_type in v6_object_types]
                object_tables.append(obj_dict['nw_obj_global_firewall/internet-service-basic'][0]['response']['results'])
            elif 'v6' in rule_type and 'adom' in rule_type:
                object_tables = [obj_dict['nw_obj_adom_firewall/address6'], obj_dict['nw_obj_adom_firewall/addrgrp6'], \
                    obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
            elif 'nat' in rule_type and 'adom' in rule_type:
                object_tables = [obj_dict['nw_obj_adom_firewall/address'], obj_dict['nw_obj_adom_firewall/addrgrp'], \
                    obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
            elif 'nat' in rule_type and 'global' in rule_type:
                object_tables = [obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
            else:
                object_tables = []
            break_flag = False # if we find a match we stop the two inner for-loops
            for tab in object_tables:
                if break_flag:
                    found = True
                    break
                else:
                    for obj in tab:
                        if obj[name_key] == el:
                            if uid_key in obj:
                                ref_list.append(obj[uid_key])
                            # in case of internet-service-object we find no uid field, but custom q_origin_key_
                            elif 'q_origin_key' in obj:
                                ref_list.append('q_origin_key_' + str(obj['q_origin_key']))
                            else:
                                logger.error('found object without expected uid')
                            break_flag = True
                            found = True
                            break
        else:
            logger.warning(f"resolve_raw_objects for obj_type '{obj_type}' not implemented yet")
        if not found:
            objects_not_found.append(el)

    for obj in objects_not_found:
        if obj != 'all' and obj != 'Original':
            if not create_data_issue(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, obj_name=obj, severity=1, rule_uid=rule_uid, mgm_id=mgm_id, object_type=object_type):
                logger.warning("resolve_raw_objects: encountered error while trying to log an import data issue using create_data_issue")

            desc = "found a broken network object reference '" + obj + "' "
            if object_type is not None:
                desc +=  "(type=" + object_type + ") "
            desc += "in rule with UID '" + str(rule_uid) + "'"
            setAlert(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, title="object reference error", mgm_id=mgm_id, severity=1, role='importer', \
                description=desc, source='import', alertCode=16)

    return delimiter.join(ref_list)

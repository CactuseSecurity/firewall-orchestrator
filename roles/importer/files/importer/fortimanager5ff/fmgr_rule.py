import logging, ipaddress
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
sys.path.append(importer_base_dir + '/fortimanager5ff')
import common, fmgr_service, fmgr_network, fmgr_zone, fwcommon


def normalize_access_rules(full_config, config2import, import_id, rule_types):
    rules = []
    list_delimiter = '|'

    for rule_table in rule_types:
        for pkg_name in full_config[rule_table]:
            for rule_orig in full_config[rule_table][pkg_name]:
                rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
                rule.update({ 'control_id': import_id})
                rule.update({ 'rulebase_name': pkg_name})    # the rulebase_name will be set to the pkg_name as there is no rulebase_name in FortiMangaer
                rule.update({ 'rule_ruleid': rule_orig['policyid']})
                rule.update({ 'rule_uid': rule_orig['uuid']})
                rule.update({ 'rule_num': rule_orig['obj seq']})
                if 'name' in rule_orig:
                    rule.update({ 'rule_name': rule_orig['name']})
                rule.update({ 'rule_installon': None })
                rule.update({ 'rule_implied': False })
                rule.update({ 'rule_time': None })
                rule.update({ 'rule_type': 'access' })
                rule.update({ 'parent_rule_id': None })

                if 'comments' in rule_orig:
                    rule.update({ 'rule_comment': rule_orig['comments']})
                else:
                    rule.update({ 'rule_comment': None })
                if rule_orig['action']==0:
                    rule.update({ 'rule_action': 'Drop' })
                else:
                    rule.update({ 'rule_action': 'Accept' })
                if 'status' in rule_orig and (rule_orig['status']=='enable' or rule_orig['status']==1):
                    rule.update({ 'rule_disabled': False })
                else:
                    rule.update({ 'rule_disabled': True })
                if rule_orig['logtraffic'] == 'disable':
                    rule.update({ 'rule_track': 'None'})
                else:
                    rule.update({ 'rule_track': 'Log'})

                rule['rule_src'] = common.extend_string_list(rule['rule_src'], rule_orig, 'srcaddr', list_delimiter)
                rule['rule_dst'] = common.extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr', list_delimiter)
                rule['rule_svc'] = common.extend_string_list(rule['rule_svc'], rule_orig, 'service', list_delimiter)

                rule['rule_src'] = common.extend_string_list(rule['rule_src'], rule_orig, 'srcaddr6', list_delimiter)
                rule['rule_dst'] = common.extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr6', list_delimiter)

                if len(rule_orig['srcintf'])>0:
                    src_obj_zone = fmgr_zone.add_zone_if_missing (config2import, rule_orig['srcintf'][0], import_id)
                    rule.update({ 'rule_from_zone': src_obj_zone }) # todo: currently only using the first zone
                if len(rule_orig['dstintf'])>0:
                    dst_obj_zone = fmgr_zone.add_zone_if_missing (config2import, rule_orig['dstintf'][0], import_id)
                    rule.update({ 'rule_to_zone': dst_obj_zone }) # todo: currently only using the first zone

                rule.update({ 'rule_src_neg': rule_orig['srcaddr-negate']=='disable'})
                rule.update({ 'rule_dst_neg': rule_orig['dstaddr-negate']=='disable'})
                rule.update({ 'rule_svc_neg': rule_orig['service-negate']=='disable'})

#                rule.update({ 'rule_src_refs': common.resolve_objects(rule['rule_src'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid', rule_type=rule_table) })
#                rule.update({ 'rule_dst_refs': common.resolve_objects(rule['rule_dst'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid', rule_type=rule_table) })
                rule.update({ 'rule_src_refs': common.resolve_raw_objects(rule['rule_src'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) })
                rule.update({ 'rule_dst_refs': common.resolve_raw_objects(rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) })
                rule.update({ 'rule_svc_refs': rule['rule_svc'] }) # services do not have uids, so using name instead

                rules.append(rule)

    config2import.update({'rules': rules})


def normalize_nat_rules(full_config, config2import, import_id, rule_tables):
    nat_rules = []
    list_delimiter = '|'
    original_obj_name = 'Original'
    original_obj_uid = '01234-12345-23456-34567'
    config2import['network_objects'].append(fmgr_network.create_network_object(import_id=import_id, name=original_obj_name, type='network', ip='0.0.0.0/0',\
        uid=original_obj_uid, color='black', comment='"original" network object created by FWO importer for NAT purposes'))


    for rule_table in rule_tables:
        for pkg in full_config['rules_global_nat']:
            for rule_orig in full_config[rule_table][pkg]:
                rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
                if rule_orig['nat'] == 1:   # assuming source nat
                    rule.update({ 'control_id': import_id})
                    rule.update({ 'rulebase_name': pkg})    # the rulebase_name just has to be a unique string among devices
                    rule.update({ 'rule_ruleid': rule_orig['policyid']})
                    rule.update({ 'rule_uid': rule_orig['uuid']})
                    rule.update({ 'rule_num': rule_orig['obj seq']})
                    if 'comments' in rule_orig:
                        rule.update({ 'rule_comment': rule_orig['comments']})
                    rule.update({ 'rule_action': 'Drop' })  # not used for nat rules
                    rule.update({ 'rule_track': 'None'}) # not used for nat rules

                    rule['rule_src'] = common.extend_string_list(rule['rule_src'], rule_orig, 'orig-addr', list_delimiter)
                    rule['rule_dst'] = common.extend_string_list(rule['rule_dst'], rule_orig, 'dst-addr', list_delimiter)
                    
                    if rule_orig['protocol']==17:
                        svc_name = 'udp_' + str(rule_orig['orig-port'])
                    elif rule_orig['protocol']==6:
                        svc_name = 'tcp_' + str(rule_orig['orig-port'])
                    else:
                        svc_name = 'svc_' + str(rule_orig['orig-port'])
                    # need to create a helper service object and add it to the nat rule, also needs to be added to service list

                    if not 'service_objects' in config2import: # is normally defined
                        config2import['service_objects'] = []
                    config2import['service_objects'].append(fmgr_service.create_svc_object(import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes'))
                    rule['rule_svc'] = svc_name

                    #rule['rule_src'] = common.extend_string_list(rule['rule_src'], rule_orig, 'srcaddr6', list_delimiter)
                    #rule['rule_dst'] = common.extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr6', list_delimiter)

                    if len(rule_orig['srcintf'])>0:
                        rule.update({ 'rule_from_zone': rule_orig['srcintf'][0] }) # todo: currently only using the first zone
                    if len(rule_orig['dstintf'])>0:
                        rule.update({ 'rule_to_zone': rule_orig['dstintf'][0] }) # todo: currently only using the first zone

                    rule.update({ 'rule_src_neg': False})
                    rule.update({ 'rule_dst_neg': False})
                    rule.update({ 'rule_svc_neg': False})
                    # rule.update({ 'rule_src_refs': common.resolve_objects(rule['rule_src'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid', rule_type=rule_table) })
                    # rule.update({ 'rule_dst_refs': common.resolve_objects(rule['rule_dst'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid', rule_type=rule_table) })
                    rule.update({ 'rule_src_refs': common.resolve_raw_objects(rule['rule_src'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) })
                    rule.update({ 'rule_dst_refs': common.resolve_raw_objects(rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) })
                    # services do not have uids, so using name instead
                    rule.update({ 'rule_svc_refs': rule['rule_svc'] })
                    rule.update({ 'rule_type': 'original' })
                    rule.update({ 'rule_installon': None })
                    if 'status' in rule_orig and (rule_orig['status']=='enable' or rule_orig['status']==1):
                        rule.update({ 'rule_disabled': False })
                    else:
                        rule.update({ 'rule_disabled': True })
                    rule.update({ 'rule_implied': False })
                    rule.update({ 'rule_time': None })
                    rule.update({ 'parent_rule_id': None })

                    nat_rules.append(rule)
                    
                    ############## now adding the xlate rule part ##########################
                    xlate_rule = dict(rule) # copy the original (match) rule
                    xlate_rule.update({'rule_src': '', 'rule_dst': '', 'rule_svc': ''})
                    xlate_rule['rule_src'] = common.extend_string_list(xlate_rule['rule_src'], rule_orig, 'orig-addr', list_delimiter)
                    xlate_rule['rule_dst'] = original_obj_name
                    
                    if rule_orig['protocol']==17:
                        svc_name = 'udp_' + str(rule_orig['nat-port'])
                    elif rule_orig['protocol']==6:
                        svc_name = 'tcp_' + str(rule_orig['nat-port'])
                    else:
                        svc_name = 'svc_' + str(rule_orig['nat-port'])
                    # need to create a helper service object and add it to the nat rule, also needs to be added to service list!
                    # fmgr_service.create_svc_object(name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes')
                    config2import['service_objects'].append(fmgr_service.create_svc_object(import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['nat-port'], comment='service created by FWO importer for NAT purposes'))
                    xlate_rule['rule_svc'] = svc_name

                    # xlate_rule.update({ 'rule_src_refs': common.resolve_objects(xlate_rule['rule_src'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid' ) }, rule_type=rule_table)
                    # xlate_rule.update({ 'rule_dst_refs': common.resolve_objects(xlate_rule['rule_dst'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid' ) }, rule_type=rule_table)
                    xlate_rule.update({ 'rule_src_refs': common.resolve_objects(xlate_rule['rule_src'], list_delimiter, full_config, 'name', 'uuid' ) }, rule_type=rule_table)
                    xlate_rule.update({ 'rule_dst_refs': common.resolve_objects(xlate_rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid' ) }, rule_type=rule_table)
                    xlate_rule.update({ 'rule_svc_refs': xlate_rule['rule_svc'] })  # services do not have uids, so using name instead

                    xlate_rule.update({ 'rule_type': 'xlate' })

                    nat_rules.append(xlate_rule)

    config2import['rules'].extend(nat_rules)

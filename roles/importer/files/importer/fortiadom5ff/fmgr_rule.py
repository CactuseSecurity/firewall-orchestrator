import logging, copy
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
sys.path.append(importer_base_dir + '/fortiadom5ff')
import common, fmgr_service, fmgr_network, fmgr_zone, fwcommon


def check_headers_needed(full_config, rule_types):
    found_v4 = False
    found_v6 = False
    for rule_table in rule_types:
        if full_config[rule_table] is not None:
            if rule_table in fwcommon.rule_access_scope_v4:
                found_v4 = True
            if rule_table in fwcommon.rule_access_scope_v6:
                found_v6 = True
    if found_v4 and found_v6:
        return True, True
    return False, False


def insert_header(rules, import_id, header_text, rulebase_name, rule_uid, rule_number, src_refs, dst_refs):
    rule = {
        "control_id": import_id,
        "rule_head_text": header_text,
        "rulebase_name": rulebase_name,
        "rule_ruleid": None,
        "rule_uid":  rule_uid + rulebase_name,
        "rule_num": rule_number,
        "rule_disabled": False,
        "rule_src": "all",
        "rule_dst": "all", 
        "rule_svc": "ALL",
        "rule_src_neg": False,
        "rule_dst_neg": False,
        "rule_svc_neg": False,
        "rule_src_refs": src_refs,
        "rule_dst_refs": dst_refs,
        "rule_svc_refs": "ALL",
        "rule_action": "Accept",
        "rule_track": "None",
        "rule_installon": None,
        "rule_time": None,
        "rule_type": "access",
        "parent_rule_id": None,
        "rule_implied": False,
        "rule_comment": None
    }
    rules.append(rule)


def normalize_access_rules(full_config, config2import, import_id, rule_types):
    rules = []
    list_delimiter = '|'
    first_v4, first_v6 = check_headers_needed(full_config, rule_types)

    vip_nat_rule_number = 0
    nat_rule_number = 0
    rule_number = 0
    src_ref_all = ""
    dst_ref_all = ""
    for rule_table in rule_types:
        for localPkgName in full_config[rule_table]:
            if rule_table in fwcommon.rule_access_scope_v6 and first_v6:
                src_ref_all = common.resolve_raw_objects("all", list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table)
                dst_ref_all = common.resolve_raw_objects("all", list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table)
                insert_header(rules, import_id, "IPv6 rules", localPkgName, "IPv6HeaderText", 0, src_ref_all, dst_ref_all)
                rule_number += 1
                first_v6 = False
            elif rule_table in fwcommon.rule_access_scope_v4 and first_v4:
                insert_header(rules, import_id, "IPv4 rules", localPkgName, "IPv4HeaderText", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1
                first_v4 = False
            if rule_table == 'rules_global_header_v4' and len(full_config['rules_global_header_v4'][localPkgName])>0:
                insert_header(rules, import_id, "Global Header Rules IPv4", localPkgName, "IPv4GlobalHeaderRules", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1
            if rule_table == 'rules_global_header_v6' and len(full_config['rules_global_header_v6'][localPkgName])>0:
                insert_header(rules, import_id, "Global Header Rules IPv6", localPkgName, "IPv6GlobalHeaderRules", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1
            if rule_table == 'rules_global_footer_v4' and len(full_config['rules_global_footer_v4'][localPkgName])>0:
                insert_header(rules, import_id, "Global Footer Rules IPv4", localPkgName, "IPv4GlobalFooterRules", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1
            if rule_table == 'rules_global_footer_v6' and len(full_config['rules_global_footer_v6'][localPkgName])>0:
                insert_header(rules, import_id, "Global Footer Rules IPv6", localPkgName, "IPv6GlobalFooterRules", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1
            if rule_table == 'rules_adom_v4' and len(full_config['rules_adom_v4'][localPkgName])>0:
                insert_header(rules, import_id, "Adom Rules IPv4", localPkgName, "IPv4AdomRules", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1
            if rule_table == 'rules_adom_v6' and len(full_config['rules_adom_v6'][localPkgName])>0:
                insert_header(rules, import_id, "Adom Rules IPv6", localPkgName, "IPv6AdomRules", rule_number, src_ref_all, dst_ref_all)
                rule_number += 1

            for rule_orig in full_config[rule_table][localPkgName]:
                rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
                rule.update({ 'control_id': import_id})
                rule.update({ 'rulebase_name': localPkgName})    # the rulebase_name will be set to the pkg_name as there is no rulebase_name in FortiMangaer
                rule.update({ 'rule_ruleid': rule_orig['policyid']})
                rule.update({ 'rule_uid': rule_orig['uuid']})
                rule.update({ 'rule_num': rule_number})
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

                rule.update({ 'rule_src_refs': common.resolve_raw_objects(rule['rule_src'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) })
                rule.update({ 'rule_dst_refs': common.resolve_raw_objects(rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) })
                rule.update({ 'rule_svc_refs': rule['rule_svc'] }) # services do not have uids, so using name instead

                # now dealing with VIPs (dst NAT part) of combined rules
                if "match-vip" in rule_orig and rule_orig["match-vip"]==1:
                    logging.warning("found VIP destination Access/NAT rule (but not parsing yet); no. " + str(vip_nat_rule_number))
                    vip_nat_rule_number += 1

                # now dealing with src NAT part of combined rules
                if "nat" in rule_orig and rule_orig["nat"]==1:
                    #logging.debug("found mixed Access/NAT rule no. " + str(nat_rule_number))
                    nat_rule_number += 1
                    xlate_rule = copy.deepcopy(rule)
                    rule['rule_type'] = 'combined'
                    xlate_rule['rule_type'] = 'xlate'
                    xlate_rule['rule_comment'] = None
                    xlate_rule['rule_disabled'] = False
                    if 'ippool' in rule_orig:
                        if rule_orig['ippool']==0:  # hiding behind outbound interface
                            # logging.debug("found outbound interface hide nat rule") # needs to be checked
                            if 'dstintf' in rule_orig:
                                if len(rule_orig['dstintf'])==1:
                                    hideInterface=rule_orig['dstintf'][0]
                                else:
                                    logging.warning("did not find exactly one nat hiding interface")
                                # need to 
                                # - find out ip of outbound interface
                                # - create an object for the ip of the dst interface and add it here as xlate src
                                # alternative: add dummy object "outbound-interface"
                                logging.warning("hide nat behind outgoing interface not implemented yet; hide interface: " + hideInterface)

                        elif rule_orig['ippool']==1:
                            poolNameArray = rule_orig['poolname']
                            if len(poolNameArray)>0:
                                if len(poolNameArray)>1:
                                    logging.warning("found more than one ippool - ignoring all but first pool")
                                poolName = poolNameArray[0]
                                xlate_rule['rule_src'] = poolName
                                xlate_rule['rule_src_refs'] = "ippool-uuid-" + poolName
                            else:
                                logging.warning("found ippool rule without ippool")
                        else:
                            logging.warning("found ippool rule with unexpected ippool value: " + rule_orig['ippool'])
                    
                    if 'fixedport' in rule_orig and rule_orig['fixedport']!=0:
                        logging.warning("found fixedport translation - ignoring for now")
                        # need example for interpretation of config
                    else: # no port translation - svc stays original:
                        xlate_rule['rule_svc'] = 'Original'
                        xlate_rule['rule_svc_refs'] = 'Original'

                    if 'natip' in rule_orig and rule_orig['natip']!=["0.0.0.0","0.0.0.0"]:
                        logging.warning("found explicit natip rule - ignoring for now")
                        # need example for interpretation of config

                    # last step missing is dst nat, setting to no dst NAT for the moment
                    xlate_rule['rule_dst'] = 'Original'
                    xlate_rule['rule_dst_refs'] = 'Original-UID'
 
                    rules.append(rule)
                    rules.append(xlate_rule)

                else:
                    rules.append(rule)
                rule_number += 1

    config2import.update({'rules': rules})


def normalize_nat_rules(full_config, config2import, import_id, rule_tables):
    nat_rules = []
    list_delimiter = '|'
    rule_number = 0

    for rule_table in rule_tables:
        for localPkgName in full_config['rules_global_nat']:
            for rule_orig in full_config[rule_table][localPkgName]:
                rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
                if rule_orig['nat'] == 1:   # assuming source nat
                    rule.update({ 'control_id': import_id})
                    rule.update({ 'rulebase_name': localPkgName})    # the rulebase_name just has to be a unique string among devices
                    rule.update({ 'rule_ruleid': rule_orig['policyid']})
                    rule.update({ 'rule_uid': rule_orig['uuid']})
                    # rule.update({ 'rule_num': rule_orig['obj seq']})
                    rule.update({ 'rule_num': rule_number })
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
                    config2import['service_objects'].append(fmgr_service.create_svc_object( \
                        import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes'))
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
                    xlate_rule['rule_dst'] = 'Original'
                    
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

                    xlate_rule.update({ 'rule_src_refs': common.resolve_objects(xlate_rule['rule_src'], list_delimiter, full_config, 'name', 'uuid' ) }, rule_type=rule_table)
                    xlate_rule.update({ 'rule_dst_refs': common.resolve_objects(xlate_rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid' ) }, rule_type=rule_table)
                    xlate_rule.update({ 'rule_svc_refs': xlate_rule['rule_svc'] })  # services do not have uids, so using name instead

                    xlate_rule.update({ 'rule_type': 'xlate' })

                    nat_rules.append(xlate_rule)
                    rule_number += 1
    config2import['rules'].extend(nat_rules)

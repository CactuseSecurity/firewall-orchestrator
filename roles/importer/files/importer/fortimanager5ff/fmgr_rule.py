import logging, ipaddress
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
sys.path.append(importer_base_dir + '/fortimanager5ff')
import common, fwcommon


def normalize_rules(full_config, config2import, import_id):
    rules = []
    list_delimiter = '|'
    #for rule_orig in full_config['access_rules']:
    for dev_id in full_config['v4_rulebases_by_dev_id']:
        for rule_orig in full_config['v4_rulebases_by_dev_id'][dev_id]:
            rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
            rule.update({ 'control_id': import_id})
            rule.update({ 'rule_ruleid': rule_orig['policyid']})
            rule.update({ 'rule_uid': rule_orig['uuid']})
            rule.update({ 'rule_num': rule_orig['obj seq']})
            rule.update({ 'rule_name': rule_orig['name']})
            if 'comments' in rule_orig:
                rule.update({ 'rule_comment': rule_orig['comments']})
            if rule_orig['action']==0:
                rule.update({ 'rule_action': 'Drop' })
            else:
                rule.update({ 'rule_action': 'Accept' })
            rule.update({ 'rule_disabled': not rule_orig['status']=='enable'})
            if rule_orig['logtraffic'] == 'disable':
                rule.update({ 'rule_track': 'None'})
            else:
                rule.update({ 'rule_track': 'Log'})

            rule['rule_src'] = common.extend_string_list(rule['rule_src'], rule_orig, 'srcaddr', list_delimiter)
            rule['rule_dst'] = common.extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr', list_delimiter)
            rule['rule_svc'] = common.extend_string_list(rule['rule_svc'], rule_orig, 'service', list_delimiter)

            rule['rule_src'] = common.extend_string_list(rule['rule_src'], rule_orig, 'srcaddr6', list_delimiter)
            rule['rule_dst'] = common.extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr6', list_delimiter)

            rule.update({ 'rule_from_zone': rule_orig['srcintf'][0] }) # todo: currently only using the first zone
            rule.update({ 'rule_to_zone': rule_orig['dstintf'][0] }) # todo: currently only using the first zone

            rule.update({ 'rule_src_neg': rule_orig['srcaddr-negate']=='disable'})
            rule.update({ 'rule_dst_neg': rule_orig['dstaddr-negate']=='disable'})
            rule.update({ 'rule_svc_neg': rule_orig['service-negate']=='disable'})

            rule.update({ 'rule_src_refs': common.resolve_objects(rule['rule_src'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid' ) })
            rule.update({ 'rule_dst_refs': common.resolve_objects(rule['rule_dst'], list_delimiter, config2import['network_objects'], 'obj_name', 'obj_uid' ) })
            # todo: need to resolve rule object refs
            # rule.update({ 'rule_svc_refs': resolve_objects(rule['rule_svc'], list_delimiter, config2import['network_services'], 'svcj_name', 'svc_uid' ) })
            rule.update({ 'rule_svc_refs': rule['rule_svc'] })

            rules.append(rule)

    config2import.update({'access_rules': rules})

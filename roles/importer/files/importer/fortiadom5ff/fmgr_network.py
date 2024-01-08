from asyncio.log import logger
import ipaddress
from fwo_log import getFwoLogger
from fwo_const import list_delimiter, nat_postfix
from fmgr_zone import add_zone_if_missing
from fwo_config import readConfig
from fwo_const import fwo_config_filename
from fwo_api import setAlert, create_data_issue

def normalize_nwobjects(full_config, config2import, import_id, nw_obj_types, jwt=None, mgm_id=None):
    logger = getFwoLogger()
    nw_objects = []
    for obj_type in nw_obj_types:
        for obj_orig in full_config[obj_type]:
            obj_zone = 'global'
            obj = {}
            obj.update({'obj_name': obj_orig['name']})
            if 'subnet' in obj_orig: # ipv4 object
                ipa = ipaddress.ip_network(str(obj_orig['subnet'][0]) + '/' + str(obj_orig['subnet'][1]))
                if ipa.num_addresses > 1:
                    obj.update({ 'obj_typ': 'network' })
                else:
                    obj.update({ 'obj_typ': 'host' })
                obj.update({ 'obj_ip': ipa.with_prefixlen })
            elif 'ip6' in obj_orig: # ipv6 object
                ipa = ipaddress.ip_network(str(obj_orig['ip6']).replace("\\", ""))
                if ipa.num_addresses > 1:
                    obj.update({ 'obj_typ': 'network' })
                else:
                    obj.update({ 'obj_typ': 'host' })
                obj.update({ 'obj_ip': ipa.with_prefixlen })
            elif 'member' in obj_orig: # addrgrp4 / addrgrp6
                obj.update({ 'obj_typ': 'group' })
                obj.update({ 'obj_member_names' : list_delimiter.join(obj_orig['member']) })
                obj.update({ 'obj_member_refs' : resolve_objects(obj['obj_member_names'], list_delimiter, full_config, 'name', 'uuid', jwt=jwt, import_id=import_id)}, mgm_id=mgm_id)
            elif 'startip' in obj_orig: # ippool object
                obj.update({ 'obj_typ': 'ip_range' })
                obj.update({ 'obj_ip': obj_orig['startip'] })
                obj.update({ 'obj_ip_end': obj_orig['endip'] })
            elif 'start-ip' in obj_orig: # standard ip range object
                obj.update({ 'obj_typ': 'ip_range' })
                obj.update({ 'obj_ip': obj_orig['start-ip'] })
                obj.update({ 'obj_ip_end': obj_orig['end-ip'] })
            elif 'extip' in obj_orig: # vip object, simplifying to a single ip
                obj.update({ 'obj_typ': 'host' })
                if 'extip' not in obj_orig or len(obj_orig['extip'])==0:
                    logger.error("vip (extip): found empty extip field for " + obj_orig['name'])
                else:
                    if len(obj_orig['extip'])>1:
                        logger.warning("vip (extip): found more than one extip, just using the first one for " + obj_orig['name'])
                    set_ip_in_obj(obj, obj_orig['extip'][0])   # resolving nat range if there is one
                    nat_obj = {}
                    nat_obj.update({'obj_typ': 'host' })
                    nat_obj.update({'obj_color': 'black'})
                    nat_obj.update({'obj_comment': 'FWO-auto-generated nat object for VIP'})
                    if 'obj_ip_end' in obj: # this obj is a range - include the end ip in name and uid as well to avoid akey conflicts
                        nat_obj.update({'obj_ip_end': obj['obj_ip_end']})

                # now dealing with the nat ip obj (mappedip)
                if 'mappedip' not in obj_orig or len(obj_orig['mappedip'])==0:
                    logger.warning("vip (extip): found empty mappedip field for " + obj_orig['name'])
                else:
                    if len(obj_orig['mappedip'])>1:
                        logger.warning("vip (extip): found more than one mappedip, just using the first one for " + obj_orig['name'])
                    nat_ip = obj_orig['mappedip'][0]
                    set_ip_in_obj(nat_obj, nat_ip)
                    obj.update({ 'obj_nat_ip': nat_obj['obj_ip'] }) # save nat ip in vip obj
                    if 'obj_ip_end' in nat_obj: # this nat obj is a range - include the end ip in name and uid as well to avoid akey conflicts
                        obj.update({ 'obj_nat_ip_end': nat_obj['obj_ip_end'] }) # save nat ip in vip obj
                        nat_obj.update({'obj_name': nat_obj['obj_ip'] + '-' + nat_obj['obj_ip_end'] + nat_postfix})
                    else:
                        nat_obj.update({'obj_name': nat_obj['obj_ip'] + nat_postfix})
                    nat_obj.update({'obj_uid': nat_obj['obj_name']})                    
                    ###### range handling

                if 'associated-interface' in obj_orig and len(obj_orig['associated-interface'])>0: # and obj_orig['associated-interface'][0] != 'any':
                    obj_zone = obj_orig['associated-interface'][0]
                nat_obj.update({'obj_zone': obj_zone })
                nat_obj.update({'control_id': import_id})
                if nat_obj not in nw_objects:   # rare case when a destination nat is down for two different orig ips to the same dest ip
                    nw_objects.append(nat_obj)
                else:
                    pass
            else: # 'fqdn' in obj_orig: # "fully qualified domain name address" // other unknown types
                obj.update({ 'obj_typ': 'network' })
                obj.update({ 'obj_ip': '0.0.0.0/0'})
            if 'comment' in obj_orig:
                obj.update({'obj_comment': obj_orig['comment']})
            if 'color' in obj_orig and obj_orig['color']==0:
                obj.update({'obj_color': 'black'})  # todo: deal with all other colors (will be currently ignored)
                                                    # we would need a list of fortinet color codes
            if 'uuid' not in obj_orig:
                obj_orig.update({'uuid': obj_orig['name']})
            obj.update({'obj_uid': obj_orig['uuid']})

            # here only picking first associated interface as zone:
            if 'associated-interface' in obj_orig and len(obj_orig['associated-interface'])>0: # and obj_orig['associated-interface'][0] != 'any':
                obj_zone = obj_orig['associated-interface'][0]
                # adding zone if it not yet exists
                obj_zone = add_zone_if_missing (config2import, obj_zone, import_id)
            obj.update({'obj_zone': obj_zone })
            
            obj.update({'control_id': import_id})
            nw_objects.append(obj)

    # finally add "Original" network object for natting
    original_obj_name = 'Original'
    original_obj_uid = 'Original'
    nw_objects.append(create_network_object(import_id=import_id, name=original_obj_name, type='network', ip='0.0.0.0/0',\
        uid=original_obj_uid, zone='global', color='black', comment='"original" network object created by FWO importer for NAT purposes'))

    config2import.update({'network_objects': nw_objects})


def set_ip_in_obj(nw_obj, ip): # add start and end ip in nw_obj if it is a range, otherwise do nothing
    if '-' in ip: # dealing with range
        ip_start, ip_end = ip.split('-')
        nw_obj.update({'obj_ip': ip_start })
        if ip_end != ip_start:
            nw_obj.update({'obj_ip_end': ip_end })
    else:
        nw_obj.update({'obj_ip': ip })


# for members of groups, the name of the member obj needs to be fetched separately (starting from API v1.?)
def resolve_nw_uid_to_name(uid, nw_objects):
    # return name of nw_objects element where obj_uid = uid
    for obj in nw_objects:
        if obj['obj_uid'] == uid:
            return obj['obj_name']
    return 'ERROR: uid "' + uid + '" not found'


def add_member_names_for_nw_group(idx, nw_objects):
    group = nw_objects.pop(idx)
    if group['obj_member_refs'] == '' or group['obj_member_refs'] == None:
        #member_names = None
        #obj_member_refs = None
        group['obj_member_names'] = None
        group['obj_member_refs'] = None
    else:
        member_names = ''
        obj_member_refs = group['obj_member_refs'].split(list_delimiter)
        for ref in obj_member_refs:
            member_name = resolve_nw_uid_to_name(ref, nw_objects)
            member_names += member_name + list_delimiter
        group['obj_member_names'] = member_names[:-1]
    nw_objects.insert(idx, group)


def create_network_object(import_id, name, type, ip, uid, color, comment, zone):
    # if zone is None or zone == '':
    #     zone = 'global'
    return {
        'control_id': import_id,
        'obj_name': name,
        'obj_typ': type,
        'obj_ip': ip,
        'obj_uid': uid,
        'obj_color': color,
        'obj_comment': comment,
        'obj_zone': zone
    }


# TODO: reduce commplexity if possible
def get_nw_obj(nat_obj_name, nwobjects):
    for obj in nwobjects:
        if 'obj_name' in obj and obj['obj_name']==nat_obj_name:
            return obj
    return None


# this removes all obj_nat_ip entries from all network objects
# these were used during import but might cause issues if imported into db
def remove_nat_ip_entries(config2import):
    for obj in config2import['network_objects']:
        if 'obj_nat_ip' in obj:
            obj.pop('obj_nat_ip')


def get_first_ip_of_destination(obj_ref, config2import):

    logger = getFwoLogger()
    if list_delimiter in obj_ref:
        obj_ref = obj_ref.split(list_delimiter)[0]
        # if destination does not contain exactly one ip, raise a warning 
        logger.info('src nat behind interface: more than one NAT IP - just using the first one for routing decision for obj_ref ' + obj_ref)

    for obj in config2import['network_objects']:
        if 'obj_uid' in obj and obj['obj_uid']==obj_ref:
            return obj['obj_ip']
    logger.warning('src nat behind interface: found no IP info for destination object ' + obj_ref)
    return None


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
        if rule_type is not None:
            if obj_type == 'network':
                if 'v4' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                elif 'v6' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
                elif 'v4' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address'], obj_dict['nw_obj_adom_firewall/addrgrp'], \
                        obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp'], \
                        obj_dict['nw_obj_adom_firewall/vip'] ]
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
                                ref_list.append(obj[uid_key])
                                break_flag = True
                                found = True
                                break
            elif obj_type == 'service':
                logger.warning('todo later')
        else:
            logger.warning('decide what to do')
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

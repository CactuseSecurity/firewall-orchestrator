from asyncio.log import logger
import ipaddress
from fwo_log import getFwoLogger
from fwo_const import list_delimiter, nat_postfix
from fmgr_zone import add_zone_if_missing
from fwo_config import readConfig
from model_controllers.import_state_controller import ImportStateController
from copy import deepcopy


def normalize_network_objects(import_state: ImportStateController, native_config, native_config_global, normalized_config, normalized_config_global, nw_obj_types):
    nw_objects = []
    
    if 'objects' not in native_config:
        return # no objects to normalize
    for current_obj_type in native_config['objects']:
        if not(current_obj_type in nw_obj_types and 'data' in native_config['objects'][current_obj_type]):
            continue
        for obj_orig in native_config['objects'][current_obj_type]['data']:
            normalize_network_object(obj_orig, nw_objects, normalized_config, import_state)

    if native_config.get('is-super-manager',False):
        # finally add "Original" network object for natting (only in global domain)
        original_obj_name = 'Original'
        original_obj_uid = 'Original'
        nw_objects.append(create_network_object(name=original_obj_name, type='network', ip='0.0.0.0', ip_end='255.255.255.255',\
            uid=original_obj_uid, zone='global', color='black', comment='"original" network object created by FWO importer for NAT purposes'))

    normalized_config.update({'network_objects': nw_objects})


def normalize_network_object(obj_orig, nw_objects, normalized_config, import_state):
    obj_zone = 'global'
    obj = {}
    obj.update({'obj_name': obj_orig['name']})
    if 'subnet' in obj_orig: # ipv4 object
        _parse_subnet(obj, obj_orig)
    elif 'ip6' in obj_orig: # ipv6 object
        normalize_network_object_ipv6(obj_orig, obj)
    elif 'member' in obj_orig: # addrgrp4 / addrgrp6
        obj.update({ 'obj_typ': 'group' })
        obj.update({ 'obj_member_names' : list_delimiter.join(obj_orig['member']) })
        obj.update({ 'obj_member_refs' : None}) # TODO: decide how to implement this # resolve_objects(obj['obj_member_names'], list_delimiter, native_config, 'name', 'uuid', jwt=import_state.Jwt, import_id=import_state.ImportId)}, mgm_id=import_state.MgmDetails.Id)
    elif 'startip' in obj_orig: # ippool object
        obj.update({ 'obj_typ': 'ip_range' })
        obj.update({ 'obj_ip': obj_orig['startip'] })
        obj.update({ 'obj_ip_end': obj_orig['endip'] })
    elif 'start-ip' in obj_orig: # standard ip range object
        obj.update({ 'obj_typ': 'ip_range' })
        obj.update({ 'obj_ip': obj_orig['start-ip'] })
        obj.update({ 'obj_ip_end': obj_orig['end-ip'] })
    elif 'extip' in obj_orig: # vip object, simplifying to a single ip
        normalize_vip_object(obj_orig, obj, nw_objects)
    else: # 'fqdn' in obj_orig: # "fully qualified domain name address" // other unknown types
        obj.update({ 'obj_typ': 'network' })
        obj.update({ 'obj_ip': '0.0.0.0'})
        obj.update({ 'obj_ip_end': '255.255.255.255'})

    # if obj_ip_end is not define, set it to obj_ip (assuming host)
    if obj.get('obj_ip_end',None) is None and obj.get('obj_typ', None)=='host':
        obj['obj_ip_end']= obj.get('obj_ip', None)

    obj.update({'obj_comment': obj_orig.get('comment', None)})
    # todo: deal with all other colors (will be currently ignored)
    # we would need a list of fortinet color codes, maybe:
    # https://community.fortinet.com/t5/Support-Forum/Object-color-codes-for-CLI/td-p/249479
    #if 'color' in obj_orig and obj_orig['color']==0:
    #    obj.update({'obj_color': 'black'})
    obj.update({'obj_color': 'black'})
                                            

    obj.update({'obj_uid': obj_orig.get('uuid', obj_orig['name'])})  # using name as fallback, but this should not happen

    # here only picking first associated interface as zone:
    if 'associated-interface' in obj_orig and len(obj_orig['associated-interface'])>0: # and obj_orig['associated-interface'][0] != 'any':
        obj_zone = deepcopy(obj_orig['associated-interface'][0])
        # adding zone if it not yet exists
        obj_zone = add_zone_if_missing (normalized_config, obj_zone)
    obj.update({'obj_zone': obj_zone })
    
    #obj.update({'control_id': import_state.ImportId})
    nw_objects.append(obj)


def _parse_subnet (obj, obj_orig):
    ipa = ipaddress.ip_network(str(obj_orig['subnet'][0]) + '/' + str(obj_orig['subnet'][1]))
    if ipa.num_addresses > 1:
        obj.update({ 'obj_typ': 'network' })
    else:
        obj.update({ 'obj_typ': 'host' })
    obj.update({ 'obj_ip': str(ipa.network_address) })
    obj.update({ 'obj_ip_end': str(ipa.broadcast_address) })


def normalize_network_object_ipv6(obj_orig, obj):
    ipa = ipaddress.ip_network(obj_orig['ip6'])
    if ipa.num_addresses > 1:
        obj.update({ 'obj_typ': 'network' })
    else:
        obj.update({ 'obj_typ': 'host' })
    obj.update({ 'obj_ip': str(ipa.network_address) })
    obj.update({ 'obj_ip_end': str(ipa.broadcast_address) })


def normalize_vip_object(obj_orig, obj, nw_objects):
    obj_zone = 'global'
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
            nat_obj.update({'obj_ip_end': str(obj['obj_ip_end'])})

        normalize_vip_object_nat_ip(obj_orig, obj, nat_obj)

        if 'obj_ip_end' not in nat_obj:
            nat_obj.update({'obj_ip_end': str(obj['obj_nat_ip'])})

        if 'associated-interface' in obj_orig and len(obj_orig['associated-interface'])>0: # and obj_orig['associated-interface'][0] != 'any':
            obj_zone = obj_orig['associated-interface'][0]
        nat_obj.update({'obj_zone': obj_zone })
        # nat_obj.update({'control_id': import_state.ImportId})
        if nat_obj not in nw_objects:   # rare case when a destination nat is down for two different orig ips to the same dest ip
            nw_objects.append(nat_obj)


def normalize_vip_object_nat_ip(obj_orig, obj, nat_obj):
    # now dealing with the nat ip obj (mappedip)
    if 'mappedip' not in obj_orig or len(obj_orig['mappedip'])==0:
        logger.warning("vip (extip): found empty mappedip field for " + obj_orig['name'])
        return

    if len(obj_orig['mappedip'])>1:
        logger.warning("vip (extip): found more than one mappedip, just using the first one for " + obj_orig['name'])
    nat_ip = obj_orig['mappedip'][0]
    set_ip_in_obj(nat_obj, str(nat_ip))
    obj.update({ 'obj_nat_ip': str(nat_obj['obj_ip']) }) # save nat ip in vip obj
    if 'obj_ip_end' in nat_obj: # this nat obj is a range - include the end ip in name and uid as well to avoid akey conflicts
        obj.update({ 'obj_nat_ip_end': str(nat_obj['obj_ip_end']) }) # save nat ip in vip obj
        nat_obj.update({'obj_name': nat_obj['obj_ip'] + '-' + nat_obj['obj_ip_end'] + nat_postfix})
    else:
        obj.update({ 'obj_nat_ip_end': str(nat_obj['obj_ip']) }) # assuming host with obj_nat_ip_end = obj_nat_ip
        nat_obj.update({'obj_name': nat_obj['obj_ip'] + nat_postfix})
    nat_obj.update({'obj_uid': nat_obj['obj_name']})                    
    ###### range handling


def set_ip_in_obj(nw_obj, ip): # add start and end ip in nw_obj if it is a range, otherwise do nothing
    if '-' in ip: # dealing with range
        ip_start, ip_end = ip.split('-')
        nw_obj.update({'obj_ip': str(ip_start) })
        if ip_end != ip_start:
            nw_obj.update({'obj_ip_end': str(ip_end) })
    else:
        nw_obj.update({'obj_ip': str(ip) })


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


def create_network_object(name, type, ip, ip_end, uid, color, comment, zone):
    # if zone is None or zone == '':
    #     zone = 'global'
    return {
        'obj_name': name,
        'obj_typ': type,
        'obj_ip': ip,
        'obj_ip_end': ip_end,
        'obj_uid': uid,
        'obj_color': color,
        'obj_comment': comment,
        'obj_zone': zone
    }


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
            if 'obj_type' in obj and obj['obj_type']=='group':
                if 'obj_member_refs' in obj and list_delimiter in obj['obj_member_refs']:
                    return get_first_ip_of_destination(obj['obj_member_refs'].split(list_delimiter)[0], config2import)
            elif 'obj_ip' in obj:
                return obj['obj_ip']
    logger.warning('src nat behind interface: found no IP info for destination object ' + obj_ref)
    return None

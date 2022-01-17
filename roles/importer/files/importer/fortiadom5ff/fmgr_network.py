import logging, ipaddress
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
sys.path.append(r"/usr/local/fworch/importer")
import common, fmgr_zone #, fwcommon


def normalize_nwobjects(full_config, config2import, import_id, nw_obj_types):
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
                obj.update({ 'obj_member_names' : common.list_delimiter.join(obj_orig['member']) })
                obj.update({ 'obj_member_refs' : common.resolve_objects(obj['obj_member_names'], common.list_delimiter, full_config, 'name', 'uuid')})
            elif 'startip' in obj_orig: # ippool object
                obj.update({ 'obj_typ': 'ip_range' })
                obj.update({ 'obj_ip': obj_orig['startip'] })
                obj.update({ 'obj_ip_end': obj_orig['endip'] })
            elif 'extip' in obj_orig: # vip object, simplifying to a single ip
                obj.update({ 'obj_typ': 'host' })
                if 'extip' not in obj_orig or len(obj_orig['extip']==0):
                    logging.error("normalizing network object vip (extip): found empty extip field!")
                else:
                    if len(obj_orig['extip']>1):
                        logging.warning("normalizing network object vip (extip): found more than one extip, just using the first one")
                    obj.update({ 'obj_ip': obj_orig['extip'][0] })

                # now dealing with the nat ip obj (mappedip)
                nat_obj = {}
                nat_obj.update({'obj_name': obj_orig['name'] + '_nat'})
                nat_obj.update({'obj_typ': 'host' })
                nat_obj.update({'obj_color': 'black'})
                nat_obj.update({'obj_comment': 'FWO-auto-generated nat object for VIP'})
                nat_obj.update({'uuid': nat_obj['name']})
                if 'mappedip' not in obj_orig or len(obj_orig['mappedip']==0):
                    logging.error("normalizing network object vip (extip): found empty mappedip field!")
                else:
                    if len(obj_orig['mappedip']>1):
                        logging.warning("normalizing network object vip (extip): found more than one mappedip, just using the first one")
                    nat_obj.update({ 'obj_ip': obj_orig['mappedip'][0] })
                if 'associated-interface' in obj_orig and len(obj_orig['associated-interface'])>0: # and obj_orig['associated-interface'][0] != 'any':
                    obj_zone = obj_orig['associated-interface'][0]
                nat_obj.update({'obj_zone': obj_zone })
                nw_objects.append(nat_obj)
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
                obj_zone = fmgr_zone.add_zone_if_missing (config2import, obj_zone, import_id)
            obj.update({'obj_zone': obj_zone })
            
            obj.update({'control_id': import_id})
            nw_objects.append(obj)

    # finally add "Original" network object for natting
    original_obj_name = 'Original'
    original_obj_uid = 'Original-UID'
    nw_objects.append(create_network_object(import_id=import_id, name=original_obj_name, type='network', ip='0.0.0.0/0',\
        uid=original_obj_uid, zone='global', color='black', comment='"original" network object created by FWO importer for NAT purposes'))

    config2import.update({'network_objects': nw_objects})


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
        obj_member_refs = group['obj_member_refs'].split(common.list_delimiter)
        for ref in obj_member_refs:
            member_name = resolve_nw_uid_to_name(ref, nw_objects)
            member_names += member_name + common.list_delimiter
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

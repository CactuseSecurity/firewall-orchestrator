import logging, ipaddress
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
# sys.path.append(importer_base_dir + '/fortimanager5ff')
sys.path.append(r"/usr/local/fworch/importer")
import common #, fwcommon


def normalize_nwobjects(full_config, config2import, import_id):
    nw_objects = []
    # 'obj_typ': obj_type, 'obj_ip': first_ip, 'obj_ip_end': last_ip,
    # 'obj_member_refs': member_refs, 'obj_member_names': member_names}])
    for obj_orig in full_config['network_objects']:
        obj = {}
        obj.update({ 'obj_typ': 'group' })  # setting default network obj type first
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
        if 'member' in obj_orig: # addrgrp4 / addrgrp6
            obj['obj_member_names'] = common.list_delimiter.join(obj_orig['member'])
            obj['obj_member_refs'] = common.resolve_objects(obj['obj_member_names'], common.list_delimiter, full_config['network_objects'], 'name', 'uuid')
        if 'fqdn' in obj_orig: # "fully qualified domain name address"
            obj.update({ 'obj_typ': 'network' })
            obj.update({ 'obj_ip': '0.0.0.0/0'})
        if 'comment' in obj_orig:
            obj.update({'obj_comment': obj_orig['comment']})
        if 'color' in obj_orig and obj_orig['color']==0:
            obj.update({'obj_color': 'black'})
            # todo: deal with all other colors (will be currently ignored)
            # we would need a list of fortinet color codes
        obj.update({'obj_uid': obj_orig['uuid']})
        obj.update({'control_id': import_id})
        nw_objects.append(obj)
        
        # todo: handle groups
        # if 'list' in obj_orig:
        # obj['obj_typ'] = 'group' })

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

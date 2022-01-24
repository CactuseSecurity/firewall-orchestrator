import logging
import common, fwcommon


def parse_network_objects_to_json(full_config, config2import, import_id):
    nw_objects = []

    for obj_table in full_config['object_tables']:
        collect_nw_objects(obj_table, nw_objects)
    for nw_obj in nw_objects:
        nw_obj.update({'control_id': import_id})
    for idx in range(0, len(nw_objects)-1):
        if nw_objects[idx]['obj_typ'] == 'group':
            add_member_names_for_nw_group(idx, nw_objects)
    config2import.update({'network_objects': nw_objects})
    

# collect_nw_objects from object tables and write them into global nw_objects dict
def collect_nw_objects(object_table, nw_objects):
    nw_obj_type_to_host_list = [
        'simple-gateway', 'simple-cluster', 'CpmiVsClusterNetobj', 'CpmiVsxClusterNetobj', 'CpmiVsxClusterMember', 'CpmiAnyObject', 
        'CpmiClusterMember', 'CpmiGatewayPlain', 'CpmiHostCkp', 'CpmiGatewayCluster', 'checkpoint-host' 
    ]

    if object_table['object_type'] in fwcommon.nw_obj_table_names:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                ip_addr = ''                
                member_refs = None
                member_names = None
                if 'members' in obj:
                    member_refs = ''
                    member_names = ''
                    for member in obj['members']:
                        member_refs += member + common.list_delimiter
                    member_refs = member_refs[:-1]
                    if obj['members'] == '':
                        obj['members'] = None
                ip_addr = fwcommon.get_ip_of_obj(obj)
                first_ip = ip_addr
                last_ip = ip_addr
                obj_type = obj['type']
                if obj_type=='group':
                    first_ip = None
                    last_ip = None

                if obj_type == 'address-range' or obj_type == 'multicast-address-range':
                    obj_type = 'ip_range'
                    # logging.debug("parse_network::collect_nw_objects - found range object '" + obj['name'] + "' with ip: " + ip_addr)
                    if '-' in ip_addr:
                        first_ip, last_ip = ip_addr.split('-')
                    else:
                        logging.warning("parse_network::collect_nw_objects - found range object '" + obj['name'] + "' without hyphen: " + ip_addr)
                elif (obj_type in nw_obj_type_to_host_list):
                    # logging.debug("parse_network::collect_nw_objects - rewriting non-standard cp-host-type '" + obj['name'] + "' with object type '" + obj_type + "' to host")
                    # logging.debug("obj_dump:" + json.dumps(obj, indent=3))
                    obj_type = 'host'
                # adding the object:
                if not 'comments' in obj or obj['comments']=='':
                    obj['comments'] = None
                nw_objects.extend([{'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                        'obj_comment': obj['comments'],
                                        'obj_typ': obj_type, 'obj_ip': first_ip, 'obj_ip_end': last_ip,
                                        'obj_member_refs': member_refs, 'obj_member_names': member_names}])


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

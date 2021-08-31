import sys
sys.path.append(r"/usr/local/fworch/importer")
import logging
import json
import common, cpcommon


def csv_dump_nw_obj(nw_obj, import_id):
    result_line =  common.csv_add_field(import_id)                  # control_id
    result_line += common.csv_add_field(nw_obj['obj_name'])         # obj_name
    result_line += common.csv_add_field(nw_obj['obj_typ'])          # ob_typ
    result_line += common.csv_add_field(nw_obj['obj_member_names']) # obj_member_names
    result_line += common.csv_add_field(nw_obj['obj_member_refs'])  # obj_member_refs
    result_line += common.csv_delimiter                             # obj_sw
    if nw_obj['obj_typ'] == 'group':
        result_line += common.csv_delimiter                         # obj_ip for groups = null
    else:
        result_line += common.csv_add_field(nw_obj['obj_ip'])       # obj_ip
    if 'obj_ip_end' in nw_obj:
        result_line += common.csv_add_field(nw_obj['obj_ip_end'])   # obj_ip_end
    else:
        result_line += common.csv_delimiter
    result_line += common.csv_add_field(nw_obj['obj_color'])        # obj_color
    result_line += common.csv_add_field(nw_obj['obj_comment'])      # obj_comment
    result_line += common.csv_delimiter                             # obj_location
    if 'obj_zone' in nw_obj:
        result_line += common.csv_add_field(nw_obj['obj_zone'])     # obj_zone
    else:
        result_line += common.csv_delimiter
    result_line += common.csv_add_field(nw_obj['obj_uid'])          # obj_uid
    result_line += common.csv_delimiter                             # last_change_admin
    # add last_change_time
    result_line += common.line_delimiter
    return result_line


# collect_nw_objects from object tables and write them into global nw_objects dict
def collect_nw_objects(object_table, nw_objects):
    nw_obj_type_to_host_list = [
        'simple-gateway', 'simple-cluster', 'CpmiVsClusterNetobj', 'CpmiVsxClusterNetobj', 'CpmiVsxClusterMember', 'CpmiAnyObject', 
        'CpmiClusterMember', 'CpmiGatewayPlain', 'CpmiHostCkp', 'CpmiGatewayCluster', 'checkpoint-host' 
    ]

    if object_table['object_type'] in cpcommon.nw_obj_table_names:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                members = ''
                ip_addr = ''
                member_refs = ''
                member_names = ''
                
                if 'members' in obj:
                    for member in obj['members']:
                        member_refs += member + common.list_delimiter
                    member_refs = member_refs[:-1]
                ip_addr = cpcommon.get_ip_of_obj(obj)
                first_ip = ip_addr
                last_ip = ip_addr
                obj_type = obj['type']

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
    return 'ERROR: uid ' + uid + ' not found'


def add_member_names_for_nw_group(idx, nw_objects):
    member_names = ''
    group = nw_objects.pop(idx)
    obj_member_refs = group['obj_member_refs'].split(common.list_delimiter)
    for ref in obj_member_refs:
        member_name = resolve_nw_uid_to_name(ref, nw_objects)
        # print ("found member of group " + group['obj_name'] + ": " + member_name)
        member_names += member_name + common.list_delimiter
    group['obj_member_names'] = member_names[:-1]
    nw_objects.insert(idx, group)

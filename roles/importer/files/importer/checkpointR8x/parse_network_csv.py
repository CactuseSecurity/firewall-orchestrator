from fwo_base import csv_add_field
from fwo_const import csv_delimiter, line_delimiter


def csv_dump_nw_obj(nw_obj, import_id):
    result_line =  csv_add_field(import_id)                  # control_id
    result_line += csv_add_field(nw_obj['obj_name'])         # obj_name
    result_line += csv_add_field(nw_obj['obj_typ'])          # ob_typ
    if nw_obj['obj_member_names'] != None:
        result_line += csv_add_field(nw_obj['obj_member_names']) # obj_member_names
    else:
        result_line += csv_delimiter                         # no obj_member_names
    if nw_obj['obj_member_refs'] != None:
        result_line += csv_add_field(nw_obj['obj_member_refs'])  # obj_member_refs
    else:
        result_line += csv_delimiter                         # no obj_member_refs
    result_line += csv_delimiter                             # obj_sw
    if nw_obj['obj_typ'] == 'group':
        result_line += csv_delimiter                         # obj_ip for groups = null
        result_line += csv_delimiter                         # obj_ip_end for groups = null
    else:
        result_line += csv_add_field(nw_obj['obj_ip'])       # obj_ip
        if 'obj_ip_end' in nw_obj:
           result_line += csv_add_field(nw_obj['obj_ip_end'])# obj_ip_end
        else:
           result_line += csv_delimiter
    result_line += csv_add_field(nw_obj['obj_color'])        # obj_color
    if nw_obj['obj_comment'] != None:
        result_line += csv_add_field(nw_obj['obj_comment'])  # obj_comment
    else:
        result_line += csv_delimiter                         # no obj_comment
    result_line += csv_delimiter                             # obj_location
    if 'obj_zone' in nw_obj:
        result_line += csv_add_field(nw_obj['obj_zone'])     # obj_zone
    else:
        result_line += csv_delimiter
    result_line += csv_add_field(nw_obj['obj_uid'])          # obj_uid
    result_line += csv_delimiter                             # last_change_admin
    # add last_change_time
    result_line += line_delimiter
    return result_line

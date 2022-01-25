import common


def csv_dump_svc_obj(svc_obj, import_id):
    result_line =  common.csv_add_field(import_id)                          # control_id
    result_line += common.csv_add_field(svc_obj['svc_name'])                # svc_name
    result_line += common.csv_add_field(svc_obj['svc_typ'])                 # svc_typ
    result_line += common.csv_delimiter                                     # no svc_prod_specific    
    if svc_obj['svc_member_names'] != None:
        result_line += common.csv_add_field(svc_obj['svc_member_names'])    # svc_member_names
    else:
        result_line += common.csv_delimiter                                 # no svc_member_names
    if svc_obj['svc_member_refs'] != None:
        result_line += common.csv_add_field(svc_obj['svc_member_refs'])     # obj_member_refs
    else:
        result_line += common.csv_delimiter                                 # no svc_member_refs
    result_line += common.csv_add_field(svc_obj['svc_color'])               # svc_color
    result_line += common.csv_add_field(svc_obj['ip_proto'])                # ip_proto
    if svc_obj['svc_port']!=None:
        result_line += str(svc_obj['svc_port']) + common.csv_delimiter      # svc_port
    else:
        result_line += common.csv_delimiter                                 # no svc_port    
    if svc_obj['svc_port_end']!=None:
        result_line += str(svc_obj['svc_port_end']) + common.csv_delimiter  # svc_port_end
    else:
        result_line += common.csv_delimiter                                 # no svc_port_end    
    if 'svc_source_port' in svc_obj:
        result_line += common.csv_add_field(svc_obj['svc_source_port'])     # svc_source_port
    else:
        result_line += common.csv_delimiter                                 # svc_source_port
    if 'svc_source_port_end' in svc_obj:
        result_line += common.csv_add_field(svc_obj['svc_source_port_end']) # svc_source_port_end
    else:
        result_line += common.csv_delimiter                                 # svc_source_port_end
    if 'svc_comment' in svc_obj and svc_obj['svc_comment'] != None:
        result_line += common.csv_add_field(svc_obj['svc_comment'])         # svc_comment
    else:
        result_line += common.csv_delimiter                                 # no svc_comment
    if 'rpc_nr' in svc_obj and svc_obj['rpc_nr'] != None:
        result_line += common.csv_add_field(str(svc_obj['rpc_nr']))         # rpc_nr
    else:
        result_line += common.csv_delimiter                                 # no rpc_nr
    if 'svc_timeout_std' in svc_obj:
        result_line += common.csv_add_field(svc_obj['svc_timeout_std'])     # svc_timeout_std
    else:
        result_line += common.csv_delimiter                                 # svc_timeout_std
    if 'svc_timeout' in svc_obj and svc_obj['svc_timeout']!="" and svc_obj['svc_timeout']!=None:
        result_line += common.csv_add_field(str(svc_obj['svc_timeout']))    # svc_timeout
    else:
        result_line += common.csv_delimiter                                 # svc_timeout null
    result_line += common.csv_add_field(svc_obj['svc_uid'])                 # svc_uid
    result_line += common.csv_delimiter                                     # last_change_admin
    result_line += common.line_delimiter                                    # last_change_time
    return result_line

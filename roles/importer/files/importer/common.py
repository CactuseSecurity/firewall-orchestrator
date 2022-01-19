import logging

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
apostrophe = "\""
section_header_uids=[]
# nat_postfix = '_nat_nw_obj'
nat_postfix = '_nat'


def set_log_level(log_level, debug_level):
    # todo: save the initial value, reset initial value at the end
    logger = logging.getLogger(__name__)
    # todo: use log_level to define non debug logging
    #       use debug_level to define different debug levels
    if debug_level >= 1:
        logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    else:
        logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
        # logging.basicConfig(level=logging.WARNING, format='%(asctime)s - %(levelname)s - %(message)s')
    # elif debug_level >= 2:
    #     logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
    #     logging.basicConfig(filename='/var/log/fworch/importer_ll.debug', filemode='a', level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
    # elif debug_level >= 3:
    #     logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    #     logging.basicConfig(filename='/var/log/fworch/importer_ll', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    logger.debug ("debug_level: "+ str(debug_level) )


def csv_add_field(content, no_csv_delimiter=False):
    if (content == None or content == '') and not no_csv_delimiter:  # do not add apostrophes for empty fields
        field_result = csv_delimiter
    else:
        # add apostrophes at beginning and end and remove any ocurrence of them within the string
        if (isinstance(content, str)):
            escaped_field = content.replace(apostrophe,"")
            field_result = apostrophe + escaped_field + apostrophe
        else:   # leave non-string values as is
            field_result = str(content)
        if not no_csv_delimiter:
            field_result += csv_delimiter
    return field_result
 

def sanitize(content):
    if content == None:
        return None
    result = str(content)
    result = result.replace(apostrophe,"")  # remove possibly contained apostrophe
    result = result.replace(line_delimiter," ")  # replace possibly contained CR with space
    #if result != '':  # do not add apostrophes for empty fields
    #    result = apostrophe + escaped_field + apostrophe
    return result


def extend_string_list(list_string, src_dict, key, delimiter):
    if list_string is None:
        list_string = ''
    if list_string == '':
        if key in src_dict:
            result = delimiter.join(src_dict[key])
        else:
            result = ''
    else:
        if key in src_dict:
            old_list = list_string.split(delimiter)
            combined_list = old_list + src_dict[key]
            result = delimiter.join(combined_list)
        else:
            result = list_string
    return result


# def resolve_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None):
#     ref_list = []
#     for el in obj_name_string_list.split(delimiter):
#         for obj in obj_dict:
#             if obj[name_key] == el:
#                 ref_list.append(obj[uid_key])
#                 break
#     return delimiter.join(ref_list)

def resolve_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key):
    # guessing ipv4 and adom (to also search global objects)
    return resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type='v4_adom', obj_type='network')


def resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None, obj_type='network'):
    ref_list = []
    for el in obj_name_string_list.split(delimiter):
        if rule_type is not None:
            if obj_type == 'network':
                if 'v4' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                elif 'v6' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
                elif 'v4' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address'], obj_dict['nw_obj_adom_firewall/addrgrp'], \
                        obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                elif 'v6' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address6'], obj_dict['nw_obj_adom_firewall/addrgrp6'], \
                        obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
                elif 'nat' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address'], obj_dict['nw_obj_adom_firewall/addrgrp'], \
                        obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                elif 'nat' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                break_flag = False # if we find a match we stop the two inner for-loops
                for tab in object_tables:
                    if break_flag:
                        break
                    else:
                        for obj in tab:
                            if obj[name_key] == el:
                                ref_list.append(obj[uid_key])
                                break_flag = True
                                break
            elif obj_type == 'service':
                print('later')  # todo
        else:
            print('decide what to do')
    return delimiter.join(ref_list)

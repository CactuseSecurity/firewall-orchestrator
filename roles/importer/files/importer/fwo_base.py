import json
import jsonpickle
from fwo_data_networking import InterfaceSerializable, RouteSerializable
import fwo_globals
from fwo_const import max_objs_per_chunk, csv_delimiter, apostrophe, line_delimiter
from fwo_log import getFwoLogger, getFwoAlertLogger
from copy import deepcopy


def split_list(list_in, max_list_length):
    if len(list_in)<max_list_length:
        return [list_in]
    else:
        list_of_lists = []
        i=0
        while i<len(list_in):
            last_element_in_chunk = min(len(list_in), i+max_list_length)
            list_of_lists.append(list_in[i:last_element_in_chunk])
            i += max_list_length
    return list_of_lists


# split the config into chunks of max size "max_objs_per_chunk" to avoid 
# timeout of import while writing data to import table
# each object table to import is handled here 
def split_config(config2import, current_import_id, mgm_id):
    conf_split_dict_of_lists = {}
    max_number_of_chunks = 0

    object_lists = ["network_objects", "service_objects", "user_objects", "rules", "zone_objects", "interfaces", "routing"]

    for obj_list_name in object_lists:
        if obj_list_name in config2import:

            if obj_list_name == 'interfaces':
                if_obj_list = config2import['interfaces']
                if_obj_list_ser = []
                for iface in if_obj_list:
                    if_obj_list_ser.append(InterfaceSerializable(iface))
                if_dict = json.loads(jsonpickle.encode(if_obj_list_ser, unpicklable=False))
                config2import['interfaces'] = if_dict

            if obj_list_name == 'routing':
                route_obj_list = config2import['routing']
                route_obj_list_ser = []
                for route in route_obj_list:
                    route_obj_list_ser.append(RouteSerializable(route))
                route_dict = json.loads(jsonpickle.encode(route_obj_list_ser, unpicklable=False))
                config2import['routing'] = route_dict
                
            split_list_tmp = split_list(config2import[obj_list_name], max_objs_per_chunk)
            conf_split_dict_of_lists.update({obj_list_name: split_list_tmp})
            if len(split_list_tmp)>max_number_of_chunks:
                max_number_of_chunks = len(split_list_tmp)
        else:
            conf_split_dict_of_lists.update({obj_list_name: []})
    conf_split = []
    current_chunk = 0
    while current_chunk<max_number_of_chunks:
        single_chunk = {}
        for obj_list_name in object_lists:
            single_chunk[obj_list_name] = []
        for obj_list_name in object_lists:
            if current_chunk<len(conf_split_dict_of_lists[obj_list_name]):
                single_chunk[obj_list_name] = conf_split_dict_of_lists[obj_list_name][current_chunk]

        conf_split.append(single_chunk)
        current_chunk += 1

    # now adding meta data around (start_import_flag used as trigger)
    config_split_with_metadata = []
    current_chunk_number = 0
    for conf_chunk in conf_split:
        config_split_with_metadata.append({
            "config": conf_chunk,
            "start_import_flag": False,
            "importId": int(current_import_id), 
            "mgmId": int(mgm_id), 
            "chunk_number": current_chunk_number
        })
        current_chunk_number += 1
    # setting the trigger in the last chunk:
    config_split_with_metadata[len(config_split_with_metadata)-1]["start_import_flag"] = True
    if fwo_globals.debug_level>0:
        config_split_with_metadata[len(config_split_with_metadata)-1]["debug_mode"] = True
    return config_split_with_metadata


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
    return result


def extend_string_list(list_string, src_dict, key, delimiter, jwt=None, import_id=None):
    if list_string is None:
        list_string = ''
    if list_string == '':
        if key in src_dict:
            result = delimiter.join(src_dict[key])
        else:
            result = ''
#            fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id, key)
    else:
        if key in src_dict:
            old_list = list_string.split(delimiter)
            combined_list = old_list + src_dict[key]
            result = delimiter.join(combined_list)
        else:
            result = list_string
#            fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id, key)
    return result


def jsonToLogFormat(jsonData):
    if type(jsonData) is dict:
        jsonString = json.dumps(jsonData)
    elif isinstance(jsonData, str):
        jsonString = jsonData
    else:
        jsonString = str(jsonData)
    
    if jsonString[0] == '{' and jsonString[-1] == '}':
        jsonString = jsonString[1:len(jsonString)-1]
    return jsonString


def writeAlertToLogFile(jsonData):
    logger = getFwoAlertLogger()
    jsonDataCopy = deepcopy(jsonData)   # make sure the original alert is not changed
    if type(jsonDataCopy) is dict and 'jsonData' in jsonDataCopy:
        subDict = json.loads(jsonDataCopy.pop('jsonData'))
        jsonDataCopy.update(subDict)
    alertText = "FWORCHAlert - " + jsonToLogFormat(jsonDataCopy)
    logger.info(alertText)


def set_ssl_verification(ssl_verification_mode):
    logger = getFwoLogger()
    if ssl_verification_mode == '' or ssl_verification_mode == 'off':
        ssl_verification = False
        if fwo_globals.debug_level>5:
            logger.debug("ssl_verification: False")
    else:
        ssl_verification = ssl_verification_mode
        if fwo_globals.debug_level>5:
            logger.debug("ssl_verification: [ca]certfile=" + ssl_verification)
    return ssl_verification

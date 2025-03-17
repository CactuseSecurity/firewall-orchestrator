import json
from copy import deepcopy
import re
from enum import Enum
from typing import Any, get_type_hints
import ipaddress 

import fwo_globals
from fwo_const import csv_delimiter, apostrophe, line_delimiter
from fwo_log import getFwoLogger, getFwoAlertLogger


class ConfigAction(Enum):
    INSERT = 'INSERT'
    UPDATE = 'UPDATE'
    DELETE = 'DELETE'

class ConfFormat(Enum):
    # NORMALIZED = auto()
    NORMALIZED = 'NORMALIZED'
    
    CHECKPOINT = 'CHECKPOINT'
    FORTINET = 'FORTINET'
    PALOALTO = 'PALOALTO'
    CISCOFIREPOWER = 'CISCOFIREPOWER'

    NORMALIZED_LEGACY = 'NORMALIZED_LEGACY'

    CHECKPOINT_LEGACY = 'CHECKPOINT_LEGACY'
    FORTINET_LEGACY = 'FORTINET_LEGACY'
    PALOALTO_LEGACY = 'PALOALTO_LEGACY'
    CISCOFIREPOWER_LEGACY = 'CISCOFIREPOWER_LEGACY'

    @staticmethod
    def IsLegacyConfigFormat(confFormatString):
        return ConfFormat(confFormatString) in [ConfFormat.NORMALIZED_LEGACY, ConfFormat.CHECKPOINT_LEGACY, 
                                    ConfFormat.CISCOFIREPOWER_LEGACY, ConfFormat.FORTINET_LEGACY, 
                                    ConfFormat.PALOALTO_LEGACY]


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


def stringIsUri(s):
    return re.match('http://.+', s) or re.match('https://.+', s) or  re.match('file://.+', s)


def serializeDictToClass(data: dict, cls):
    # Unpack the dictionary into keyword arguments
    return cls(**data)


def serializeDictToClassRecursively(data: dict, cls: Any) -> Any:
    try:
        init_args = {}
        type_hints = get_type_hints(cls)

        if type_hints == {}:
            raise ValueError(f"no type hints found, assuming dict '{str(cls)}")

        for field, field_type in type_hints.items():

            if field in data:
                value = data[field]

                # Handle list types
                if hasattr(field_type, '__origin__') and field_type.__origin__ == list:
                    inner_type = field_type.__args__[0]
                    if isinstance(value, list):
                        init_args[field] = [
                            serializeDictToClassRecursively(item, inner_type) if isinstance(item, dict) else item
                            for item in value
                        ]
                    else:
                        raise ValueError(f"Expected a list for field '{field}', but got {type(value).__name__}")

                # Handle dictionary (nested objects)
                elif isinstance(value, dict):
                    init_args[field] = serializeDictToClassRecursively(value, field_type)

                # Handle Enum types
                elif isinstance(field_type, type) and issubclass(field_type, Enum):
                    init_args[field] = field_type[value]

                # Direct assignment for basic types
                else:
                    init_args[field] = value

        # Create an instance of the class with the collected arguments
        return cls(**init_args)

    except (TypeError, ValueError, KeyError) as e:
        # If an error occurs, return the original dictionary as is
        return data


def oldSerializeDictToClassRecursively(data: dict, cls: Any) -> Any:
    # Create an empty dictionary to store keyword arguments
    init_args = {}

    # Get the class's type hints (this is a safer way to access annotations)
    type_hints = get_type_hints(cls)

    # Iterate over the class fields
    for field, field_type in type_hints.items():
        if field in data:
            if hasattr(field_type, '__origin__') and field_type.__origin__ == list:
                # Handle list types
                inner_type = field_type.__args__[0]
                init_args[field] = [
                    serializeDictToClassRecursively(item, inner_type) if isinstance(item, dict) else item
                    for item in data[field]
                ]
            elif isinstance(data[field], dict):
                # Recursively convert nested dictionaries into the appropriate class
                init_args[field] = serializeDictToClassRecursively(data[field], field_type)
            else:
                # Directly assign the value if it's not a dict
                init_args[field] = data[field]

    # Create an instance of the class with the collected arguments
    return cls(**init_args)


def deserializeClassToDictRecursively(obj: Any, seen=None) -> Any:
    if seen is None:
        seen = set()

    # Handle simple immutable types directly (int, float, bool, str) and None
    if obj is None or isinstance(obj, (int, float, bool, str, ConfFormat, ConfigAction)):
        return obj

    # Check for circular references
    if id(obj) in seen:
        return f"<Circular reference to {obj.__class__.__name__}>"
    
    seen.add(id(obj))

    if isinstance(obj, list):
        # If the object is a list, deserialize each item
        return [deserializeClassToDictRecursively(item, seen) for item in obj]
    elif isinstance(obj, dict):
        # If the object is a dictionary, deserialize each key-value pair
        return {key: deserializeClassToDictRecursively(value, seen) for key, value in obj.items()}
    elif isinstance(obj, Enum):
        # If the object is an Enum, convert it to its value
        return obj.value
    elif hasattr(obj, '__dict__'):
        # If the object is a class instance, deserialize its attributes
        return {
            key: deserializeClassToDictRecursively(value, seen)
            for key, value in obj.__dict__.items()
            if not callable(value) and not key.startswith('__')
        }
    else:
        # For other types, return the value as is
        return obj


def cidrToRange(ip):
    logger = getFwoLogger()

    if isinstance(ip, str):
        # dealing with ranges:
        if '-' in ip:
            return '-'.split(ip)

        ipVersion = validIPAddress(ip)
        if ipVersion=='Invalid':
            logger.warning("error while decoding ip '" + ip + "'")
            return [ip]
        elif ipVersion=='IPv4':
            net = ipaddress.IPv4Network(ip)
        elif ipVersion=='IPv6':
            net = ipaddress.IPv6Network(ip)    
        return [str(net.network_address), str(net.broadcast_address)]
            
    return [ip]


def validIPAddress(IP: str) -> str: 
    try: 
        t = type(ipaddress.ip_address(IP))
        if t is ipaddress.IPv4Address:
            return "IPv4"
        elif t is ipaddress.IPv6Address:
            return "IPv6"
        else:
            return 'Invalid'
    except:
        try:
            t = type(ipaddress.ip_network(IP))
            if t is ipaddress.IPv4Network:
                return "IPv4"
            elif t is ipaddress.IPv6Network:
                return "IPv6"
            else:
                return 'Invalid'        
        except:
            return "Invalid"


def validate_ip_address(address):
    try:
        # ipaddress.ip_address(address)
        ipaddress.ip_network(address)
        return True
        # print("IP address {} is valid. The object returned is {}".format(address, ip))
    except ValueError:
        return False
        # print("IP address {} is not valid".format(address)) 

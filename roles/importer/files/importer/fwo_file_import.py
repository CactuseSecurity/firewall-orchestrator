"""
    read config from file and convert to non-legacy format (in case of legacy input)
"""
import json, requests
from typing import Any

from fwo_log import FWOLogger
import fwo_globals
from fwo_exceptions import ConfigFileNotFound, FwoImporterError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from fwconfig_base import ConfFormat

import traceback
from model_controllers.import_state_controller import ImportStateController

"""
    supported input formats:

    1) legacy normalized old: 

    {
        "network_objects": [x,y],
        "service_objects": [a,b,c],
        ...
        "rules": [x,y,z]
    }

    2) normalized (new from v9 onwards) --> dicts with uid as id

    {
        "ConfigFormat": "NORMALIZED",
        "managers": [
            {
            "ManagerUid": "6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2",
            "ManagerName": "MGM NAME",
            "IsGlobal": false,
            "Configs": {
                "action": "INSERT",
                "network_objects": [
                    {

                    }
                }
            }
        ]
    }

    3) native legacy formats

    these will we wrapped with the following:

    TODO: need to detect native format from file
    
    {
        "ConfigFormat": "<NATIVE_FORMAT>_LEGACY",
        "config": configJson
    }
        
    output formats:

    a) NORMALIZED:

    check point
    {
        "users": {},
        "object_tables": [
            {
            "object_type": "hosts",
            "object_chunks": [
                {
                "objects": [
    }
    

"""


def read_json_config_from_file(import_state: ImportStateController) -> FwConfigManagerListController:

    config_json = read_file(import_state)

    # try to convert normalized config from file to config object
    try:
        manager_list = FwConfigManagerListController(**config_json)
        if len(manager_list.ManagerSet)==0:
            FWOLogger.warning(f'read a config file without manager sets from {import_state.import_file_name}, trying native config')
            manager_list.native_config = config_json
            manager_list.ConfigFormat = detect_legacy_format(config_json)
        return manager_list
    except Exception: # legacy stuff from here
        FWOLogger.info(f"could not serialize config {str(traceback.format_exc())}")
        raise FwoImporterError(f"could not serialize config {import_state.import_file_name} - trying legacy formats")


def detect_legacy_format(config_json: dict[str, Any]) -> ConfFormat:

    result = ConfFormat.NORMALIZED_LEGACY

    if 'object_tables' in config_json:
        result = ConfFormat.CHECKPOINT_LEGACY
    elif 'domains' in config_json:
        result = ConfFormat.FORTIMANAGER

    return result


def read_file(import_state: ImportStateController) -> dict[str, Any]:
    config_json: dict[str, Any] = {}
    if import_state.import_file_name=="":
        return config_json
    try:
        if import_state.import_file_name.startswith('http://') or import_state.import_file_name.startswith('https://'):   # get conf file via http(s)
            session = requests.Session()
            session.headers = { 'Content-Type': 'application/json' }
            session.verify=fwo_globals.verify_certs
            r = session.get(import_state.import_file_name, )
            if r.ok:
                return json.loads(r.text)
            else:
                r.raise_for_status()
        else:   # reading from local file
            if import_state.import_file_name.startswith('file://'):   # remove file uri identifier
                filename = import_state.import_file_name[7:]
            else:
                filename = import_state.import_file_name
            with open(filename, 'r') as json_file:
                config_json = json.load(json_file)
    except requests.exceptions.RequestException:
        try:
            r # check if response "r" is defined # type: ignore TODO: This practice is suspicious at best
            import_state.appendErrorString(f'got HTTP status code{str(r.status_code)} while trying to read config file from URL {import_state.import_file_name}') # type: ignore
        except NameError:
            import_state.appendErrorString(f'got error while trying to read config file from URL {import_state.import_file_name}')
        import_state.increaseErrorCounterByOne()

        import_state.api_call.complete_import(import_state)
        raise ConfigFileNotFound(import_state.get_error_string()) from None
    except Exception: 
        import_state.appendErrorString(f"Could not read config file {import_state.import_file_name}")
        import_state.increaseErrorCounterByOne()
        FWOLogger.error("unspecified error while reading config file: " + str(traceback.format_exc()))
        import_state.api_call.complete_import(import_state)
        raise ConfigFileNotFound(f"unspecified error while reading config file {import_state.import_file_name}")

    return config_json


def handle_error_on_config_file_serialization(importState: ImportStateController, exception: Exception):
    importState.appendErrorString(f"Could not understand config file format in file {importState.import_file_name}")
    importState.increaseErrorCounterByOne()
    importState.api_call.complete_import(importState)
    FWOLogger.error(f"unspecified error while trying to serialize config file {importState.import_file_name}: {str(traceback.format_exc())}")
    raise FwoImporterError from exception


# def serialize_dict_to_class_recursively(data: dict, cls: Any) -> Any:
#     try:
#         init_args = {}
#         type_hints = get_type_hints(cls)

#         if type_hints == {}:
#             raise ValueError(f"no type hints found, assuming dict '{str(cls)}")

#         for field, field_type in type_hints.items():

#             if field not in data:
#                 continue

#             value = data[field]

#             # Handle list types
#             if hasattr(field_type, '__origin__') and field_type.__origin__ == list:
#                 inner_type = field_type.__args__[0]
#                 if isinstance(value, list):
#                     init_args[field] = [
#                         serialize_dict_to_class_recursively(item, inner_type) if isinstance(item, dict) else item
#                         for item in value
#                     ]
#                 else:
#                     raise ValueError(f"Expected a list for field '{field}', but got {type(value).__name__}")

#             # Handle dictionary (nested objects)
#             elif isinstance(value, dict):
#                 init_args[field] = serialize_dict_to_class_recursively(value, field_type)

#             # Handle Enum types
#             elif isinstance(field_type, type) and issubclass(field_type, Enum):
#                 init_args[field] = field_type[value]

#             # Direct assignment for basic types
#             else:
#                 init_args[field] = value

#         # Create an instance of the class with the collected arguments
#         return cls(**init_args)

#     except (TypeError, ValueError, KeyError) as e:
#         # If an error occurs, return the original dictionary as is
#         return data

#     except Exception:
#         raise


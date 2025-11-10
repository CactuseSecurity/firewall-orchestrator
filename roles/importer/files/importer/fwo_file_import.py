"""
    read config from file and convert to non-legacy format (in case of legacy input)
"""
import json, requests
from typing import Any

from fwo_log import getFwoLogger
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


################# MAIN FUNC #########################
def read_json_config_from_file(importState: ImportStateController) -> FwConfigManagerListController:

    configJson = read_file(importState)
    logger = getFwoLogger(debug_level=importState.DebugLevel)

    # try to convert normalized config from file to config object
    try:
        managerList = FwConfigManagerListController(**configJson)
        if len(managerList.ManagerSet)==0:
            logger.warning(f'read a config file without manager sets from {importState.ImportFileName}, trying native config')
            managerList.native_config = configJson
            managerList.ConfigFormat = detect_legacy_format(configJson)
        return managerList
    except Exception: # legacy stuff from here
        logger.info(f"could not serialize config {str(traceback.format_exc())}")
        raise FwoImporterError(f"could not serialize config {importState.ImportFileName} - trying legacy formats")


########### HELPERS ##################

def detect_legacy_format(configJson: dict[str, Any]) -> ConfFormat:

    result = ConfFormat.NORMALIZED_LEGACY

    if 'object_tables' in configJson:
        result = ConfFormat.CHECKPOINT_LEGACY
    elif 'domains' in configJson:
        result = ConfFormat.FORTIMANAGER

    return result


def read_file(importState: ImportStateController) -> dict[str, Any]:
    logger = getFwoLogger(debug_level=importState.DebugLevel)
    configJson: dict[str, Any] = {}
    if importState.ImportFileName=="":
        return configJson
    try:
        if importState.ImportFileName.startswith('http://') or importState.ImportFileName.startswith('https://'):   # get conf file via http(s)
            session = requests.Session()
            session.headers = { 'Content-Type': 'application/json' }
            session.verify=fwo_globals.verify_certs
            r = session.get(importState.ImportFileName, )
            if r.ok:
                return json.loads(r.text)
            else:
                r.raise_for_status()
        else:   # reading from local file
            if importState.ImportFileName.startswith('file://'):   # remove file uri identifier
                filename = importState.ImportFileName[7:]
            else:
                filename = importState.ImportFileName
            with open(filename, 'r') as json_file:
                configJson = json.load(json_file)
    except requests.exceptions.RequestException:
        try:
            r # check if response "r" is defined # type: ignore TODO: This practice is suspicious at best
            importState.appendErrorString(f'got HTTP status code{str(r.status_code)} while trying to read config file from URL {importState.ImportFileName}') # type: ignore
        except NameError:
            importState.appendErrorString(f'got error while trying to read config file from URL {importState.ImportFileName}')
        importState.increaseErrorCounterByOne()

        importState.api_call.complete_import(importState)
        raise ConfigFileNotFound(importState.getErrorString()) from None
    except Exception: 
        importState.appendErrorString(f"Could not read config file {importState.ImportFileName}")
        importState.increaseErrorCounterByOne()
        logger.error("unspecified error while reading config file: " + str(traceback.format_exc()))
        importState.api_call.complete_import(importState)
        raise ConfigFileNotFound(f"unspecified error while reading config file {importState.ImportFileName}")

    return configJson


def handle_error_on_config_file_serialization(importState: ImportStateController, exception: Exception):
    logger = getFwoLogger(debug_level=importState.DebugLevel)
    importState.appendErrorString(f"Could not understand config file format in file {importState.ImportFileName}")
    importState.increaseErrorCounterByOne()
    importState.api_call.complete_import(importState)
    logger.error(f"unspecified error while trying to serialize config file {importState.ImportFileName}: {str(traceback.format_exc())}")
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


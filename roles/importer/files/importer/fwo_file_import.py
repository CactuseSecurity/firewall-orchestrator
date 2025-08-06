"""
    read config from file and convert to non-legacy format (in case of legacy input)
"""
from typing import Any, get_type_hints
from enum import Enum
import json, requests, requests.packages

from fwo_log import getFwoLogger
import fwo_globals
from fwo_exceptions import ConfigFileNotFound, FwoImporterError
from models.fwconfigmanagerlist import FwConfigManagerList
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfig import FwConfig
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
def read_json_config_from_file(importState: ImportStateController) -> FwConfigManagerList:

    configJson = readFile(importState)
    logger = getFwoLogger(debug_level=importState.DebugLevel)

    # try to convert normalized config from file to config object
    try:
        managerList = FwConfigManagerListController(**configJson)
        if len(managerList.ManagerSet)==0:
            logger.warning(f'read a config file without manager sets from {importState.ImportFileName}, trying native config')
            managerList.native_config = configJson
            managerList.ConfigFormat = detectLegacyFormat(importState, configJson)
        return managerList
    except Exception: # legacy stuff from here
        logger.info(f"could not serialize config {str(traceback.format_exc())}")
        raise FwoImporterError(f"could not serialize config {importState.ImportFileName} - trying legacy formats")


########### HELPERS ##################

def detectLegacyFormat(importState, configJson) -> ConfFormat:

    result = ConfFormat.NORMALIZED_LEGACY

    if 'object_tables' in configJson:
        result = ConfFormat.CHECKPOINT_LEGACY
    elif 'domains' in configJson:
        result = ConfFormat.FORTIMANAGER

    return result


def readFile(importState: ImportStateController) -> dict:
    logger = getFwoLogger(debug_level=importState.DebugLevel)
    try:
        if importState.ImportFileName is not None:
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
            r # check if response "r" is defined
            importState.appendErrorString(f'got HTTP status code{str(r.status_code)} while trying to read config file from URL {importState.ImportFileName}')
        except NameError:
            importState.appendErrorString(f'got error while trying to read config file from URL {importState.ImportFileName}')
        importState.increaseErrorCounterByOne()
        # api_call = FwoApiCall(FwoApi(ApiUri=importState.api_connection.FwoApiUri, Jwt=jwt))

        importState.api_connection.complete_import(importState)
        raise ConfigFileNotFound(importState.ErrorString) from None
    except Exception: 
        importState.appendErrorString(f"Could not read config file {importState.ImportFileName}")
        importState.increaseErrorCounterByOne()
        logger.error("unspecified error while reading config file: " + str(traceback.format_exc()))
        complete_import(importState)
        raise Exception(f"unspecified error while reading config file {importState.ImportFileName}")

    return configJson


def handleErrorOnConfigFileSerialization(importState: ImportStateController, exception: Exception):
    logger = getFwoLogger(debug_level=importState.DebugLevel)
    importState.appendErrorString(f"Could not understand config file format in file {importState.ImportFileName}")
    importState.increaseErrorCounterByOne()
    complete_import(importState)
    logger.error(f"unspecified error while trying to serialize config file {importState.ImportFileName}: {str(traceback.format_exc())}")
    raise exception


def replaceOldIdsInLegacyFormats(importState: ImportStateController, config):

    # when we read from a normalized config file, it contains non-matching import ids, so updating them
    # for native configs this function should do nothing
    def replace_import_id(config, current_import_id):
        for tab in ['network_objects', 'service_objects', 'user_objects', 'zone_objects', 'rules']:
            if tab in config:
                for item in config[tab]:
                    if 'control_id' in item:
                        item['control_id'] = current_import_id
            else: # assuming native config is read
                pass


    # when we read from a normalized config file, it contains non-matching dev_ids in gw_ tables
    def replace_device_id(config, mgm_details):
        logger = getFwoLogger()

        if isinstance(config, FwConfig):
            config = config.Config
        if 'routing' in config or 'interfaces' in config:
            if len(mgm_details['devices'])>1:
                logger.warning('importing from config file with more than one device - just picking the first device at random')
            if len(mgm_details['devices'])>=1:
                # just picking the first device
                dev_id = mgm_details['devices'][0]['id']
                if 'routing' in config:
                    i=0
                    while i<len(config['routing']):
                        config['routing'][i]['routing_device'] = dev_id
                        i += 1
                if 'interfaces' in config:
                    i=0
                    while i<len(config['interfaces']):
                        config['interfaces'][i]['routing_device'] = dev_id
                        i += 1    

    if (isinstance(config, FwConfigManagerList)):
        replace_device_id(config.Config, importState.MgmDetails)
        if config.ConfigFormat == ConfFormat.NORMALIZED:
            # before importing from normalized config file, we need to replace the import id:
            if importState.ImportFileName is not None:
                replace_import_id(config.Config, importState.ImportId)
    else:   # assuming legacy normalized config
        replace_device_id(config, importState.MgmDetails)
        if isinstance(config, FwConfig):
            if importState.ImportFileName is not None and 'network_objects' in config.Config:
                # we have read normalized config from file
                replace_import_id(config.Config, importState.ImportId)
        else:
            if importState.ImportFileName is not None and 'network_objects' in config:
                # we have read normalized config from file
                replace_import_id(config, importState.ImportId)

    return config

def addWrapperForLegacyConfig(confFormat: ConfFormat, config: dict) -> dict:
    return {
        "ConfigFormat": str(confFormat),
        "config": config
    }


def convertFromLegacyNormalizedToNormalized(importState: ImportStateController, configJson: dict):
    logger = getFwoLogger(debug_level=importState.DebugLevel)
    
    logger.info("assuming legacy normalized config")

    try:
        configResult = FwConfig.fromJson(configJson)
    except Exception:
        handleErrorOnConfigFileSerialization(importState, exception=Exception)
    
    return configResult



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


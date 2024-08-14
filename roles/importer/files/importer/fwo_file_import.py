"""
    read config from file and convert to non-legacy format (in case of legacy input)
"""

import json, requests, requests.packages
from fwo_log import getFwoLogger
import fwo_globals
from fwo_exception import ConfigFileNotFound
from fwo_api import complete_import
from fwconfig import FwConfig, FwConfigManagerList, ConfFormat
import traceback
from fwoBaseImport import ImportState
from fwo_base import serializeDictToClassRecursively

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
def readJsonConfigFromFile(importState: ImportState) -> FwConfig:
    configJson = readFile(importState)
    config = None

    # now try to convert to config object
    try:
        configFwConfigManagerList = serializeDictToClassRecursively(configJson, FwConfigManagerList)
        return configFwConfigManagerList
    except: # legacy stuff from here
        logger = getFwoLogger()
        logger.info(f"could not serialize config {str(traceback.format_exc())}")
        if 'ConfigFormat' in configJson:
            if configJson['ConfigFormat']=='NORMALIZED':
                try:
                    config = FwConfigManagerList.FromJson(configJson)
                    # FwConfigManagerList.ConvertFromLegacyNormalizedConfig(configJson, importState.MgmDetails)
                except Exception: 
                    handleErrorOnConfigFileSerialization(importState, exception=Exception)
            elif configJson['ConfigFormat']=='NORMALIZED_LEGACY':
                try:
                    config = FwConfig.fromJson(config)
                except Exception: 
                    handleErrorOnConfigFileSerialization(importState, exception=Exception)
        else:
            addWrapperForLegacyConfig(configJson, detectLegacyFormat(importState, configJson))

            config = convertFromLegacyNormalizedToNormalized(importState, configJson)

        # IsLegacyConfigFormat(string)
        if config.IsLegacy():
            config = replaceOldIdsInLegacyFormats(importState, config)

    return config

########### HELPERS ##################

def detectLegacyFormat(importState, configJson) -> ConfFormat:

    result = ConfFormat.NORMALIZED_LEGACY

    if 'object_tables' in configJson:
        result = ConfFormat.CHECKPOINT_LEGACY
    # elif ...

    return result


def readFile(importState: ImportState) -> dict:
    logger = getFwoLogger()
    try:
        if importState.ImportFileName is not None:
            if importState.ImportFileName.startswith('http://') or importState.ImportFileName.startswith('https://'):   # get conf file via http(s)
                session = requests.Session()
                session.headers = { 'Content-Type': 'application/json' }
                session.verify=fwo_globals.verify_certs
                r = session.get(importState.ImportFileName, )
                r.raise_for_status()
            else:   # reading from local file
                if importState.ImportFileName.startswith('file://'):   # remove file uri identifier
                    filename = importState.ImportFileName[7:]
                with open(filename, 'r') as json_file:
                    configJson = json.load(json_file)
    except requests.exceptions.RequestException:
        try:
            r # check if response "r" is defined
            importState.setErrorString(f'got HTTP status code{str(r.status_code)} while trying to read config file from URL {importState.ImportFileName}')
        except NameError:
            importState.setErrorString(f'got error while trying to read config file from URL {importState.ImportFileName}')
        importState.setErrorCounter(importState.ErrorCount+1)
        complete_import(importState)
        raise ConfigFileNotFound(importState.ErrorString) from None
    except Exception: 
        importState.setErrorString(f"Could not read config file {importState.ImportFileName}")
        importState.setErrorCounter(importState.ErrorCount+1)
        logger.error("unspecified error while reading config file: " + str(traceback.format_exc()))
        raise 

    return configJson


def handleErrorOnConfigFileSerialization(importState: ImportState, exception: Exception):
    logger = getFwoLogger()
    importState.setErrorString(f"Could not understand config file format in file {importState.ImportFileName}")
    importState.setErrorCounter(importState.ErrorCount+1)
    complete_import(importState)
    logger.error(f"unspecified error while trying to serialize config file {importState.ImportFileName}: {str(traceback.format_exc())}")
    raise exception


def replaceOldIdsInLegacyFormats(importState: ImportState, config):

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


def convertFromLegacyNormalizedToNormalized(importState: ImportState, configJson: dict):
    logger = getFwoLogger()
    
    logger.info("assuming legacy normalized config")

    try:
        configResult = FwConfig.fromJson(configJson)
    except Exception:
        handleErrorOnConfigFileSerialization(importState, exception=Exception)
    
    return configResult

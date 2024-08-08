import json, requests, requests.packages
from fwo_log import getFwoLogger
import fwo_globals
from fwo_exception import ConfigFileNotFound
from fwo_api import complete_import
from fwconfig import FwConfig, FwConfigManagerList, ConfFormat
import traceback
from fwoBaseImport import ImportState


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


def readJsonConfigFromFile(importState: ImportState) -> FwConfig:
    logger = getFwoLogger()

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


    try:
        if importState.ImportFileName is not None:
            if importState.ImportFileName.startswith('http://') or importState.ImportFileName.startswith('https://'):   # get conf file via http(s)
                session = requests.Session()
                session.headers = { 'Content-Type': 'application/json' }
                session.verify=fwo_globals.verify_certs
                r = session.get(importState.ImportFileName, )
                r.raise_for_status()
                config = json.loads(r.content)
            else:   # reading from local file
                if importState.ImportFileName.startswith('file://'):   # remove file uri identifier
                    filename = importState.ImportFileName[7:]
                with open(filename, 'r') as json_file:
                    configJson = json.load(json_file)
                    config = FwConfig.fromJson(configJson)
    except requests.exceptions.RequestException:
        try:
            # check if response "r" is defined:
            r
            
            importState.setErrorString('got HTTP status code{code} while trying to read config file from URL {filename}'.format(code=str(r.status_code), filename=filename))
        except NameError:
            importState.setErrorString('got error while trying to read config file from URL {filename}'.format(filename=filename))
        importState.setErrorCounter(importState.ErrorCount+1)
        complete_import(importState)
        raise ConfigFileNotFound(importState.ErrorString) from None
    except Exception: 
        # logger.exception("import_management - error while reading json import from file", traceback.format_exc())
        importState.setErrorString("Could not read config file {filename}".format(filename=filename))
        importState.setErrorCounter(importState.ErrorCount+1)
        complete_import(importState)
        # raise ConfigFileNotFound(importState.ErrorString) from None
        logger.error("unspecified error while reading config file: " + str(traceback.format_exc()))

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

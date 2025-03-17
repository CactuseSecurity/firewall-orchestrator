import json, requests, requests.packages
from fwo_log import getFwoLogger
import fwo_globals
from fwo_exception import ConfigFileNotFound
from fwo_api import complete_import


def readJsonConfigFromFile(importState, config):

    # when we read from a normalized config file, it contains non-matching dev_ids in gw_ tables
    def replace_device_id(config, mgm_details):
        logger = getFwoLogger()
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
                    config = json.load(json_file)
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
    except:
        # logger.exception("import_management - error while reading json import from file", traceback.format_exc())
        importState.setErrorString("Could not read config file {filename}".format(filename=filename))
        importState.setErrorCounter(importState.ErrorCount+1)
        complete_import(importState)
        raise ConfigFileNotFound(importState.ErrorString) from None
    
    replace_device_id(config, importState.MgmDetails)

    return config

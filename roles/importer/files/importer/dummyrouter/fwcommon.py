import sys
from common import importer_base_dir, ConfigFileNotFound, complete_import
sys.path.append(importer_base_dir + '/dummyrouter')
from curses import raw
from fwo_log import getFwoLogger
import fwo_globals
from fwo_data_networking import Interface, Route, getRouteDestination
import json, requests, requests.packages
from datetime import datetime
import jsonpickle

def has_config_changed(_, __, ___):
    return True


def get_config(config2import, _, current_import_id, mgm_details, limit=100, force=False, jwt=''):
    router_file_url = mgm_details['configPath']
    error_count = 0
    change_count = 0
    start_time=datetime.now()
    error_string = ''

    if len(mgm_details['devices'])!=1:
        logger = getFwoLogger()
        logger.error('expected exactly one device but found: ' + str(mgm_details['devices']))
        exit(1)
    dev_id = mgm_details['devices'][0]['id'] 
    
    try:
        session = requests.Session()
        #session.headers = { 'Content-Type': 'application/json' }
        session.verify=fwo_globals.verify_certs
        r = session.get(router_file_url, )
        r.raise_for_status()
        cfg = json.loads(r.content)

    except requests.exceptions.RequestException:
        error_string = "got HTTP status code" + str(r.status_code) + " while trying to read config file from URL " + router_file_url
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise ConfigFileNotFound(error_string) from None
    except:
        error_string = "Could not read config file " + router_file_url
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise ConfigFileNotFound(error_string) from None

    # deserialize network info from json into objects

    # device_id, name, ip, netmask_bits, state_up=True, ip_version=4
    ifaces = []
    for iface in cfg['interfaces']:
        ifaces.append(Interface(dev_id, iface['name'], iface['ip'], iface['netmask_bits'], state_up=iface['state_up'], ip_version=iface['ip_version']))
    cfg['interfaces'] = ifaces

    # device_id, target_gateway, destination, static=True, source=None, interface=None, metric=None, distance=None, ip_version=4
    routes = []
    for route in cfg['routing']:
        routes.append(Route(dev_id, route['target_gateway'], route['destination'], static=route['static'], interface=route['interface'], metric=route['metric'], distance=route['distance'], ip_version=route['ip_version']))
    cfg['routing'] = routes

    cfg['routing'].sort(key=getRouteDestination,reverse=True)

    config2import.update({'interfaces': cfg['interfaces'], 'routing': cfg['routing']})

    return 0

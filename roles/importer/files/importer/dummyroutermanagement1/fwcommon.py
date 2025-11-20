from typing import Any
from fwo_log import get_fwo_logger
import fwo_globals
from model_controllers.interface_controller import Interface
from model_controllers.route_controller import Route, get_route_destination
import json, requests
from datetime import datetime
from fwo_exceptions import ConfigFileNotFound

def has_config_changed(_, __, ___):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import: dict[str, Any], current_import_id: str, mgm_details: dict[str, Any], jwt: str=''):
    router_file_url = mgm_details['configPath']
    error_count = 0
    change_count = 0
    start_time=datetime.now()
    error_string = ''

    if len(mgm_details['devices'])!=1:
        logger = get_fwo_logger()
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

    except requests.exceptions.RequestException as e:
        error_string = "got HTTP status code" + str(e.response.status_code if e.response else None) + " while trying to read config file from URL " + router_file_url
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt) # type: ignore # TODO: function does not exist
        raise ConfigFileNotFound(error_string) from None
    except Exception:
        error_string = "Could not read config file " + router_file_url
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt) # type: ignore # TODO: function does not exist
        raise ConfigFileNotFound(error_string) from None

    # deserialize network info from json into objects

    # device_id, name, ip, netmask_bits, state_up=True, ip_version=4
    ifaces: list[Interface] = []
    for iface in cfg['interfaces']:
        ifaces.append(Interface(dev_id, iface['name'], iface['ip'], iface['netmask_bits'], state_up=iface['state_up'], ip_version=iface['ip_version']))
    cfg['interfaces'] = ifaces

    # device_id, target_gateway, destination, static=True, source=None, interface=None, metric=None, distance=None, ip_version=4
    routes: list[Route] = []
    for route in cfg['routing']:
        routes.append(Route(dev_id, route['target_gateway'], route['destination'], static=route['static'], interface=route['interface'], metric=route['metric'], distance=route['distance'], ip_version=route['ip_version']))
    cfg['routing'] = routes

    cfg['routing'].sort(key=get_route_destination,reverse=True)

    config2import.update({'interfaces': cfg['interfaces'], 'routing': cfg['routing']})

    return 0

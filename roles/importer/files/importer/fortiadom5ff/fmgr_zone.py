import fmgr_getter
from fwo_exceptions import FwoNormalizedConfigParseError

def get_zones(sid, fm_api_url, native_config, adom_name, limit):
    
    if adom_name == '':
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/global/obj/dynamic/interface', 'interface_global', limit=limit)
    else:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/adom/' + adom_name + '/obj/dynamic/interface', 'interface_' + adom_name, limit=limit)

def normalize_zones(native_config, normalized_config_adom, is_global_loop_iteration):
    zones = []
    fetched_zones = []
    if is_global_loop_iteration:
        fetched_zones.append('any')
    for zone_type in native_config['zones']:
        for mapping in zone_type.get('data', []):
            if not mapping['dynamic_mapping'] is None:
                fetch_dynamic_mapping(mapping, fetched_zones)
            if not mapping['platform_mapping'] is None:
                fetch_platform_mapping(mapping, fetched_zones)

    for zone in fetched_zones:
        zones.append({'zone_name': zone})
    normalized_config_adom.update({'zone_objects': zones})

def fetch_dynamic_mapping(mapping, fetched_zones):
    for dyn_mapping in mapping['dynamic_mapping']:
        if 'name' in dyn_mapping and not dyn_mapping['name'] in fetched_zones:
            fetched_zones.append(dyn_mapping['name'])
        if 'local-intf' in dyn_mapping:
            for local_interface in dyn_mapping['local-intf']:
                if local_interface not in fetched_zones:
                    fetched_zones.append(local_interface)

def fetch_platform_mapping(mapping, fetched_zones):
    for dyn_mapping in mapping['platform_mapping']:
        if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in fetched_zones:
            fetched_zones.append(dyn_mapping['intf-zone'])

def find_zones_in_normalized_config(native_zone_list : list, normalized_config, normalized_config_global):
    """Verifies that input zones exist in normalized config"""
    zone_out_list = []
    for zone in native_zone_list:
        if zone in normalized_config['zone_objects'] or zone in normalized_config_global['zone_objects']:
            zone_out_list.append(zone)
        else:
            raise FwoNormalizedConfigParseError('Could not find zone ' + zone + ' in normalized config.')
    return zone_out_list

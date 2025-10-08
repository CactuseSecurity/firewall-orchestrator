import fmgr_getter

def get_zones(sid, fm_api_url, native_config, adom_name, limit):
    
    if adom_name == '':
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/global/obj/dynamic/interface', 'interface_global', limit=limit)
    else:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/adom/' + adom_name + '/obj/dynamic/interface', 'interface_' + adom_name, limit=limit)

def normalize_zones(native_config, normalized_config_dict):
    zones = []
    fetched_zones = []
    for zone_type in native_config['zones']:
        for mapping in zone_type.get('data', []):
            if not mapping['dynamic_mapping'] is None:
                fetch_dynamic_mapping(mapping, fetched_zones)
            if not mapping['platform_mapping'] is None:
                fetch_platform_mapping(mapping, fetched_zones)

    for zone in fetched_zones:
        zones.append({'zone_name': zone['name']})
    normalized_config_dict.update({'zone_objects': zones})

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

def add_zone_if_missing(normalized_config_dict: dict, zone_string):
    # adding zone if it not yet exists

    # also transforming any into global (normalized global zone)
    if zone_string == 'any':
        zone_string = 'global'    
    if zone_string is not None:
        if 'zone_objects' not in normalized_config_dict: # no zones yet? add empty zone_objects array
            normalized_config_dict.update({'zone_objects': []})
        zone_exists = False
        for zone in normalized_config_dict['zone_objects']:
            if zone_string == zone['zone_name']:
                zone_exists = True
        if not zone_exists:
            normalized_config_dict['zone_objects'].append({'zone_name': zone_string})
    return zone_string
    
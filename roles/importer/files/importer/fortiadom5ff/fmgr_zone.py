
def normalize_zones(full_config, config2import, import_id):
    zones = []
    for orig_zone in full_config['zone_objects']['zone_list']:
        zone = {}
        zone.update({'zone_name': orig_zone})
        zones.append(zone)
        
    config2import.update({'zone_objects': zones})


def add_zone_if_missing(normalized_config: dict, zone_string):
    # adding zone if it not yet exists

    # also transforming any into global (normalized global zone)
    if zone_string == 'any':
        zone_string = 'global'    
    if zone_string is not None:
        if 'zone_objects' not in normalized_config: # no zones yet? add empty zone_objects array
            normalized_config.update({'zone_objects': []})
        zone_exists = False
        for zone in normalized_config['zone_objects']:
            if zone_string == zone['zone_name']:
                zone_exists = True
        if not zone_exists:
            normalized_config['zone_objects'].append({'zone_name': zone_string})
    return zone_string
    
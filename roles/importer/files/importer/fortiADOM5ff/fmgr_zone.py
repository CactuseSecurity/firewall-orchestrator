import logging, ipaddress
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
sys.path.append(importer_base_dir + '/fortimanager5ff')
import common, fwcommon


def normalize_zones(full_config, config2import, import_id):
    zones = []
    for orig_zone in full_config['zone_objects']['zone_list']:
        zone = {}
        zone.update({'zone_name': orig_zone})
        zone.update({'control_id': import_id})
        zones.append(zone)
        
    config2import.update({'zone_objects': zones})


def add_zone_if_missing (config2import, zone_string, import_id):
    # adding zone if it not yet exists

    # also transforming any into global (normalized global zone)
    if zone_string == 'any':
        zone_string = 'global'    
    if zone_string is not None:
        if 'zone_objects' not in config2import: # no zones yet? add empty zone_objects array
            config2import.update({'zone_objects': []})
        zone_exists = False
        for zone in config2import['zone_objects']:
            if zone_string == zone['zone_name']:
                zone_exists = True
        if not zone_exists:
            config2import['zone_objects'].append({'zone_name': zone_string, 'control_id': import_id})
    return zone_string
    
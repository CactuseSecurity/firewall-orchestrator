
from typing import Any


def normalize_zones(full_config: dict[str, Any], config2import: dict[str, Any], import_id: int):
    zones: list[dict[str, Any]] = []
    for orig_zone in full_config['zone_objects']['zone_list']:
        zone: dict[str, Any] = {}
        zone.update({'zone_name': orig_zone})
        zone.update({'control_id': import_id})
        zones.append(zone)
        
    config2import.update({'zone_objects': zones})


def add_zone_if_missing(config2import: dict[str, Any], zone_string: str | None, import_id: int) -> str | None:
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
    
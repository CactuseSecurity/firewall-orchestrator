import fmgr_getter
from fwo_exceptions import FwoNormalizedConfigParseError
from typing import Any


def get_zones(sid: str, fm_api_url: str, native_config: dict[str, Any], adom_name: str, limit: int):
    
    if adom_name == '':
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/global/obj/dynamic/interface', 'interface_global', limit=limit)
    else:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/adom/' + adom_name + '/obj/dynamic/interface', 'interface_' + adom_name, limit=limit)


def normalize_zones(native_config: dict[str, Any], normalized_config_adom: dict[str, Any], is_global_loop_iteration: bool):
    zones: list[dict[str, Any]] = []
    fetched_zones: list[str] = []
    if is_global_loop_iteration: # can not find the following zones in api return
        statically_add_missing_global_zones(fetched_zones)
    for zone_type in native_config['zones']:
        for mapping in zone_type.get('data', []):
            if 'defmap-intf' in mapping and mapping['defmap-intf'] not in fetched_zones:
                fetched_zones.append(mapping['defmap-intf'])
            if mapping['dynamic_mapping'] is not None:
                fetch_dynamic_mapping(mapping, fetched_zones)
            if mapping['platform_mapping'] is not None:
                fetch_platform_mapping(mapping, fetched_zones)

    for zone in fetched_zones:
        zones.append({'zone_name': zone})
    normalized_config_adom.update({'zone_objects': zones})


def statically_add_missing_global_zones(fetched_zones: list[str]) -> None:
    for zone in ['any', 'sslvpn_tun_intf', 'virtual-wan-link']:
        fetched_zones.append(zone)

    # double check, if these zones cannot be parsed from api results


def fetch_dynamic_mapping(mapping: dict[str, Any], fetched_zones: list[str]) -> None:
    for dyn_mapping in mapping['dynamic_mapping']:
        if 'name' in dyn_mapping and not dyn_mapping['name'] in fetched_zones:
            fetched_zones.append(dyn_mapping['name'])
        if 'local-intf' in dyn_mapping:
            for local_interface in dyn_mapping['local-intf']:
                if local_interface not in fetched_zones:
                    fetched_zones.append(local_interface)

def fetch_platform_mapping(mapping: dict[str, Any], fetched_zones: list[str]) -> None:
    for dyn_mapping in mapping['platform_mapping']:
        if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in fetched_zones:
            fetched_zones.append(dyn_mapping['intf-zone'])

def find_zones_in_normalized_config(native_zone_list: list[str], normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any]) -> list[str]:
    """Verifies that input zones exist in normalized config"""
    zone_out_list: list[str] = []
    for nativ_zone in native_zone_list:
        was_zone_found = False
        for normalized_zone in normalized_config_adom['zone_objects'] + normalized_config_global['zone_objects']:
            if nativ_zone == normalized_zone['zone_name']:
                zone_out_list.append(normalized_zone['zone_name'])
                was_zone_found = True
                break
        if not was_zone_found:
            raise FwoNormalizedConfigParseError('Could not find zone ' + nativ_zone + ' in normalized config.')
    return sorted(zone_out_list)

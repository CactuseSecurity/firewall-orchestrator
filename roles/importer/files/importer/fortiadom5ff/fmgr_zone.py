import fmgr_getter

def get_zones(sid, fm_api_url, native_config, adom_name, limit):
    native_config.update({"zones": {}})

    # get global zones
    if adom_name == '':
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/global/obj/dynamic/interface', 'interface_global', limit=limit)
    else:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config['zones'], sid, fm_api_url, '/pm/config/adom/' + adom_name + '/obj/dynamic/interface', 'interface_' + adom_name, limit=limit)

    # # get local zones
    # for device in native_config['devices']:
    #     local_pkg_name = device['package']
    #     for adom in native_config['adoms']:
    #         if adom['name']==adom_name:
    #             if local_pkg_name not in adom['package_names']:
    #                 logger.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
    #                 return 1
    #             else:
    #                 fmgr_getter.update_config_with_fortinet_api_call(
    #                     native_config['zones'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/obj/dynamic/interface", device['id'], debug=debug_level, limit=limit)

    # native_config['zones']['zone_list'] = []
    # for device in native_config['zones']:
    #     for mapping in native_config['zones'][device]:
    #         if not isinstance(mapping, str):
    #             if not mapping['dynamic_mapping'] is None:
    #                 for dyn_mapping in mapping['dynamic_mapping']:
    #                     if 'name' in dyn_mapping and not dyn_mapping['name'] in native_config['zones']['zone_list']:
    #                         native_config['zones']['zone_list'].append(dyn_mapping['name'])
    #                     if 'local-intf' in dyn_mapping and not dyn_mapping['local-intf'][0] in native_config['zones']['zone_list']:
    #                         native_config['zones']['zone_list'].append(dyn_mapping['local-intf'][0])
    #             if not mapping['platform_mapping'] is None:
    #                 for dyn_mapping in mapping['platform_mapping']:
    #                     if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in native_config['zones']['zone_list']:
    #                         native_config['zones']['zone_list'].append(dyn_mapping['intf-zone'])

def normalize_zones(full_config, config2import, import_id):
    zones = []
    for orig_zone in full_config['zone_objects']['zone_list']:
        zone = {}
        zone.update({'zone_name': orig_zone})
        zones.append(zone)
        
    config2import.update({'zone_objects': zones})


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
    

def normalize_users(full_config, config2import, import_id, user_scope):
    users = []
    # for orig_zone in full_config['zones']['zone_list']:
    #     zone = {}
    #     zone.update({'zone_name': orig_zone})
    #     zone.update({'control_id': import_id})
    #     zones.append(zone)
    
    for scope in user_scope:
        config2import.update({scope: users})


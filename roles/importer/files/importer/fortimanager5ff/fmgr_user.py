import logging, ipaddress
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
sys.path.append(importer_base_dir + '/fortimanager5ff')
import common, fwcommon


def normalize_users(full_config, config2import, import_id):
    users = []
    # for orig_zone in full_config['zones']['zone_list']:
    #     zone = {}
    #     zone.update({'zone_name': orig_zone})
    #     zone.update({'control_id': import_id})
    #     zones.append(zone)
        
    config2import.update({'user_objects': users})


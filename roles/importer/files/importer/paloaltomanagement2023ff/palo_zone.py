from asyncio.log import logger
from fwo_log import getFwoLogger
from fwo_const import list_delimiter


def normalize_zones(full_config, config2import, import_id):
    zones = []
    for zone_orig in full_config["/Network/Zones"]:
        zones.append({
            "zone_name":  zone_orig["@name"],
            "zone_uid":   zone_orig["@name"],
            "control_id": import_id
        })
    
    config2import['zone_objects'] = zones

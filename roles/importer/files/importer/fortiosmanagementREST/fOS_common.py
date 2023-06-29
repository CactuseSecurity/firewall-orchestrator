import sys
from common import importer_base_dir
sys.path.append(importer_base_dir + '/fortiosmanagementREST')
from curses import raw
from fwo_log import getFwoLogger
from fwo_const import list_delimiter, fwo_config_filename
from fwo_config import readConfig
from fwo_api import setAlert, create_data_issue


# TODO: deal with objects with identical names (e.g. all ipv4 & all ipv6)
def resolve_objects (obj_name_string_list, lookup_dict={}, delimiter=list_delimiter, jwt=None, import_id=None, mgm_id=None):
    logger = getFwoLogger()
    fwo_config = readConfig(fwo_config_filename)

    ref_list = []
    objects_not_found = []
    for el in obj_name_string_list.split(delimiter):
        found = False
        if el in lookup_dict:
            ref_list.append(lookup_dict[el])
        else:
            objects_not_found.append(el)

    for obj in objects_not_found:
        if obj != 'all' and obj != 'Original':
            if not create_data_issue(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, obj_name=obj, severity=1, mgm_id=mgm_id):
                logger.warning("resolve_raw_objects: encountered error while trying to log an import data issue using create_data_issue")

            desc = "found a broken object reference '" + obj + "' "
            setAlert(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, title="object reference error", mgm_id=mgm_id, severity=1, role='importer', \
                description=desc, source='import', alertCode=16)

    return delimiter.join(ref_list)

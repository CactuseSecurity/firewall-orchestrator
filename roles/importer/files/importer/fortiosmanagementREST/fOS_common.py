# import sys
# from common import importer_base_dir
# sys.path.append(importer_base_dir + '/fortiosmanagementREST')
from fwo_log import FWOLogger
from fwo_const import list_delimiter, fwo_config_filename
from fwo_config import read_config
from fwo_api_call import setAlert, create_data_issue #type: ignore #TYPING: Importing a Class method


# TODO: deal with objects with identical names (e.g. all ipv4 & all ipv6)
def resolve_objects (obj_name_string_list: str, lookup_dict: dict[str, str]={}, delimiter: str=list_delimiter, jwt: str | None = None, import_id: int | None= None, mgm_id: int | None = None):
    fwo_config = read_config(fwo_config_filename)

    ref_list: list[str] = []
    objects_not_found: list[str] = []
    for el in obj_name_string_list.split(delimiter):
        if el in lookup_dict:
            ref_list.append(lookup_dict[el])
        else:
            objects_not_found.append(el)

    for obj in objects_not_found:
        if obj != 'all' and obj != 'Original':
            if not create_data_issue(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, obj_name=obj, severity=1, mgm_id=mgm_id):
                FWOLogger.warning("resolve_raw_objects: encountered error while trying to log an import data issue using create_data_issue")

            desc = "found a broken object reference '" + obj + "' "
            setAlert(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, title="object reference error", mgm_id=mgm_id, severity=1, role='importer', \
                description=desc, source='import', alertCode=16)

    return delimiter.join(ref_list)

import fwo_api_call as fwo_api_call
import json
import fwo_const
from fwo_log import FWOLogger
# TODO delete this file 
def getFwoAlerter() -> dict[str, str]:
    try: 
        with open(fwo_const.FWO_CONFIG_FILENAME, "r") as fwo_config:
            fwo_config = json.loads(fwo_config.read())
        user_management_api_base_url = fwo_config['middleware_uri']
        fwo_api_base_url = fwo_config['api_uri']
    except Exception:
        FWOLogger.error("getFwoAlerter - error while reading FWO config file")        
        raise

    try:
        with open(fwo_const.BASE_DIR + '/etc/secrets/importer_pwd', 'r') as file:
            importer_pwd = file.read().replace('\n', '')
    except Exception:
        FWOLogger.error("getFwoAlerter - error while reading importer pwd file")
        raise

    jwt = fwo_api_call.login(fwo_const.IMPORTER_USER_NAME, importer_pwd, user_management_api_base_url) # type: ignore

    return { "fwo_api_base_url": fwo_api_base_url, "jwt": jwt }

#    fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id=import_id, obj_name=obj['obj_name'], severity=1, rule_uid=rule_uid, mgm_id=mgm_id, object_type=obj['obj_typ'])

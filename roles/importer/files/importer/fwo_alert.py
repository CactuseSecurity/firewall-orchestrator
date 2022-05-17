import fwo_api
import json
import fwo_const
import fwo_log

def getFwoAlerter(debug_level=0, proxy='', ssl_mode=fwo_const.fwo_api_verify_certs):
    logger = fwo_log.getFwoLogger(debug_level=debug_level)
    try: 
        with open(fwo_const.fwo_config_filename, "r") as fwo_config:
            fwo_config = json.loads(fwo_config.read())
        user_management_api_base_url = fwo_config['middleware_uri']
        fwo_api_base_url = fwo_config['api_uri']
    except:
        logger.error("getFwoAlerter - error while reading FWO config file")        
        raise

    try:
        with open(fwo_const.base_dir + '/etc/secrets/importer_pwd', 'r') as file:
            importer_pwd = file.read().replace('\n', '')
    except:
        logger.error("getFwoAlerter - error while reading importer pwd file")
        raise

    jwt = fwo_api.login(fwo_const.importer_user_name, importer_pwd, user_management_api_base_url, ssl_verification=ssl_mode, proxy=proxy)

    return { "fwo_api_base_url": fwo_api_base_url, "jwt": jwt }

#    fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id=import_id, obj_name=obj['obj_name'], severity=1, rule_uid=rule_uid, mgm_id=mgm_id, object_type=obj['obj_typ'])

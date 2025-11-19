
from fwo_log import get_fwo_logger
import sys, json
from fwo_const import importer_pwd_file

def read_config(fwo_config_filename: str = '/etc/fworch/fworch.json') -> dict[str, str | int | None]:
    logger = get_fwo_logger()
    try:
        # read fwo config (API URLs)
        with open(fwo_config_filename, "r") as fwo_config:
            fwo_config = json.loads(fwo_config.read())
        user_management_api_base_url = fwo_config['middleware_uri']
        fwo_api_base_url = fwo_config['api_uri']
        fwo_version = fwo_config['product_version']
        fwo_major_version = int(fwo_version.split('.')[0])

        # read importer password from file
        with open(importer_pwd_file, 'r') as file:
            importerPwd = file.read().replace('\n', '')

    except KeyError as e:
        logger.error("config key not found in "+ fwo_config_filename + ": " + e.args[0])
        sys.exit(1)
    except FileNotFoundError as e:
        logger.error("config file not found or unable to access: "+ fwo_config_filename)
        sys.exit(1)
    except Exception:
        logger.error("unspecified error occurred while trying to read config file: "+ fwo_config_filename)
        sys.exit(1)
    config: dict[str, str | int | None] = {
        "fwo_major_version": fwo_major_version, 
        "user_management_api_base_url": user_management_api_base_url, 
        "fwo_api_base_url": fwo_api_base_url,
        "importerPassword": importerPwd
    }
    return config

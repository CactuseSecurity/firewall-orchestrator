import json
import sys

from fwo_const import IMPORTER_PWD_FILE
from fwo_log import FWOLogger
from models.fwo_config import FwoConfig


class FwoConfigController:
    fwo_config: FwoConfig

    def __init__(self, fwo_config_filename: str = "/etc/fworch/fworch.json"):
        self.read_config(fwo_config_filename)

    def read_config(self, fwo_config_filename: str = "/etc/fworch/fworch.json"):
        try:
            # read fwo config (API URLs)
            with open(fwo_config_filename) as fwo_config:
                fwo_config_json = json.loads(fwo_config.read())
            user_management_api_base_url = fwo_config_json["middleware_uri"]
            fwo_api_base_url = fwo_config_json["api_uri"]
            fwo_version = fwo_config_json["product_version"]
            fwo_major_version = int(fwo_version.split(".")[0])

            # read importer password from file
            with open(IMPORTER_PWD_FILE) as file:
                importer_pwd = file.read().replace("\n", "")

        except KeyError as e:
            FWOLogger.error("config key not found in " + fwo_config_filename + ": " + e.args[0])
            sys.exit(1)
        except FileNotFoundError:
            FWOLogger.error("config file not found or unable to access: " + fwo_config_filename)
            sys.exit(1)
        except Exception:
            FWOLogger.error("unspecified error occurred while trying to read config file: " + fwo_config_filename)
            sys.exit(1)

        self.fwo_config = FwoConfig(
            fwo_api_url=fwo_api_base_url,
            fwo_user_mgmt_api_uri=user_management_api_base_url,
            api_fetch_size=fwo_config_json.get("fwApiElementsPerFetch", 150),
            importer_password=importer_pwd,
            major_version=fwo_major_version,
        )

    def as_dict(self) -> dict[str, str | int | None]:
        return {
            "fwo_api_base_url": self.fwo_config.fwo_api_url,
            "user_management_api_base_url": self.fwo_config.fwo_user_mgmt_api_uri,
            "api_fetch_size": self.fwo_config.api_fetch_size,
            "importer_password": self.fwo_config.importer_password,
            "fwo_major_version": self.fwo_config.major_version,
        }

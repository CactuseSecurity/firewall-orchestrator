import json
import sys

from fwo_const import FWO_CONFIG_FILENAME, IMPORTER_PWD_FILE
from fwo_log import FWOLogger
from models.fwo_config import FwoConfig


class FwoConfigController:
    fwo_config: FwoConfig

    def __init__(
        self,
        fwo_config_filename: str = FWO_CONFIG_FILENAME,
        force: bool = False,
        is_full_import: bool = False,
        clear: bool = False,
        debug_level: int = 0,
    ):
        (
            user_management_api_base_url,
            fwo_api_base_url,
            importer_user_name,
            importer_pwd,
            fwo_major_version,
            api_fetch_size,
            sleep_timer,
        ) = self.read_config(fwo_config_filename)

        self.fwo_config = FwoConfig(
            fwo_api_url=fwo_api_base_url,
            fwo_user_mgmt_api_uri=user_management_api_base_url,
            api_fetch_size=api_fetch_size,
            major_version=fwo_major_version,
            importer_password=importer_pwd,
            importer_user_name=importer_user_name,
            sleep_timer=sleep_timer,
            force=force,
            is_full_import=is_full_import,
            clear=clear,
            debug_level=debug_level,
        )

    def read_config(
        self, fwo_config_filename: str = "/etc/fworch/fworch.json"
    ) -> tuple[str, str, str, str, int, int, int]:
        try:
            # read fwo config (API URLs)
            with open(fwo_config_filename) as fwo_config:
                fwo_config_json = json.loads(fwo_config.read())
            user_management_api_base_url = fwo_config_json["middleware_uri"]
            fwo_api_base_url = fwo_config_json["api_uri"]
            fwo_version = fwo_config_json["product_version"]
            importer_user_name = fwo_config_json.get("importer_user_name", "importer")
            fwo_major_version = int(fwo_version.split(".")[0])
            api_fetch_size = fwo_config_json.get("fwApiElementsPerFetch", 150)
            sleep_timer = fwo_config_json.get("importSleepTime", 90)

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

        return (
            user_management_api_base_url,
            fwo_api_base_url,
            importer_user_name,
            importer_pwd,
            fwo_major_version,
            api_fetch_size,
            sleep_timer,
        )

    def as_dict(self) -> dict[str, str | int | None]:
        return {
            "fwo_api_base_url": self.fwo_config.fwo_api_url,
            "user_management_api_base_url": self.fwo_config.fwo_user_mgmt_api_uri,
            "api_fetch_size": self.fwo_config.api_fetch_size,
            "importer_password": self.fwo_config.importer_password,
            "importer_user_name": self.fwo_config.importer_user_name,
            "fwo_major_version": self.fwo_config.major_version,
            "sleep_timer": self.fwo_config.sleep_timer,
        }

    def update_settings(
        self,
        *,
        fwo_api_url: str | None = None,
        fwo_user_mgmt_api_uri: str | None = None,
        api_fetch_size: int | None = None,
        major_version: int | None = None,
        importer_password: str | None = None,
        importer_user_name: str | None = None,
        sleep_timer: int | None = None,
        ssl_verification: bool | None = None,
        suppress_certificate_warnings: bool | None = None,
        suppress_consistency_check: bool | None = None,
    ):
        if fwo_api_url is not None:
            self.fwo_config.fwo_api_url = fwo_api_url
        if fwo_user_mgmt_api_uri is not None:
            self.fwo_config.fwo_user_mgmt_api_uri = fwo_user_mgmt_api_uri
        if api_fetch_size is not None:
            self.fwo_config.api_fetch_size = api_fetch_size
        if major_version is not None:
            self.fwo_config.major_version = major_version
        if importer_password is not None:
            self.fwo_config.importer_password = importer_password
        if importer_user_name is not None:
            self.fwo_config.importer_user_name = importer_user_name
        if sleep_timer is not None:
            self.fwo_config.sleep_timer = sleep_timer
        if ssl_verification is not None:
            self.fwo_config.ssl_verification = ssl_verification
        if suppress_certificate_warnings is not None:
            self.fwo_config.suppress_certificate_warnings = suppress_certificate_warnings
        if suppress_consistency_check is not None:
            self.fwo_config.suppress_consistency_check = suppress_consistency_check

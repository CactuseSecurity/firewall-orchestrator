import functools
import json
import sys

from fwo_const import FWO_CONFIG_FILENAME, IMPORTER_PWD_FILE
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.fwo_config import FwoConfig, TlsIdentity

FWO_CONFIG_FILE = "/etc/fworch/fworch.json"


@functools.lru_cache(maxsize=1)
def read_tls_identity(fwo_config_filename: str) -> TlsIdentity:
    """
    Read the local TLS identity straight from the config file.

    Kept independent of the FwoConfigController so FwoApi can attach the identity to every
    internal call (including the very first login) without needing a config instance.
    """
    try:
        with open(fwo_config_filename) as fwo_config:
            fwo_config_json = json.loads(fwo_config.read())
        return TlsIdentity(
            client_certificate=str(fwo_config_json["tls_client_certificate"]),
            client_private_key=str(fwo_config_json["tls_client_private_key"]),
            ca_certificate=str(fwo_config_json["tls_ca_certificate"]),
        )
    except KeyError as key_error:
        raise FwoImporterError(
            f"TLS identity key not found in {fwo_config_filename}: {key_error.args[0]}"
        ) from key_error
    except OSError as os_error:
        raise FwoImporterError(f"config file not found or unable to access: {fwo_config_filename}") from os_error
    except ValueError as value_error:
        raise FwoImporterError(f"config file is not valid json: {fwo_config_filename}") from value_error


class FwoConfigController:
    fwo_config: FwoConfig

    def __init__(
        self,
        fwo_config_filename: str = FWO_CONFIG_FILENAME,
        force: bool = False,
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
            clear=clear,
            debug_level=debug_level,
        )

    def read_config(self, fwo_config_filename: str = FWO_CONFIG_FILE) -> tuple[str, str, str, str, int, int, int]:
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
            read_tls_identity(fwo_config_filename)  # fail fast on a missing TLS identity

            # read importer password from file
            with open(IMPORTER_PWD_FILE) as file:
                importer_pwd = file.read().replace("\n", "")

        except KeyError as e:
            FWOLogger.error("config key not found in " + fwo_config_filename + ": " + e.args[0])
            sys.exit(1)
        except FileNotFoundError:
            FWOLogger.error("config file not found or unable to access: " + fwo_config_filename)
            sys.exit(1)
        except FwoImporterError as tls_error:
            # read_tls_identity already names the offending key or file; keep that detail
            # instead of letting it fall through to the generic message below.
            FWOLogger.error(str(tls_error))
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

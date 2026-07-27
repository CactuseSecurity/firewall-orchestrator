from __future__ import annotations

import functools
import json
import sys
from typing import NamedTuple

from fwo_const import IMPORTER_PWD_FILE
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger

FWO_CONFIG_FILE = "/etc/fworch/fworch.json"


class TlsIdentity(NamedTuple):
    """Paths FWO components use to authenticate to, and verify, internal endpoints."""

    client_certificate: str
    client_private_key: str
    ca_certificate: str


@functools.lru_cache(maxsize=1)
def read_tls_identity(fwo_config_filename: str) -> TlsIdentity:
    """
    Read the local TLS identity straight from the config file.

    Deliberately independent of the ServiceProvider: login runs during bootstrap and
    again after every management, where the container has been reset, so resolving the
    identity through a registered service would break the import loop.
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


def read_config(fwo_config_filename: str = FWO_CONFIG_FILE) -> dict[str, str | int | None]:
    try:
        # read fwo config (API URLs)
        with open(fwo_config_filename) as fwo_config:
            fwo_config_json = json.loads(fwo_config.read())
        user_management_api_base_url = fwo_config_json["middleware_uri"]
        fwo_api_base_url = fwo_config_json["api_uri"]
        fwo_version = fwo_config_json["product_version"]
        tls_identity = read_tls_identity(fwo_config_filename)
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
    except FwoImporterError as tls_error:
        # read_tls_identity already names the offending key or file; keep that detail
        # instead of letting it fall through to the generic message below.
        FWOLogger.error(str(tls_error))
        sys.exit(1)
    except Exception:
        FWOLogger.error("unspecified error occurred while trying to read config file: " + fwo_config_filename)
        sys.exit(1)
    config: dict[str, str | int | None] = {
        "fwo_major_version": fwo_major_version,
        "user_management_api_base_url": user_management_api_base_url,
        "fwo_api_base_url": fwo_api_base_url,
        "tls_client_certificate": tls_identity.client_certificate,
        "tls_client_private_key": tls_identity.client_private_key,
        "tls_ca_certificate": tls_identity.ca_certificate,
        "importerPassword": importer_pwd,
    }
    return config

"""
Parser for OPNsense configurations.

The script retrieves and converts an OPNsense configuration into a simplified
normalized JSON structure.
"""

from typing import Any

import requests
import xmltodict
from fw_modules.opnsense25ff.opnsense_normalizer import normalize_opnsense_config
from fw_modules.opnsense25ff.opnsense_sanitizer import remove_opnsense_sensitive_data
from fwo_base import ensure_device_name
from fwo_exceptions import FwoNativeConfigFetchError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon
from models.fwconfigmanager import FwConfigManager
from requests.auth import HTTPBasicAuth


class OPNsense25common(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        return get_config(config_in=config_in, import_state=import_state)


def ensure_manager_set(config_in: FwConfigManagerListController, import_state: ImportStateController) -> None:
    """Add an empty manager to configs read from file, which carry native data only."""
    if len(config_in.ManagerSet) > 0:
        return
    config_in.add_manager(
        manager=FwConfigManager(
            manager_uid=import_state.state.mgm_details.uid,
            manager_name=import_state.state.mgm_details.name,
            is_super_manager=import_state.state.mgm_details.is_super_manager,
            sub_manager_ids=import_state.state.mgm_details.sub_manager_ids,
            domain_name=import_state.state.mgm_details.domain_name,
            domain_uid=import_state.state.mgm_details.domain_uid,
            configs=[],
        )
    )


def fetch_native_config(import_state: ImportStateController) -> dict[str, Any]:
    """Download the full config.xml from the OPNsense core backup API and parse it into a dict."""
    # curl -kv -u "$key:$secret" 'https://{opensense}/api/core/backup/download/this'
    os_api_url = f"https://{import_state.state.mgm_details.hostname}:{import_state.state.mgm_details.port!s}/api/core/backup/download/this"
    with requests.Session() as session:
        session.verify = import_state.state.verify_certs
        session.auth = HTTPBasicAuth(import_state.state.mgm_details.import_user, import_state.state.mgm_details.secret)

        FWOLogger.debug("[*] receiving OPNsense config.xml ...")
        response = session.get(os_api_url, timeout=60)
        response.raise_for_status()
        FWOLogger.debug("[+] success!")

        return xmltodict.parse(response.content)


def get_config(
    config_in: FwConfigManagerListController, import_state: ImportStateController
) -> tuple[int, FwConfigManagerListController]:
    try:
        ensure_device_name(import_state)
        ensure_manager_set(config_in, import_state)

        # Stage 1: config retrieval - only contact the firewall if no native config was supplied
        if config_in.native_config_is_empty():
            native_config = fetch_native_config(import_state)
        else:
            FWOLogger.debug("[*] using native OPNsense config provided from file ...")
            native_config = config_in.native_config or {}

        # Stage 2: sanitizing config
        config_in.native_config = remove_opnsense_sensitive_data(native_config)
        FWOLogger.debug("[+] sanitizing complete!")

        # Stage 3: normalizing config
        config_in = normalize_opnsense_config(config_in, import_state=import_state)
        FWOLogger.debug("[+] normalizing complete!")

        return 0, config_in

    except requests.exceptions.RequestException as error:
        msg = f"[-] get_config: API request failed: {error}"
        FWOLogger.exception(msg, exc_info=True)
        raise FwoNativeConfigFetchError(msg) from error
    except Exception:
        FWOLogger.exception("[-] get_config: failed to process OPNsense configuration", exc_info=True)
        raise

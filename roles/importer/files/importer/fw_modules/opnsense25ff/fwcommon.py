"""
Parser for OPNsense configurations.

The script retrieves and converts an OPNsense configuration into a simplified
normalized JSON structure.
"""

import requests
import xmltodict
from fw_modules.opnsense25ff.opnsense_normalizer import normalize_opnsense_config
from fw_modules.opnsense25ff.opnsense_sanitizer import remove_opnsense_sensitive_data
from fwo_exceptions import FwoNativeConfigFetchError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon
from requests.auth import HTTPBasicAuth


class OPNsense25common(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        return get_config(config_in=config_in, import_state=import_state)


def get_config(
    config_in: FwConfigManagerListController, import_state: ImportStateController
) -> tuple[int, FwConfigManagerListController]:
    ensure_device_name(import_state)

    # retrieve full opnsense config into full_config
    # curl -kv -u "$key:$secret" 'https://{opensense}/api/core/backup/download/this'
    os_api_url = f"https://{import_state.state.mgm_details.hostname}:{import_state.state.mgm_details.port!s}/api/core/backup/download/this"
    with requests.Session() as session:
        session.verify = import_state.state.verify_certs
        session.auth = HTTPBasicAuth(import_state.state.mgm_details.import_user, import_state.state.mgm_details.secret)

        try:
            # Stage 1: config retrieval
            FWOLogger.debug("[*] receiving OPNsense config.xml ...")
            response = session.get(os_api_url, timeout=60)
            response.raise_for_status()
            FWOLogger.debug("[+] success!")

            # Stage 2: sanitizing config
            raw_config = remove_opnsense_sensitive_data(xmltodict.parse(response.content))
            FWOLogger.debug("[+] sanitizing complete!")

            # Stage 3: saving sanitized config
            config_in.native_config = raw_config
            FWOLogger.debug("[+] parsing complete!")

            # Stage 4: normalizing config
            config_in = normalize_opnsense_config(config_in, import_state=import_state)
            FWOLogger.debug("[+] normalizing complete!")

            return 0, config_in

        except requests.exceptions.RequestException as e:
            msg = f"[-] get_config: API request failed: {e}"
            FWOLogger.error(msg)
            raise FwoNativeConfigFetchError(msg) from e


def ensure_device_name(import_state: ImportStateController) -> None:
    mgm_details = import_state.state.mgm_details
    gw_map = import_state.state.gateway_map.get(mgm_details.current_mgm_id, {})
    gateway_uid = next(iter(gw_map.keys()), None)

    if (
        mgm_details.devices
        and "name" in mgm_details.devices[0]
        and (gateway_uid is None or mgm_details.devices[0]["name"] in gw_map)
    ):
        return

    if gateway_uid is None:
        gateway_uid = mgm_details.name or mgm_details.hostname

    mgm_details.devices = [{"name": gateway_uid}]

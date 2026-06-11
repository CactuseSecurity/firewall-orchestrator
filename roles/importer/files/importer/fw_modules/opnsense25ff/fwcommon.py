"""Parser for OPNsense configurations.

The script retrieves and converts an OPNsense configuration into a simplified
normalized JSON structure.
"""

from socket import gethostname

import requests
import xmltodict
from fw_modules.opnsense25ff.opnsense_normalizer import \
    _normalize_opnsense_config
from fw_modules.opnsense25ff.opnsense_parser import _parse_opnsense_config
from fw_modules.opnsense25ff.opnsense_sanitizer import \
    remove_opnsense_sensitive_data
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import \
    FwConfigManagerListController
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
            config_in = _normalize_opnsense_config(config_in, import_state=import_state)
            FWOLogger.debug("[+] normalizing complete!")

            return 0, config_in

        except requests.exceptions.RequestException as e:
            raise FWOLogger.error(f"[-] get_config: API request failed: {e}") from e

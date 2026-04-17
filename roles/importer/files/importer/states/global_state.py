import warnings

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from models.fwo_config_controller import FwoConfigController


class GlobalState:
    fwo_config_controller: FwoConfigController
    importer_version: int

    fwo_api: FwoApi
    fwo_api_call: FwoApiCall
    # stm tabellen

    def __init__(
        self,
        config_filename: str,
        force: bool,
        clear: bool,
        debug_level: int,
    ):
        self.fwo_config_controller = FwoConfigController(
            config_filename, force=force, clear=clear, debug_level=debug_level
        )
        self.importer_version = self.fwo_config_controller.fwo_config.major_version
        self.login_to_api()

    def login_to_api(self):
        self.fwo_api = FwoApi(
            self.fwo_config_controller.fwo_config.fwo_api_url,
            importer_user_name=self.fwo_config_controller.fwo_config.importer_user_name,
            importer_password=self.fwo_config_controller.fwo_config.importer_password,
            importer_mgm_uri=self.fwo_config_controller.fwo_config.fwo_user_mgmt_api_uri,
            fwo_user_mgmt_api_uri=self.fwo_config_controller.fwo_config.fwo_user_mgmt_api_uri,
        )

        self.fwo_api_call = FwoApiCall(self.fwo_api)
        suppress_certificate_warnings = self.fwo_api_call.get_config_value(key="importSuppressCertificateWarnings")
        self.fwo_config_controller.update_settings(
            ssl_verification=self.fwo_api_call.get_config_value(key="importCheckCertificates") == "True",
            suppress_certificate_warnings=suppress_certificate_warnings == "True",
        )

        if suppress_certificate_warnings != "True":
            warnings.resetwarnings()

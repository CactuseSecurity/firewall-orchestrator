"""
the configuraton of a firewall orchestrator itself
as read from the global config file including FWO URI
"""


class FwoConfig:
    fwo_api_url: str
    fwo_user_mgmt_api_uri: str
    api_fetch_size: int
    major_version: int
    importer_password: str
    importer_user_name: str
    sleep_timer: int = 90
    force: bool = False
    clear: bool = False
    debug_level: int = 0
    ssl_verification: bool = True
    suppress_certificate_warnings: bool = False
    suppress_consistency_check: bool = False

    def __init__(
        self,
        fwo_api_url: str,
        fwo_user_mgmt_api_uri: str,
        api_fetch_size: int = 150,
        major_version: int = 0,
        importer_password: str = "",
        importer_user_name: str = "importer",
        sleep_timer: int = 90,
        force: bool = False,
        clear: bool = False,
        debug_level: int = 0,
        ssl_verification: bool = True,
        suppress_certificate_warnings: bool = False,
        suppress_consistency_check: bool = False,
    ):
        self.importer_password = importer_password
        self.importer_user_name = importer_user_name
        self.fwo_api_url = fwo_api_url
        self.fwo_user_mgmt_api_uri = fwo_user_mgmt_api_uri
        self.api_fetch_size = api_fetch_size
        self.major_version = major_version
        self.sleep_timer = sleep_timer
        self.force = force
        self.clear = clear
        self.debug_level = debug_level
        self.ssl_verification = ssl_verification
        self.suppress_certificate_warnings = suppress_certificate_warnings
        self.suppress_consistency_check = suppress_consistency_check

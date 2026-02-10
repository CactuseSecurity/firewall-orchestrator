"""
the configuraton of a firewall orchestrator itself
as read from the global config file including FWO URI
"""


class FwoConfig:
    fwo_api_url: str
    fwo_user_mgmt_api_uri: str | None
    api_fetch_size: int
    importer_password: str | None
    major_version: int | None = None

    def __init__(
        self,
        fwo_api_url: str,
        fwo_user_mgmt_api_uri: str | None,
        api_fetch_size: int = 150,
        importer_password: str | None = None,
        major_version: int | None = None,
    ):
        self.fwo_api_url = fwo_api_url
        self.fwo_user_mgmt_api_uri = fwo_user_mgmt_api_uri
        self.api_fetch_size = api_fetch_size
        self.importer_password = importer_password
        self.major_version = major_version

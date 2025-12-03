"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""

class FworchConfig:
    fwo_api_url: str
    fwo_user_mgmt_api_uri: str | None
    api_fetch_size: int
    importer_password: str | None

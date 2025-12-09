from typing import Any

from models.fworch_config import FworchConfig

"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""


class FworchConfigController(FworchConfig):
    def __init__(
        self,
        fwo_api_url: str | None,
        fwo_user_mgmt_api_uri: str | None,
        importer_pwd: str | None,
        api_fetch_size: int = 500,
    ):
        if fwo_api_url is not None:
            self.fwo_api_url = fwo_api_url
        else:
            self.fwo_api_fwo_user_mgmt_api_uri = None
        if fwo_user_mgmt_api_uri is not None:
            self.fwo_user_mgmt_api_uri = fwo_user_mgmt_api_uri
        else:
            self.fwo_user_mgmt_api_uri = None
        self.importer_password = importer_pwd
        self.api_fetch_size = api_fetch_size

    @classmethod
    def from_json(cls, json_dict: dict[str, Any]) -> "FworchConfigController":
        fwo_api_uri = json_dict["fwo_api_base_url"]
        fwo_user_mgmt_api_uri = json_dict["user_management_api_base_url"]
        fwo_importer_pwd = json_dict.get("importerPassword")

        return cls(fwo_api_uri, fwo_user_mgmt_api_uri, fwo_importer_pwd)

    def __str__(self):
        return f"{self.fwo_api_url}, {self.fwo_user_mgmt_api_uri}, {self.api_fetch_size}"  # type: ignore

    def set_importer_pwd(self, importer_password: str | None):
        self.importer_password = importer_password

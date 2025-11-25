"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
from pydantic import Field, BaseModel


class FworchConfig(BaseModel):
    fwo_api_url: str = Field(description="Firewall Orchestrator API URL", alias="FwoApiUrl")
    fwo_user_mgmt_api_uri: str | None = Field(description="Firewall Orchestrator User Management API URI", alias="FwoUserMgmtApiUri")
    api_fetch_size: int = Field(description="Number of items to fetch per API call", alias="ApiFetchSize", default=500)
    importer_password: str | None = Field(description="Password for the importer to authenticate with FWO", alias="ImporterPassword")

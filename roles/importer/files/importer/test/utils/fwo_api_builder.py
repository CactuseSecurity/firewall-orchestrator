from fwo_api import FwoApi
from query_analyzer import QueryAnalyzer


def build_fwo_api(
    api_uri: str,
    jwt: str,
    refresh_token: str | None = None,
    user_management_api_base_url: str = "",
) -> FwoApi:
    """
    Create an FwoApi holding the given token pair without running the login done in __init__.
    """
    fwo_api = FwoApi.__new__(FwoApi)
    fwo_api.fwo_api_url = api_uri
    fwo_api.fwo_jwt = jwt
    fwo_api.fwo_refresh_token = refresh_token
    fwo_api.user_management_api_base_url = user_management_api_base_url
    fwo_api.query_info = {}
    fwo_api.query_analyzer = QueryAnalyzer()
    return fwo_api

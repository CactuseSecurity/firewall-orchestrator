"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FworchConfig():
    FwoApiUri: str
    FwoUserMgmtApiUri: str | None
    ApiFetchSize: int
    ImporterPassword: str | None

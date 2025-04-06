"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FworchConfig():
    FwoApiUri: str
    FwoUserMgmtApiUri: str
    ApiFetchSize: int
    ImporterPassword: str

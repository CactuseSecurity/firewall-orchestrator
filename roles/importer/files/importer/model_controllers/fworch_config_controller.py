from models.fworch_config import FworchConfig

"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FworchConfigController(FworchConfig):

    def __init__(self, fwoApiUri, fwoUserMgmtApiUri, importerPwd, apiFetchSize=500):
        if fwoApiUri is not None:
            self.FwoApiUri = fwoApiUri
        else:
            self.FwoApiUFwoUserMgmtApiri = None
        if fwoUserMgmtApiUri is not None:
            self.FwoUserMgmtApiUri = fwoUserMgmtApiUri
        else:
            self.FwoUserMgmtApiUri = None
        self.ImporterPassword = importerPwd
        self.ApiFetchSize = apiFetchSize

    @classmethod
    def fromJson(cls, json_dict):
        fwoApiUri = json_dict['fwo_api_base_url']
        fwoUserMgmtApiUri = json_dict['user_management_api_base_url']
        if 'importerPassword' in json_dict:
            fwoImporterPwd = json_dict['importerPassword']
        else:
            fwoImporterPwd = None
        
        return cls(fwoApiUri, fwoUserMgmtApiUri, fwoImporterPwd)

    def __str__(self):
        return f"{self.FwoApiUri}, {self.FwoUserMgmtApi}, {self.ApiFetchSize}"

    def setImporterPwd(self, importerPassword):
        self.ImporterPassword = importerPassword        

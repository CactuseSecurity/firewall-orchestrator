from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized

# this class provides the base objects for importing a config into the FWO API
class FwConfigImportBase():
    ImportDetails: ImportStateController
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
        self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        self.NormalizedConfig = config

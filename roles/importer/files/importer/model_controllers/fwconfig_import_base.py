from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.group_flats_mapper import GroupFlatsMapper
from model_controllers.uid2id_mapper import Uid2IdMapper

# this class provides the base objects for importing a config into the FWO API
class FwConfigImportBase():
    ImportDetails: ImportStateController
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
        self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        self.NormalizedConfig = config
        self.group_flats_mapper = GroupFlatsMapper(importState, config)
        self.uid2id_mapper = Uid2IdMapper(importState)

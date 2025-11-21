import traceback
import fwo_const
from fwo_log import FWOLogger
from fwo_api import FwoApi
from model_controllers.import_state_controller import ImportStateController
from services.service_provider import ServiceProvider

# this class is used for rolling back an import
class FwConfigImportRollback():
    ImportDetails: ImportStateController

    def __init__(self):
        service_provider = ServiceProvider()
        global_state = service_provider.get_global_state()
        self.ImportDetails = global_state.import_state

        
    # this function deletes all new entries added in this import
    # also resets all entries that have been marked removed
    # also deletes latest_config for this management
    # TODO: also take super management id into account as second option

    def rollbackCurrentImport(self) -> None | int:
        rollbackMutation = FwoApi.get_graphql_code([f"{fwo_const.graphql_query_path}import/rollbackImport.graphql"])
        try:
            query_variables = {
                'importId': self.ImportDetails.ImportId
            }
            rollbackResult = self.ImportDetails.api_call.call(rollbackMutation, query_variables=query_variables)
            if 'errors' in rollbackResult:
                FWOLogger.exception("error while trying to roll back current import for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(rollbackResult['errors']))
                return 1 # error
            else:
                FWOLogger.info("import " + str(self.ImportDetails.ImportId) + " has been rolled back successfully")

        except Exception:
            FWOLogger.exception(f"failed to rollback current importfor mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        return 0

import traceback
import fwo_const
from fwo_log import FWOLogger
from fwo_api import FwoApi
from model_controllers.import_state_controller import ImportStateController
from services.service_provider import ServiceProvider

# this class is used for rolling back an import
class FwConfigImportRollback():
    import_state: ImportStateController

    def __init__(self):
        service_provider = ServiceProvider()
        global_state = service_provider.get_global_state()
        self.import_state = global_state.import_state

        
    # this function deletes all new entries added in this import
    # also resets all entries that have been marked removed
    # also deletes latest_config for this management
    # TODO: also take super management id into account as second option

    def rollback_current_import(self):
        rollback_mutation = FwoApi.get_graphql_code([f"{fwo_const.GRAPHQL_QUERY_PATH}import/rollbackImport.graphql"])
        try:
            query_variables = {
                'importId': self.import_state.import_id
            }
            rollback_result = self.import_state.api_call.call(rollback_mutation, query_variables=query_variables)
            if 'errors' in rollback_result:
                FWOLogger.exception("error while trying to roll back current import for mgm id " +
                                str(self.import_state.mgm_details.mgm_id) + ": " + str(rollback_result['errors']))
            else:
                FWOLogger.info("import " + str(self.import_state.import_id) + " has been rolled back successfully")

        except Exception:
            FWOLogger.exception(f"failed to rollback current importfor mgm id {str(self.import_state.mgm_details.mgm_id)}: {str(traceback.format_exc())}")

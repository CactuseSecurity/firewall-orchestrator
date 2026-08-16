import traceback

import fwo_const
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_log import FWOLogger
from models.import_state import ImportState


# this class is used for rolling back an import
class FwConfigImportRollback:
    # this function deletes all new entries added in this import
    # also resets all entries that have been marked removed
    # also deletes latest_config for this management
    # TODO: also take super management id into account as second option

    def rollback_current_import(self, import_state: ImportState, fwo_api_call: FwoApiCall):
        # data-only rollback: keeps the import_control row (stamped as failed by unlock_import)
        rollback_mutation = FwoApi.get_graphql_code(
            [f"{fwo_const.GRAPHQL_QUERY_PATH}import/rollbackImportData.graphql"]
        )
        try:
            query_variables = {"importIds": [import_state.import_id]}
            rollback_result = fwo_api_call.call(rollback_mutation, query_variables=query_variables)
            if "errors" in rollback_result:
                FWOLogger.exception(
                    "error while trying to roll back current import for mgm id "
                    + str(import_state.mgm_details.mgm_id)
                    + ": "
                    + str(rollback_result["errors"])
                )
            else:
                FWOLogger.info("import " + str(import_state.import_id) + " has been rolled back successfully")

        except Exception:
            FWOLogger.exception(
                f"failed to rollback current import for mgm id {import_state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )

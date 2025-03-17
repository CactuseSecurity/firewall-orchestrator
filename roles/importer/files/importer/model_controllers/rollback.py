import traceback
from fwo_log import getFwoLogger
from model_controllers.fwconfig_import import FwConfigImport

# this class is used for rolling back an import
class FwConfigImportRollback(FwConfigImport):

    def __init__(self, config: FwConfigImport):
        self.ImportDetails = config.ImportDetails
        self.NormalizedConfig = config.NormalizedConfig
        self.NetworkObjectTypeMap = config.NetworkObjectTypeMap
        self.ServiceObjectTypeMap = config.ServiceObjectTypeMap
        self.UserObjectTypeMap = config.UserObjectTypeMap
        
    # this function deletes all new entries added in this import
    # also resets all entries that have been marked removed
    # also deletes latest_config for this management
    # TODO: use mutation from file roles/lib/files/FWO.Api.Client/APIcalls/import/rollback.graphql
    #       but currently we cannot guarantee that lib is present on the importer machine!?
    #       so we might have to move APIcalls to common role
    def rollbackCurrentImport(self) -> None:
        logger = getFwoLogger()
        rollbackMutation = """
            mutation rollbackCurrentImport($mgmId: Int!, $lastImportId: bigint!) {
            delete_rule(where: {mgm_id: {_eq: $mgmId}, rule_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rulebase(where: {mgm_id: {_eq: $mgmId}, created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_object(where: {mgm_id: {_eq: $mgmId}, obj_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_service(where: {mgm_id: {_eq: $mgmId}, svc_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_usr(where: {mgm_id: {_eq: $mgmId}, user_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_zone(where: {mgm_id: {_eq: $mgmId}, zone_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_objgrp(where: {import_created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_svcgrp(where: {import_created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_usergrp(where: {import_created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_objgrp_flat(where: {import_created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_svcgrp_flat(where: {import_created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_usergrp_flat(where: {import_created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rule_to(where: {rt_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rule_from(where: {rf_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rule_service(where: {rs_create: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rule_nwobj_resolved(where: {mgm_id: {_eq: $mgmId}, created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rule_svc_resolved(where: {mgm_id: {_eq: $mgmId}, created: {_eq: $lastImportId}}) {
                affected_rows
            }
            delete_rule_user_resolved(where: {mgm_id: {_eq: $mgmId}, created: {_eq: $lastImportId}}) {
                affected_rows
            }
            update_rule(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rulebase(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_object(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_service(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_usr(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_zone(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_objgrp(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_svcgrp(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_usergrp(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_objgrp_flat(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_svcgrp_flat(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_usergrp_flat(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rule_to(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rule_from(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rule_service(where: {removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rule_nwobj_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rule_svc_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            update_rule_user_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $lastImportId}}, _set: {removed: null}) {
                affected_rows
            }
            delete_latest_config (where:{mgm_id: {_eq:$mgmId}}) {
                affected_rows
            }
            delete_import_control(where: {mgm_id: {_eq: $mgmId}, control_id: {_eq: $lastImportId}}) {
                affected_rows
            }
            }
        """
        try:
            queryVariables = {
                'mgmId': self.ImportDetails.MgmDetails.Id, 
                'currentImportId': self.ImportDetails.ImportId
            }
            rollbackResult = self.ImportDetails.call(rollbackMutation, queryVariables=queryVariables)
            if 'errors' in rollbackResult:
                logger.exception("error while trying to roll back current import for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(rollbackResult['errors']))
                return 1 # error
            else:
                logger.info("import " + str(self.ImportDetails.ImportId) + " has been rolled back successfully")

        except:
            logger.exception(f"failed to rollback current importfor mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        return 0
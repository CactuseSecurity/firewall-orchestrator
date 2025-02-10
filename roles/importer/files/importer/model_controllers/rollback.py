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
        
    def rollbackCurrentImport(self) -> None:
        logger = getFwoLogger()
        rollbackMutation = """
            mutation rollbackCurrentImport($mgmId: Int!, $currentImportId: bigint!) {
                delete_rule(where: {mgm_id: {_eq: $mgmId}, rule_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_object(where: {mgm_id: {_eq: $mgmId}, obj_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_service(where: {mgm_id: {_eq: $mgmId}, svc_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_usr(where: {mgm_id: {_eq: $mgmId}, user_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_zone(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_objgrp(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_svcgrp(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_usergrp(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_objgrp_flat(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_svcgrp_flat(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_usergrp_flat(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_to(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_from(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_service(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_nwobj_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_svc_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_user_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                update_rule(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_object(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_service(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_usr(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_zone(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_objgrp(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_svcgrp(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_usergrp(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_objgrp_flat(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_svcgrp_flat(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_usergrp_flat(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_to(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_from(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_service(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_nwobj_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_svc_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_user_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
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
        except:
            logger.exception(f"failed to rollback current importfor mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        return 0
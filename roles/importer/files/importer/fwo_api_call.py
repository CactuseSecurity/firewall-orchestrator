# library for all FWORCH API calls in importer module
import traceback
import json
import datetime
import time
from typing import TYPE_CHECKING, Any

import fwo_const
import fwo_globals
from fwo_log import FWOLogger
from fwo_api import FwoApi
from fwo_exceptions import FwoApiFailedLockImport
from query_analyzer import QueryAnalyzer
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController

# NOTE: we cannot import ImportState(Controller) here due to circular refs

class FwoApiCall(FwoApi):

    def __init__(self, api: FwoApi):
        self.fwo_api_url = api.fwo_api_url
        self.fwo_jwt = api.fwo_jwt
        self.query_info = {}
        self.query_analyzer = QueryAnalyzer()


    def get_mgm_ids(self, query_variables: dict[str, list[Any]] = {}) -> list[int]:
        # from 9.0 do not import sub-managers separately
        mgm_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "device/getManagementWithSubs.graphql"])
        result = self.call(mgm_query, query_variables=query_variables)
        if 'data' in result and 'management' in result['data']:
            return [mgm['id'] for mgm in result['data']['management']]
        return [] 


    def get_config_value(self, key: str='limit') -> str|None:
        query_variables: dict[str, str] = {'key': key}
        cfg_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "config/getConfigValue.graphql"])
        
        try:
            result = self.call(cfg_query, query_variables=query_variables)
        except Exception:
            FWOLogger.error("fwo_api: failed to get config value for key " + key)
            return None

        if 'data' in result and 'config' in result['data']:
            first_result = result['data']['config'][0]
            if 'config_value' in first_result:
                return first_result['config_value']
            else:
                return None
        else:
            return None


    def get_config_values(self, keyFilter:str='limit') -> dict[str, str]|None:
        query_variables: dict[str, str] = {'keyFilter': keyFilter+"%"}
        config_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "config/getConfigValuesByKeyFilter.graphql"])
        
        try:
            result = self.call(config_query, query_variables=query_variables)
        except Exception:
            FWOLogger.error("fwo_api: failed to get config values for key filter " + keyFilter)
            return None

        if 'data' in result and 'config' in result['data']:
            resultArray = result['data']['config']
            dict1 = {v['config_key']: v['config_value'] for _,v in enumerate(resultArray)}
            return dict1
        else:
            return None


    # this mgm field is used by mw dailycheck scheduler
    def log_import_attempt(self, mgm_id: int, successful: bool = False):
        now = datetime.datetime.now().isoformat()
        query_variables: dict[str, Any] = { "mgmId": mgm_id, "timeStamp": now, "success": successful }
        mgm_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/updateManagementLastImportAttempt.graphql"])
        return self.call(mgm_mutation, query_variables=query_variables)


    def setImportLock(self, mgm_details: ManagementController, is_full_import: int = False, is_initial_import: int = False, debug_level: int = 0) -> int:
        import_id = -1
        mgm_id = mgm_details.Id
        try: # set import lock
            lock_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/addImport.graphql"])
            lock_result = self.call(lock_mutation, 
                            query_variables={"mgmId": mgm_id, "isFullImport": is_full_import, "isInitialImport": is_initial_import })
            if lock_result['data']['insert_import_control']['returning'][0]['control_id']:
                import_id = lock_result['data']['insert_import_control']['returning'][0]['control_id']
            return import_id
        except Exception:
            FWOLogger.error("import_management - failed to get import lock for management id " + str(mgm_id))
            if import_id == -1:
                self.create_data_issue(mgm_id=int(mgm_id), severity=1, 
                    description="failed to get import lock for management id " + str(mgm_id))
                self.set_alert(import_id=import_id, title="import error", mgm_id=mgm_id, severity=1, \
                    description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=mgm_details)
                raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgm_id)) from None
            else:
                return import_id


    def count_rule_changes_per_import(self, import_id: int):
        change_count_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getRuleChangesPerImport.graphql"])
        try:
            count_result = self.call(change_count_query, query_variables={'importId': import_id})
            rule_changes_in_import = int(count_result['data']['changelog_rule_aggregate']['aggregate']['count'])
        except Exception as e:
            FWOLogger.exception(f"failed to count changes for import id {str(import_id)}: {str(e)}")
            rule_changes_in_import = 0
        return rule_changes_in_import


    def count_any_changes_per_import(self, import_id: int):
        change_count_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getChangesPerImport.graphql"])
        try:
            count_result = self.call(change_count_query, query_variables={'importId': import_id})
            changes_in_import = int(count_result['data']['changelog_object_aggregate']['aggregate']['count']) + \
                int(count_result['data']['changelog_service_aggregate']['aggregate']['count']) + \
                int(count_result['data']['changelog_user_aggregate']['aggregate']['count']) + \
                int(count_result['data']['changelog_rule_aggregate']['aggregate']['count'])
        except Exception as e:
            FWOLogger.exception(f"failed to count changes for import id {str(import_id)}: {str(e)}")
            changes_in_import = 0
        return changes_in_import


    def unlock_import(self, import_state: 'ImportStateController'):
        import_id = import_state.ImportId
        mgm_id = import_state.MgmDetails.Id
        import_stats = import_state.Stats
        
        try:    
            query_variables: dict[str, Any] = {
                "stopTime": datetime.datetime.now().isoformat(), 
                "importId": import_id,
                "success": import_stats.ErrorCount == 0, 
                "anyChangesFound": import_stats.getTotalChangeNumber() > 0, 
                "ruleChangesFound": import_stats.getRuleChangeNumber() > 0, 
                "changeNumber": import_stats.getRuleChangeNumber()
            }

            unlock_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/updateImportStopTime.graphql"])

            unlock_result = self.call(unlock_mutation, query_variables=query_variables)
            if 'errors' in unlock_result:
                raise FwoApiFailedLockImport(unlock_result['errors'])
            _ = unlock_result['data']['update_import_control']['affected_rows']
        except Exception as e:
            FWOLogger.exception("failed to unlock import for management id " + str(mgm_id) + ": " + str(e))
            import_state.increaseErrorCounterByOne()


    #   currently temporarily only working with single chunk
    def import_json_config(self, importState: 'ImportStateController', config: FwConfigNormalized, startImport: bool = True):
        import_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/addImportConfig.graphql"])

        try:
            debug_mode = (fwo_globals.debug_level>0)
            query_vars: dict[str, Any] = {
                'debug_mode': debug_mode,
                'mgmId': importState.MgmDetails.Id,
                'importId': importState.ImportId,
                'config': config,
                'start_import_flag': startImport,
            }
            import_result = self.call(import_mutation, query_variables=query_vars)
            # note: this will not detect errors in triggered stored procedure run
            if 'errors' in import_result:
                FWOLogger.exception("fwo_api:import_json_config - error while writing importable config for mgm id " +
                                str(importState.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes_in_import_control = import_result['data']['insert_import_config']['affected_rows']
        except Exception:
            FWOLogger.exception(f"failed to write normalized config for mgm id {str(importState.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes_in_import_control==1:
            return 0
        else:
            return 1


    def delete_json_config_in_import_table(self, importState: 'ImportStateController', query_variables: dict[str, Any]) -> int:
        delete_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/deleteImportConfig.graphql"])
        try:
            delete_result = self.call(delete_mutation, query_variables=query_variables)
            changes_in_delete_config = delete_result['data']['delete_import_config']['affected_rows']
        except Exception:
            FWOLogger.exception("failed to delete config without changes")
            return -1  # indicating error
        return changes_in_delete_config


    def get_error_string_from_imp_control(self, _: 'ImportStateController', query_variables: dict[str, Any]) -> list[dict[str, Any]]: # TODO: confirm return type
        error_query = "query getErrors($importId:bigint) { import_control(where:{control_id:{_eq:$importId}}) { import_errors } }"
        return self.call(error_query, query_variables=query_variables)['data']['import_control']


    def create_data_issue(self, importId: int | None = None, obj_name: str | None = None, mgm_id: int | None = None, dev_id: int | None = None, severity: int = 1,
            rule_uid: str | None = None, object_type: str | None = None, description: str | None = None, source: str = 'import') -> bool:
        if obj_name=='all' or obj_name=='Original': 
            return True # ignore resolve errors for enriched objects that are not in the native config
        
        create_data_issue_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/addLogEntry.graphql"])
        
        query_variables: dict[str, Any] = {"source": source, "severity": severity }

        if dev_id is not None:
            query_variables.update({"devId": dev_id})
        if mgm_id is not None:
            query_variables.update({"mgmId": mgm_id})
        if obj_name is not None:
            query_variables.update({"objectName": obj_name})
        if object_type is not None:
            query_variables.update({"objectType": object_type})
        if rule_uid is not None:
            query_variables.update({"ruleUid": rule_uid})
        if description is not None:
            query_variables.update({"description": description})

        try:
            result = self.call(create_data_issue_mutation, query_variables=query_variables)
            changes = result['data']['insert_log_data_issue']['returning']
        except Exception as e:
            FWOLogger.error(f"failed to create log_data_issue: {json.dumps(query_variables)}: {str(e)}")
            raise # TODO: or return False?
        return len(changes)==1


    def set_alert(self, import_id: int | None = None, title: str | None = None, mgm_id: int | None = None, dev_id: int | None = None, severity: int | None = 1,
            jsonData: dict[str, Any] | None = None, description: str | None = None, source: str = 'import', user_id: int | None = None, refAlert: str | None = None, alertCode: int | None = None, mgm_details: ManagementController | None = None):

        addAlert_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/addAlert.graphql"])
        getAlert_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/getAlertByManagement.graphql"])
        ackAlert_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/updateAlert.graphql"])

        query_variables = {"source": source }

        self._set_alert_build_query_vars(query_variables, dev_id, user_id, mgm_id, refAlert, title, description, alertCode)

        if jsonData is None:
            jsonData = {}
        if severity is not None:
            jsonData.update({"severity": severity})
        if import_id is not None:
            jsonData.update({"import_id": import_id})
        if mgm_details is not None:
            jsonData.update({"mgm_name": mgm_details.Name})
        query_variables.update({"jsonData": json.dumps(jsonData)})

        try:
            import_result = self.call(addAlert_mutation, query_variables=query_variables)
            newAlertId = import_result['data']['insert_alert']['returning'][0]['newIdLong']
            if alertCode is None or mgm_id is not None:
                return True
            # Acknowledge older alert for same problem on same management
            query_variables: dict[str, Any] = { "mgmId": mgm_id, "alertCode": alertCode, "currentAlertId": newAlertId }
            existingUnacknowledgedAlerts = self.call(getAlert_query, query_variables=query_variables)
            if 'data' not in existingUnacknowledgedAlerts or 'alert' not in existingUnacknowledgedAlerts['data']:
                return False
            for alert in existingUnacknowledgedAlerts['data']['alert']:
                if 'alert_id' in alert:
                    now = datetime.datetime.now().isoformat()
                    query_variables = { "userId": 0, "alertId": alert['alert_id'], "ackTimeStamp": now }
                    _ = self.call(ackAlert_mutation, query_variables=query_variables)
        except Exception as e:
            FWOLogger.error(f"failed to create alert entry: {json.dumps(query_variables)}; exception: {str(e)}")
            raise
        return True

    def _set_alert_build_query_vars(self, query_variables: dict[str, Any], dev_id: int | None, user_id: int | None, mgm_id: int | None, refAlert: str | None, title: str | None, description: str | None, alertCode: int | None):
        if dev_id is not None:
            query_variables.update({"devId": dev_id})
        if user_id is not None:
            query_variables.update({"userId": user_id})
        if mgm_id is not None:
            query_variables.update({"mgmId": mgm_id})
        if refAlert is not None:
            query_variables.update({"refAlert": refAlert})
        if title is not None:
            query_variables.update({"title": title})
        if description is not None:
            query_variables.update({"description": description})
        if alertCode is not None:
            query_variables.update({"alertCode": alertCode})


    def complete_import(self, importState: 'ImportStateController'):
        
        if fwo_globals.shutdown_requested:
            importState.Stats.addError("shutdown requested, aborting import")

        if not importState.responsible_for_importing:
            return
        
        success = (importState.Stats.ErrorCount==0)
        try:
            self.log_import_attempt(importState.MgmDetails.Id, successful=success)
        except Exception:
            FWOLogger.error('error while trying to log import attempt')
            importState.increaseErrorCounterByOne()

        self.unlock_import(importState)

        import_result = "import_management: import no. " + str(importState.ImportId) + \
                " for management " + importState.MgmDetails.Name + ' (id=' + str(importState.MgmDetails.Id) + ")" + \
                str(" threw errors," if importState.Stats.ErrorCount>0 else " successful,") + \
                " total change count: " + str(importState.Stats.getTotalChangeNumber()) + \
                ", rule change count: " + str(importState.Stats.getRuleChangeNumber()) + \
                ", duration: " + str(int(time.time()) - importState.StartTime) + "s" 
        import_result += ", ERRORS: " + importState.getErrorString() if importState.Stats.ErrorCount > 0 else ""
        if importState.Stats.getChangeDetails() != {} and FWOLogger.is_debug_level(4) and len(importState.getErrors()) == 0:
            import_result += ", change details: " + str(importState.Stats.getChangeDetails())
        if importState.Stats.ErrorCount>0:
            self.create_data_issue(importId=importState.ImportId, severity=1, description=importState.getErrorString())
            self.set_alert(import_id=importState.ImportId, title="import error", mgm_id=importState.MgmDetails.Id, severity=2, \
                description=str(importState.getErrorString()), source='import', alertCode=14, mgm_details=importState.MgmDetails)
        if not importState.Stats.ErrorAlreadyLogged:
            FWOLogger.info(import_result.encode().decode("unicode_escape"))
            importState.Stats.ErrorAlreadyLogged = True


    def get_last_complete_import(self, queryVars: dict[str, Any]) -> tuple[int, str]:
        mgm_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getLastCompleteImport.graphql"])
        last_full_import_date: str = ""
        last_full_import_id: int = 0
        try:
            pastDetails = self.call(mgm_query, query_variables=queryVars)
            if len(pastDetails['data']['import_control'])>0:
                last_full_import_date = pastDetails['data']['import_control'][0]['start_time']
                last_full_import_id = pastDetails['data']['import_control'][0]['control_id']
        except Exception as _:
            FWOLogger.error(f"error while getting past import details for mgm {str(queryVars)}: {str(traceback.format_exc())}")
            raise

        return last_full_import_id, last_full_import_date

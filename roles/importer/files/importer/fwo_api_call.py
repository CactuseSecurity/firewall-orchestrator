# library for all FWORCH API calls in importer module
import traceback
import json
import datetime
import time
from typing import TYPE_CHECKING, Any

import fwo_const
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
        mgm_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "device/getManagementWithSubs.graphql"])
        result = self.call(mgm_query, query_variables=query_variables)
        if 'data' in result and 'management' in result['data']:
            return [mgm['id'] for mgm in result['data']['management']]
        return [] 


    def get_config_value(self, key: str='limit') -> str | None:
        query_variables: dict[str, str] = {'key': key}
        cfg_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "config/getConfigValue.graphql"])
        
        try:
            result = self.call(cfg_query, query_variables=query_variables)
        except Exception:
            FWOLogger.error("fwo_api: failed to get config value for key " + key)
            return None

        if 'data' in result and 'config' in result['data']:
            first_result = result['data']['config'][0]
            if 'config_value' in first_result:
                return first_result['config_value']    
        return None


    def get_config_values(self, key_filter:str='limit') -> dict[str, str] | None:
        query_variables: dict[str, str] = {'keyFilter': key_filter+"%"}
        config_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "config/getConfigValuesByKeyFilter.graphql"])
        
        try:
            result = self.call(config_query, query_variables=query_variables)
        except Exception:
            FWOLogger.error("fwo_api: failed to get config values for key filter " + key_filter)
            return None

        if 'data' in result and 'config' in result['data']:
            result_array = result['data']['config']
            dict1 = {v['config_key']: v['config_value'] for _,v in enumerate(result_array)}
            return dict1
        else:
            return None


    # this mgm field is used by mw dailycheck scheduler
    def log_import_attempt(self, mgm_id: int, successful: bool = False):
        now = datetime.datetime.now().isoformat()
        query_variables: dict[str, Any] = { "mgmId": mgm_id, "timeStamp": now, "success": successful }
        mgm_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/updateManagementLastImportAttempt.graphql"])
        return self.call(mgm_mutation, query_variables=query_variables)


    def set_import_lock(self, mgm_details: ManagementController, is_full_import: int = False, is_initial_import: int = False) -> int:
        import_id = -1
        mgm_id = mgm_details.mgm_id
        try: # set import lock
            lock_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/addImport.graphql"])
            lock_result = self.call(lock_mutation, 
                            query_variables={"mgmId": mgm_id, "isFullImport": is_full_import, "isInitialImport": is_initial_import })
            if lock_result['data']['insert_import_control']['returning'][0]['control_id']:
                import_id = lock_result['data']['insert_import_control']['returning'][0]['control_id']
            return import_id
        except Exception:
            FWOLogger.error("import_management - failed to get import lock for management id " + str(mgm_id))
            if import_id == -1:
                self.create_data_issue(mgm_id=mgm_id, severity=1, 
                    description="failed to get import lock for management id " + str(mgm_id))
                self.set_alert(import_id=import_id, title="import error", mgm_id=mgm_id, severity=1, \
                    description="fwo_api: failed to get import lock", source='import', alert_code=15, mgm_details=mgm_details)
                raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgm_id)) from None
            else:
                return import_id


    def count_rule_changes_per_import(self, import_id: int):
        change_count_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getRuleChangesPerImport.graphql"])
        try:
            count_result = self.call(change_count_query, query_variables={'importId': import_id})
            rule_changes_in_import = int(count_result['data']['changelog_rule_aggregate']['aggregate']['count'])
        except Exception as e:
            FWOLogger.exception(f"failed to count changes for import id {str(import_id)}: {str(e)}")
            rule_changes_in_import = 0
        return rule_changes_in_import


    def count_any_changes_per_import(self, import_id: int):
        change_count_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getChangesPerImport.graphql"])
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


    def unlock_import(self, import_state: 'ImportStateController', success: bool = True):
        import_id = import_state.import_id
        mgm_id = import_state.mgm_details.mgm_id
        import_stats = import_state.stats
        
        try:    
            query_variables: dict[str, Any] = {
                "stopTime": datetime.datetime.now().isoformat(), 
                "importId": import_id,
                "success": success,
                "anyChangesFound": import_stats.get_total_change_number() > 0, 
                "ruleChangesFound": import_stats.get_rule_change_number() > 0, 
                "changeNumber": import_stats.get_rule_change_number()
            }

            unlock_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/updateImportStopTime.graphql"])

            unlock_result = self.call(unlock_mutation, query_variables=query_variables)
            if 'errors' in unlock_result:
                raise FwoApiFailedLockImport(unlock_result['errors'])
            _ = unlock_result['data']['update_import_control']['affected_rows']
        except Exception as e:
            FWOLogger.exception("failed to unlock import for management id " + str(mgm_id) + ": " + str(e))


    #   currently temporarily only working with single chunk
    def import_json_config(self, import_state: 'ImportStateController', config: FwConfigNormalized, start_import: bool = True):
        import_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/addImportConfig.graphql"])

        try:
            query_vars: dict[str, Any] = {
                'debug_mode': FWOLogger.is_debug_level(1),
                'mgmId': import_state.mgm_details.mgm_id,
                'importId': import_state.import_id,
                'config': config,
                'start_import_flag': start_import,
            }
            import_result = self.call(import_mutation, query_variables=query_vars)
            # note: this will not detect errors in triggered stored procedure run
            if 'errors' in import_result:
                FWOLogger.exception("fwo_api:import_json_config - error while writing importable config for mgm id " +
                                str(import_state.mgm_details.mgm_id) + ": " + str(import_result['errors']))
            else:
                _ = import_result['data']['insert_import_config']['affected_rows']
        except Exception:
            FWOLogger.exception(f"failed to write normalized config for mgm id {str(import_state.mgm_details.mgm_id)}: {str(traceback.format_exc())}")


    def delete_json_config_in_import_table(self, query_variables: dict[str, Any]):
        delete_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/deleteImportConfig.graphql"])
        try:
            delete_result = self.call(delete_mutation, query_variables=query_variables)
            _ = delete_result['data']['delete_import_config']['affected_rows']
        except Exception:
            FWOLogger.exception("failed to delete config without changes")


    def get_error_string_from_imp_control(self, _: 'ImportStateController', query_variables: dict[str, Any]) -> list[dict[str, Any]]: # TYPING: confirm return type
        error_query = "query getErrors($importId:bigint) { import_control(where:{control_id:{_eq:$importId}}) { import_errors } }"
        return self.call(error_query, query_variables=query_variables)['data']['import_control']


    def create_data_issue(self, obj_name: str | None = None, mgm_id: int | None = None, dev_id: int | None = None, severity: int = 1,
            rule_uid: str | None = None, object_type: str | None = None, description: str | None = None, source: str = 'import') -> None:
        if obj_name=='all' or obj_name=='Original': 
            return # ignore resolve errors for enriched objects that are not in the native config
        
        create_data_issue_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "monitor/addLogEntry.graphql"])
        
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
            if len(changes)!=1:
                FWOLogger.warning(f"create_data_issue: unexpected result creating data issue: {json.dumps(result)}")
        except Exception as e:
            FWOLogger.error(f"failed to create log_data_issue: {json.dumps(query_variables)}: {str(e)}")


    def set_alert(
            self, 
            import_id: int | None = None, 
            title: str | None = None, 
            mgm_id: int | None = None, 
            dev_id: int | None = None, 
            severity: int | None = 1,
            json_data: dict[str, Any] | None = None, 
            description: str | None = None, 
            source: str = 'import', 
            user_id: int | None = None, 
            ref_alert: str | None = None, 
            alert_code: int | None = None, 
            mgm_details: ManagementController | None = None
        ):

        add_alert_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "monitor/addAlert.graphql"])
        get_alert_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "monitor/getAlertByManagement.graphql"])
        ack_alert_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "monitor/updateAlert.graphql"])

        query_variables = {"source": source }

        self._set_alert_build_query_vars(query_variables, dev_id, user_id, mgm_id, ref_alert, title, description, alert_code)

        if json_data is None:
            json_data = {}
        if severity is not None:
            json_data.update({"severity": severity})
        if import_id is not None:
            json_data.update({"import_id": import_id})
        if mgm_details is not None:
            json_data.update({"mgm_name": mgm_details.name})
        query_variables.update({"jsonData": json.dumps(json_data)})

        try:
            import_result = self.call(add_alert_mutation, query_variables=query_variables)
            new_alert_id = import_result['data']['insert_alert']['returning'][0]['newIdLong']
            if alert_code is None or mgm_id is None: #WWS-CHECK: changed: "mgm_id is not None" -> "mgm_id is None" # TODO: review
                return
            
            # Acknowledge older alert for same problem on same management
            query_variables: dict[str, Any] = { "mgmId": mgm_id, "alertCode": alert_code, "currentAlertId": new_alert_id }
            existing_unacknowledged_alerts = self.call(get_alert_query, query_variables=query_variables)
            if 'data' not in existing_unacknowledged_alerts or 'alert' not in existing_unacknowledged_alerts['data']:
                return
            
            for alert in existing_unacknowledged_alerts['data']['alert']:
                if 'alert_id' in alert:
                    now = datetime.datetime.now().isoformat()
                    query_variables = { "userId": 0, "alertId": alert['alert_id'], "ackTimeStamp": now }
                    _ = self.call(ack_alert_mutation, query_variables=query_variables)
        except Exception as e:
            FWOLogger.error(f"failed to create alert entry: {json.dumps(query_variables)}; exception: {str(e)}")
            raise

    def _set_alert_build_query_vars(self, query_variables: dict[str, Any], dev_id: int | None, user_id: int | None, mgm_id: int | None, ref_alert: str | None, title: str | None, description: str | None, alert_code: int | None):
        if dev_id is not None:
            query_variables.update({"devId": dev_id})
        if user_id is not None:
            query_variables.update({"userId": user_id})
        if mgm_id is not None:
            query_variables.update({"mgmId": mgm_id})
        if ref_alert is not None:
            query_variables.update({"refAlert": ref_alert})
        if title is not None:
            query_variables.update({"title": title})
        if description is not None:
            query_variables.update({"description": description})
        if alert_code is not None:
            query_variables.update({"alertCode": alert_code})


    def complete_import(self, import_state: 'ImportStateController', exception: BaseException | None = None):
        if not import_state.responsible_for_importing:
            return

        try:
            self.log_import_attempt(import_state.mgm_details.mgm_id, successful=exception is None)
        except Exception:
            FWOLogger.error('error while trying to log import attempt')

        self.unlock_import(import_state, success=exception is None)

        exception_message: str | None = None
        if exception is not None and hasattr(exception, "message"):
            exception_message = getattr(exception, "message", None)
        else:
            exception_message = str(exception)

        import_result = "import_management: import no. " + str(import_state.import_id) + \
                " for management " + import_state.mgm_details.name + ' (id=' + str(import_state.mgm_details.mgm_id) + ")" + \
                str(" threw errors," if exception is not None else " successful,") + \
                " total change count: " + str(import_state.stats.get_total_change_number()) + \
                ", rule change count: " + str(import_state.stats.get_rule_change_number()) + \
                ", duration: " + str(int(time.time()) - import_state.start_time) + "s" 
        import_result += ", ERRORS: " + exception_message if exception_message is not None else ""
        
        if import_state.stats.get_change_details() != {} and FWOLogger.is_debug_level(4) and exception is None:
            import_result += ", change details: " + str(import_state.stats.get_change_details())
        
        if exception is not None:
            self.create_data_issue(severity=1, description=exception_message)
            self.set_alert(import_id=import_state.import_id, title="import error", mgm_id=import_state.mgm_details.mgm_id, severity=2, \
                description=exception_message, source='import', alert_code=14, mgm_details=import_state.mgm_details)
        
        FWOLogger.info(import_result.encode().decode("unicode_escape"))
            


    def get_last_complete_import(self, query_vars: dict[str, Any]) -> tuple[int, str]:
        mgm_query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLastCompleteImport.graphql"])
        last_full_import_date: str = ""
        last_full_import_id: int = 0
        try:
            past_details = self.call(mgm_query, query_variables=query_vars)
            if len(past_details['data']['import_control'])>0:
                last_full_import_date = past_details['data']['import_control'][0]['start_time']
                last_full_import_id = past_details['data']['import_control'][0]['control_id']
        except Exception as _:
            FWOLogger.error(f"error while getting past import details for mgm {str(query_vars)}: {str(traceback.format_exc())}")
            raise

        return last_full_import_id, last_full_import_date

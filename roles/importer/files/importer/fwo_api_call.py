# library for FWORCH API calls
import traceback
import requests
import json
import datetime
import time
from typing import TYPE_CHECKING
# if TYPE_CHECKING: # prevents circular import problems
#    from model_controllers.import_state_controller import ImportStateController

import fwo_const
import fwo_globals
from fwo_log import getFwoLogger
from fwo_api import FwoApi
from fwo_exceptions import FwoApiFailedLockImport
from query_analyzer import QueryAnalyzer
from model_controllers.import_statistics_controller import ImportStatisticsController
from models.management import Management


class FwoApiCall(FwoApi):

    def __init__(self, api: FwoApi):
        self.FwoApiUrl = api.FwoApiUrl
        self.FwoJwt = api.FwoJwt
        self.query_info = {}
        self.query_analyzer = QueryAnalyzer()


    def get_mgm_ids(self, query_variables):
        # from 9.0 do not import sub-managers separately
        mgm_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "device/getManagementWithSubs.graphql"])
        return self.call(mgm_query, query_variables=query_variables)['data']['management']


    def get_config_value(self, key='limit'):
        query_variables = {'key': key}
        cfg_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "config/getConfigValue.graphql"])
        
        try:
            result = self.call(cfg_query, query_variables=query_variables)
        except Exception:
            logger = getFwoLogger()
            logger.error("fwo_api: failed to get config value for key " + key)
            return None

        if 'data' in result and 'config' in result['data']:
            first_result = result['data']['config'][0]
            if 'config_value' in first_result:
                return first_result['config_value']
            else:
                return None
        else:
            return None


    def get_config_values(self, keyFilter='limit'):
        query_variables = {'keyFilter': keyFilter+"%"}
        config_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "config/getConfigValuesByKeyFilter.graphql"])
        
        try:
            result = self.call(config_query, query_variables=query_variables)
        except Exception:
            logger = getFwoLogger()
            logger.error("fwo_api: failed to get config values for key filter " + keyFilter)
            return None

        if 'data' in result and 'config' in result['data']:
            resultArray = result['data']['config']
            dict1 = {v['config_key']: v['config_value'] for k,v in enumerate(resultArray)}
            return dict1
        else:
            return None


    # this mgm field is used by mw dailycheck scheduler
    def log_import_attempt(self, mgm_id, successful=False):
        now = datetime.datetime.now().isoformat()
        query_variables = { "mgmId": mgm_id, "timeStamp": now, "success": successful }
        mgm_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/updateManagementLastImportAttempt.graphql"])
        return self.call(mgm_mutation, query_variables=query_variables)


    def setImportLock(self, mgm_details: Management, is_full_import: int = False, is_initial_import: int = False, debug_level: int = 0) -> int:
        logger = getFwoLogger(debug_level=debug_level)
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
            logger.error("import_management - failed to get import lock for management id " + str(mgm_id))
            if import_id == -1:
                self.create_data_issue(mgm_id=int(mgm_id), severity=1, 
                    description="failed to get import lock for management id " + str(mgm_id))
                self.set_alert(import_id=import_id, title="import error", mgm_id=str(mgm_id), severity=1, \
                    description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=mgm_details)
                raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgm_id)) from None


    def count_rule_changes_per_import(self, import_id):
        logger = getFwoLogger()
        change_count_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getRuleChangesPerImport.graphql"])
        try:
            count_result = self.call(change_count_query, query_variables={'importId': import_id})
            rule_changes_in_import = int(count_result['data']['changelog_rule_aggregate']['aggregate']['count'])
        except Exception as e:
            logger.exception(f"failed to count changes for import id {str(import_id)}: {str(e)}")
            rule_changes_in_import = 0
        return rule_changes_in_import


    def count_any_changes_per_import(self, import_id):
        logger = getFwoLogger()
        change_count_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getChangesPerImport.graphql"])
        try:
            count_result = self.call(change_count_query, query_variables={'importId': import_id})
            changes_in_import = int(count_result['data']['changelog_object_aggregate']['aggregate']['count']) + \
                int(count_result['data']['changelog_service_aggregate']['aggregate']['count']) + \
                int(count_result['data']['changelog_user_aggregate']['aggregate']['count']) + \
                int(count_result['data']['changelog_rule_aggregate']['aggregate']['count'])
        except Exception as e:
            logger.exception(f"failed to count changes for import id {str(import_id)}: {str(e)}")
            changes_in_import = 0
        return changes_in_import


    def unlock_import(self, import_id: int, mgm_id: int, import_stats: ImportStatisticsController) -> int:
        logger = getFwoLogger()
        error_during_import_unlock = 0
        query_variables = {"stopTime": datetime.datetime.now().isoformat(), "importId": import_id,
                        "success": import_stats.ErrorCount == 0, "anyChangesFound": import_stats.getTotalChangeNumber() > 0, 
                        "ruleChangesFound": import_stats.getRuleChangeNumber() > 0, "changeNumber": import_stats.getRuleChangeNumber()}

        unlock_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/updateImportStopTime.graphql"])

        try:
            unlock_result = self.call(unlock_mutation, query_variables=query_variables)
            changes_in_import_control = unlock_result['data']['update_import_control']['affected_rows']
        except Exception as e:
            logger.exception("failed to unlock import for management id " + str(mgm_id))
            error_during_import_unlock = 1
        return error_during_import_unlock


    #   currently temporarily only working with single chunk
    def import_json_config(self, importState, config, startImport=True):
        logger = getFwoLogger(debug_level=importState.DebugLevel)
        import_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/addImportConfig.graphql"])

        try:
            debug_mode = (fwo_globals.debug_level>0)
            query_vars = {
                'debug_mode': debug_mode,
                'mgmId': importState.MgmDetails.Id,
                'importId': importState.ImportId,
                'config': config,
                'start_import_flag': startImport,
            }
            import_result = self.call(import_mutation, query_variables=query_vars)
            # note: this will not detect errors in triggered stored procedure run
            if 'errors' in import_result:
                logger.exception("fwo_api:import_json_config - error while writing importable config for mgm id " +
                                str(importState.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes_in_import_control = import_result['data']['insert_import_config']['affected_rows']
        except Exception:
            logger.exception(f"failed to write normalized config for mgm id {str(importState.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes_in_import_control==1:
            return 0
        else:
            return 1


    def update_hit_counter(self, importState, normalizedConfig):
        logger = getFwoLogger(debug_level=importState.DebugLevel)
        # currently only data for check point firewalls is collected!

        query_varsLocal = {"mgmId": importState.MgmDetails.Id}
        # prerequesite: rule_uids are unique across a management
        # this is guaranteed for the newer devices
        # older devices like netscreen or FortiGate (via ssh) need to be checked
        # when hits information should be gathered here in the future

        for manager in sorted(normalizedConfig.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            for config in manager.Configs:
                found_hits, last_hit_update_mutation = self._build_hit_mutation(config.rulebases)
                last_hit_update_mutation += " ]) { affected_rows } }"

                if found_hits:
                    self.update_hits_via_api(importState, last_hit_update_mutation, query_varsLocal)
                    return 0
                else:
                    if len(config.rulebases)>0:
                        logger.debug("found only rules without hit information for mgm_id " + str(importState.MgmDetails.Id))
                        return 1

    @staticmethod
    def _build_hit_mutation(rulebases):
        found_hits = False
        # TODO (of minor importance) import rules per gateway to show which gateways have no hits
        last_hit_update_mutation = """
            mutation updateRuleLastHit($mgmId:Int!) {
                update_rule_metadata_many(updates: [
        """

        for rb in rulebases:
            for rule in rb.Rules:
                if 'last_hit' in rule and rule['last_hit'] is not None:
                    found_hits = True
                    update_expr = '{{ where: {{ device: {{ mgm_id:{{_eq:$mgmId}} }} rule_uid: {{ _eq: "{rule_uid}" }} }}, _set: {{ rule_last_hit: "{last_hit}" }} }}, '.format(rule_uid=rule["rule_uid"], last_hit=rule['last_hit'])
                    last_hit_update_mutation += update_expr
        return found_hits, last_hit_update_mutation


    def update_hits_via_api(self, importState, last_hit_update_mutation, query_varsLocal):
        try:
            update_result = self.call(last_hit_update_mutation, query_variables=query_varsLocal)
            if 'errors' in update_result:
                getFwoLogger().logger.exception("fwo_api:update_hit_counter - error while updating hit counters for mgm id " +
                                str(importState.MgmDetails.Id) + ": " + str(update_result['errors']))
            update_counter = len(update_result['data']['update_rule_metadata_many'])
        except Exception:
            getFwoLogger().logger.exception("failed to update hit counter for mgm id " + str(importState.MgmDetails.Id))
            return 1 # error
        
        return 0


    def delete_json_config_in_import_table(self, importState, query_variables):
        logger = getFwoLogger(debug_level=importState.DebugLevel)
        delete_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/deleteImportConfig.graphql"])
        try:
            delete_result = self.call(delete_mutation, query_variables=query_variables)
            changes_in_delete_config = delete_result['data']['delete_import_config']['affected_rows']
        except Exception:
            logger.exception("failed to delete config without changes")
            return -1  # indicating error
        return changes_in_delete_config


    def get_error_string_from_imp_control(self, importState, query_variables):
        error_query = "query getErrors($importId:bigint) { import_control(where:{control_id:{_eq:$importId}}) { import_errors } }"
        return self.call(error_query, query_variables=query_variables)['data']['import_control']


    def create_data_issue(self, import_id=None, obj_name=None, mgm_id=None, dev_id=None, severity=1,
            rule_uid=None, object_type=None, description=None, source='import'):
        logger = getFwoLogger()
        if obj_name=='all' or obj_name=='Original': 
            return True # ignore resolve errors for enriched objects that are not in the native config
        
        create_data_issue_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/addLogEntry.graphql"])
        
        query_variables = {"source": source, "severity": severity }

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
            import_result = self.call(create_data_issue_mutation, query_variables=query_variables, role=role)
            changes = import_result['data']['insert_log_data_issue']['affected_rows']
        except Exception:
            logger.error("failed to create log_data_issue: " + json.dumps(query_variables))
            return False
        return changes==1


    def set_alert(self, import_id=None, title=None, mgm_id=None, dev_id=None, severity=1,
            jsonData=None, description=None, source='import', user_id=None, refAlert=None, alertCode=None, mgm_details = None):

        logger = getFwoLogger()

        addAlert_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/addAlert.graphql"])
        getAlert_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/getAlertByManagement.graphql"])
        ackAlert_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "monitor/updateAlert.graphql"])

        query_variables = {"source": source }

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

        if jsonData is None:
            jsonData = {}
        if severity != None:
            jsonData.update({"severity": severity})
        if import_id != None:
            jsonData.update({"import_id": import_id})
        if mgm_details != None:
            jsonData.update({"mgm_name": mgm_details.Name})
        query_variables.update({"jsonData": json.dumps(jsonData)})

        try:
            import_result = self.call(addAlert_mutation, query_variables=query_variables, role=role)
            newAlertId = import_result['data']['insert_alert']['returning'][0]['newIdLong']
            if alertCode is None or mgm_id is not None:
                return True
            # Acknowledge older alert for same problem on same management
            query_variables = { "mgmId": mgm_id, "alertCode": alertCode, "currentAlertId": newAlertId }
            existingUnacknowledgedAlerts = call(self, getAlert_query, query_variables=query_variables, role=role)
            if 'data' in existingUnacknowledgedAlerts and 'alert' in existingUnacknowledgedAlerts['data']:
                for alert in existingUnacknowledgedAlerts['data']['alert']:
                    if 'alert_id' in alert:
                        now = datetime.datetime.now().isoformat()
                        query_variables = { "userId": 0, "alertId": alert['alert_id'], "ackTimeStamp": now }
                        updateResult = self.call(ackAlert_mutation, query_variables=query_variables, role=role)
        except Exception:
            logger.error("failed to create alert entry: " + json.dumps(query_variables))
            return False
        return True


    def complete_import(self, importState: "ImportStateController"):
        logger = getFwoLogger(debug_level=importState.DebugLevel)
        
        if fwo_globals.shutdown_requested:
            self.addError("shutdown requested, aborting import")

        if not importState.responsible_for_importing:
            return
        
        success = (importState.Stats.ErrorCount==0)
        try:
            self.log_import_attempt(importState.MgmDetails.Id, successful=success)
        except Exception:
            logger.error('error while trying to log import attempt')
            importState.increaseErrorCounterByOne()

        try: # finalize import by unlocking it
            importState.increaseErrorCounter(self.unlock_import(importState.ImportId, importState.MgmDetails.Id, importState.Stats))
        except Exception:
            logger.error("import_management - unspecified error while unlocking import: " + str(traceback.format_exc()))
            importState.increaseErrorCounterByOne()

        import_result = "import_management: import no. " + str(importState.ImportId) + \
                " for management " + importState.MgmDetails.Name + ' (id=' + str(importState.MgmDetails.Id) + ")" + \
                str(" threw errors," if importState.Stats.ErrorCount>0 else " successful,") + \
                " total change count: " + str(importState.Stats.getTotalChangeNumber()) + \
                ", rule change count: " + str(importState.Stats.getRuleChangeNumber()) + \
                ", duration: " + str(int(time.time()) - importState.StartTime) + "s" 
        import_result += ", ERRORS: " + importState.getErrorString() if importState.Stats.ErrorCount > 0 else ""
        if importState.Stats.getChangeDetails() != {} and importState.DebugLevel>3 and len(importState.getErrors()) == 0:
            import_result += ", change details: " + str(importState.Stats.getChangeDetails())
        if importState.Stats.ErrorCount>0:
            self.create_data_issue(import_id=importState.ImportId, severity=1, description=importState.getErrorString())
            self.set_alert(import_id=importState.ImportId, title="import error", mgm_id=importState.MgmDetails.Id, severity=2, \
                description=str(importState.getErrorString()), source='import', alertCode=14, mgm_details=importState.MgmDetails)
        if not importState.Stats.ErrorAlreadyLogged:
            logger.info(import_result.encode().decode("unicode_escape"))
            importState.Stats.ErrorAlreadyLogged = True


    def get_last_import(self, query_vars, debug_level=0):
        mgm_query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getLastImport.graphql"])
        lastFullImportDate = None
        lastFullImportId = None
        try:
            pastDetails = self.call(mgm_query, query_variables=query_vars)
            if len(pastDetails['data']['import_control'])>0:
                lastFullImportDate = pastDetails['data']['import_control'][0]['start_time']
                lastFullImportId = pastDetails['data']['import_control'][0]['control_id']
        except Exception as e:
            logger = getFwoLogger()
            logger.error(f"error while getting past import details for mgm {str(query_vars)}: {str(traceback.format_exc())}")
            raise

        return lastFullImportId, lastFullImportDate

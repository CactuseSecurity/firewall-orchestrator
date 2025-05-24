from models.import_statistics import ImportStatistics

class ImportStatisticsController(ImportStatistics):

    def __init__(self):
        self.ErrorCount = 0
        self.ErrorDetails = []
        self.NetworkObjectAddCount = 0
        self.NetworkObjectDeleteCount = 0
        self.NetworkObjectChangeCount = 0
        self.ServiceObjectAddCount = 0
        self.ServiceObjectDeleteCount = 0
        self.ServiceObjectChangeCount = 0
        self.UserObjectAddCount = 0
        self.UserObjectDeleteCount = 0
        self.UserObjectChangeCount = 0
        self.ZoneObjectAddCount = 0
        self.ZoneObjectDeleteCount = 0
        self.ZoneObjectChangeCount = 0
        self.RuleAddCount = 0
        self.RuleDeleteCount = 0
        self.RuleChangeCount = 0
        self.RuleMoveCount = 0
        self.RulebaseAddCount = 0
        self.rule_enforce_change_count = 0
        self.rulebase_add_count = 0
        self.rulebase_change_count = 0
        self.rulebase_delete_count = 0
        self.ErrorAlreadyLogged = False
    
    def addError(self, error: str):
        self.ErrorCount += 1
        self.ErrorDetails.append(error)

    def getTotalChangeNumber(self):
        return self.NetworkObjectAddCount + self.NetworkObjectDeleteCount + self.NetworkObjectChangeCount + \
            self.ServiceObjectAddCount + self.ServiceObjectDeleteCount + self.ServiceObjectChangeCount + \
            self.UserObjectAddCount + self.UserObjectDeleteCount + self.UserObjectChangeCount + \
            self.ZoneObjectAddCount + self.ZoneObjectDeleteCount + self.ZoneObjectChangeCount + \
            self.RuleAddCount + self.RuleDeleteCount + self.RuleChangeCount + self.RuleMoveCount + \
            self.rule_enforce_change_count + self.rulebase_add_count + self.rulebase_change_count + self.rulebase_delete_count


    def getChangeDetails(self):
        result = {}
        self.collect_nw_obj_change_details(result)
        self.collect_svc_obj_change_details(result)
        self.collect_usr_obj_change_details(result)
        self.collect_zone_obj_change_details(result)
        self.collect_rule_change_details(result)
        return result


    def collect_nw_obj_change_details(self, result):
        if result is None:
            result = {}
        if self.NetworkObjectAddCount > 0:
            result['NetworkObjectAddCount'] = self.NetworkObjectAddCount
        if self.NetworkObjectDeleteCount > 0:
            result['NetworkObjectDeleteCount'] = self.NetworkObjectDeleteCount
        if self.NetworkObjectChangeCount > 0:
            result['NetworkObjectChangeCount'] = self.NetworkObjectChangeCount


    def collect_svc_obj_change_details(self, result):
        if result is None:
            result = {}
        if self.ServiceObjectAddCount > 0:
            result['ServiceObjectAddCount'] = self.ServiceObjectAddCount
        if self.ServiceObjectDeleteCount > 0:
            result['ServiceObjectDeleteCount'] = self.ServiceObjectDeleteCount
        if self.ServiceObjectChangeCount > 0:
            result['ServiceObjectChangeCount'] = self.ServiceObjectChangeCount


    def collect_usr_obj_change_details(self, result):
        if result is None:
            result = {}
        if self.UserObjectAddCount > 0:
            result['UserObjectAddCount'] = self.UserObjectAddCount
        if self.UserObjectDeleteCount > 0:
            result['UserObjectDeleteCount'] = self.UserObjectDeleteCount
        if self.UserObjectChangeCount > 0:
            result['UserObjectChangeCount'] = self.UserObjectChangeCount


    def collect_zone_obj_change_details(self, result):
        if result is None:
            result = {}         
        if self.ZoneObjectAddCount > 0:
            result['ZoneObjectAddCount'] = self.ZoneObjectAddCount
        if self.ZoneObjectDeleteCount > 0:
            result['ZoneObjectDeleteCount'] = self.ZoneObjectDeleteCount
        if self.ZoneObjectChangeCount > 0:
            result['ZoneObjectChangeCount'] = self.ZoneObjectChangeCount


    def collect_rule_change_details(self, result):
        if result is None:
            result = {}         
        if self.RuleAddCount > 0:
            result['RuleAddCount'] = self.RuleAddCount
        if self.RuleDeleteCount > 0:
            result['RuleDeleteCount'] = self.RuleDeleteCount
        if self.RuleChangeCount > 0:
            result['RuleChangeCount'] = self.RuleChangeCount
        if self.RuleMoveCount > 0:
            result['RuleMoveCount'] = self.RuleMoveCount    
        if self.rule_enforce_change_count > 0:
            result['rule_enforce_change_count'] = self.rule_enforce_change_count    
        if self.rulebase_change_count > 0:
            result['rulebase_change_count'] = self.rulebase_change_count
        if self.rulebase_add_count > 0:
            result['rulebase_add_count'] = self.rulebase_add_count
        if self.rulebase_delete_count > 0:
            result['rulebase_delete_count'] = self.rulebase_delete_count

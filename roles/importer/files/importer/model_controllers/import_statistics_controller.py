from models.import_statistics import ImportStatistics


class ImportStatisticsController:
    def __init__(self, statistics: ImportStatistics | None = None):
        self.statistics = statistics if statistics is not None else ImportStatistics()

    def get_total_change_number(self):
        return (
            self.statistics.network_object_add_count
            + self.statistics.network_object_delete_count
            + self.statistics.network_object_change_count
            + self.statistics.service_object_add_count
            + self.statistics.service_object_delete_count
            + self.statistics.service_object_change_count
            + self.statistics.user_object_add_count
            + self.statistics.user_object_delete_count
            + self.statistics.user_object_change_count
            + self.statistics.zone_object_add_count
            + self.statistics.zone_object_delete_count
            + self.statistics.zone_object_change_count
            + self.statistics.rule_add_count
            + self.statistics.rule_delete_count
            + self.statistics.rule_change_count
            + self.statistics.rule_enforce_change_count
            + self.statistics.rulebase_add_count
            + self.statistics.rulebase_change_count
            + self.statistics.rulebase_delete_count
        )

    def get_rule_change_number(self):
        return (
            self.statistics.rule_add_count
            + self.statistics.rule_delete_count
            + self.statistics.rule_change_count
            + self.statistics.rule_enforce_change_count
            + self.statistics.rulebase_add_count
            + self.statistics.rulebase_change_count
            + self.statistics.rulebase_delete_count
        )

    def get_change_details(self):
        result: dict[str, int] = {}
        self.collect_nw_obj_change_details(result)
        self.collect_svc_obj_change_details(result)
        self.collect_usr_obj_change_details(result)
        self.collect_zone_obj_change_details(result)
        self.collect_rule_change_details(result)
        return result

    def collect_nw_obj_change_details(self, result: dict[str, int]):
        if self.statistics.network_object_add_count > 0:
            result["NetworkObjectAddCount"] = self.statistics.network_object_add_count
        if self.statistics.network_object_delete_count > 0:
            result["NetworkObjectDeleteCount"] = self.statistics.network_object_delete_count
        if self.statistics.network_object_change_count > 0:
            result["NetworkObjectChangeCount"] = self.statistics.network_object_change_count

    def collect_svc_obj_change_details(self, result: dict[str, int]):
        if self.statistics.service_object_add_count > 0:
            result["ServiceObjectAddCount"] = self.statistics.service_object_add_count
        if self.statistics.service_object_delete_count > 0:
            result["ServiceObjectDeleteCount"] = self.statistics.service_object_delete_count
        if self.statistics.service_object_change_count > 0:
            result["ServiceObjectChangeCount"] = self.statistics.service_object_change_count

    def collect_usr_obj_change_details(self, result: dict[str, int]):
        if self.statistics.user_object_add_count > 0:
            result["UserObjectAddCount"] = self.statistics.user_object_add_count
        if self.statistics.user_object_delete_count > 0:
            result["UserObjectDeleteCount"] = self.statistics.user_object_delete_count
        if self.statistics.user_object_change_count > 0:
            result["UserObjectChangeCount"] = self.statistics.user_object_change_count

    def collect_zone_obj_change_details(self, result: dict[str, int]):
        if self.statistics.zone_object_add_count > 0:
            result["ZoneObjectAddCount"] = self.statistics.zone_object_add_count
        if self.statistics.zone_object_delete_count > 0:
            result["ZoneObjectDeleteCount"] = self.statistics.zone_object_delete_count
        if self.statistics.zone_object_change_count > 0:
            result["ZoneObjectChangeCount"] = self.statistics.zone_object_change_count

    def collect_rule_change_details(self, result: dict[str, int]):
        if self.statistics.rule_add_count > 0:
            result["RuleAddCount"] = self.statistics.rule_add_count
        if self.statistics.rule_delete_count > 0:
            result["RuleDeleteCount"] = self.statistics.rule_delete_count
        if self.statistics.rule_change_count > 0:
            result["RuleChangeCount"] = self.statistics.rule_change_count
        if self.statistics.rule_move_count > 0:
            result["RuleMoveCount"] = self.statistics.rule_move_count
        if self.statistics.rule_enforce_change_count > 0:
            result["rule_enforce_change_count"] = self.statistics.rule_enforce_change_count
        if self.statistics.rulebase_change_count > 0:
            result["rulebase_change_count"] = self.statistics.rulebase_change_count
        if self.statistics.rulebase_add_count > 0:
            result["rulebase_add_count"] = self.statistics.rulebase_add_count
        if self.statistics.rulebase_delete_count > 0:
            result["rulebase_delete_count"] = self.statistics.rulebase_delete_count

    def increment_network_object_add_count(self, increment: int = 1):
        self.statistics.network_object_add_count += increment

    def increment_network_object_delete_count(self, increment: int = 1):
        self.statistics.network_object_delete_count += increment

    def increment_network_object_change_count(self, increment: int = 1):
        self.statistics.network_object_change_count += increment

    def increment_service_object_add_count(self, increment: int = 1):
        self.statistics.service_object_add_count += increment

    def increment_service_object_delete_count(self, increment: int = 1):
        self.statistics.service_object_delete_count += increment

    def increment_service_object_change_count(self, increment: int = 1):
        self.statistics.service_object_change_count += increment

    def increment_rule_add_count(self, increment: int = 1):
        self.statistics.rule_add_count += increment

    def increment_rule_delete_count(self, increment: int = 1):
        self.statistics.rule_delete_count += increment

    def increment_rule_change_count(self, increment: int = 1):
        self.statistics.rule_change_count += increment

    def increment_rule_move_count(self, increment: int = 1):
        self.statistics.rule_move_count += increment

    def increment_rule_enforce_change_count(self, increment: int = 1):
        self.statistics.rule_enforce_change_count += increment

    def increment_rulebase_link_add_count(self, increment: int = 1):
        self.statistics.rulebase_link_add_count += increment

    def increment_rulebase_link_delete_count(self, increment: int = 1):
        self.statistics.rulebase_link_delete_count += increment

    def increment_rulebase_link_change_count(self, increment: int = 1):
        self.statistics.rulebase_link_change_count += increment

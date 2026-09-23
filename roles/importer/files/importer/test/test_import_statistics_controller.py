from model_controllers.import_statistics_controller import ImportStatisticsController


class TestImportStatisticsController:
    def test_rule_change_number_ignores_non_security_relevant_changes(self):
        # A comment-only change is written to the changelog (rule_change_count) but must not
        # set policy_changes_found / security_relevant_changes_counter, as the change report
        # filters on security_relevant and would otherwise be empty.
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()

        assert stats.get_rule_change_number() == 0
        assert stats.get_total_change_number() == 1

    def test_rule_change_number_counts_security_relevant_changes(self):
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()
        stats.increment_rule_change_count_security_relevant()

        assert stats.get_rule_change_number() == 1
        assert stats.get_total_change_number() == 1

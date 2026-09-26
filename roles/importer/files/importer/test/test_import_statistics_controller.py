from model_controllers.import_statistics_controller import ImportStatisticsController


class TestImportStatisticsController:
    def test_documentation_only_change_counts_as_rule_change_but_not_as_security_relevant(self) -> None:
        # A comment-only change creates a new rule version: it must set policy_changes_found, as
        # the rule_owner prefilter of the variance analysis relies on it (the rule name can carry
        # an owner marker). It must not raise security_relevant_changes_counter, as the change
        # report filters on security_relevant and rule change notifications would be empty.
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()

        assert stats.get_rule_change_number() == 1
        assert stats.get_security_relevant_rule_change_number() == 0
        assert stats.get_total_change_number() == 1

    def test_security_relevant_change_counts_as_both(self) -> None:
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()
        stats.increment_rule_change_count_security_relevant()

        assert stats.get_rule_change_number() == 1
        assert stats.get_security_relevant_rule_change_number() == 1
        assert stats.get_total_change_number() == 1

    def test_rule_adds_deletes_and_rulebase_changes_count_as_both(self) -> None:
        stats = ImportStatisticsController()
        stats.increment_rule_add_count()
        stats.increment_rule_delete_count()
        stats.increment_rulebase_add_count()

        assert stats.get_rule_change_number() == 3
        assert stats.get_security_relevant_rule_change_number() == 3

    def test_collect_rule_change_details_reports_security_relevant_changes(self) -> None:
        # The change details are written to the import result log line, so the security-relevant
        # counter has to be collected there once it is set.
        stats = ImportStatisticsController()
        stats.increment_rule_change_count(3)
        stats.increment_rule_change_count_security_relevant(2)

        result: dict[str, int] = {}
        stats.collect_rule_change_details(result)

        assert result["rule_change_count"] == 3
        assert result["rule_change_count_security_relevant"] == 2

    def test_collect_rule_change_details_omits_security_relevant_changes_when_zero(self) -> None:
        # An import with documentation-only rule changes must not report a security-relevant
        # change counter at all.
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()

        result: dict[str, int] = {}
        stats.collect_rule_change_details(result)

        assert result["rule_change_count"] == 1
        assert "rule_change_count_security_relevant" not in result

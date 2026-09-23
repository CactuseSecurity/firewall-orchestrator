from model_controllers.import_statistics_controller import ImportStatisticsController


class TestImportStatisticsController:
    def test_rule_change_number_ignores_non_security_relevant_changes(self) -> None:
        # A comment-only change is written to the changelog (rule_change_count) but must not
        # set policy_changes_found / security_relevant_changes_counter, as the change report
        # filters on security_relevant and would otherwise be empty.
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()

        assert stats.get_rule_change_number() == 0
        assert stats.get_total_change_number() == 1

    def test_rule_change_number_counts_security_relevant_changes(self) -> None:
        stats = ImportStatisticsController()
        stats.increment_rule_change_count()
        stats.increment_rule_change_count_security_relevant()

        assert stats.get_rule_change_number() == 1
        assert stats.get_total_change_number() == 1

    def test_collect_rule_change_details_reports_security_relevant_changes(self) -> None:
        # The import overview reads rule_change_count_security_relevant from the import details,
        # so the counter has to be collected there once it is set.
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

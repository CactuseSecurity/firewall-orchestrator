using FWO.Api.Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleQueriesTest
    {
        [Test]
        public void GetRuleDetailsById_ShouldSelectTypeAndIdForTopLevelSourceObjects()
        {
            string fromBlock = ExtractObjectBlock(
                RuleQueries.getRuleDetailsById,
                "rule_froms(",
                "objgrp_flats(");

            StringAssert.Contains("obj_id", fromBlock);
            StringAssert.Contains("type: stm_obj_typ {", fromBlock);
        }

        [Test]
        public void GetRuleDetailsById_ShouldSelectTypeAndIdForTopLevelDestinationObjects()
        {
            string toBlock = ExtractObjectBlock(
                RuleQueries.getRuleDetailsById,
                "rule_tos(",
                "objgrp_flats(");

            StringAssert.Contains("obj_id", toBlock);
            StringAssert.Contains("type: stm_obj_typ {", toBlock);
        }

        private static readonly List<(string name, string fragments, string labelFragment)> kReportFragmentSets =
        [
            (nameof(RuleQueries.ruleOverviewFragments), RuleQueries.ruleOverviewFragments, "ruleLabelsAtReportTime"),
            (nameof(RuleQueries.ruleDetailsForReportFragments), RuleQueries.ruleDetailsForReportFragments, "ruleLabelsAtReportTime"),
            (nameof(RuleQueries.natRuleOverviewFragments), RuleQueries.natRuleOverviewFragments, "ruleLabelsAtReportTime"),
            (nameof(RuleQueries.ruleOverviewForChangeReportFragments), RuleQueries.ruleOverviewForChangeReportFragments, "ruleLabelsAfterChange"),
            (nameof(RuleQueries.ruleDetailsForChangeReportFragments), RuleQueries.ruleDetailsForChangeReportFragments, "ruleLabelsBeforeChange"),
            (nameof(RecertQueries.ruleOpenRecertFragments), RecertQueries.ruleOpenRecertFragments, "ruleLabelsAtReportTime")
        ];

        /// <summary>
        /// Rule reports load the labels of the rules, and every fragment a report fragment set spreads is defined in it.
        /// </summary>
        [Test]
        public void ReportFragments_LoadRuleLabelsAndDefineEverySpreadFragment()
        {
            Assert.Multiple(() =>
            {
                foreach ((string name, string fragments, string labelFragment) in kReportFragmentSets)
                {
                    HashSet<string> defined = [.. Regex.Matches(fragments, @"fragment\s+(\w+)\s+on").Select(match => match.Groups[1].Value)];
                    List<string> spread = [.. Regex.Matches(fragments, @"\.\.\.\s*(\w+)").Select(match => match.Groups[1].Value).Distinct()];
                    Assert.That(spread, Does.Contain(labelFragment), name);
                    Assert.That(spread.Where(fragment => !defined.Contains(fragment)), Is.Empty, name);
                }
            });
        }

        /// <summary>
        /// Rule-level labels are filtered by the import ids of their change events, like the other time dependent rule data.
        /// </summary>
        [Test]
        public void RuleLabelFragments_FilterByTheImportOfTheReport()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleQueries.ruleOverviewFragments, Does.Contain("created_event: { import_control_id: { _lte: $import_id_end } }"));
                Assert.That(RuleQueries.ruleOverviewFragments, Does.Contain("removed_event: { import_control_id: { _gt: $import_id_start } }"));
                Assert.That(RuleQueries.ruleOverviewFragments, Does.Contain("{ removed_event_id: { _is_null: true } }"));
                Assert.That(RuleQueries.ruleDetailsForChangeReportFragments, Does.Contain("import_control_id: { _lte: $import_id_old }"));
                Assert.That(RuleQueries.ruleDetailsForChangeReportFragments, Does.Contain("import_control_id: { _lte: $import_id_new }"));
            });
        }

        private static string ExtractObjectBlock(string query, string startMarker, string endMarker)
        {
            int startIndex = query.IndexOf(startMarker, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                Assert.Fail($"Could not find '{startMarker}' in the query.");
            }

            int objectStart = query.IndexOf("object{", startIndex, StringComparison.Ordinal);
            if (objectStart < 0)
            {
                objectStart = query.IndexOf("object {", startIndex, StringComparison.Ordinal);
            }
            if (objectStart < 0)
            {
                Assert.Fail($"Could not find object selection after '{startMarker}'.");
            }

            int endIndex = query.IndexOf(endMarker, objectStart, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                Assert.Fail($"Could not find '{endMarker}' after '{startMarker}'.");
            }

            return query.Substring(objectStart, endIndex - objectStart);
        }
    }
}

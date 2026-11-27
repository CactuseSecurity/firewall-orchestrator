using FWO.Api.Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal partial class RuleQueriesTest
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

        [Test]
        public void DetailQueriesOfTheRuleTabDeclareAllUsedVariables()
        {
            string[] queries =
            [
                RuleQueries.getRuleDetails, RuleQueries.getNatRuleDetails, ObjectQueries.getNetworkObjectDetailsByIds,
                ObjectQueries.getNetworkServiceDetailsByIds, ObjectQueries.getUserDetailsByIds
            ];

            foreach (string query in queries)
            {
                HashSet<string> declared = [.. DeclaredVariableRegex().Matches(query).Select(match => match.Groups[1].Value)];
                HashSet<string> used = [.. UsedVariableRegex().Matches(query).Select(match => match.Groups[1].Value)];
                Assert.That(used, Is.Not.Empty);
                Assert.That(used, Is.SubsetOf(declared), query);
            }
        }

        [Test]
        public void GetNatRuleDetails_ShouldReturnDeletedVersions()
        {
            string operation = RuleQueries.getNatRuleDetails[RuleQueries.getNatRuleDetails.IndexOf("query getNatRuleDetails", StringComparison.Ordinal)..];

            Assert.That(operation, Does.Not.Contain("active: { _eq: true }"));
            Assert.That(operation, Does.Contain("{ removed: { _gt: $import_id_start } }"));
            Assert.That(operation, Does.Contain("nat_rule: { _eq: true }"));
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

        [GeneratedRegex(@"\$(\w+)\s*:")]
        private static partial Regex DeclaredVariableRegex();

        [GeneratedRegex(@"\$(\w+)(?!\w|\s*:)")]
        private static partial Regex UsedVariableRegex();
    }
}

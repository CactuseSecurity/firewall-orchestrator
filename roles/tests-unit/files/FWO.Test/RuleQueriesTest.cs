using FWO.Api.Client.Queries;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
                "rule_froms(order_by:{ object: {obj_id: asc}}){",
                "objgrp_flats(");

            StringAssert.Contains("obj_id", fromBlock);
            StringAssert.Contains("type {", fromBlock);
        }

        [Test]
        public void GetRuleDetailsById_ShouldSelectTypeAndIdForTopLevelDestinationObjects()
        {
            string toBlock = ExtractObjectBlock(
                RuleQueries.getRuleDetailsById,
                "rule_tos(order_by: { object: { obj_name: asc } }) {",
                "objgrp_flats(");

            StringAssert.Contains("obj_id", toBlock);
            StringAssert.Contains("type {", toBlock);
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

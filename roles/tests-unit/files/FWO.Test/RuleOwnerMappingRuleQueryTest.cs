using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the containment the full reinitialize rests on. A mapping source query may be narrowed to the
    /// rules its source can map, so its empty result does not say whether a rule base exists - RunFullReinitialize
    /// asks countActiveRulesForOwnerMapping for that. The answer is only usable while that count matches at
    /// least every rule a source query can return: a source reaching past it, or a predicate added to the count,
    /// makes an empty mapping result read as "there is no rule base", which keeps the obsolete mappings active
    /// without recording a run or raising an alert.
    /// <para>
    /// Nothing but that silence reports the mismatch at run time, which is why it is pinned on the query text
    /// here. The queries are compared without whitespace, because their formatting carries no meaning.
    /// </para>
    /// </summary>
    [TestFixture]
    internal class RuleOwnerMappingRuleQueryTest
    {
        /// <summary>
        /// The rule set countActiveRulesForOwnerMapping counts, written as it appears in the query text.
        /// Pinned exactly: a predicate added here narrows what counts as an existing rule base, which every
        /// mapping source query would have to be narrowed by as well.
        /// </summary>
        private const string kCountedRuleBase = "firewall_rule_aggregate(where:{removed:{_is_null:true}access_rule:{_eq:true}})";

        /// <summary>
        /// The predicates of the counted rule base, each of which a mapping source query has to carry to stay
        /// within it.
        /// </summary>
        private static readonly List<string> kRuleBasePredicates = new() { "removed:{_is_null:true}", "access_rule:{_eq:true}" };

        /// <summary>
        /// Enumerates the rule queries of the mapping sources by name, so a failure points at the file to repair.
        /// </summary>
        private static IEnumerable<TestCaseData> MappingSourceQueries()
        {
            yield return new TestCaseData(RuleQueries.getRulesForOwnerMappingCustomField)
                .SetName("getRulesForOwnerMappingCustomField_StaysWithinTheCountedRuleBase");
            yield return new TestCaseData(RuleQueries.getRulesForOwnerMappingNameField)
                .SetName("getRulesForOwnerMappingNameField_StaysWithinTheCountedRuleBase");
            yield return new TestCaseData(RuleQueries.getRulesForOwnerMappingIpBased)
                .SetName("getRulesForOwnerMappingIpBased_StaysWithinTheCountedRuleBase");
        }

        [Test]
        public void CountQuery_CountsEveryActiveAccessRule()
        {
            string countQuery = WithoutWhitespace(RuleQueries.countActiveRulesForOwnerMapping);

            Assert.That(countQuery, Does.Contain(kCountedRuleBase),
                "the rule base the full reinitialize asks about must stay every active access rule - " +
                "narrowing it makes an empty mapping result read as 'there is no rule base'");
        }

        [TestCaseSource(nameof(MappingSourceQueries))]
        public void MappingSourceQuery_StaysWithinTheCountedRuleBase(string mappingSourceQuery)
        {
            string queryWithoutWhitespace = WithoutWhitespace(mappingSourceQuery);

            Assert.Multiple(() =>
            {
                foreach (string predicate in kRuleBasePredicates)
                {
                    Assert.That(queryWithoutWhitespace, Does.Contain(predicate),
                        $"a mapping source query missing '{predicate}' can return a rule " +
                        "countActiveRulesForOwnerMapping does not count, so an empty result would keep the obsolete mappings");
                }
            });
        }

        /// <summary>
        /// Removes the whitespace of a query, so its predicates can be compared regardless of formatting.
        /// </summary>
        /// <param name="query">Query text to normalize.</param>
        /// <returns>The query without any whitespace.</returns>
        private static string WithoutWhitespace(string query)
        {
            return string.Concat(query.Where(character => !char.IsWhiteSpace(character)));
        }
    }
}

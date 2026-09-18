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
    /// <para>
    /// Every <see cref="RuleQueries"/> access has to stay inside a test body. NUnit evaluates a
    /// <c>TestCaseSource</c> while it builds the test cases, which happens before the
    /// <see cref="TestInitializer"/> set-up fixture points FWO_BASE_DIR at the repository. RuleQueries would
    /// then read its files from the installation path, and a failed type initializer is cached for the whole
    /// process - so one such access fails every test in the assembly that touches a query, not just this one.
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

        [Test]
        public void CountQuery_CountsEveryActiveAccessRule()
        {
            string countQuery = WithoutWhitespace(RuleQueries.countActiveRulesForOwnerMapping);

            Assert.That(countQuery, Does.Contain(kCountedRuleBase),
                "the rule base the full reinitialize asks about must stay every active access rule - " +
                "narrowing it makes an empty mapping result read as 'there is no rule base'");
        }

        [Test]
        public void MappingSourceQueries_StayWithinTheCountedRuleBase()
        {
            // resolved here rather than in a TestCaseSource, see the note on the class
            Dictionary<string, string> mappingSourceQueries = new()
            {
                { nameof(RuleQueries.getRulesForOwnerMappingCustomField), RuleQueries.getRulesForOwnerMappingCustomField },
                { nameof(RuleQueries.getRulesForOwnerMappingNameField), RuleQueries.getRulesForOwnerMappingNameField },
                { nameof(RuleQueries.getRulesForOwnerMappingIpBased), RuleQueries.getRulesForOwnerMappingIpBased }
            };

            Assert.Multiple(() =>
            {
                foreach (KeyValuePair<string, string> mappingSourceQuery in mappingSourceQueries)
                {
                    string queryWithoutWhitespace = WithoutWhitespace(mappingSourceQuery.Value);
                    foreach (string predicate in kRuleBasePredicates)
                    {
                        Assert.That(queryWithoutWhitespace, Does.Contain(predicate),
                            $"{mappingSourceQuery.Key} is missing '{predicate}' and can therefore return a rule " +
                            "countActiveRulesForOwnerMapping does not count, so an empty result would keep the obsolete mappings");
                    }
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

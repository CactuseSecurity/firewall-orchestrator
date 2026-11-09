using System.Reflection;
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
    /// here. The queries are compared without whitespace, because their formatting carries no meaning, and the
    /// source queries are discovered by their name prefix, so a mapping source added later is covered without
    /// anybody remembering this test.
    /// </para>
    /// <para>
    /// Every <see cref="RuleQueries"/> access has to stay inside a test body, the helpers called from one
    /// included. NUnit evaluates a <c>TestCaseSource</c> while it builds the test cases, which happens before
    /// the <see cref="TestInitializer"/> set-up fixture points FWO_BASE_DIR at the repository. RuleQueries would
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
        /// Name prefix every query a mapping source reads its rules from carries. The incremental queries are
        /// named getChangedRulesForRuleOwnerMapping... and are deliberately not covered: they answer which rules
        /// changed, not whether a rule base exists.
        /// </summary>
        private const string kMappingSourceQueryPrefix = "getRulesForOwnerMapping";

        /// <summary>
        /// Start of the rule filter of a mapping source query, up to the object its predicates live in.
        /// </summary>
        private const string kRuleFilterStart = "firewall_rule(where:";

        /// <summary>
        /// The predicates of the counted rule base by their field name, each of which a mapping source query has
        /// to carry in its own firewall_rule filter to stay within it. Only a predicate of the rule itself
        /// counts: the same text nested in a filter on a related object - rule_tos in the IP based query, for
        /// instance - says nothing about which rules are returned.
        /// </summary>
        private static readonly Dictionary<string, string> kRuleBasePredicates = new()
        {
            { "removed", "{_is_null:true}" },
            { "access_rule", "{_eq:true}" }
        };

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
            Dictionary<string, string> mappingSourceQueries = MappingSourceQueries();

            Assert.That(mappingSourceQueries, Is.Not.Empty,
                $"no query named '{kMappingSourceQueryPrefix}...' exists in {nameof(RuleQueries)} anymore, so this " +
                "guard would pass without checking anything - point the prefix at the name the mapping sources use now");

            Assert.Multiple(() =>
            {
                foreach (KeyValuePair<string, string> mappingSourceQuery in mappingSourceQueries)
                {
                    Dictionary<string, string> ruleFilter = TopLevelRuleFilterOf(mappingSourceQuery.Value, mappingSourceQuery.Key);
                    foreach (KeyValuePair<string, string> predicate in kRuleBasePredicates)
                    {
                        ruleFilter.TryGetValue(predicate.Key, out string? actualPredicate);
                        Assert.That(actualPredicate, Is.EqualTo(predicate.Value),
                            $"{mappingSourceQuery.Key} does not filter its rules by '{predicate.Key}: {predicate.Value}' " +
                            "and can therefore return a rule countActiveRulesForOwnerMapping does not count, so an " +
                            "empty result would keep the obsolete mappings");
                    }
                }
            });
        }

        /// <summary>
        /// Collects the query text of every mapping source query by its name prefix, so a source added later is
        /// covered as soon as it follows the naming of the existing ones.
        /// </summary>
        /// <returns>The query text of each mapping source query by its field name.</returns>
        private static Dictionary<string, string> MappingSourceQueries()
        {
            Dictionary<string, string> mappingSourceQueries = new();
            foreach (FieldInfo field in typeof(RuleQueries).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(string) && field.Name.StartsWith(kMappingSourceQueryPrefix, StringComparison.Ordinal))
                {
                    mappingSourceQueries[field.Name] = field.GetValue(null) as string ?? "";
                }
            }
            return mappingSourceQueries;
        }

        /// <summary>
        /// Reads the predicates of the firewall_rule filter of a mapping source query, without the ones nested
        /// in a filter on a related object.
        /// </summary>
        /// <param name="query">Query text of the mapping source.</param>
        /// <param name="queryName">Name of the query, for the failure message.</param>
        /// <returns>The value of each top level predicate by its field name.</returns>
        private static Dictionary<string, string> TopLevelRuleFilterOf(string query, string queryName)
        {
            string queryWithoutWhitespace = WithoutWhitespace(query);
            int filterStart = queryWithoutWhitespace.IndexOf(kRuleFilterStart, StringComparison.Ordinal);
            Assert.That(filterStart, Is.GreaterThanOrEqualTo(0),
                $"{queryName} does not read its rules through '{kRuleFilterStart}...', so this guard cannot tell " +
                "whether it stays within the counted rule base");

            string ruleFilter = BraceGroupAt(queryWithoutWhitespace, filterStart + kRuleFilterStart.Length);
            Assert.That(ruleFilter, Is.Not.Empty, $"the rule filter of {queryName} is not a balanced object");

            return TopLevelEntriesOf(ruleFilter);
        }

        /// <summary>
        /// Cuts the balanced brace group starting at a position out of a text.
        /// </summary>
        /// <param name="text">Text to cut the group out of.</param>
        /// <param name="start">Position of the opening brace.</param>
        /// <returns>The group including its braces, or an empty string if there is none.</returns>
        private static string BraceGroupAt(string text, int start)
        {
            if (start >= text.Length || text[start] != '{')
            {
                return "";
            }

            int depth = 0;
            for (int position = start; position < text.Length; position++)
            {
                depth += text[position] == '{' ? 1 : 0;
                depth -= text[position] == '}' ? 1 : 0;
                if (depth == 0)
                {
                    return text[start..(position + 1)];
                }
            }
            return "";
        }

        /// <summary>
        /// Splits an object of a whitespace free query into its own entries, without descending into the
        /// objects its values are.
        /// </summary>
        /// <param name="braceGroup">Object including its braces.</param>
        /// <returns>The value of each entry by its field name.</returns>
        private static Dictionary<string, string> TopLevelEntriesOf(string braceGroup)
        {
            Dictionary<string, string> entries = new();
            string content = braceGroup[1..^1];
            int position = 0;
            while (position < content.Length)
            {
                int separator = content.IndexOf(':', position);
                if (separator < 0)
                {
                    break;
                }

                string value = ValueAt(content, separator + 1);
                if (value.Length == 0)
                {
                    break;
                }

                entries[content[position..separator].Trim(',')] = value;
                position = separator + 1 + value.Length;
            }
            return entries;
        }

        /// <summary>
        /// Reads the value an entry of a whitespace free object carries, which is an object of its own or a
        /// scalar up to the next entry.
        /// </summary>
        /// <param name="content">Content of the object the entry belongs to.</param>
        /// <param name="start">Position the value starts at.</param>
        /// <returns>The value, or an empty string if there is none.</returns>
        private static string ValueAt(string content, int start)
        {
            if (start >= content.Length)
            {
                return "";
            }
            if (content[start] == '{')
            {
                return BraceGroupAt(content, start);
            }

            int end = content.IndexOf(',', start);
            return end < 0 ? content[start..] : content[start..end];
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

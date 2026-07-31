using System.Text.Json;
using System.Text.RegularExpressions;
using FWO.Basics;
using FWO.Data.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the Hasura select permissions that the report queries built by
    /// <see cref="DynGraphqlQuery"/> depend on. Hasura rejects an aggregate field unless the
    /// requesting role has "allow_aggregations" on the aggregated table, so a missing flag breaks
    /// a report for that role only - invisible to every other test (see the statistics report,
    /// which failed for auditor, modeller, recertifier and reporter on rule_enforced_on_gateway).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ReportAggregationPermissionTest
    {
        /// <summary>
        /// Roles that may open the reporting page (see Pages/Reporting/Report.razor).
        /// The "admin" role is omitted because it is not defined in the Hasura metadata at all
        /// (it bypasses permissions via the admin secret), and "middleware-server" is omitted
        /// because scheduled reports run with a JWT carrying the report owner's roles.
        /// </summary>
        private static readonly List<string> kReportCapableRoles =
        [
            Roles.Auditor,
            Roles.FwAdmin,
            Roles.Reporter,
            Roles.ReporterViewAll,
            Roles.Modeller,
            Roles.Recertifier
        ];

        /// <summary>
        /// Maps every relationship aggregated by a report query to the table it resolves to.
        /// Relationship names are not unique across the schema (for example "services" exists on
        /// both management and modelling owners), so the target table is pinned explicitly here and
        /// kept honest by ReportQueries_AggregateOnlyKnownRelationships.
        /// </summary>
        private static readonly Dictionary<string, string> kAggregatedRelationshipTables = new()
        {
            { "objects", "firewall.nw_object" },
            { "services", "firewall.nw_service" },
            { "usrs", "firewall.nw_user" },
            { "rules", "firewall.rule" },
            { "rule_enforced_on_gateways", "firewall.rule_enforced_on_gateway" },
            { "recertifications", "public.recertification" }
        };

        /// <summary>
        /// Days used for the unused rules filter. The default of int.MaxValue makes the query
        /// compiler overflow, so the test uses the same magnitude the UI offers.
        /// </summary>
        private const int kUnusedForDays = 30;

        private static readonly TimeSpan kRegexTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Matches an aggregate field selection, for example "objects_aggregate(where: ...)".
        /// Aliases such as "rules_aggregate: rule_enforced_on_gateways_aggregate(" do not match,
        /// because only the real field name is followed by an opening parenthesis.
        /// </summary>
        private static readonly Regex kAggregateFieldRegex =
            new(@"([A-Za-z_][A-Za-z0-9_]*)_aggregate\s*\(", RegexOptions.None, kRegexTimeout);

        /// <summary>
        /// Matches an aggregation predicate inside a boolean expression, for example
        /// "recertifications_aggregate: { count: { ... } }".
        /// </summary>
        private static readonly Regex kAggregatePredicateRegex =
            new(@"([A-Za-z_][A-Za-z0-9_]*)_aggregate\s*:\s*\{", RegexOptions.None, kRegexTimeout);

        private static readonly Lazy<JsonDocument?> kMetadata = new(LoadMetadata);

        /// <summary>
        /// Fails as soon as a report query aggregates a relationship that is not listed in
        /// kAggregatedRelationshipTables, so new aggregates cannot silently escape the
        /// permission check below.
        /// </summary>
        [Test]
        public void ReportQueries_AggregateOnlyKnownRelationships()
        {
            HashSet<string> aggregatedRelationships = CollectAggregatedRelationships();

            Assert.That(
                aggregatedRelationships,
                Is.EquivalentTo(kAggregatedRelationshipTables.Keys),
                "Report queries and the aggregated relationship map drifted apart. Add the missing " +
                "relationship (with its target table) to kAggregatedRelationshipTables, or remove the " +
                "obsolete entry.");
        }

        /// <summary>
        /// Asserts that every report-capable role which may select an aggregated table is also
        /// allowed to aggregate it.
        /// </summary>
        [TestCaseSource(nameof(AggregationPermissionCases))]
        public void AggregatedTable_AllowsAggregationsForReportCapableRole(string qualifiedTable, string role)
        {
            if (kMetadata.Value is null)
            {
                Assert.Ignore("Hasura metadata is not available in this environment, the permission check only runs where replace_metadata.json is reachable.");
                return;
            }

            JsonElement? permission = FindSelectPermission(qualifiedTable, role);

            if (permission is null)
            {
                Assert.Pass($"Role '{role}' has no select permission on '{qualifiedTable}' and therefore cannot run the report at all.");
                return;
            }

            bool allowAggregations = permission.Value.TryGetProperty("allow_aggregations", out JsonElement flag)
                && flag.ValueKind == JsonValueKind.True;

            Assert.That(
                allowAggregations,
                Is.True,
                $"Role '{role}' may select '{qualifiedTable}' but lacks \"allow_aggregations\", so every report " +
                "aggregating that table fails for this role. Add the flag in roles/api/files/replace_metadata.json.");
        }

        /// <summary>
        /// Builds one test case per aggregated table and report-capable role.
        /// </summary>
        private static IEnumerable<TestCaseData> AggregationPermissionCases()
        {
            foreach (string qualifiedTable in kAggregatedRelationshipTables.Values.Distinct().Order())
            {
                foreach (string role in kReportCapableRoles)
                {
                    yield return new TestCaseData(qualifiedTable, role)
                        .SetName($"AggregatedTable_{qualifiedTable}_{role}_AllowsAggregations");
                }
            }
        }

        /// <summary>
        /// Compiles every report type and collects the relationship names their queries aggregate.
        /// </summary>
        private static HashSet<string> CollectAggregatedRelationships()
        {
            HashSet<string> aggregatedRelationships = [];

            foreach (ReportType reportType in ReportTypeGroups.AllReportTypes())
            {
                ReportTemplate template = new();
                template.ReportParams.ReportType = (int)reportType;
                template.ReportParams.UnusedFilter.UnusedForDays = kUnusedForDays;
                DynGraphqlQuery query = Compiler.Compile(template);

                foreach (string queryText in QueryTexts(query))
                {
                    AddMatches(aggregatedRelationships, kAggregateFieldRegex, queryText);
                    AddMatches(aggregatedRelationships, kAggregatePredicateRegex, queryText);
                }
            }

            return aggregatedRelationships;
        }

        /// <summary>
        /// Returns all GraphQL documents a compiled report query consists of.
        /// </summary>
        private static IEnumerable<string> QueryTexts(DynGraphqlQuery query)
        {
            yield return query.FullQuery;
            yield return query.StandardRulesStructureQuery;
            yield return query.StandardRulesPageQuery;
        }

        /// <summary>
        /// Adds the first capture group of every regex match to the given set.
        /// </summary>
        private static void AddMatches(HashSet<string> relationships, Regex regex, string queryText)
        {
            foreach (Match match in regex.Matches(queryText))
            {
                relationships.Add(match.Groups[1].Value);
            }
        }

        /// <summary>
        /// Looks up the select permission of a role on a "schema.table" qualified table.
        /// </summary>
        private static JsonElement? FindSelectPermission(string qualifiedTable, string role)
        {
            string[] tableParts = qualifiedTable.Split('.');
            string schemaName = tableParts[0];
            string tableName = tableParts[1];

            foreach (JsonElement table in MetadataTables())
            {
                JsonElement tableIdentifier = table.GetProperty("table");
                if (tableIdentifier.GetProperty("schema").GetString() != schemaName
                    || tableIdentifier.GetProperty("name").GetString() != tableName)
                {
                    continue;
                }

                if (!table.TryGetProperty("select_permissions", out JsonElement selectPermissions))
                {
                    return null;
                }

                foreach (JsonElement selectPermission in selectPermissions.EnumerateArray())
                {
                    if (selectPermission.GetProperty("role").GetString() == role)
                    {
                        return selectPermission.GetProperty("permission");
                    }
                }

                return null;
            }

            Assert.Fail($"Table '{qualifiedTable}' is not tracked in the Hasura metadata.");
            return null;
        }

        /// <summary>
        /// Enumerates the tracked tables of the single metadata source.
        /// </summary>
        private static IEnumerable<JsonElement> MetadataTables()
        {
            return kMetadata.Value!.RootElement
                .GetProperty("args")
                .GetProperty("metadata")
                .GetProperty("sources")
                .EnumerateArray()
                .SelectMany(source => source.GetProperty("tables").EnumerateArray());
        }

        /// <summary>
        /// Reads the Hasura metadata from the repository or from the directory the installer copies
        /// it to next to the tests. Returns null when the metadata is out of reach, which happens on
        /// an installed system where only the test sources are deployed.
        /// </summary>
        private static JsonDocument? LoadMetadata()
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);

            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine(currentDirectory.FullName, "roles", "api", "files", "replace_metadata.json");
                if (File.Exists(repositoryPath))
                {
                    return JsonDocument.Parse(File.ReadAllText(repositoryPath));
                }

                string installedPath = Path.Combine(currentDirectory.FullName, "replace_metadata.json");
                if (File.Exists(installedPath))
                {
                    return JsonDocument.Parse(File.ReadAllText(installedPath));
                }

                currentDirectory = currentDirectory.Parent;
            }

            return null;
        }
    }
}

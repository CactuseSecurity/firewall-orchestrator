using System.Text.Json;
using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the Hasura select permissions of firewall.rule_hit (#5669). The table holds the last hit of a rule
    /// per gateway. Roles bound to a tenant must only read the hits of gateways visible to them, in line with the
    /// permissions of public.device; nothing but a data leak would report a regression here, which is why it is
    /// checked statically.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleHitSelectPermissionTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the rule_hit select permissions cannot be checked.";
        private const string kSchema = "firewall";
        private const string kTable = "rule_hit";
        private const string kRuleMetadataTable = "rule_metadata";
        private const string kUnrestricted = "{}";

        /// <summary>
        /// The row filter of a role bound to a tenant. The tenant0 check comes first and compares the session with
        /// constants only, so the database evaluates it once per query instead of once per row, which keeps
        /// installations with tenant0 as their only tenant as fast as without any filter. Otherwise the gateway
        /// and its management must be visible, as for public.device.
        /// </summary>
        private const string kTenantScopedFilter =
            "{\"_or\":["
            + "{\"_exists\":{\"_table\":{\"name\":\"tenant\",\"schema\":\"public\"},\"_where\":{\"_and\":["
            + "{\"tenant_id\":{\"_eq\":\"x-hasura-tenant-id\"}},{\"tenant_id\":{\"_eq\":1}}]}}},"
            + "{\"_and\":[{\"dev_id\":{\"_in\":\"x-hasura-visible-devices\"}},"
            + "{\"device\":{\"mgm_id\":{\"_in\":\"x-hasura-visible-managements\"}}}]}]}";

        private static readonly Dictionary<string, string> kExpectedFilters = new()
        {
            [Roles.Auditor] = kUnrestricted,
            [Roles.Importer] = kUnrestricted,
            [Roles.MiddlewareServer] = kUnrestricted,
            [Roles.Modeller] = kTenantScopedFilter,
            [Roles.Recertifier] = kTenantScopedFilter,
            [Roles.Reporter] = kTenantScopedFilter,
            [Roles.ReporterViewAll] = kUnrestricted
        };

        private static readonly List<string> kHitColumns = ["dev_id", "rule_last_hit", "rule_metadata_id"];
        private static readonly List<string> kWritingRoles = [Roles.Importer];
        private static readonly List<string> kWritePermissionKinds = ["insert_permissions", "update_permissions", "delete_permissions"];

        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);

        [SetUp]
        public void RequireMetadata()
        {
            if (kMetadataFileInfo.Value is null)
            {
                Assert.Ignore(kMetadataOutOfReach);
            }
        }

        /// <summary>
        /// Every role reads exactly the rows meant for it: all for the roles that see every gateway, the hits of
        /// visible gateways for the roles bound to a tenant.
        /// </summary>
        [Test]
        public void RuleHitSelect_ScopesTenantBoundRolesToVisibleGateways()
        {
            Dictionary<string, string> filters = ReadPermissions(kTable, "select_permissions")
                .ToDictionary(RoleOf, permission => JsonSerializer.Serialize(permission.GetProperty("permission").GetProperty("filter")));

            Assert.That(filters, Is.EquivalentTo(kExpectedFilters));
        }

        /// <summary>
        /// No role reads more than the three columns of the table, and none may aggregate over rows it cannot see.
        /// </summary>
        [Test]
        public void RuleHitSelect_GrantsTheHitColumnsWithoutAggregations()
        {
            List<string> violations = [];
            foreach (JsonElement permission in ReadPermissions(kTable, "select_permissions"))
            {
                JsonElement entry = permission.GetProperty("permission");
                List<string> columns = [.. entry.GetProperty("columns").EnumerateArray().Select(column => column.GetString() ?? "")];
                if (!columns.Order().SequenceEqual(kHitColumns))
                {
                    violations.Add($"{RoleOf(permission)} selects {string.Join(", ", columns)}");
                }
                if (entry.TryGetProperty("allow_aggregations", out JsonElement aggregations) && aggregations.GetBoolean())
                {
                    violations.Add($"{RoleOf(permission)} may aggregate");
                }
            }

            Assert.That(violations, Is.Empty, string.Join("; ", violations));
        }

        /// <summary>
        /// The rule fragments of the reports select the hits below the rule metadata, so every role that reads
        /// rule metadata needs the select permission; without it the rule queries of that role fail as a whole.
        /// A role that cannot read rule metadata cannot run these fragments and gets no access to the hits.
        /// </summary>
        [Test]
        public void RuleHitSelect_IsGrantedToExactlyTheRolesThatReadRuleMetadata()
        {
            List<string> ruleMetadataRoles = [.. ReadPermissions(kRuleMetadataTable, "select_permissions").Select(RoleOf)];
            List<string> hitRoles = [.. ReadPermissions(kTable, "select_permissions").Select(RoleOf)];

            Assert.Multiple(() =>
            {
                Assert.That(hitRoles, Is.EquivalentTo(ruleMetadataRoles));
                Assert.That(hitRoles, Does.Not.Contain(Roles.Requester));
            });
        }

        /// <summary>
        /// Hits are written by the rule hit import alone.
        /// </summary>
        [Test]
        public void RuleHitWrites_AreLeftToTheImporter()
        {
            List<string> writingRoles = [.. kWritePermissionKinds
                .SelectMany(kind => ReadPermissions(kTable, kind))
                .Select(RoleOf)
                .Distinct()];

            Assert.That(writingRoles, Is.EquivalentTo(kWritingRoles));
        }

        private static string RoleOf(JsonElement permission) => permission.GetProperty("role").GetString() ?? "";

        private static List<JsonElement> ReadPermissions(string tableName, string kind)
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));

            foreach (JsonElement table in metadata.RootElement.GetProperty("args").GetProperty("metadata")
                .GetProperty("sources").EnumerateArray().SelectMany(source => source.GetProperty("tables").EnumerateArray()))
            {
                JsonElement declaration = table.GetProperty("table");
                if (declaration.GetProperty("name").GetString() != tableName || declaration.GetProperty("schema").GetString() != kSchema)
                {
                    continue;
                }
                return table.TryGetProperty(kind, out JsonElement permissions)
                    ? permissions.EnumerateArray().Select(entry => entry.Clone()).ToList()
                    : [];
            }
            throw new InvalidOperationException($"{kSchema}.{tableName} is not tracked in {kMetadataFile}.");
        }

        private static FileInfo? LocateMetadata()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                FileInfo repository = new(Path.Combine(directory.FullName, "roles", "api", "files", kMetadataFile));
                FileInfo installed = new(Path.Combine(directory.FullName, kMetadataFile));
                if (repository.Exists)
                {
                    return repository;
                }
                if (installed.Exists)
                {
                    return installed;
                }
                directory = directory.Parent;
            }
            return null;
        }
    }
}

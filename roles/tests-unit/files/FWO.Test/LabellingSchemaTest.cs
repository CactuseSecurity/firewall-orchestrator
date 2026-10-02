using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Pins the labelling schema (issue #4949) across the places that have to agree: the creation scripts of a new
    /// installation and the Hasura metadata. Nothing in the build runs the sql, so a table missing in the metadata
    /// or a permission granted too widely would first show up in a running installation. The schema is still in
    /// development and has no upgrade script yet, as no installation carries it.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal partial class LabellingSchemaTest
    {
        private const string kSchema = "labelling";
        private const string kCreationTablesFile = "fworch-create-tables-labelling.sql";
        private const string kFirewallTablesFile = "fworch-create-tables-firewall.sql";
        private const string kForeignKeysFile = "fworch-create-foreign-keys.sql";
        private const string kIndicesFile = "fworch-create-indices.sql";
        private const string kFillConfigFile = "fworch-fill-config.sql";
        private const string kLabelLogicDefault = "VALUES ('labelLogic', 'AND', 0);";
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMiddlewareRole = "middleware-server";
        private const string kImporterRole = "importer";
        private const string kAuditorRole = "auditor";
        private const string kRemovedEventColumn = "removed_event_id";
        private const string kChangeEventTable = "label_change_event";
        private const string kRuleZoneColumn = "rule_src_zone";
        private const int kMaxIdentifierLength = 63;

        private static readonly string kCreationDirectory = Path.Combine("roles", "database", "files", "sql", "creation");
        private static readonly string kMetadataDirectory = Path.Combine("roles", "api", "files");
        private static readonly List<string> kRuleLabelColumns = ["rule_src_labels", "rule_dst_labels"];
        private static readonly List<string> kWriterRoles = [kImporterRole, kMiddlewareRole];
        private static readonly List<string> kLinkTablesWithHistory =
        [
            "rule_source_label", "rule_destination_label", "rule_label",
            "connection_source_label", "connection_destination_label", "connection_label",
            "owner_network_label"
        ];
        private static readonly List<string> kImporterLinkTables = ["rule_source_label", "rule_destination_label", "rule_label"];
        private static readonly List<string> kWriteKinds = ["insert_permissions", "update_permissions"];
        private static readonly List<JsonNode> kNoNodes = [];
        private static readonly List<string> kRemovedEventColumns = [kRemovedEventColumn];

        private string creationTables = "";
        private string firewallTables = "";
        private string foreignKeys = "";
        private string indices = "";
        private string fillConfig = "";
        private List<JsonNode> metadataTables = [];

        [OneTimeSetUp]
        public void LoadSources()
        {
            creationTables = ReadFile(kCreationDirectory, kCreationTablesFile);
            firewallTables = ReadFile(kCreationDirectory, kFirewallTablesFile);
            foreignKeys = ReadFile(kCreationDirectory, kForeignKeysFile);
            indices = ReadFile(kCreationDirectory, kIndicesFile);
            fillConfig = ReadFile(kCreationDirectory, kFillConfigFile);
            JsonNode metadata = JsonNode.Parse(ReadFile(kMetadataDirectory, kMetadataFile))
                ?? throw new AssertionException("The Hasura metadata is empty.");
            metadataTables = [.. Nodes(metadata["args"]?["metadata"]?["sources"])
                .SelectMany(source => Nodes(source["tables"]))];
        }

        /// <summary>
        /// The labelling tables, their foreign keys and indices are created, all named within the identifier
        /// length of Postgres, which would otherwise cut the names short.
        /// </summary>
        [Test]
        public void Creation_CreatesLabellingObjectsWithFittingNames()
        {
            List<string> tables = TableNames(creationTables);
            List<string> keys = Matches(ForeignKeyRegex(), foreignKeys);
            List<string> indexNames = Matches(IndexRegex(), indices);
            Assert.Multiple(() =>
            {
                Assert.That(tables, Is.Not.Empty);
                Assert.That(keys, Is.Not.Empty);
                Assert.That(indexNames, Is.Not.Empty);
                Assert.That(keys.Concat(indexNames).Where(name => name.Length > kMaxIdentifierLength), Is.Empty);
            });
        }

        /// <summary>
        /// The label columns of a rule are part of the rule table.
        /// </summary>
        [Test]
        public void Creation_AddsLabelColumnsToTheRuleTable()
        {
            Assert.Multiple(() =>
            {
                foreach (string column in kRuleLabelColumns)
                {
                    Assert.That(firewallTables, Does.Contain($"\"{column}\" jsonb"), column);
                }
            });
        }

        /// <summary>
        /// The labels of a rule side are combined with AND unless configured otherwise.
        /// </summary>
        [Test]
        public void Creation_DefaultsTheLabelLogicToAnd()
        {
            Assert.That(fillConfig, Does.Contain(kLabelLogicDefault));
        }

        /// <summary>
        /// Every labelling table is tracked by Hasura, and nothing else is tracked in the schema.
        /// </summary>
        [Test]
        public void Metadata_TracksEveryLabellingTable()
        {
            List<string> tracked = [.. LabellingTables().Select(table => table["table"]?["name"]?.GetValue<string>() ?? "")];
            Assert.That(tracked, Is.EquivalentTo(TableNames(creationTables)));
        }

        /// <summary>
        /// Label assignments and change events are history, so no role may delete them. The auditor only reads,
        /// and only the importer and the middleware write.
        /// </summary>
        [Test]
        public void Metadata_LetsOnlyTheWritersChangeLabellingData()
        {
            Assert.Multiple(() =>
            {
                foreach (JsonNode table in LabellingTables())
                {
                    string name = table["table"]?["name"]?.GetValue<string>() ?? "";
                    Assert.That(table["delete_permissions"], Is.Null, name);
                    Assert.That(Roles(table, "select_permissions"), Does.Contain(kAuditorRole), name);
                    foreach (string kind in kWriteKinds)
                    {
                        Assert.That(Roles(table, kind), Is.SubsetOf(kWriterRoles), $"{name} / {kind}");
                    }
                }
            });
        }

        /// <summary>
        /// An assignment is never rewritten, only closed by its removal event, and a change event is never changed.
        /// </summary>
        [Test]
        public void Metadata_KeepsTheLabelHistoryAppendOnly()
        {
            Assert.Multiple(() =>
            {
                foreach (string name in kLinkTablesWithHistory)
                {
                    foreach (JsonNode permission in Permissions(FindTable(name), "update_permissions"))
                    {
                        List<string> columns = Columns(permission);
                        Assert.That(columns, Is.EqualTo(kRemovedEventColumns), name);
                    }
                }
                Assert.That(FindTable(kChangeEventTable)["update_permissions"], Is.Null, kChangeEventTable);
            });
        }

        /// <summary>
        /// The importer writes the label assignments of the rules it imports, and of nothing else.
        /// </summary>
        [Test]
        public void Metadata_LetsTheImporterAssignRuleLabelsOnly()
        {
            Assert.Multiple(() =>
            {
                foreach (string name in kLinkTablesWithHistory)
                {
                    bool expected = kImporterLinkTables.Contains(name);
                    Assert.That(Roles(FindTable(name), "insert_permissions").Contains(kImporterRole), Is.EqualTo(expected), name);
                }
            });
        }

        /// <summary>
        /// Whoever sees the full rule, including its zones, sees its labels as well, and the importer can write them.
        /// </summary>
        [Test]
        public void Metadata_ExposesRuleLabelColumnsAlongsideTheRuleZones()
        {
            JsonNode rule = FindTable("rule", "firewall");
            List<JsonNode> permissions = [.. Permissions(rule, "select_permissions"), .. Permissions(rule, "insert_permissions")];
            List<JsonNode> withZones = [.. permissions.Where(permission => Columns(permission).Contains(kRuleZoneColumn))];
            Assert.That(withZones, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                foreach (JsonNode permission in withZones)
                {
                    Assert.That(Columns(permission), Is.SupersetOf(kRuleLabelColumns), permission["role"]?.GetValue<string>());
                }
            });
        }

        private static List<string> TableNames(string sql)
        {
            return Matches(TableRegex(), sql);
        }

        private static List<string> Matches(Regex regex, string text)
        {
            return [.. regex.Matches(text).Select(match => match.Groups[1].Value)];
        }

        private IEnumerable<JsonNode> LabellingTables()
        {
            return metadataTables.Where(table => table["table"]?["schema"]?.GetValue<string>() == kSchema);
        }

        private JsonNode FindTable(string name, string schema = kSchema)
        {
            return metadataTables.FirstOrDefault(table => table["table"]?["schema"]?.GetValue<string>() == schema
                    && table["table"]?["name"]?.GetValue<string>() == name)
                ?? throw new AssertionException($"The table {schema}.{name} is missing from the metadata.");
        }

        private static List<JsonNode> Permissions(JsonNode table, string kind)
        {
            return Nodes(table[kind]);
        }

        private static List<string> Roles(JsonNode table, string kind)
        {
            return [.. Permissions(table, kind).Select(permission => permission["role"]?.GetValue<string>() ?? "")];
        }

        private static List<string> Columns(JsonNode? permission)
        {
            return [.. Nodes(permission?["permission"]?["columns"]).Select(column => column.GetValue<string>())];
        }

        private static List<JsonNode> Nodes(JsonNode? array)
        {
            return [.. array?.AsArray().OfType<JsonNode>() ?? kNoNodes];
        }

        /// <summary>
        /// Reads a file from the repository, walking up from the directory of the test assembly. The sql sources
        /// are not deployed next to the installed tests, so the checks are skipped there instead of passing on
        /// no input at all.
        /// </summary>
        /// <param name="repositoryDirectory">Directory of the file relative to the repository root.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>The content of the file.</returns>
        private static string ReadFile(string repositoryDirectory, string fileName)
        {
            for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                FileInfo file = new(Path.Combine(directory.FullName, repositoryDirectory, fileName));
                if (file.Exists)
                {
                    return File.ReadAllText(file.FullName);
                }
            }
            Assert.Ignore($"{fileName} is not reachable in this environment, so the labelling schema cannot be checked.");
            return "";
        }

        [GeneratedRegex(@"create table (?:if not exists )?labelling\.(\w+)", RegexOptions.IgnoreCase)]
        private static partial Regex TableRegex();

        [GeneratedRegex(@"ADD CONSTRAINT (labelling_\w+_fkey)")]
        private static partial Regex ForeignKeyRegex();

        [GeneratedRegex(@"INDEX IF NOT EXISTS (idx_labelling_\w+) ON labelling\.")]
        private static partial Regex IndexRegex();
    }
}

using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the Hasura metadata of time_object after its move to the firewall schema (#5493). Hasura prefixes the
    /// GraphQL names of every schema but public, so without the custom name all API calls, the importer and the C#
    /// clients that use time_object would fail at runtime.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HasuraTimeObjectSchemaTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the time_object metadata cannot be checked.";
        private const string kTable = "time_object";
        private const string kSchema = "firewall";

        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);

        /// <summary>
        /// The relationships the API calls and clients use to reach time objects, by the table that declares them.
        /// </summary>
        private static readonly List<string> kExpectedRelationships =
        [
            "firewall.rule_time.time_object",
            "flow.timeobject.time_objects",
            "public.import_control.time_objects",
            "public.import_control.timeObjectsByRemoved",
            "public.management.time_objects"
        ];

        [SetUp]
        public void RequireMetadata()
        {
            if (kMetadataFileInfo.Value is null)
            {
                Assert.Ignore(kMetadataOutOfReach);
            }
        }

        /// <summary>
        /// The table is tracked once, in the firewall schema.
        /// </summary>
        [Test]
        public void TimeObject_IsTrackedInTheFirewallSchemaOnly()
        {
            using JsonDocument metadata = ReadMetadata();
            List<string> schemas = [.. Tables(metadata)
                .Select(table => table.GetProperty("table"))
                .Where(identifier => identifier.GetProperty("name").GetString() == kTable)
                .Select(identifier => identifier.GetProperty("schema").GetString() ?? "")];

            Assert.That(schemas, Is.EqualTo(new List<string> { kSchema }));
        }

        /// <summary>
        /// The custom name keeps the root fields and types named time_object instead of firewall_time_object.
        /// </summary>
        [Test]
        public void TimeObject_KeepsItsGraphQlNames()
        {
            using JsonDocument metadata = ReadMetadata();
            JsonElement configuration = FindTimeObject(metadata).GetProperty("configuration");

            Assert.Multiple(() =>
            {
                Assert.That(configuration.GetProperty("custom_name").GetString(), Is.EqualTo(kTable));
                Assert.That(configuration.GetProperty("custom_root_fields").EnumerateObject(), Is.Empty);
                Assert.That(configuration.GetProperty("custom_column_names").EnumerateObject(), Is.Empty);
            });
        }

        /// <summary>
        /// No relationship points to the table in its former schema, which Hasura would report as inconsistent, and
        /// the relationships used by the API calls keep their names.
        /// </summary>
        [Test]
        public void TimeObject_RelationshipsPointToTheFirewallSchema()
        {
            using JsonDocument metadata = ReadMetadata();
            List<string> relationships = [];
            List<string> wrongSchemas = [];
            foreach (JsonElement table in Tables(metadata))
            {
                JsonElement identifier = table.GetProperty("table");
                string tableName = $"{identifier.GetProperty("schema").GetString()}.{identifier.GetProperty("name").GetString()}";
                foreach (JsonElement relationship in Relationships(table))
                {
                    string relationshipName = $"{tableName}.{relationship.GetProperty("name").GetString()}";
                    JsonElement? target = RemoteTable(relationship);
                    if (target?.GetProperty("name").GetString() == kTable)
                    {
                        relationships.Add(relationshipName);
                        if (target.Value.GetProperty("schema").GetString() != kSchema)
                        {
                            wrongSchemas.Add(relationshipName);
                        }
                    }
                    else if (relationship.GetProperty("name").GetString() == kTable)
                    {
                        relationships.Add(relationshipName);
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(wrongSchemas, Is.Empty);
                Assert.That(relationships, Is.EquivalentTo(kExpectedRelationships));
            });
        }

        /// <summary>
        /// Returns the remote table an array relationship names, or null if the relationship uses a column of its own table.
        /// </summary>
        private static JsonElement? RemoteTable(JsonElement relationship)
        {
            JsonElement usage = relationship.GetProperty("using");
            if (usage.TryGetProperty("foreign_key_constraint_on", out JsonElement constraint)
                && constraint.ValueKind == JsonValueKind.Object
                && constraint.TryGetProperty("table", out JsonElement table))
            {
                return table;
            }
            if (usage.TryGetProperty("manual_configuration", out JsonElement manual)
                && manual.TryGetProperty("remote_table", out JsonElement remoteTable))
            {
                return remoteTable;
            }
            return null;
        }

        private static IEnumerable<JsonElement> Relationships(JsonElement table)
        {
            return Enumerate(table, "object_relationships").Concat(Enumerate(table, "array_relationships"));
        }

        private static List<JsonElement> Enumerate(JsonElement table, string property)
        {
            return table.TryGetProperty(property, out JsonElement entries) ? [.. entries.EnumerateArray()] : [];
        }

        private static JsonElement FindTimeObject(JsonDocument metadata)
        {
            return Tables(metadata).Single(table =>
                table.GetProperty("table").GetProperty("name").GetString() == kTable
                && table.GetProperty("table").GetProperty("schema").GetString() == kSchema);
        }

        private static IEnumerable<JsonElement> Tables(JsonDocument metadata)
        {
            return metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources")
                .EnumerateArray().SelectMany(source => source.GetProperty("tables").EnumerateArray());
        }

        private static JsonDocument ReadMetadata()
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            return JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));
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

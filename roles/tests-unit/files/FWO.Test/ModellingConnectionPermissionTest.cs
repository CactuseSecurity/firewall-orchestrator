using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the relationships read by the connection fragments (connectionDetails, connectionResolvedDetails):
    /// Hasura hides a relationship from every role without select permission on its remote table, so a role reading
    /// the connection fragments without it has every connection query rejected with "field not found". This broke
    /// the variance analysis of the middleware server, which reads connections in role middleware-server.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionPermissionTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the connection permissions cannot be checked.";
        private const string kModellingSchema = "modelling";
        private const string kConnectionTable = "connection";
        private const string kPermittedOwnersTable = "permitted_owners";
        private const string kAllColumnsMarker = "*";

        // read by both connection fragments, so every role holding it reads the fragments as a whole
        private const string kFragmentColumn = "interface_permission";

        private static readonly List<string> kFragmentReaders = ["auditor", "middleware-server", "modeller", "recertifier"];

        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);

        [SetUp]
        public void RequireMetadata()
        {
            if (kMetadataFileInfo.Value is null)
            {
                Assert.Ignore(kMetadataOutOfReach);
            }
        }

        [Test]
        public void ConnectionFragmentReaders_CanSelectPermittedOwners()
        {
            List<string> fragmentReaders = ReadSelectingRoles(kConnectionTable, kFragmentColumn);
            List<string> permittedOwnerReaders = ReadSelectingRoles(kPermittedOwnersTable, null);

            List<string> missing = [.. fragmentReaders.Except(permittedOwnerReaders)];

            Assert.That(missing, Is.Empty, "roles reading the connection fragments without select permission on permitted_owners: "
                + string.Join(", ", missing));
        }

        /// <summary>
        /// Keeps the derivation of the fragment readers from passing the check above on no roles at all.
        /// </summary>
        [Test]
        public void ConnectionFragmentReaders_IncludeTheKnownReaders()
        {
            List<string> fragmentReaders = ReadSelectingRoles(kConnectionTable, kFragmentColumn);

            Assert.That(fragmentReaders, Is.SupersetOf(kFragmentReaders));
        }

        /// <summary>
        /// Lists the roles with select permission on the given modelling table, restricted to the roles allowed to
        /// select the given column if one is named.
        /// </summary>
        private static List<string> ReadSelectingRoles(string tableName, string? requiredColumn)
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));
            JsonElement table = FindTable(metadata.RootElement, tableName);

            List<string> roles = [];
            if (!table.TryGetProperty("select_permissions", out JsonElement permissions))
            {
                return roles;
            }
            foreach (JsonElement permission in permissions.EnumerateArray())
            {
                if (requiredColumn is null || SelectsColumn(permission, requiredColumn))
                {
                    roles.Add(permission.GetProperty("role").GetString() ?? "");
                }
            }
            return roles;
        }

        private static JsonElement FindTable(JsonElement metadataRoot, string tableName)
        {
            IEnumerable<JsonElement> tables = metadataRoot.GetProperty("args").GetProperty("metadata").GetProperty("sources")
                .EnumerateArray().SelectMany(source => source.GetProperty("tables").EnumerateArray());
            foreach (JsonElement table in tables)
            {
                JsonElement declaration = table.GetProperty("table");
                if (declaration.GetProperty("name").GetString() == tableName && declaration.GetProperty("schema").GetString() == kModellingSchema)
                {
                    return table.Clone();
                }
            }
            throw new InvalidOperationException($"{kModellingSchema}.{tableName} is not tracked in {kMetadataFile}.");
        }

        private static bool SelectsColumn(JsonElement permission, string column)
        {
            JsonElement columns = permission.GetProperty("permission").GetProperty("columns");
            if (columns.ValueKind == JsonValueKind.String)
            {
                return columns.GetString() == kAllColumnsMarker;
            }
            return columns.EnumerateArray().Any(entry => entry.GetString() == column);
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

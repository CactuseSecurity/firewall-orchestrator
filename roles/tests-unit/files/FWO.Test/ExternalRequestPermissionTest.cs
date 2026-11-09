using System.Text.Json;
using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the Hasura write boundary for scheduler-processed external requests.
    /// </summary>
    [TestFixture]
    internal class ExternalRequestPermissionTest
    {
        private static readonly List<string> kExpectedWriterRoles = [Roles.MiddlewareServer];
        private static readonly List<string> kNoWriterRoles = [];

        /// <summary>
        /// Verifies that user-facing roles cannot create or modify scheduler input directly.
        /// </summary>
        [Test]
        public void ExternalRequest_OnlyMiddlewareServerHasExplicitWritePermission()
        {
            using JsonDocument? metadata = LoadMetadata();
            if (metadata is null)
            {
                Assert.Ignore("Hasura metadata is not available in this environment.");
                return;
            }

            JsonElement externalRequestTable = FindTable(metadata, "public", "ext_request");

            Assert.Multiple(() =>
            {
                Assert.That(
                    CollectPermissionRoles(externalRequestTable, "insert_permissions"),
                    Is.EquivalentTo(kExpectedWriterRoles),
                    "Only the middleware-server role may insert ext_request rows.");
                Assert.That(
                    CollectPermissionRoles(externalRequestTable, "update_permissions"),
                    Is.EquivalentTo(kExpectedWriterRoles),
                    "Only the middleware-server role may update ext_request rows.");
                Assert.That(
                    CollectPermissionRoles(externalRequestTable, "delete_permissions"),
                    Is.EquivalentTo(kNoWriterRoles),
                    "No explicit role may delete ext_request rows.");
            });
        }

        /// <summary>
        /// Finds a tracked Hasura table by schema and name.
        /// </summary>
        private static JsonElement FindTable(JsonDocument metadata, string schemaName, string tableName)
        {
            IEnumerable<JsonElement> tables = metadata.RootElement
                .GetProperty("args")
                .GetProperty("metadata")
                .GetProperty("sources")
                .EnumerateArray()
                .SelectMany(source => source.GetProperty("tables").EnumerateArray());

            foreach (JsonElement table in tables)
            {
                JsonElement identifier = table.GetProperty("table");
                if (identifier.GetProperty("schema").GetString() == schemaName
                    && identifier.GetProperty("name").GetString() == tableName)
                {
                    return table;
                }
            }

            throw new AssertionException($"Table '{schemaName}.{tableName}' is not tracked in the Hasura metadata.");
        }

        /// <summary>
        /// Returns every role listed under one write-permission property.
        /// </summary>
        private static List<string> CollectPermissionRoles(JsonElement table, string permissionProperty)
        {
            HashSet<string> writerRoles = [];
            if (!table.TryGetProperty(permissionProperty, out JsonElement permissions))
            {
                return [];
            }

            foreach (JsonElement permission in permissions.EnumerateArray())
            {
                string? role = permission.GetProperty("role").GetString();
                if (!string.IsNullOrWhiteSpace(role))
                {
                    writerRoles.Add(role);
                }
            }
            return [.. writerRoles];
        }

        /// <summary>
        /// Reads Hasura metadata from the repository or the installed test directory.
        /// </summary>
        private static JsonDocument? LoadMetadata()
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);
            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine(
                    currentDirectory.FullName,
                    "roles",
                    "api",
                    "files",
                    "replace_metadata.json");
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

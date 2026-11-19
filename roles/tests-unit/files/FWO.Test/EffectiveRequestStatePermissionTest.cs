using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the least-privilege contract between effective-state materialization and Hasura metadata.
    /// </summary>
    [TestFixture]
    internal class EffectiveRequestStatePermissionTest
    {
        private const string kMutationFileName = "updateEffectiveRequestElementSnapshot.graphql";
        private const string kMiddlewareServerRole = "middleware-server";
        private static readonly List<string> kSnapshotColumns =
        [
            "ip", "ip_end", "port", "port_end", "ip_proto_id", "group_name", "name", "start_time", "end_time"
        ];
        private static readonly List<string> kExistingMiddlewareColumns =
        [
            "flow_nwgrp_id", "flow_nwobj_id", "flow_svcgrp_id", "flow_svcobj_id", "object_task_id", "flow_timeobj_id"
        ];
        private static readonly List<string> kForbiddenBroadColumns =
        [
            "task_id", "field", "request_action", "user_id", "device_id"
        ];

        private JsonDocument metadata = null!;
        private string graphQlRoot = "";

        [OneTimeSetUp]
        public void LoadContractFiles()
        {
            string? metadataPath = FindRepositoryOrInstalledFile("roles", "api", "files", "replace_metadata.json");
            graphQlRoot = FindRepositoryOrInstalledDirectory("roles", "common", "files", "fwo-api-calls") ?? "";
            if (metadataPath is null || graphQlRoot.Length == 0)
            {
                Assert.Ignore("The Hasura metadata or GraphQL API calls are not available in this environment.");
                return;
            }
            metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        }

        /// <summary>
        /// A broader or narrower mutation would drift from the snapshot permission boundary.
        /// </summary>
        [Test]
        public void EffectiveSnapshotMutation_SetsOnlySnapshotColumns()
        {
            Assert.That(ReadMutationSetColumns(), Is.EquivalentTo(kSnapshotColumns));
        }

        /// <summary>
        /// The service role may retain its existing Flow links and gain only snapshot columns.
        /// </summary>
        [Test]
        public void MiddlewareServer_CanUpdateExactlyRequiredRequestElementColumns()
        {
            List<string> writableColumns = ReadMiddlewareServerWritableColumns();
            List<string> expectedColumns = [.. kExistingMiddlewareColumns, .. kSnapshotColumns];

            Assert.Multiple(() =>
            {
                Assert.That(writableColumns, Is.EquivalentTo(expectedColumns));
                Assert.That(writableColumns, Has.None.Matches<string>(kForbiddenBroadColumns.Contains));
            });
        }

        /// <summary>
        /// Every field written by the dedicated mutation must be authorized for the service role.
        /// </summary>
        [Test]
        public void EffectiveSnapshotMutation_ColumnsAreWritableByMiddlewareServer()
        {
            Assert.That(ReadMiddlewareServerWritableColumns(), Is.SupersetOf(ReadMutationSetColumns()));
        }

        private List<string> ReadMutationSetColumns()
        {
            string mutationPath = Path.Combine(graphQlRoot, "request", kMutationFileName);
            Assert.That(File.Exists(mutationPath), Is.True, $"The dedicated mutation is missing at '{mutationPath}'.");
            string mutation = File.ReadAllText(mutationPath);
            Match setBlock = Regex.Match(mutation, @"_set\s*:\s*\{(?<body>[^}]*)\}");
            Assert.That(setBlock.Success, Is.True, "The dedicated mutation has no literal _set block.");
            return Regex.Matches(setBlock.Groups["body"].Value,
                    @"(?m)^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:")
                .Select(match => match.Groups["field"].Value)
                .ToList();
        }

        private List<string> ReadMiddlewareServerWritableColumns()
        {
            JsonElement table = FindTable("request", "reqelement");
            JsonElement permission = table.GetProperty("update_permissions")
                .EnumerateArray()
                .Single(entry => entry.GetProperty("role").GetString() == kMiddlewareServerRole)
                .GetProperty("permission");
            return permission.GetProperty("columns").EnumerateArray()
                .Select(column => column.GetString() ?? "")
                .ToList();
        }

        private JsonElement FindTable(string schema, string name)
        {
            return metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources")
                .EnumerateArray()
                .SelectMany(source => source.GetProperty("tables").EnumerateArray())
                .Single(table => table.GetProperty("table").GetProperty("schema").GetString() == schema
                    && table.GetProperty("table").GetProperty("name").GetString() == name);
        }

        private static string? FindRepositoryOrInstalledFile(params string[] repositoryParts)
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);
            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine([currentDirectory.FullName, .. repositoryParts]);
                if (File.Exists(repositoryPath))
                {
                    return repositoryPath;
                }
                string installedPath = Path.Combine(currentDirectory.FullName, repositoryParts[^1]);
                if (File.Exists(installedPath))
                {
                    return installedPath;
                }
                currentDirectory = currentDirectory.Parent;
            }
            return null;
        }

        private static string? FindRepositoryOrInstalledDirectory(params string[] repositoryParts)
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);
            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine([currentDirectory.FullName, .. repositoryParts]);
                if (Directory.Exists(repositoryPath))
                {
                    return repositoryPath;
                }
                string installedPath = Path.Combine(currentDirectory.FullName, repositoryParts[^1]);
                if (Directory.Exists(installedPath))
                {
                    return installedPath;
                }
                currentDirectory = currentDirectory.Parent;
            }
            return null;
        }
    }
}

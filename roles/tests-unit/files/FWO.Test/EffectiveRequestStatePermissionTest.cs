using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the least-privilege contract between effective-state materialization and Hasura metadata.
    /// </summary>
    [TestFixture]
    internal partial class EffectiveRequestStatePermissionTest
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

        /// <summary>
        /// The opt-in live fixture must seed every required fresh-install request field.
        /// </summary>
        [Test]
        public void IntegrationFixture_SeedVariablesSatisfyFreshInstallSchema()
        {
            Dictionary<string, object?> ticket = InvokeFixtureVariables("TicketSeedVariables", "smoke-ticket");
            Dictionary<string, object?> task = InvokeFixtureVariables("RequestTaskSeedVariables",
                "smoke-task", 41L);

            Assert.Multiple(() =>
            {
                Assert.That(ticket["state"], Is.EqualTo(0));
                Assert.That(ticket["locked"], Is.False);
                Assert.That(task["state"], Is.EqualTo(0));
                Assert.That(task["requestAction"], Is.EqualTo("create"));
                Assert.That(task["locked"], Is.False);
            });
        }

        /// <summary>
        /// The live denial check uses database-valid values and distinguishes authorization from SQL errors.
        /// </summary>
        [Test]
        public void IntegrationFixture_BroadDenialRecognizesOnlyRoleSchemaRejection()
        {
            Dictionary<string, object?> variables = InvokeFixtureVariables("BroadUpdateVariables", 71L, 72L);
            bool forbiddenField = InvokeFixturePredicate("IsForbiddenBroadMutationError",
                new InvalidOperationException("field 'task_id' not found in type: 'request_reqelement_set_input'"));
            bool foreignKey = InvokeFixturePredicate("IsForbiddenBroadMutationError",
                new InvalidOperationException("insert or update violates foreign key constraint"));

            Assert.Multiple(() =>
            {
                Assert.That(variables["id"], Is.EqualTo(71L));
                Assert.That(variables["taskId"], Is.EqualTo(72L));
                Assert.That(variables["field"], Is.EqualTo("source"));
                Assert.That(variables["requestAction"], Is.EqualTo("create"));
                Assert.That(variables["userId"], Is.Null);
                Assert.That(forbiddenField, Is.True);
                Assert.That(foreignKey, Is.False);
            });
        }

        private List<string> ReadMutationSetColumns()
        {
            string mutationPath = Path.Combine(graphQlRoot, "request", kMutationFileName);
            Assert.That(File.Exists(mutationPath), Is.True, $"The dedicated mutation is missing at '{mutationPath}'.");
            string mutation = File.ReadAllText(mutationPath);
            Match setBlock = MutationSetBlockRegex().Match(mutation);
            Assert.That(setBlock.Success, Is.True, "The dedicated mutation has no literal _set block.");
            return MutationSetColumnRegex().Matches(setBlock.Groups["body"].Value)
                .Select(match => match.Groups["field"].Value)
                .ToList();
        }

        [GeneratedRegex(@"_set\s*:\s*\{(?<body>[^}]*)\}")]
        private static partial Regex MutationSetBlockRegex();

        [GeneratedRegex(@"(?m)^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:")]
        private static partial Regex MutationSetColumnRegex();

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

        private static Dictionary<string, object?> InvokeFixtureVariables(string methodName,
            params object[] arguments)
        {
            MethodInfo? method = typeof(EffectiveRequestStateMutationIntegrationTest).GetMethod(methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"The live fixture is missing {methodName}.");
            return method?.Invoke(null, arguments) as Dictionary<string, object?> ?? [];
        }

        private static bool InvokeFixturePredicate(string methodName, Exception exception)
        {
            MethodInfo? method = typeof(EffectiveRequestStateMutationIntegrationTest).GetMethod(methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"The live fixture is missing {methodName}.");
            return method?.Invoke(null, [exception]) as bool? ?? false;
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

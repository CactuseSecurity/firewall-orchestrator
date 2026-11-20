using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
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
        private const string kSnapshotBatchMutationFileName = "updateEffectiveRequestElementSnapshots.graphql";
        private const string kObjectTaskLinkBatchMutationFileName = "updateRequestElementObjectTaskIds.graphql";
        private const string kMiddlewareServerRole = Roles.MiddlewareServer;
        private const string kApproverRole = Roles.Approver;
        private const string kRequesterRole = Roles.Requester;
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
        private static readonly List<string> kExistingInteractiveColumns =
        [
            "id", "network_object_id", "original_nat_id", "service_id", "task_id", "user_id",
            "group_name", "name", "rule_uid", "ip", "ip_end", "device_id", "ip_proto_id", "port",
            "port_end", "field", "request_action", "flow_nwobj_id", "flow_nwgrp_id", "flow_svcobj_id",
            "flow_svcgrp_id", "start_time", "end_time", "time_obj_id", "flow_timeobj_id", "object_task_id"
        ];
        private static readonly List<string> kUpdateManyMutationRoots =
        [
            "update_request_reqelement_many"
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
        /// Snapshot batching must use the narrow update-many input instead of the broad request-element mutation.
        /// </summary>
        [Test]
        public void EffectiveSnapshotBatchMutation_UsesOnlyRequestElementUpdateMany()
        {
            AssertBatchMutationContract(kSnapshotBatchMutationFileName,
                "updateEffectiveRequestElementSnapshots");
        }

        /// <summary>
        /// Exact ticket-local links use the same narrow update-many root and return only affected-row proofs.
        /// </summary>
        [Test]
        public void ObjectTaskLinkBatchMutation_UsesOnlyRequestElementUpdateMany()
        {
            AssertBatchMutationContract(kObjectTaskLinkBatchMutationFileName,
                "updateRequestElementObjectTaskIds");
        }

        /// <summary>
        /// Workflow callers can only use the snapshot batch when RequestQueries registers its operation file.
        /// </summary>
        [Test]
        public void EffectiveSnapshotBatchMutation_IsRegisteredForApiClients()
        {
            AssertRequestQueryRegistration("updateEffectiveRequestElementSnapshots");
        }

        /// <summary>
        /// Ticket creation can only batch exact links when RequestQueries registers its operation file.
        /// </summary>
        [Test]
        public void ObjectTaskLinkBatchMutation_IsRegisteredForApiClients()
        {
            AssertRequestQueryRegistration("updateRequestElementObjectTaskIds");
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
        /// The roles executing the action already expose the necessary update root and columns; this feature must not
        /// expand any role's established column set.
        /// </summary>
        [Test]
        public void RequestElementBatchPermissions_PreserveRoleColumnMatrix()
        {
            List<string> middlewareColumns = ReadWritableColumns(kMiddlewareServerRole);
            List<string> approverColumns = ReadWritableColumns(kApproverRole);
            List<string> requesterColumns = ReadWritableColumns(kRequesterRole);

            Assert.Multiple(() =>
            {
                Assert.That(middlewareColumns, Is.SupersetOf(kSnapshotColumns));
                Assert.That(middlewareColumns, Does.Contain("object_task_id"));
                Assert.That(middlewareColumns, Has.None.Matches<string>(kForbiddenBroadColumns.Contains));
                Assert.That(approverColumns, Is.EquivalentTo(kExistingInteractiveColumns));
                Assert.That(approverColumns, Is.SupersetOf(kSnapshotColumns));
                Assert.That(requesterColumns, Is.EquivalentTo(kExistingInteractiveColumns));
                Assert.That(requesterColumns, Does.Contain("object_task_id"));
            });
        }

        /// <summary>Batch support keeps the established row checks and adds no role presets.</summary>
        [Test]
        public void RequestElementBatchPermissions_PreserveFiltersChecksAndPresets()
        {
            JsonElement middleware = ReadUpdatePermission(kMiddlewareServerRole);
            JsonElement approver = ReadUpdatePermission(kApproverRole);
            JsonElement requester = ReadUpdatePermission(kRequesterRole);

            Assert.Multiple(() =>
            {
                Assert.That(middleware.GetProperty("filter").EnumerateObject(), Is.Empty);
                Assert.That(middleware.GetProperty("check").EnumerateObject(), Is.Empty);
                Assert.That(approver.GetProperty("filter").EnumerateObject(), Is.Empty);
                Assert.That(approver.GetProperty("check").TryGetProperty("_and", out _), Is.True);
                Assert.That(requester.GetProperty("filter").EnumerateObject(), Is.Empty);
                Assert.That(requester.GetProperty("check").TryGetProperty("_and", out _), Is.True);
                Assert.That(middleware.TryGetProperty("set", out _), Is.False);
                Assert.That(approver.TryGetProperty("set", out _), Is.False);
                Assert.That(requester.TryGetProperty("set", out _), Is.False);
            });
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
            Dictionary<string, object?> element = InvokeFixtureVariables("RequestElementSeedVariables", 42L);

            Assert.Multiple(() =>
            {
                Assert.That(ticket["state"], Is.EqualTo(0));
                Assert.That(ticket["locked"], Is.False);
                Assert.That(task["state"], Is.EqualTo(0));
                Assert.That(task["requestAction"], Is.EqualTo("create"));
                Assert.That(task["locked"], Is.False);
                Assert.That(element["taskId"], Is.EqualTo(42L));
                Assert.That(element["field"], Is.EqualTo("source"));
                Assert.That(element["requestAction"], Is.EqualTo("create"));
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

        /// <summary>
        /// The live snapshot assertion compares timestamptz values as instants, independent of the runner timezone.
        /// </summary>
        [Test]
        public void IntegrationFixture_SnapshotComparisonUsesAbsoluteTimestamps()
        {
            DateTimeOffset startTime = new(2026, 11, 19, 8, 30, 0, TimeSpan.Zero);
            DateTimeOffset endTime = startTime.AddHours(1);
            WfReqElement readback = new()
            {
                Id = 71,
                TaskId = 72,
                IpString = "198.51.100.25/32",
                IpEnd = "198.51.100.26/32",
                Port = 443,
                PortEnd = 444,
                ProtoId = 6,
                GroupName = "integration-snapshot-group",
                Name = "integration-snapshot-name",
                StartTime = startTime.ToLocalTime().LocalDateTime,
                EndTime = endTime.ToLocalTime().LocalDateTime
            };
            MethodInfo? method = typeof(EffectiveRequestStateMutationIntegrationTest).GetMethod("AssertSnapshot",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "The live fixture is missing AssertSnapshot.");
            Assert.DoesNotThrow(() => method?.Invoke(null,
                [new ReturnId { UpdatedIdLong = 71 }, readback, 71L, 72L, startTime, endTime]));
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

        private void AssertBatchMutationContract(string fileName, string operationName)
        {
            string mutationPath = Path.Combine(graphQlRoot, "request", fileName);
            Assert.That(File.Exists(mutationPath), Is.True,
                $"The dedicated batch mutation is missing at '{mutationPath}'.");
            string mutation = File.ReadAllText(mutationPath);
            List<string> mutationRoots = BatchMutationRootRegex().Matches(mutation)
                .Select(match => match.Groups["field"].Value)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(mutation, Does.Contain($"mutation {operationName}("));
                Assert.That(mutation, Does.Contain("$updates: [request_reqelement_updates!]!"));
                Assert.That(mutationRoots, Is.EqualTo(kUpdateManyMutationRoots));
                Assert.That(mutation, Does.Contain("affected_rows"));
                Assert.That(mutation, Does.Not.Contain("_set:"));
                Assert.That(mutation, Does.Not.Contain("returning"));
            });
        }

        private static void AssertRequestQueryRegistration(string fieldName)
        {
            FieldInfo? field = typeof(RequestQueries).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, $"RequestQueries does not register '{fieldName}'.");
            Assert.That(field?.GetValue(null), Is.TypeOf<string>().And.Contains($"mutation {fieldName}("));
        }

        [GeneratedRegex(@"_set\s*:\s*\{(?<body>[^}]*)\}")]
        private static partial Regex MutationSetBlockRegex();

        [GeneratedRegex(@"(?m)^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:")]
        private static partial Regex MutationSetColumnRegex();

        [GeneratedRegex(@"(?m)^\s{2}(?<field>update_[A-Za-z0-9_]+)\s*\(")]
        private static partial Regex BatchMutationRootRegex();

        private List<string> ReadMiddlewareServerWritableColumns()
        {
            return ReadWritableColumns(kMiddlewareServerRole);
        }

        private List<string> ReadWritableColumns(string role)
        {
            return ReadUpdatePermission(role).GetProperty("columns").EnumerateArray()
                .Select(column => column.GetString() ?? "")
                .ToList();
        }

        private JsonElement ReadUpdatePermission(string role)
        {
            return FindTable("request", "reqelement").GetProperty("update_permissions")
                .EnumerateArray()
                .Single(entry => entry.GetProperty("role").GetString() == role)
                .GetProperty("permission");
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

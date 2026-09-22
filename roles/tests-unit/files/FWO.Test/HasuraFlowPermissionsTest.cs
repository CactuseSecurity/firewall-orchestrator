using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Pins the Flow eligibility permissions in the Hasura metadata (SEC-09).
    /// The predicate lives in three places that have to agree: FlowObjectEligibility in C#, the select
    /// filters on the Flow catalog tables, and the insert/update checks on request.reqelement. Nothing
    /// in the build applies replace_metadata.json, so a change that silently drops one of the conditions
    /// would otherwise first show up as a hole in a running installation.
    /// This test reads the file the installer posts, so it also fails when the file stops being valid
    /// json. It does not replace applying the metadata to a running Hasura, which is what proves an
    /// expression is one Hasura accepts at all; that belongs in the installation test.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HasuraFlowPermissionsTest
    {
        private static readonly List<string> kWorkflowRoles = ["approver", "implementer", "planner", "requester", "reviewer"];
        private static readonly List<string> kFlowCatalogTables = ["nwobject", "svcobject", "timeobject"];
        private static readonly List<string> kFlowRelationships = ["flow_nwobject", "flow_nwgroup", "flow_svcobject", "flow_svcgroup"];
        private const string kShownCondition = "\"show_in_request_module\":{\"_eq\":true}";
        private const string kNotRetiredCondition = "\"removed_date\":{\"_is_null\":true}";
        private const string kLiveStateCondition = "\"state\":{\"_in\":[\"requested\",\"implemented\"]}";
        private const string kNonNegativeProtocolCondition = "\"ip_proto_id\":{\"_gte\":0}";

        private JsonNode metadata = null!;

        [OneTimeSetUp]
        public void LoadMetadata()
        {
            string metadataPath = ResolveMetadataPath();
            if (!File.Exists(metadataPath))
            {
                Assert.Ignore($"The Hasura metadata was not found at '{metadataPath}'.");
            }

            metadata = JsonNode.Parse(File.ReadAllText(metadataPath))
                ?? throw new AssertionException("The Hasura metadata is empty.");
        }

        /// <summary>
        /// A workflow role may only see catalog entries that are offered, not retired and live. The
        /// protocol of an entry is deliberately not part of this: the canonical ANY service is a live
        /// entry the platform attaches itself, and hiding its row would stop a stored ticket that
        /// carries it from being confirmed as live.
        /// </summary>
        [Test]
        public void FlowCatalogSelectFilters_LetAWorkflowRoleSeeLiveEntriesOnly()
        {
            foreach (string tableName in kFlowCatalogTables)
            {
                JsonNode table = FindTable("flow", tableName);
                foreach (string role in kWorkflowRoles)
                {
                    string filter = Serialize(FindPermission(table, "select_permissions", role)?["filter"]);
                    Assert.Multiple(() =>
                    {
                        Assert.That(filter, Does.Contain(kShownCondition), $"flow.{tableName} / {role}");
                        Assert.That(filter, Does.Contain(kNotRetiredCondition), $"flow.{tableName} / {role}");
                        Assert.That(filter, Does.Contain(kLiveStateCondition), $"flow.{tableName} / {role}");
                        Assert.That(filter, Does.Not.Contain(kNonNegativeProtocolCondition), $"flow.{tableName} / {role}");
                    });
                }
            }
        }

        /// <summary>
        /// The middleware runs the flow sync and the flow creation, so it has to see the whole catalog.
        /// </summary>
        [Test]
        public void FlowCatalogSelectFilters_LeaveTheMiddlewareUnfiltered()
        {
            foreach (string tableName in kFlowCatalogTables)
            {
                JsonNode table = FindTable("flow", tableName);
                Assert.That(Serialize(FindPermission(table, "select_permissions", "middleware-server")?["filter"]), Is.EqualTo("{}"),
                    $"flow.{tableName}");
            }
        }

        /// <summary>
        /// An inserted request element is authored by the user in full, so it is held to what the request
        /// module offers: a live entry that is not one of the internal representations.
        /// </summary>
        [Test]
        public void ReqElementInsertChecks_HoldEveryAttachedEntryToWhatTheRequestModuleOffers()
        {
            JsonNode table = FindTable("request", "reqelement");
            foreach (string role in RolesWith(table, "insert_permissions"))
            {
                string check = Serialize(FindPermission(table, "insert_permissions", role)?["check"]);
                AssertLifecycleConditionsArePresent(check, $"insert / {role}");
                Assert.Multiple(() =>
                {
                    // the element carries its own protocol, and the service entry it names carries one too
                    Assert.That(check, Does.Contain($"{{\"_or\":[{{\"ip_proto_id\":{{\"_is_null\":true}}}},{{{kNonNegativeProtocolCondition}}}]}}"),
                        $"insert / {role}");
                    Assert.That(check, Does.Contain($"{{{kLiveStateCondition}}},{{{kNonNegativeProtocolCondition}}}"), $"insert / {role}");
                });
            }
        }

        /// <summary>
        /// An update rewrites the whole row, including the values the platform itself wrote, and Hasura
        /// evaluates the check against the resulting row whether or not those columns were touched. The
        /// lifecycle conditions therefore stay, while the request-module-only protocol conditions must
        /// not, or every later save of a ticket carrying the canonical ANY service or the ANY IP
        /// protocol would fail.
        /// </summary>
        [Test]
        public void ReqElementUpdateChecks_HoldEveryAttachedEntryToBeingLive()
        {
            JsonNode table = FindTable("request", "reqelement");
            foreach (string role in RolesWith(table, "update_permissions"))
            {
                string check = Serialize(FindPermission(table, "update_permissions", role)?["check"]);
                AssertLifecycleConditionsArePresent(check, $"update / {role}");
                Assert.That(check, Does.Not.Contain(kNonNegativeProtocolCondition), $"update / {role}");
            }
        }

        /// <summary>
        /// Asserts that the check names every Flow relationship and holds each of them to being live.
        /// </summary>
        /// <param name="check">The serialized check expression.</param>
        /// <param name="context">Names the permission in an assertion message.</param>
        private static void AssertLifecycleConditionsArePresent(string check, string context)
        {
            Assert.Multiple(() =>
            {
                foreach (string relationship in kFlowRelationships)
                {
                    Assert.That(check, Does.Contain($"\"{relationship}\""), $"{context} / {relationship}");
                }
                Assert.That(check, Does.Contain(kShownCondition), context);
                Assert.That(check, Does.Contain(kNotRetiredCondition), context);
                Assert.That(check, Does.Contain(kLiveStateCondition), context);
            });
        }

        /// <summary>
        /// The roles a table carries permissions of, without the middleware, which is unrestricted by
        /// design and whose permissions are pinned separately.
        /// </summary>
        /// <param name="table">The table node to read.</param>
        /// <param name="permissionKind">Name of the permission list.</param>
        /// <returns>The user roles holding such a permission.</returns>
        private static List<string> RolesWith(JsonNode table, string permissionKind)
        {
            List<string> roles = [.. (table[permissionKind]?.AsArray() ?? [])
                .Select(permission => permission?["role"]?.GetValue<string>() ?? "")
                .Where(role => role.Length > 0 && role != "middleware-server")];
            Assert.That(roles, Is.Not.Empty, $"no {permissionKind} found to check");
            return roles;
        }

        private JsonNode FindTable(string schema, string name)
        {
            JsonNode? table = FindTables(metadata)
                .FirstOrDefault(candidate => candidate["table"]?["schema"]?.GetValue<string>() == schema
                    && candidate["table"]?["name"]?.GetValue<string>() == name);
            return table ?? throw new AssertionException($"The table {schema}.{name} is missing from the metadata.");
        }

        private static JsonNode? FindPermission(JsonNode table, string permissionKind, string role)
        {
            return (table[permissionKind]?.AsArray() ?? [])
                .FirstOrDefault(permission => permission?["role"]?.GetValue<string>() == role)?["permission"];
        }

        /// <summary>
        /// Walks the metadata for every table list, wherever the source structure keeps it.
        /// </summary>
        /// <param name="node">The node to descend into.</param>
        /// <returns>Every table entry found below it.</returns>
        private static IEnumerable<JsonNode> FindTables(JsonNode? node)
        {
            if (node is JsonObject jsonObject)
            {
                foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
                {
                    if (property.Key == "tables" && property.Value is JsonArray tables)
                    {
                        foreach (JsonNode? table in tables.Where(table => table?["table"] != null))
                        {
                            yield return table!;
                        }
                    }
                    foreach (JsonNode table in FindTables(property.Value))
                    {
                        yield return table;
                    }
                }
            }
            else if (node is JsonArray jsonArray)
            {
                foreach (JsonNode table in jsonArray.SelectMany(FindTables))
                {
                    yield return table;
                }
            }
        }

        /// <summary>
        /// Serializes an expression without whitespace, so a condition can be matched as written here
        /// regardless of how the file is indented.
        /// </summary>
        /// <param name="node">The expression node, null when the permission is missing.</param>
        /// <returns>The compact json of the expression, or an empty object when there is none.</returns>
        private static string Serialize(JsonNode? node)
        {
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "{}";
        }

        /// <summary>
        /// Locates replace_metadata.json relative to the api call directory the tests already resolve.
        /// </summary>
        /// <returns>The absolute path of the metadata the installer posts.</returns>
        private static string ResolveMetadataPath()
        {
            string? commonFilesPath = Environment.GetEnvironmentVariable("FWO_BASE_DIR");
            return string.IsNullOrEmpty(commonFilesPath)
                ? ""
                : Path.GetFullPath(Path.Combine(commonFilesPath, "..", "..", "api", "files", "replace_metadata.json"));
        }
    }
}

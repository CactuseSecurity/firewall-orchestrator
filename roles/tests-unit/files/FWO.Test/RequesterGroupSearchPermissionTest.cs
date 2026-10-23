using System.Text.Json;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the requester permission contract used by the network and service group searches.
    /// </summary>
    [TestFixture]
    internal class RequesterGroupSearchPermissionTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kRequesterRole = "requester";
        private const string kVisibleManagements = "x-hasura-visible-managements";
        private const int kGroupMemberLookupLimit = WfObjectTaskHelper.kGroupMemberLimit + 1;
        private const string kNetworkGroupFilter =
            "{\"_and\":[{\"object\":{\"mgm_id\":{\"_in\":\"x-hasura-visible-managements\"}}},{\"active\":{\"_eq\":true}},{\"removed\":{\"_is_null\":true}}]}";
        private const string kServiceGroupFilter =
            "{\"_and\":[{\"service\":{\"mgm_id\":{\"_in\":\"x-hasura-visible-managements\"}}},{\"active\":{\"_eq\":true}},{\"removed\":{\"_is_null\":true}}]}";

        private static readonly List<string> kNetworkGroupColumns =
            ["active", "objgrp_id", "objgrp_member_id", "removed"];
        private static readonly List<string> kServiceGroupColumns =
            ["active", "removed", "svcgrp_id", "svcgrp_member_id"];
        private static readonly List<int> kNetworkGroupTypes = [2, 11];
        private static readonly List<int> kServiceTypes = [1, 2];

        /// <summary>
        /// Ensures requester can read the network-group search result and its member links.
        /// </summary>
        [Test]
        public void NetworkGroupSearch_RequesterPermissionIncludesSearchContract()
        {
            using JsonDocument? metadata = LoadMetadata();
            if (metadata is null)
            {
                Assert.Ignore("Hasura metadata is not available in this environment.");
                return;
            }

            JsonElement networkObject = FindTable(metadata, "firewall", "nw_object");
            JsonElement memberTable = FindTable(metadata, "firewall", "nw_object_group");
            JsonElement objectPermission = FindPermission(networkObject, kRequesterRole);
            JsonElement memberPermission = FindPermission(memberTable, kRequesterRole);

            Assert.Multiple(() =>
            {
                Assert.That(Columns(objectPermission), Does.Contain("obj_member_names"));
                Assert.That(FilterContainsManagements(objectPermission), Is.True);
                Assert.That(FilterContainsValues(objectPermission, "obj_typ_id", kNetworkGroupTypes), Is.True);
                Assert.That(HasRelationship(networkObject, "array_relationships", "objgrps"), Is.True);
                Assert.That(HasRelationship(memberTable, "object_relationships", "object"), Is.True);
                Assert.That(HasRelationship(memberTable, "object_relationships", "objectByObjgrpMemberId"), Is.True);
                Assert.That(Columns(memberPermission), Is.EquivalentTo(kNetworkGroupColumns));
                Assert.That(memberPermission.GetProperty("limit").GetInt32(), Is.EqualTo(kGroupMemberLookupLimit));
                Assert.That(JsonSerializer.Serialize(memberPermission.GetProperty("filter")),
                    Is.EqualTo(kNetworkGroupFilter));
            });
        }

        /// <summary>
        /// Ensures requester can read the service-group search result and its member links.
        /// </summary>
        [Test]
        public void ServiceGroupSearch_RequesterPermissionIncludesSearchContract()
        {
            using JsonDocument? metadata = LoadMetadata();
            if (metadata is null)
            {
                Assert.Ignore("Hasura metadata is not available in this environment.");
                return;
            }

            JsonElement service = FindTable(metadata, "firewall", "nw_service");
            JsonElement memberTable = FindTable(metadata, "firewall", "nw_service_group");
            JsonElement servicePermission = FindPermission(service, kRequesterRole);
            JsonElement memberPermission = FindPermission(memberTable, kRequesterRole);

            Assert.Multiple(() =>
            {
                Assert.That(Columns(servicePermission), Does.Contain("svc_member_names"));
                Assert.That(FilterContainsManagements(servicePermission), Is.True);
                Assert.That(FilterContainsValues(servicePermission, "svc_typ_id", kServiceTypes), Is.True);
                Assert.That(HasRelationship(service, "array_relationships", "svcgrps"), Is.True);
                Assert.That(HasRelationship(memberTable, "object_relationships", "service"), Is.True);
                Assert.That(HasRelationship(memberTable, "object_relationships", "serviceBySvcgrpMemberId"), Is.True);
                Assert.That(Columns(memberPermission), Is.EquivalentTo(kServiceGroupColumns));
                Assert.That(memberPermission.GetProperty("limit").GetInt32(), Is.EqualTo(kGroupMemberLookupLimit));
                Assert.That(JsonSerializer.Serialize(memberPermission.GetProperty("filter")),
                    Is.EqualTo(kServiceGroupFilter));
            });
        }

        private static JsonElement FindTable(JsonDocument metadata, string schemaName, string tableName)
        {
            foreach (JsonElement source in metadata.RootElement.GetProperty("args").GetProperty("metadata")
                .GetProperty("sources").EnumerateArray())
            {
                foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
                {
                    JsonElement identifier = table.GetProperty("table");
                    if (identifier.GetProperty("schema").GetString() == schemaName
                        && identifier.GetProperty("name").GetString() == tableName)
                    {
                        return table;
                    }
                }
            }

            throw new AssertionException($"Table '{schemaName}.{tableName}' is not tracked in Hasura metadata.");
        }

        private static JsonElement FindPermission(JsonElement table, string role)
        {
            foreach (JsonElement permission in table.GetProperty("select_permissions").EnumerateArray())
            {
                if (permission.GetProperty("role").GetString() == role)
                {
                    return permission.GetProperty("permission");
                }
            }

            throw new AssertionException($"Role '{role}' has no select permission on the requested table.");
        }

        private static List<string> Columns(JsonElement permission)
        {
            return permission.GetProperty("columns").EnumerateArray()
                .Select(column => column.GetString() ?? string.Empty).ToList();
        }

        private static bool FilterContainsManagements(JsonElement permission)
        {
            return permission.GetProperty("filter").ToString().Contains(kVisibleManagements, StringComparison.Ordinal);
        }

        private static bool FilterContainsValues(JsonElement permission, string fieldName, List<int> expectedValues)
        {
            JsonElement filter = permission.GetProperty("filter");
            foreach (JsonElement condition in filter.GetProperty("_and").EnumerateArray())
            {
                if (!condition.TryGetProperty(fieldName, out JsonElement field))
                {
                    continue;
                }

                JsonElement values = field.GetProperty("_in");
                List<int> actualValues = values.EnumerateArray().Select(value => value.GetInt32()).ToList();
                return expectedValues.All(actualValues.Contains);
            }

            return false;
        }

        private static bool HasRelationship(JsonElement table, string relationshipKind, string relationshipName)
        {
            return table.GetProperty(relationshipKind).EnumerateArray()
                .Any(relationship => relationship.GetProperty("name").GetString() == relationshipName);
        }

        private static JsonDocument? LoadMetadata()
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);
            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine(currentDirectory.FullName, "roles", "api", "files", kMetadataFile);
                if (File.Exists(repositoryPath))
                {
                    return JsonDocument.Parse(File.ReadAllText(repositoryPath));
                }

                string installedPath = Path.Combine(currentDirectory.FullName, kMetadataFile);
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

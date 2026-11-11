using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the owner binding of report objects (GHSA-6g84, role-api-replace-metadata-004): a user role creates
    /// reports, schedules, templates and report notifications only for itself and cannot hand them to another user.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ReportOwnerPermissionTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the report owner permissions cannot be checked.";
        private const string kSessionUserId = "x-hasura-user-id";

        private static readonly List<string> kUserRoles = ["auditor", "modeller", "recertifier", "reporter", "reporter-viewall"];

        private static readonly Dictionary<string, string> kOwnerColumns = new()
        {
            ["report"] = "report_owner_id",
            ["report_schedule"] = "report_schedule_owner",
            ["report_template"] = "report_template_owner",
            ["notification"] = "user_id"
        };

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
        public void Insert_OfUserRoles_BindsTheOwnerToTheSession()
        {
            List<string> violations = [];
            foreach ((string table, string ownerColumn) in kOwnerColumns)
            {
                foreach (JsonElement permission in ReadUserRolePermissions(table, "insert_permissions"))
                {
                    if (!OwnerIsPreset(permission, ownerColumn) && !OwnerIsCheckedAgainstSession(permission, ownerColumn))
                    {
                        violations.Add($"{table}: {RoleOf(permission)}");
                    }
                }
            }
            Assert.That(violations, Is.Empty, "inserts that accept a foreign owner: " + string.Join(", ", violations));
        }

        [Test]
        public void Update_OfUserRoles_KeepsTheOwner()
        {
            List<string> violations = [];
            foreach ((string table, string ownerColumn) in kOwnerColumns)
            {
                foreach (JsonElement permission in ReadUserRolePermissions(table, "update_permissions"))
                {
                    if (WritesColumn(permission, ownerColumn) && !OwnerIsCheckedAgainstSession(permission, ownerColumn))
                    {
                        violations.Add($"{table}: {RoleOf(permission)}");
                    }
                }
            }
            Assert.That(violations, Is.Empty, "updates that can hand the object to another owner: " + string.Join(", ", violations));
        }

        private static bool OwnerIsPreset(JsonElement permission, string ownerColumn)
        {
            return permission.GetProperty("permission").TryGetProperty("set", out JsonElement set)
                && set.ValueKind == JsonValueKind.Object
                && set.TryGetProperty(ownerColumn, out JsonElement value)
                && string.Equals(value.GetString(), kSessionUserId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The check names the owner column bound to the session user (directly or within _and/_or).</summary>
        private static bool OwnerIsCheckedAgainstSession(JsonElement permission, string ownerColumn)
        {
            if (!permission.GetProperty("permission").TryGetProperty("check", out JsonElement check) || check.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            string serialized = JsonSerializer.Serialize(check).ToLowerInvariant();
            return serialized.Contains($"\"{ownerColumn}\":{{\"_eq\":\"{kSessionUserId}\"}}");
        }

        private static bool WritesColumn(JsonElement permission, string column)
        {
            return permission.GetProperty("permission").TryGetProperty("columns", out JsonElement columns)
                && columns.EnumerateArray().Any(entry => entry.GetString() == column);
        }

        private static string RoleOf(JsonElement permission) => permission.GetProperty("role").GetString() ?? "";

        private static List<JsonElement> ReadUserRolePermissions(string tableName, string kind)
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));

            foreach (JsonElement table in metadata.RootElement.GetProperty("args").GetProperty("metadata")
                .GetProperty("sources").EnumerateArray().SelectMany(source => source.GetProperty("tables").EnumerateArray()))
            {
                JsonElement declaration = table.GetProperty("table");
                if (declaration.GetProperty("name").GetString() != tableName || declaration.GetProperty("schema").GetString() != "public")
                {
                    continue;
                }
                return table.TryGetProperty(kind, out JsonElement permissions)
                    ? permissions.EnumerateArray().Where(entry => kUserRoles.Contains(RoleOf(entry))).Select(entry => entry.Clone()).ToList()
                    : [];
            }
            throw new InvalidOperationException($"public.{tableName} is not tracked in {kMetadataFile}.");
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

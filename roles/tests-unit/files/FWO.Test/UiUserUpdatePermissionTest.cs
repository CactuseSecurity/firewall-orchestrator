using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the Hasura update permissions of public.uiuser.
    /// The uiuser row decides who an account is: uuid is the dn a login and a token refresh resolve
    /// against LDAP to derive roles, tenant_id is the tenant, and ldap_connection_id is the
    /// directory the account belongs to. A role that may write those columns on its own row can
    /// rewrite its own identity and then have the next refresh rebuild its authorization from the
    /// rewritten values, which is how an auditor reached role admin (SEC-01).
    /// Self-service on this table therefore exists for presentation preferences only, and nothing
    /// but a live escalation attempt would report a regression, which is why it is checked here.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiUserUpdatePermissionTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the uiuser update permissions cannot be checked.";
        private const string kUiUserTable = "uiuser";
        private const string kPublicSchema = "public";
        private const string kSubjectColumn = "uuid";
        private const string kSubjectSessionVariable = "x-hasura-uuid";

        /// <summary>
        /// The middleware runs the login and refresh flows and legitimately writes the identity of
        /// a user, for example when it first creates the row or follows a directory change. It
        /// authenticates against the API with its own role and is not reachable by a UI session.
        /// </summary>
        private const string kServiceRole = "middleware-server";

        /// <summary>
        /// The only column a role may write on its own uiuser row. A language is a presentation
        /// preference: it is displayed back to the user and never consulted to authorize anything.
        /// </summary>
        private const string kPreferenceColumn = "uiuser_language";

        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);

        /// <summary>
        /// No role reachable from a UI session may write a column of uiuser other than the
        /// presentation preference, whatever its row filter says.
        /// </summary>
        [Test]
        public void UiUserUpdate_GrantsNonServiceRolesThePreferenceColumnOnly()
        {
            List<string> violations = [];

            foreach (JsonElement permission in ReadUpdatePermissions())
            {
                string role = permission.GetProperty("role").GetString() ?? "";
                if (role == kServiceRole)
                {
                    continue;
                }

                List<string> columns = ReadColumns(permission);
                List<string> forbidden = columns.Where(column => column != kPreferenceColumn).ToList();
                if (forbidden.Count > 0)
                {
                    violations.Add($"{role} may update {string.Join(", ", forbidden)}");
                }
            }

            Assert.That(violations, Is.Empty,
                "a role reachable from a UI session may only update " + kPreferenceColumn
                + " on uiuser, because every other column feeds authorization: "
                + string.Join("; ", violations));
        }

        /// <summary>
        /// A row filter alone proves only that the row belonged to the caller before the update.
        /// Without a post-update check Hasura does not constrain the row afterwards, so an update
        /// may move the row out from under the caller - which is what makes rewriting the subject
        /// possible in the first place. Every self-service permission therefore needs a check
        /// equal to its filter.
        /// </summary>
        [Test]
        public void UiUserUpdate_ConstrainsTheRowAfterTheUpdateAsWellAsBefore()
        {
            List<string> violations = new();

            foreach (JsonElement permission in ReadUpdatePermissions())
            {
                string role = permission.GetProperty("role").GetString() ?? "";
                if (role == kServiceRole)
                {
                    continue;
                }

                JsonElement entry = permission.GetProperty("permission");
                string filter = ReadCondition(entry, "filter");
                string check = ReadCondition(entry, "check");

                if (check != filter)
                {
                    violations.Add($"{role} filters on {filter} but checks {check}");
                }
            }

            Assert.That(violations, Is.Empty,
                "a self-service update of uiuser must constrain the row after the update exactly as "
                + "before it, otherwise the update may move the row to another subject: "
                + string.Join("; ", violations));
        }

        /// <summary>
        /// The self-service permissions are scoped to the caller's own row, and the session
        /// variable that scopes them has to be the subject of the token. A permission that filtered
        /// on nothing would let one user rewrite the preference of another.
        /// </summary>
        [Test]
        public void UiUserUpdate_ScopesNonServiceRolesToTheirOwnRow()
        {
            List<string> violations = new();
            string expected = $"{{\"{kSubjectColumn}\":{{\"_eq\":\"{kSubjectSessionVariable}\"}}}}";

            foreach (JsonElement permission in ReadUpdatePermissions())
            {
                string role = permission.GetProperty("role").GetString() ?? "";
                if (role == kServiceRole)
                {
                    continue;
                }

                string filter = ReadCondition(permission.GetProperty("permission"), "filter");
                if (filter != expected)
                {
                    violations.Add($"{role} filters on {filter}");
                }
            }

            Assert.That(violations, Is.Empty,
                $"a self-service update of uiuser has to be scoped to {expected}: "
                + string.Join("; ", violations));
        }

        /// <summary>
        /// Reads the columns one permission entry grants.
        /// </summary>
        /// <param name="permission">One entry of the update_permissions array.</param>
        private static List<string> ReadColumns(JsonElement permission)
        {
            JsonElement columns = permission.GetProperty("permission").GetProperty("columns");
            return columns.EnumerateArray().Select(column => column.GetString() ?? "").ToList();
        }

        /// <summary>
        /// Reads a row condition of a permission entry as canonical JSON, so that a missing
        /// condition, an explicit null and an empty object stay distinguishable from one another.
        /// </summary>
        /// <param name="permission">The permission object holding the condition.</param>
        /// <param name="name">Name of the condition, "filter" or "check".</param>
        private static string ReadCondition(JsonElement permission, string name)
        {
            if (!permission.TryGetProperty(name, out JsonElement condition)
                || condition.ValueKind == JsonValueKind.Null)
            {
                return "<none>";
            }
            return JsonSerializer.Serialize(condition);
        }

        /// <summary>
        /// Reads the update_permissions of public.uiuser from the metadata file.
        /// </summary>
        /// <returns>One element per role holding an update permission on the table.</returns>
        private static List<JsonElement> ReadUpdatePermissions()
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));

            foreach (JsonElement source in metadata.RootElement.GetProperty("args")
                .GetProperty("metadata").GetProperty("sources").EnumerateArray())
            {
                foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
                {
                    JsonElement declaration = table.GetProperty("table");
                    if (declaration.GetProperty("name").GetString() != kUiUserTable
                        || declaration.GetProperty("schema").GetString() != kPublicSchema)
                    {
                        continue;
                    }
                    // A tracked table without the permission at all grants nothing, which passes
                    // every check below, so an empty list is the correct answer and not a skip.
                    if (!table.TryGetProperty("update_permissions", out JsonElement permissions))
                    {
                        return new List<JsonElement>();
                    }
                    // Cloned, because the elements outlive the JsonDocument they were read from.
                    return permissions.EnumerateArray().Select(entry => entry.Clone()).ToList();
                }
            }
            throw new InvalidOperationException($"{kPublicSchema}.{kUiUserTable} is not tracked in {kMetadataFile}.");
        }

        /// <summary>
        /// Skips the checks where the metadata is not deployed, because passing on no input at all
        /// would report the permissions as sound without having read them.
        /// </summary>
        [SetUp]
        public void SkipWithoutMetadata()
        {
            if (kMetadataFileInfo.Value is null)
            {
                Assert.Ignore(kMetadataOutOfReach);
            }
        }

        /// <summary>
        /// Locates the Hasura metadata in the repository or in the directory the installer copies
        /// it to next to the tests, walking up from the directory of the test assembly.
        /// </summary>
        /// <returns>The metadata file, or null when it is out of reach.</returns>
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

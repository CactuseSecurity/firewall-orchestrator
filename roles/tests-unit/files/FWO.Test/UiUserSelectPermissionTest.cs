using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the Hasura select permissions of public.uiuser and public.ldap_connection (SEC-19).
    /// Every role reachable from a UI session could list all local users of all tenants together with
    /// their dn, directory binding, tenant, last login, password flags and password history. The roles
    /// besides admin and auditor now see directory fields only, of their own row, of the users of their
    /// own tenant, or of every user when they belong to tenant0. Their own security state is read
    /// through computed fields returning it for the row of the session user only. Nothing but a data
    /// leak would report a regression here, which is why it is checked statically.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiUserSelectPermissionTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kApiFunctionsFile = "fworch-api-funcs.sql";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the uiuser select permissions cannot be checked.";
        private const string kPublicSchema = "public";
        private const string kUiUserTable = "uiuser";
        private const string kLdapConnectionTable = "ldap_connection";
        private const string kSessionArgument = "hasura_session";
        private const string kComputedFunctionPrefix = "uiuser_";
        private const string kPasswordHistoryColumn = "uiuser_pwd_history";

        /// <summary>
        /// The middleware runs login, token refresh and password changes and has to read the security
        /// state of every user. It authenticates with its own role and is not reachable by a UI session.
        /// </summary>
        private const string kServiceRole = "middleware-server";

        /// <summary>
        /// The auditor reviews all users read-only, security state included, but never the password
        /// history, which is used by the middleware alone.
        /// </summary>
        private const string kAuditorRole = "auditor";

        /// <summary>
        /// The importer resolves the name of a local user from its id and sees nothing else.
        /// </summary>
        private const string kImporterRole = "importer";

        private static readonly List<string> kRolesWithOwnPermission = [kServiceRole, kAuditorRole, kImporterRole];

        /// <summary>
        /// What a UI role may read of a user it can see: enough to name, mail and assign it in a workflow.
        /// </summary>
        private static readonly List<string> kDirectoryColumns =
        [
            "uiuser_email", "uiuser_first_name", "uiuser_id", "uiuser_language", "uiuser_last_name", "uiuser_username", "uuid"
        ];

        /// <summary>
        /// The security state of a user, which a UI role reads of its own row only.
        /// </summary>
        private static readonly List<string> kOwnComputedFields =
        [
            "own_last_login", "own_last_password_change", "own_password_must_be_changed"
        ];

        /// <summary>
        /// The row filter of a UI role. The tenant0 check comes first and compares the session with
        /// constants only, so the database evaluates it once per query instead of once per row, which
        /// keeps installations with tenant0 as their only tenant as fast as without any filter.
        /// </summary>
        private const string kExpectedUiUserFilter =
            "{\"_or\":["
            + "{\"_exists\":{\"_table\":{\"name\":\"tenant\",\"schema\":\"public\"},\"_where\":{\"_and\":["
            + "{\"tenant_id\":{\"_eq\":\"x-hasura-tenant-id\"}},{\"tenant_id\":{\"_eq\":1}}]}}},"
            + "{\"uiuser_id\":{\"_eq\":\"X-Hasura-User-Id\"}},"
            + "{\"tenant_id\":{\"_eq\":\"x-hasura-tenant-id\"}}]}";

        /// <summary>
        /// A UI role sees the LDAP connection it belongs to only, so the directory binding of another
        /// user cannot be read through the ldap_connection relationship of uiuser either.
        /// </summary>
        private const string kExpectedLdapConnectionFilter =
            "{\"uiusers\":{\"uiuser_id\":{\"_eq\":\"X-Hasura-User-Id\"}}}";

        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(() => LocateFile(Path.Combine("roles", "api", "files"), kMetadataFile));
        private static readonly Lazy<FileInfo?> kApiFunctionsFileInfo =
            new(() => LocateFile(Path.Combine("roles", "database", "files", "sql", "idempotent"), kApiFunctionsFile));

        /// <summary>
        /// A UI role may select the directory fields of uiuser, and none of the columns describing the
        /// security state, the tenant or the directory binding of a user.
        /// </summary>
        [Test]
        public void UiUserSelect_GrantsUiRolesDirectoryColumnsOnly()
        {
            List<string> violations = [];

            foreach (JsonElement permission in ReadPermissions(kUiUserTable, "select_permissions").Where(IsUiRole))
            {
                List<string> forbidden = ReadStrings(permission.GetProperty("permission"), "columns")
                    .Where(column => !kDirectoryColumns.Contains(column)).ToList();
                if (forbidden.Count > 0)
                {
                    violations.Add($"{ReadRole(permission)} may select {string.Join(", ", forbidden)}");
                }
            }

            Assert.That(violations, Is.Empty, "a UI role may select directory columns of uiuser only: " + string.Join("; ", violations));
        }

        /// <summary>
        /// The password history is read by the middleware alone, not even by the auditor.
        /// </summary>
        [Test]
        public void UiUserSelect_GrantsPasswordHistoryToServiceRoleOnly()
        {
            List<string> roles = ReadPermissions(kUiUserTable, "select_permissions")
                .Where(permission => ReadRole(permission) != kServiceRole)
                .Where(permission => ReadStrings(permission.GetProperty("permission"), "columns").Contains(kPasswordHistoryColumn))
                .Select(ReadRole).ToList();

            Assert.That(roles, Is.Empty, $"only {kServiceRole} may select {kPasswordHistoryColumn}");
        }

        /// <summary>
        /// A UI role sees its own row, the rows of its tenant, and all rows when it belongs to tenant0,
        /// and it may not aggregate over rows it cannot see.
        /// </summary>
        [Test]
        public void UiUserSelect_ScopesUiRolesToOwnRowOwnTenantOrTenant0()
        {
            List<string> violations = [];

            foreach (JsonElement permission in ReadPermissions(kUiUserTable, "select_permissions").Where(IsUiRole))
            {
                JsonElement entry = permission.GetProperty("permission");
                string filter = entry.TryGetProperty("filter", out JsonElement condition) ? JsonSerializer.Serialize(condition) : "<none>";
                if (filter != kExpectedUiUserFilter)
                {
                    violations.Add($"{ReadRole(permission)} filters on {filter}");
                }
                if (entry.TryGetProperty("allow_aggregations", out JsonElement aggregations) && aggregations.GetBoolean())
                {
                    violations.Add($"{ReadRole(permission)} may aggregate");
                }
            }

            Assert.That(violations, Is.Empty, "a UI role has to be scoped to " + kExpectedUiUserFilter + ": " + string.Join("; ", violations));
        }

        /// <summary>
        /// A UI role and the auditor read their own security state through the own_* computed fields,
        /// which the UI login flow depends on.
        /// </summary>
        [Test]
        public void UiUserSelect_GrantsOwnComputedFieldsToUiRolesAndAuditor()
        {
            List<string> violations = [];

            foreach (JsonElement permission in ReadPermissions(kUiUserTable, "select_permissions")
                .Where(permission => IsUiRole(permission) || ReadRole(permission) == kAuditorRole))
            {
                List<string> missing = kOwnComputedFields
                    .Except(ReadStrings(permission.GetProperty("permission"), "computed_fields")).ToList();
                if (missing.Count > 0)
                {
                    violations.Add($"{ReadRole(permission)} misses {string.Join(", ", missing)}");
                }
            }

            Assert.That(violations, Is.Empty, string.Join("; ", violations));
        }

        /// <summary>
        /// Each own_* computed field is backed by a function receiving the Hasura session, which is
        /// what restricts its value to the row of the session user, and the function is defined in the
        /// idempotent SQL run on every installation and upgrade.
        /// </summary>
        [Test]
        public void UiUserComputedFields_AreSessionBoundAndDefined()
        {
            JsonElement computedFields = ReadTable(kUiUserTable).GetProperty("computed_fields");
            string? functions = kApiFunctionsFileInfo.Value is FileInfo file ? File.ReadAllText(file.FullName) : null;
            List<string> violations = [];

            foreach (string name in kOwnComputedFields)
            {
                JsonElement? field = computedFields.EnumerateArray()
                    .Where(entry => entry.GetProperty("name").GetString() == name)
                    .Select(entry => (JsonElement?)entry).FirstOrDefault();
                string function = kComputedFunctionPrefix + name;
                if (field is null)
                {
                    violations.Add($"{name} is not declared");
                    continue;
                }
                JsonElement definition = field.Value.GetProperty("definition");
                if (definition.GetProperty("function").GetProperty("name").GetString() != function
                    || !definition.TryGetProperty("session_argument", out JsonElement session) || session.GetString() != kSessionArgument)
                {
                    violations.Add($"{name} is not bound to {function}({kSessionArgument})");
                }
                if (functions != null && !functions.Contains($"FUNCTION public.{function}(uiuser_row uiuser, {kSessionArgument} json)"))
                {
                    violations.Add($"{function} is not defined in {kApiFunctionsFile}");
                }
            }

            Assert.That(violations, Is.Empty, string.Join("; ", violations));
        }

        /// <summary>
        /// A UI role sees only the LDAP connection it belongs to.
        /// </summary>
        [Test]
        public void LdapConnectionSelect_ScopesUiRolesToOwnConnection()
        {
            List<string> violations = ReadPermissions(kLdapConnectionTable, "select_permissions").Where(IsUiRole)
                .Where(permission => JsonSerializer.Serialize(permission.GetProperty("permission").GetProperty("filter")) != kExpectedLdapConnectionFilter)
                .Select(ReadRole).ToList();

            Assert.That(violations, Is.Empty, "a UI role has to be scoped to " + kExpectedLdapConnectionFilter + ": " + string.Join(", ", violations));
        }

        /// <summary>
        /// Whether a permission entry belongs to a role reachable from a UI session other than admin,
        /// which Hasura does not list, and auditor, which has read access to all users.
        /// </summary>
        private static bool IsUiRole(JsonElement permission)
        {
            return !kRolesWithOwnPermission.Contains(ReadRole(permission));
        }

        private static string ReadRole(JsonElement permission)
        {
            return permission.GetProperty("role").GetString() ?? "";
        }

        /// <summary>
        /// Reads a list of strings of a permission, which is empty when the permission does not declare it.
        /// </summary>
        private static List<string> ReadStrings(JsonElement permission, string name)
        {
            return permission.TryGetProperty(name, out JsonElement values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(value => value.GetString() ?? "").ToList()
                : [];
        }

        /// <summary>
        /// Reads one kind of permission of a table of the public schema.
        /// </summary>
        private static List<JsonElement> ReadPermissions(string tableName, string kind)
        {
            return ReadTable(tableName).TryGetProperty(kind, out JsonElement permissions)
                ? permissions.EnumerateArray().ToList()
                : [];
        }

        /// <summary>
        /// Reads the declaration of a table of the public schema from the metadata file.
        /// </summary>
        /// <returns>A clone, because the element outlives the JsonDocument it was read from.</returns>
        private static JsonElement ReadTable(string tableName)
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));

            foreach (JsonElement source in metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources").EnumerateArray())
            {
                foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
                {
                    JsonElement declaration = table.GetProperty("table");
                    if (declaration.GetProperty("name").GetString() == tableName && declaration.GetProperty("schema").GetString() == kPublicSchema)
                    {
                        return table.Clone();
                    }
                }
            }
            throw new InvalidOperationException($"{kPublicSchema}.{tableName} is not tracked in {kMetadataFile}.");
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
        /// Locates a file in the repository or in the directory the installer copies it to next to the
        /// tests, walking up from the directory of the test assembly.
        /// </summary>
        /// <returns>The file, or null when it is out of reach.</returns>
        private static FileInfo? LocateFile(string repositoryDirectory, string fileName)
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                FileInfo repository = new(Path.Combine(directory.FullName, repositoryDirectory, fileName));
                FileInfo installed = new(Path.Combine(directory.FullName, fileName));
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

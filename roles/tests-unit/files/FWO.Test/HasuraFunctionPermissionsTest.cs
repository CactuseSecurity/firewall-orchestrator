using System.Text.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the explicit Hasura function permissions. Function permission inference is switched off, so a
    /// tracked function is only a root field for the roles listed here. The function body runs as the API
    /// database user and ignores the row filters of the tables it reads, so a function with a free tenant,
    /// owner or management argument must not reach tenant-scoped user roles.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HasuraFunctionPermissionsTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kMetadataOutOfReach =
            "The Hasura metadata is not reachable in this environment, so the function permissions cannot be checked.";
        private const string kInventoryOutOfReach =
            "The installer inventory is not reachable in this environment, so the inference default cannot be checked.";
        private const string kInferenceSetting = "api_HASURA_GRAPHQL_INFER_FUNCTION_PERMISSIONS: \"false\"";
        private const string kPublicSchema = "public";
        private const string kMiddlewareServerRole = "middleware-server";
        private const string kDeviceTypeTable = "device_type";

        private static readonly List<string> kMiddlewareServerOnly = [kMiddlewareServerRole];

        /// <summary>Roles that run the workflow path analysis; the device filter still applies to the result.</summary>
        private static readonly List<string> kPathAnalysisRoles =
            ["approver", "auditor", "implementer", kMiddlewareServerRole, "modeller", "planner", "requester", "reviewer"];

        /// <summary>Roles that may see every tenant's devices and may therefore read the device_type return type.</summary>
        private static readonly List<string> kDeviceTypeReaders = ["auditor", kMiddlewareServerRole, "reporter-viewall"];

        /// <summary>
        /// Expected root-field roles per tracked function. get_rules_for_owner is only used as a computed field of
        /// device and is governed by the device and rule select permissions.
        /// </summary>
        private static readonly Dictionary<string, List<string>> kExpectedFunctionRoles = new()
        {
            ["devices_in_path"] = kPathAnalysisRoles,
            ["get_rules_for_owner"] = [],
            ["get_visible_devices_per_tenant"] = kMiddlewareServerOnly,
            ["get_visible_managements_per_tenant"] = kMiddlewareServerOnly,
            ["recert_get_one_owner_one_mgm"] = kMiddlewareServerOnly,
            ["refresh_view_rule_with_owner"] = kMiddlewareServerOnly
        };

        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(() => LocateRepositoryFile(Path.Combine("roles", "api", "files", kMetadataFile), kMetadataFile));
        private static readonly Lazy<FileInfo?> kInventoryFileInfo = new(() => LocateRepositoryFile(Path.Combine("inventory", "group_vars", "apiserver.yml"), null));

        [SetUp]
        public void RequireMetadata()
        {
            if (kMetadataFileInfo.Value is null)
            {
                Assert.Ignore(kMetadataOutOfReach);
            }
        }

        /// <summary>
        /// Every tracked function carries exactly the expected explicit permissions; a newly tracked function fails
        /// until it is added here with a deliberate role list.
        /// </summary>
        [Test]
        public void TrackedFunctions_HaveExactlyTheExpectedPermissions()
        {
            Dictionary<string, List<string>> actualRoles = ReadFunctionRoles();
            List<string> violations = [];

            foreach ((string functionName, List<string> roles) in actualRoles)
            {
                if (!kExpectedFunctionRoles.TryGetValue(functionName, out List<string>? expectedRoles))
                {
                    violations.Add($"{functionName} is tracked without an explicit permission decision");
                }
                else if (!roles.Order().SequenceEqual(expectedRoles.Order()))
                {
                    violations.Add($"{functionName}: [{string.Join(", ", roles)}]");
                }
            }

            Assert.That(violations, Is.Empty, string.Join("; ", violations));
            Assert.That(actualRoles.Keys, Is.EquivalentTo(kExpectedFunctionRoles.Keys));
        }

        /// <summary>
        /// device_type is only the return type of the per-tenant functions. Tenant-scoped roles must not read it,
        /// so that even an installation that still infers function permissions does not hand them these functions.
        /// </summary>
        [Test]
        public void DeviceType_IsOnlyReadableByRolesThatSeeAllTenants()
        {
            List<string> readers = ReadTableSelectRoles(kDeviceTypeTable);

            Assert.That(readers, Is.EquivalentTo(kDeviceTypeReaders));
        }

        /// <summary>
        /// The installer starts Hasura without function permission inference.
        /// </summary>
        [Test]
        public void InstallerDefault_DisablesFunctionPermissionInference()
        {
            if (kInventoryFileInfo.Value is null)
            {
                Assert.Ignore(kInventoryOutOfReach);
            }

            string inventory = File.ReadAllText(kInventoryFileInfo.Value!.FullName);

            Assert.That(inventory, Does.Contain(kInferenceSetting));
        }

        /// <summary>
        /// Reads the explicit permission roles of every tracked function.
        /// </summary>
        /// <returns>Role list per function name; empty when the function has no permissions.</returns>
        private static Dictionary<string, List<string>> ReadFunctionRoles()
        {
            Dictionary<string, List<string>> functionRoles = [];
            using JsonDocument metadata = ReadMetadata();

            foreach (JsonElement source in GetSources(metadata))
            {
                if (!source.TryGetProperty("functions", out JsonElement functions))
                {
                    continue;
                }

                foreach (JsonElement function in functions.EnumerateArray())
                {
                    string name = function.GetProperty("function").GetProperty("name").GetString() ?? "";
                    functionRoles[name] = function.TryGetProperty("permissions", out JsonElement permissions)
                        ? permissions.EnumerateArray().Select(permission => permission.GetProperty("role").GetString() ?? "").ToList()
                        : [];
                }
            }

            return functionRoles;
        }

        /// <summary>
        /// Reads the roles holding a select permission on a public table.
        /// </summary>
        /// <param name="tableName">Name of the tracked table.</param>
        private static List<string> ReadTableSelectRoles(string tableName)
        {
            using JsonDocument metadata = ReadMetadata();

            foreach (JsonElement table in GetSources(metadata).SelectMany(source => source.GetProperty("tables").EnumerateArray()))
            {
                JsonElement declaration = table.GetProperty("table");
                if (declaration.GetProperty("name").GetString() == tableName && declaration.GetProperty("schema").GetString() == kPublicSchema)
                {
                    return table.TryGetProperty("select_permissions", out JsonElement permissions)
                        ? permissions.EnumerateArray().Select(permission => permission.GetProperty("role").GetString() ?? "").ToList()
                        : [];
                }
            }

            throw new InvalidOperationException($"{kPublicSchema}.{tableName} is not tracked in {kMetadataFile}.");
        }

        private static JsonDocument ReadMetadata()
        {
            FileInfo metadataFile = kMetadataFileInfo.Value ?? throw new InvalidOperationException(kMetadataOutOfReach);
            return JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));
        }

        private static JsonElement.ArrayEnumerator GetSources(JsonDocument metadata)
        {
            return metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources").EnumerateArray();
        }

        /// <summary>
        /// Locates a repository file, or the copy the installer places next to the tests, walking up from the
        /// directory of the test assembly.
        /// </summary>
        /// <param name="repositoryPath">Path relative to the repository root.</param>
        /// <param name="installedName">File name of the installed copy, or null if none is installed.</param>
        /// <returns>The file, or null when it is out of reach.</returns>
        private static FileInfo? LocateRepositoryFile(string repositoryPath, string? installedName)
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                FileInfo repository = new(Path.Combine(directory.FullName, repositoryPath));
                if (repository.Exists)
                {
                    return repository;
                }
                if (installedName != null)
                {
                    FileInfo installed = new(Path.Combine(directory.FullName, installedName));
                    if (installed.Exists)
                    {
                        return installed;
                    }
                }
                directory = directory.Parent;
            }
            return null;
        }
    }
}

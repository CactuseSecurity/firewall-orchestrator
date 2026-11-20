using System.Text.Json;
using System.Text.Json.Nodes;
using FWO.Basics;
using FWO.Data.Ai;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Guards what the technical role of the AI harness may read: the AI items of the global config, and the secret of the
/// single credential the AI settings reference, through the view v_ai_provider_credential, and nothing else. The keys
/// live in several places that have to agree with <see cref="AiSettings.kKeys"/>: the Hasura filter, the view, the
/// config seeds of a new installation and of the upgrade. Also checks that every error text of the AI settings page
/// exists in all languages.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AiHarnessAccessGuardTest
{
    private const string kPublicSchema = "public";
    private const string kConfigTable = "config";
    private const string kCredentialView = "v_ai_provider_credential";
    private const string kSelectPermissions = "select_permissions";
    private const string kUpgradeFile = "9.9.4.sql";

    private static readonly List<string> kPermissionKinds = ["select_permissions", "insert_permissions", "update_permissions", "delete_permissions"];
    private static readonly List<string> kConfigColumns = ["config_key", "config_value", "config_user"];
    private static readonly List<string> kCredentialViewColumns = ["id", "is_key_pair", "secret", "username"];
    private static readonly List<string> kLanguages = ["German", "English"];
    private static readonly List<string> kAiHarnessRoleOnly = [Roles.AiHarness];

    private JsonNode metadata = null!;
    private string rolesDirectory = "";

    /// <summary>
    /// Loads the Hasura metadata the installer posts.
    /// </summary>
    [OneTimeSetUp]
    public void LoadMetadata()
    {
        string? commonFilesPath = Environment.GetEnvironmentVariable("FWO_BASE_DIR");
        rolesDirectory = string.IsNullOrEmpty(commonFilesPath) ? "" : Path.GetFullPath(Path.Combine(commonFilesPath, "..", ".."));
        string metadataPath = Path.Combine(rolesDirectory, "api", "files", "replace_metadata.json");
        if (!File.Exists(metadataPath))
        {
            Assert.Ignore($"The Hasura metadata was not found at '{metadataPath}'.");
        }
        metadata = JsonNode.Parse(File.ReadAllText(metadataPath)) ?? throw new AssertionException("The Hasura metadata is empty.");
    }

    /// <summary>
    /// The AI harness reads the global AI items only: no user settings and no other keys, e.g. the email password.
    /// </summary>
    [Test]
    public void ConfigSelect_IsLimitedToGlobalAiItems()
    {
        JsonNode permission = FindPermission(FindTable(kConfigTable), kSelectPermissions, Roles.AiHarness)
            ?? throw new AssertionException("The ai-harness role cannot read the config.");
        string filter = Serialize(permission["filter"]);
        List<string> allowedKeys = [.. FindArrays(permission["filter"], "_in").SelectMany(keys => keys.Select(key => key!.GetValue<string>()))];

        Assert.Multiple(() =>
        {
            Assert.That(ReadColumns(permission), Is.EquivalentTo(kConfigColumns));
            Assert.That(filter, Does.Contain("\"config_user\":{\"_eq\":0}"));
            Assert.That(allowedKeys, Is.EquivalentTo(AiSettings.kKeys));
        });
    }

    /// <summary>
    /// The AI harness reads a credential only through the view, and the view is readable by the AI harness only.
    /// </summary>
    [Test]
    public void CredentialView_IsTheOnlyCredentialAccessOfTheAiHarness()
    {
        JsonNode view = FindTable(kCredentialView);
        JsonNode? permission = FindPermission(view, kSelectPermissions, Roles.AiHarness);

        Assert.Multiple(() =>
        {
            Assert.That(permission, Is.Not.Null, "The ai-harness role cannot read the credential view.");
            Assert.That(ReadColumns(permission), Is.EquivalentTo(kCredentialViewColumns));
            Assert.That(RolesOf(view), Is.EquivalentTo(kAiHarnessRoleOnly));
        });
    }

    /// <summary>
    /// Besides the AI items of the config and the credential view, the AI harness may neither read nor change anything.
    /// </summary>
    [Test]
    public void AiHarness_HasNoOtherPermission()
    {
        List<string> grants = [];
        foreach (JsonNode table in FindTables(metadata))
        {
            string name = table["table"]?["name"]?.GetValue<string>() ?? "";
            foreach (string permissionKind in kPermissionKinds)
            {
                bool isExpected = permissionKind == kSelectPermissions && (name == kConfigTable || name == kCredentialView);
                if (!isExpected && FindPermission(table, permissionKind, Roles.AiHarness) != null)
                {
                    grants.Add($"{name}: {permissionKind}");
                }
            }
        }

        Assert.That(grants, Is.Empty, string.Join("; ", grants));
    }

    /// <summary>
    /// The view joins the credential on the config item the AI settings page writes.
    /// </summary>
    [Test]
    public void CredentialView_UsesCredentialIdKey()
    {
        string viewSql = ReadRepositoryFile("database", "files", "sql", "idempotent", "fworch-ai.sql");

        Assert.That(viewSql, Does.Contain($"config.config_key = '{AiSettings.kCredentialIdKey}'"));
        Assert.That(viewSql, Does.Contain("config.config_user = 0"));
    }

    /// <summary>
    /// New installations and upgrades get every AI item, so that the harness and the settings page find them.
    /// </summary>
    [Test]
    public void ConfigSeeds_ContainAllAiKeys()
    {
        string creationSql = ReadRepositoryFile("database", "files", "sql", "creation", "fworch-fill-config.sql");
        string upgradeSql = ReadRepositoryFile("database", "files", "upgrade", kUpgradeFile);

        Assert.Multiple(() =>
        {
            foreach (string key in AiSettings.kKeys)
            {
                Assert.That(creationSql, Does.Contain($"'{key}'"), $"fworch-fill-config.sql lacks {key}");
                Assert.That(upgradeSql, Does.Contain($"'{key}'"), $"{kUpgradeFile} lacks {key}");
            }
        });
    }

    /// <summary>
    /// Every problem of the AI settings has its error text in all languages.
    /// </summary>
    [Test]
    public void ProblemTexts_ExistInAllLanguages()
    {
        string texts = ReadRepositoryFile("database", "files", "sql", "idempotent", "fworch-texts.sql");

        Assert.Multiple(() =>
        {
            foreach (AiSettingsProblem problem in Enum.GetValues<AiSettingsProblem>())
            {
                string key = AiSettingsProblemTexts.GetTextKey(problem);
                foreach (string language in kLanguages)
                {
                    Assert.That(texts, Does.Match($"VALUES \\('{key}',\\s*'{language}'"), $"{key} lacks its {language} text");
                }
            }
        });
    }

    /// <summary>
    /// Reads a file of the repository below the roles directory, ignoring the test where the repository is not at hand.
    /// </summary>
    private string ReadRepositoryFile(params string[] pathBelowRoles)
    {
        string path = rolesDirectory;
        foreach (string pathPart in pathBelowRoles)
        {
            path = Path.Combine(path, pathPart);
        }
        if (!File.Exists(path))
        {
            Assert.Ignore($"The file '{path}' was not found.");
        }
        return File.ReadAllText(path);
    }

    private JsonNode FindTable(string name)
    {
        JsonNode? table = FindTables(metadata).FirstOrDefault(candidate => candidate["table"]?["schema"]?.GetValue<string>() == kPublicSchema
            && candidate["table"]?["name"]?.GetValue<string>() == name);
        return table ?? throw new AssertionException($"The table {kPublicSchema}.{name} is missing from the metadata.");
    }

    private static JsonNode? FindPermission(JsonNode table, string permissionKind, string role)
    {
        return (table[permissionKind]?.AsArray() ?? []).FirstOrDefault(permission => permission?["role"]?.GetValue<string>() == role)?["permission"];
    }

    private static List<string> RolesOf(JsonNode table)
    {
        return [.. kPermissionKinds.SelectMany(kind => table[kind]?.AsArray() ?? []).Select(permission => permission?["role"]?.GetValue<string>() ?? "").Distinct()];
    }

    private static List<string> ReadColumns(JsonNode? permission)
    {
        return [.. (permission?["columns"]?.AsArray() ?? []).Select(column => column!.GetValue<string>())];
    }

    /// <summary>
    /// Returns every array below the node that is the value of the given operator, e.g. all lists of an _in.
    /// </summary>
    private static IEnumerable<JsonArray> FindArrays(JsonNode? node, string operatorName)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
            {
                if (property.Key == operatorName && property.Value is JsonArray values)
                {
                    yield return values;
                }
                foreach (JsonArray nested in FindArrays(property.Value, operatorName))
                {
                    yield return nested;
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonArray nested in jsonArray.SelectMany(item => FindArrays(item, operatorName)))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Walks the metadata for every table entry, wherever the source structure keeps it.
    /// </summary>
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

    private static string Serialize(JsonNode? node)
    {
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "{}";
    }
}

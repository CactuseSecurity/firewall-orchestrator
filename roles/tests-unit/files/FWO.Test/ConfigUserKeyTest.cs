using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FWO.Config.Api.Data;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Checks the keys of personal settings: table config_user_key lists exactly the properties marked
    /// [UserConfigData], on fresh installations and on upgraded ones, and the roles reachable from a UI session may
    /// only write their own rows of those keys.
    /// </summary>
    [TestFixture]
    internal class ConfigUserKeyTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kFillConfigFile = "fworch-fill-config.sql";
        private const string kSqlFilePattern = "*.sql";
        private const string kPublicSchema = "public";
        private const string kConfigTable = "config";
        private const string kUserKeyTable = "config_user_key";
        private const string kKeyColumn = "config_key";
        private const string kServiceRole = "middleware-server";
        private const string kMetadataOutOfReach = "The Hasura metadata is not reachable in this environment.";
        private const string kFillConfigOutOfReach = "The database creation scripts are not reachable in this environment.";
        private const string kUpgradeScriptsOutOfReach = "The database upgrade scripts of the repository are not reachable in this environment.";
        private const string kParsedScript = "parsed.sql";

        /// <summary>
        /// Condition every write of a non-service role on config has to satisfy: its own row of a personal setting.
        /// </summary>
        private const string kPersonalSettingCondition =
            "{\"_and\":[{\"config_user\":{\"_eq\":\"X-Hasura-User-Id\"}},{\"config_user_key\":{\"config_key\":{\"_is_null\":false}}}]}";

        private const string kMultiRowInsert =
            "INSERT INTO config_user_key (config_key) VALUES\n    ('a'),\n    ('b')\nON CONFLICT DO NOTHING;";
        private const string kSingleRowInserts =
            "INSERT INTO config_user_key (config_key) VALUES ('a');\nINSERT INTO config_user_key (config_key) VALUES ('b');";
        private const string kInsertThenDelete =
            "INSERT INTO config_user_key (config_key) VALUES ('a'), ('b'), ('c');\nDELETE FROM config_user_key WHERE config_key IN ('c');";
        private const string kCommentAndOtherTables =
            "-- INSERT INTO config_user_key (config_key) VALUES ('x');\n"
            + "INSERT INTO config (config_key, config_value, config_user) VALUES ('y', 'z', 0);\n"
            + "DELETE FROM config WHERE config_user <> 0 AND config_key NOT IN (SELECT config_key FROM config_user_key);\n"
            + "INSERT INTO config_user_key (config_key) VALUES ('a'), ('b') ON CONFLICT DO NOTHING;";

        private static readonly string[] kParsedKeys = ["a", "b"];

        private static readonly TimeSpan kRegexTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Statements changing config_user_key: inserts of one or more rows, and deletes. The body runs to the end of
        /// the statement and holds the quoted keys.
        /// </summary>
        private static readonly Regex kUserKeyStatement = new(
            @"(?:(?<insert>INSERT\s+INTO\s+config_user_key\s*\(\s*config_key\s*\)\s*VALUES)|(?<delete>DELETE\s+FROM\s+config_user_key\b))(?<body>[^;]*)",
            RegexOptions.IgnoreCase, kRegexTimeout);
        private static readonly Regex kQuotedKey = new(@"'([^']+)'", RegexOptions.None, kRegexTimeout);
        private static readonly Regex kLineComment = new(@"--[^\n]*", RegexOptions.None, kRegexTimeout);
        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);
        private static readonly Lazy<FileInfo?> kFillConfigFileInfo = new(LocateFillConfig);

        /// <summary>
        /// A statement of an SQL script on config_user_key.
        /// </summary>
        /// <param name="IsInsert">True for an insert, false for a delete.</param>
        /// <param name="Keys">Keys the statement inserts or deletes.</param>
        private sealed record UserKeyStatement(bool IsInsert, List<string> Keys);

        /// <summary>
        /// A fresh installation fills config_user_key with exactly the keys of the properties marked [UserConfigData].
        /// </summary>
        [Test]
        public void UserConfigKeys_InCreationScriptMatchUserConfigDataProperties()
        {
            FileInfo fillConfig = RequireFile(kFillConfigFileInfo, kFillConfigOutOfReach);
            List<string> scriptKeys = [.. ReadUserKeyStatements(File.ReadAllText(fillConfig.FullName))
                .Where(statement => statement.IsInsert)
                .SelectMany(statement => statement.Keys)];

            Assert.Multiple(() =>
            {
                Assert.That(scriptKeys, Is.Unique);
                Assert.That(scriptKeys, Is.EquivalentTo(GetUserConfigDataKeys()),
                    $"{kFillConfigFile} has to insert into {kUserKeyTable} exactly the keys marked [UserConfigData].");
            });
        }

        /// <summary>
        /// An installation upgraded from a version without config_user_key gets exactly the keys of the properties
        /// marked [UserConfigData]: the upgrade scripts, run in version order, insert each of them and no other key.
        /// </summary>
        [Test]
        public void UserConfigKeys_InUpgradeScriptsMatchUserConfigDataProperties()
        {
            List<FileInfo> upgradeScripts = GetUpgradeScriptsInVersionOrder();
            Dictionary<string, string> upgradeKeys = [];
            foreach (FileInfo upgradeScript in upgradeScripts)
            {
                ApplyUserKeyStatements(File.ReadAllText(upgradeScript.FullName), upgradeScript.Name, upgradeKeys);
            }
            List<string> userConfigDataKeys = GetUserConfigDataKeys();
            List<string> missingKeys = [.. userConfigDataKeys.Where(key => !upgradeKeys.ContainsKey(key))];
            List<string> unexpectedKeys = [.. upgradeKeys.Where(entry => !userConfigDataKeys.Contains(entry.Key))
                .Select(entry => $"{entry.Key} (inserted by {entry.Value})")];
            string newestScript = upgradeScripts[^1].Name;

            Assert.Multiple(() =>
            {
                Assert.That(missingKeys, Is.Empty,
                    $"Keys marked [UserConfigData] that no upgrade script inserts into {kUserKeyTable}, so upgraded installations cannot store them "
                    + $"for a user: {string.Join(", ", missingKeys)}. Insert them in the upgrade script of the release (newest: {newestScript}).");
                Assert.That(unexpectedKeys, Is.Empty,
                    $"Keys the upgrade scripts insert into {kUserKeyTable} that are not marked [UserConfigData]: {string.Join(", ", unexpectedKeys)}. "
                    + $"Delete them from {kUserKeyTable} in the upgrade script of the release (newest: {newestScript}).");
            });
        }

        /// <summary>
        /// The script reader takes the keys of single and multi row inserts, applies deletes, and skips comments and
        /// statements on other tables.
        /// </summary>
        [TestCase(kMultiRowInsert)]
        [TestCase(kSingleRowInserts)]
        [TestCase(kInsertThenDelete)]
        [TestCase(kCommentAndOtherTables)]
        public void UserKeyStatements_YieldKeysOfScript(string sql)
        {
            Dictionary<string, string> keys = [];

            ApplyUserKeyStatements(sql, kParsedScript, keys);

            Assert.That(keys.Keys, Is.EquivalentTo(kParsedKeys));
        }

        /// <summary>
        /// Every non-service role may insert and update only its own rows of personal settings.
        /// </summary>
        [Test]
        public void ConfigWrite_RestrictsNonServiceRolesToOwnPersonalSettings()
        {
            JsonElement configTable = ReadTable(kConfigTable);
            List<string> violations = [];

            foreach (JsonElement permission in configTable.GetProperty("insert_permissions").EnumerateArray())
            {
                AddViolation(violations, permission, "check");
            }
            foreach (JsonElement permission in configTable.GetProperty("update_permissions").EnumerateArray())
            {
                AddViolation(violations, permission, "filter");
                AddViolation(violations, permission, "check");
            }

            Assert.That(violations, Is.Empty, string.Join("; ", violations));
        }

        /// <summary>
        /// The relationship used by the permission conditions links config.config_key to config_user_key.config_key.
        /// </summary>
        [Test]
        public void ConfigRelationship_LinksKeyToUserConfigKeys()
        {
            JsonElement relationship = ReadTable(kConfigTable).GetProperty("object_relationships").EnumerateArray()
                .Single(candidate => candidate.GetProperty("name").GetString() == kUserKeyTable);
            JsonElement configuration = relationship.GetProperty("using").GetProperty("manual_configuration");
            JsonElement remoteTable = configuration.GetProperty("remote_table");

            Assert.Multiple(() =>
            {
                Assert.That(remoteTable.GetProperty("name").GetString(), Is.EqualTo(kUserKeyTable));
                Assert.That(remoteTable.GetProperty("schema").GetString(), Is.EqualTo(kPublicSchema));
                Assert.That(configuration.GetProperty("column_mapping").GetProperty(kKeyColumn).GetString(), Is.EqualTo(kKeyColumn));
            });
        }

        /// <summary>
        /// Every non-service role writing config can read config_user_key, which its permission conditions use.
        /// </summary>
        [Test]
        public void UserConfigKeyTable_IsReadableByEveryRoleWritingConfig()
        {
            List<string> writingRoles = [.. ReadTable(kConfigTable).GetProperty("insert_permissions").EnumerateArray()
                .Select(ReadRole)
                .Where(role => role != kServiceRole)];
            List<string> readingRoles = [.. ReadTable(kUserKeyTable).GetProperty("select_permissions").EnumerateArray()
                .Select(ReadRole)];

            Assert.That(readingRoles, Is.EquivalentTo(writingRoles));
        }

        /// <summary>
        /// Adds a violation if the condition of a non-service role differs from the personal setting condition.
        /// </summary>
        private static void AddViolation(List<string> violations, JsonElement permission, string conditionName)
        {
            string role = ReadRole(permission);
            if (role == kServiceRole)
            {
                return;
            }

            string condition = ReadCondition(permission.GetProperty("permission"), conditionName);
            if (condition != kPersonalSettingCondition)
            {
                violations.Add($"{role} {conditionName}: {condition}");
            }
        }

        /// <summary>
        /// Reads the role of a permission entry.
        /// </summary>
        private static string ReadRole(JsonElement permission)
        {
            return permission.GetProperty("role").GetString() ?? "";
        }

        /// <summary>
        /// Reads a condition of a permission as compact JSON, or "&lt;none&gt;" if the permission has none.
        /// </summary>
        private static string ReadCondition(JsonElement permission, string name)
        {
            if (!permission.TryGetProperty(name, out JsonElement condition) || condition.ValueKind == JsonValueKind.Null)
            {
                return "<none>";
            }
            return JsonSerializer.Serialize(condition);
        }

        /// <summary>
        /// Returns the config keys of the properties marked [UserConfigData].
        /// </summary>
        private static List<string> GetUserConfigDataKeys()
        {
            return [.. typeof(ConfigData).GetProperties()
                .Where(property => property.GetCustomAttribute<UserConfigDataAttribute>() != null)
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .OfType<string>()];
        }

        /// <summary>
        /// Reads the statements on config_user_key of an SQL script in their order, leaving out line comments.
        /// </summary>
        private static List<UserKeyStatement> ReadUserKeyStatements(string sql)
        {
            List<UserKeyStatement> statements = [];
            foreach (Match statement in kUserKeyStatement.Matches(kLineComment.Replace(sql, "")))
            {
                List<string> keys = [.. kQuotedKey.Matches(statement.Groups["body"].Value).Select(key => key.Groups[1].Value)];
                statements.Add(new UserKeyStatement(statement.Groups["insert"].Success, keys));
            }
            return statements;
        }

        /// <summary>
        /// Applies the statements on config_user_key of a script to the keys: inserted keys are added with the name of
        /// the script, deleted keys are removed.
        /// </summary>
        private static void ApplyUserKeyStatements(string sql, string scriptName, Dictionary<string, string> keys)
        {
            foreach (UserKeyStatement statement in ReadUserKeyStatements(sql))
            {
                foreach (string key in statement.Keys)
                {
                    if (statement.IsInsert)
                    {
                        keys[key] = scriptName;
                    }
                    else
                    {
                        keys.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the upgrade scripts of the repository in the order the installer runs them. Skips the test where the
        /// repository is not reachable, as an installation keeps only the scripts of its last upgrade.
        /// </summary>
        private static List<FileInfo> GetUpgradeScriptsInVersionOrder()
        {
            DirectoryInfo? upgradeDirectory = LocateUpgradeDirectory();
            if (upgradeDirectory is null)
            {
                Assert.Ignore(kUpgradeScriptsOutOfReach);
            }
            List<FileInfo> upgradeScripts = [.. upgradeDirectory!.EnumerateFiles(kSqlFilePattern)
                .OrderBy(script => Version.Parse(Path.GetFileNameWithoutExtension(script.Name)))];
            Assert.That(upgradeScripts, Is.Not.Empty, $"No upgrade script found in {upgradeDirectory.FullName}.");
            return upgradeScripts;
        }

        /// <summary>
        /// Reads a table entry of the public schema from the metadata file.
        /// </summary>
        private static JsonElement ReadTable(string tableName)
        {
            FileInfo metadataFile = RequireFile(kMetadataFileInfo, kMetadataOutOfReach);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));
            foreach (JsonElement source in metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources").EnumerateArray())
            {
                foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
                {
                    JsonElement declaration = table.GetProperty("table");
                    if (declaration.GetProperty("name").GetString() == tableName && declaration.GetProperty("schema").GetString() == kPublicSchema)
                    {
                        // Cloned, because the element outlives the JsonDocument it was read from.
                        return table.Clone();
                    }
                }
            }
            throw new InvalidOperationException($"{kPublicSchema}.{tableName} is not tracked in {kMetadataFile}.");
        }

        /// <summary>
        /// Skips the test where the file is not deployed, because passing on no input would report the
        /// settings as sound without having read them.
        /// </summary>
        private static FileInfo RequireFile(Lazy<FileInfo?> file, string outOfReachMessage)
        {
            if (file.Value is null)
            {
                Assert.Ignore(outOfReachMessage);
            }
            return file.Value!;
        }

        /// <summary>
        /// Locates the Hasura metadata file.
        /// </summary>
        private static FileInfo? LocateMetadata()
        {
            return Locate(Path.Combine("roles", "api", "files", kMetadataFile), kMetadataFile);
        }

        /// <summary>
        /// Locates the database creation script that fills the config tables.
        /// </summary>
        private static FileInfo? LocateFillConfig()
        {
            return Locate(Path.Combine("roles", "database", "files", "sql", "creation", kFillConfigFile),
                Path.Combine("database", "sql", "creation", kFillConfigFile));
        }

        /// <summary>
        /// Locates the directory of the database upgrade scripts in the repository, walking up from the directory of
        /// the test assembly.
        /// </summary>
        private static DirectoryInfo? LocateUpgradeDirectory()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                DirectoryInfo upgradeDirectory = new(Path.Combine(directory.FullName, "roles", "database", "files", "upgrade"));
                if (upgradeDirectory.Exists)
                {
                    return upgradeDirectory;
                }
                directory = directory.Parent;
            }
            return null;
        }

        /// <summary>
        /// Locates a file in the repository or in the installed directory tree, walking up from the directory of
        /// the test assembly.
        /// </summary>
        private static FileInfo? Locate(string repositoryPath, string installedPath)
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                FileInfo repository = new(Path.Combine(directory.FullName, repositoryPath));
                if (repository.Exists)
                {
                    return repository;
                }
                FileInfo installed = new(Path.Combine(directory.FullName, installedPath));
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

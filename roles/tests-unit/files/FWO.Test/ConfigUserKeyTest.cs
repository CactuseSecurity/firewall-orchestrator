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
    /// [UserConfigData], and the roles reachable from a UI session may only write their own rows of those keys.
    /// </summary>
    [TestFixture]
    internal class ConfigUserKeyTest
    {
        private const string kMetadataFile = "replace_metadata.json";
        private const string kFillConfigFile = "fworch-fill-config.sql";
        private const string kPublicSchema = "public";
        private const string kConfigTable = "config";
        private const string kUserKeyTable = "config_user_key";
        private const string kKeyColumn = "config_key";
        private const string kServiceRole = "middleware-server";
        private const string kMetadataOutOfReach = "The Hasura metadata is not reachable in this environment.";
        private const string kFillConfigOutOfReach = "The database creation scripts are not reachable in this environment.";

        /// <summary>
        /// Condition every write of a non-service role on config has to satisfy: its own row of a personal setting.
        /// </summary>
        private const string kPersonalSettingCondition =
            "{\"_and\":[{\"config_user\":{\"_eq\":\"X-Hasura-User-Id\"}},{\"config_user_key\":{\"config_key\":{\"_is_null\":false}}}]}";

        private static readonly Regex kUserKeyInsert = new(@"INSERT\s+INTO\s+config_user_key\s*\(\s*config_key\s*\)\s*VALUES\s*\(\s*'([^']+)'\s*\)",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        private static readonly Lazy<FileInfo?> kMetadataFileInfo = new(LocateMetadata);
        private static readonly Lazy<FileInfo?> kFillConfigFileInfo = new(LocateFillConfig);

        [Test]
        public void UserConfigKeys_InCreationScriptMatchUserConfigDataProperties()
        {
            FileInfo fillConfig = RequireFile(kFillConfigFileInfo, kFillConfigOutOfReach);
            List<string> scriptKeys = [.. kUserKeyInsert.Matches(File.ReadAllText(fillConfig.FullName))
                .Select(insert => insert.Groups[1].Value)];

            Assert.Multiple(() =>
            {
                Assert.That(scriptKeys, Is.Unique);
                Assert.That(scriptKeys, Is.EquivalentTo(GetUserConfigDataKeys()));
            });
        }

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

        private static string ReadRole(JsonElement permission)
        {
            return permission.GetProperty("role").GetString() ?? "";
        }

        private static string ReadCondition(JsonElement permission, string name)
        {
            if (!permission.TryGetProperty(name, out JsonElement condition) || condition.ValueKind == JsonValueKind.Null)
            {
                return "<none>";
            }
            return JsonSerializer.Serialize(condition);
        }

        private static List<string> GetUserConfigDataKeys()
        {
            return [.. typeof(ConfigData).GetProperties()
                .Where(property => property.GetCustomAttribute<UserConfigDataAttribute>() != null)
                .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .OfType<string>()];
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

        private static FileInfo? LocateMetadata()
        {
            return Locate(Path.Combine("roles", "api", "files", kMetadataFile), kMetadataFile);
        }

        private static FileInfo? LocateFillConfig()
        {
            return Locate(Path.Combine("roles", "database", "files", "sql", "creation", kFillConfigFile),
                Path.Combine("database", "sql", "creation", kFillConfigFile));
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

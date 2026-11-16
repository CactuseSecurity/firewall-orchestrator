using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Checks that the API calls reading global settings (queries and subscriptions on the config table) read only
    /// the global rows (config_user 0), for every config key they select. The calls reading the settings of one
    /// user are the exception; they have to filter on the user they are called for.
    /// </summary>
    [TestFixture]
    internal class GlobalConfigReaderTest
    {
        private const string kApiCallDirectoryName = "fwo-api-calls";
        private const string kGraphQlFilePattern = "*.graphql";
        private const string kSchedulerSubscriptionFile = "subscribeDailyCheckConfigChanges.graphql";
        private const string kImporterQueryFile = "getConfigValue.graphql";
        private const char kObjectStart = '{';
        private const char kObjectEnd = '}';
        private static readonly TimeSpan kRegexTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The field config, with arguments or without (selection set directly after the field name), also aliased.
        /// </summary>
        private static readonly Regex kConfigRootField = new(@"(^|[^\w])config\s*[({]", RegexOptions.None, kRegexTimeout);
        private static readonly Regex kKeyCondition = new(@"\bconfig_key\s*:", RegexOptions.None, kRegexTimeout);
        private static readonly Regex kGlobalRowCondition = new(@"\bconfig_user\s*:\s*\{\s*_eq\s*:\s*0\s*\}", RegexOptions.None, kRegexTimeout);
        private static readonly Regex kUserVariableCondition = new(@"\bconfig_user\s*:\s*\{\s*_eq\s*:\s*\$\w+\s*\}", RegexOptions.None, kRegexTimeout);

        /// <summary>
        /// Calls reading the settings of the user given as variable.
        /// </summary>
        private static readonly HashSet<string> kUserScopedFiles = ["getConfigItemsByUser.graphql", "subscribeConfigChangesByUser.graphql"];

        private const string kFilteredPerKey =
            "subscription a { config(where: {_or: [{config_key: {_eq: \"x\"}, config_user: {_eq: 0}} {config_key: {_eq: \"y\"}, config_user: {_eq: 0}}]}) { config_key } }";
        private const string kFilteredForAllKeys =
            "subscription b { config(where: {config_user: {_eq: 0}, _or: [{config_key: {_eq: \"x\"}} {config_key: {_eq: \"y\"}}]}) { config_key config_user } }";
        private const string kOneKeyUnfiltered =
            "subscription c { config(where: {_or: [{config_key: {_eq: \"x\"}, config_user: {_eq: 0}} {config_key: {_eq: \"y\"}}]}) { config_key } }";
        private const string kUnfiltered =
            "query d($key: String) { config(where: {config_key: {_eq: $key}}) { config_value config_user } }";
        private const string kOtherUser =
            "query e { config(where: {config_key: {_eq: \"x\"}, config_user: {_eq: 1}}) { config_value } }";
        private const string kNoCondition =
            "query f { config { config_key config_value } }";
        private const string kAliasedNoCondition =
            "query g { settings: config { config_key config_value } }";
        private const string kOtherTable =
            "query h { config_user_key { config_key } }";
        private const string kConfigMutation =
            "mutation i($key: String!) { update_config(where: {config_key: {_eq: $key}}, _set: {config_value: \"x\"}) { affected_rows } }";

        /// <summary>
        /// API calls reading global settings.
        /// </summary>
        private static IEnumerable<TestCaseData> GlobalConfigReaderFiles()
        {
            foreach (string filePath in GetConfigReaderFiles().Where(filePath => !IsUserScoped(filePath)))
            {
                yield return new TestCaseData(filePath).SetName($"GlobalConfigReader_{Path.GetFileNameWithoutExtension(filePath)}_ReadsGlobalRows");
            }
        }

        /// <summary>
        /// Every API call reading global settings restricts each config key it selects to config_user 0.
        /// </summary>
        [TestCaseSource(nameof(GlobalConfigReaderFiles))]
        public void GlobalConfigReader_FiltersEveryKeyOnGlobalRows(string filePath)
        {
            string apiCall = File.ReadAllText(filePath);

            Assert.That(IsRestrictedToGlobalRows(apiCall), Is.True,
                $"{Path.GetFileName(filePath)} reads config keys without restricting them to config_user 0.");
        }

        /// <summary>
        /// The API calls reading the settings of one user are exactly the known ones, and each filters on the user
        /// given as variable.
        /// </summary>
        [Test]
        public void UserScopedConfigReaders_FilterOnTheGivenUser()
        {
            List<string> userScopedFiles = [.. GetConfigReaderFiles().Where(IsUserScoped)];

            Assert.Multiple(() =>
            {
                Assert.That(userScopedFiles.Select(Path.GetFileName), Is.EquivalentTo(kUserScopedFiles));
                foreach (string filePath in userScopedFiles)
                {
                    Assert.That(kUserVariableCondition.IsMatch(File.ReadAllText(filePath)), Is.True, Path.GetFileName(filePath));
                }
            });
        }

        /// <summary>
        /// The file selection finds known readers of global settings, so the checks do not pass on an empty selection.
        /// </summary>
        [Test]
        public void GlobalConfigReaderFiles_IncludeSchedulerAndImporterCalls()
        {
            List<string?> fileNames = [.. GetConfigReaderFiles().Select(Path.GetFileName)];

            Assert.Multiple(() =>
            {
                Assert.That(fileNames, Does.Contain(kSchedulerSubscriptionFile));
                Assert.That(fileNames, Does.Contain(kImporterQueryFile));
            });
        }

        /// <summary>
        /// The filter check accepts only calls restricting every config key to config_user 0.
        /// </summary>
        [TestCase(kFilteredPerKey, true)]
        [TestCase(kFilteredForAllKeys, true)]
        [TestCase(kOneKeyUnfiltered, false)]
        [TestCase(kUnfiltered, false)]
        [TestCase(kOtherUser, false)]
        [TestCase(kNoCondition, false)]
        [TestCase(kAliasedNoCondition, false)]
        public void FilterCheck_AcceptsOnlyCallsFilteringEveryKey(string apiCall, bool expectedRestricted)
        {
            Assert.That(IsRestrictedToGlobalRows(apiCall), Is.EqualTo(expectedRestricted));
        }

        /// <summary>
        /// The file selection takes every call reading the config table, with or without arguments and also aliased,
        /// but no call on another table and no mutation of config.
        /// </summary>
        [TestCase(kFilteredPerKey, true)]
        [TestCase(kUnfiltered, true)]
        [TestCase(kNoCondition, true)]
        [TestCase(kAliasedNoCondition, true)]
        [TestCase(kOtherTable, false)]
        [TestCase(kConfigMutation, false)]
        public void ReaderSelection_TakesEveryCallReadingConfig(string apiCall, bool expectedSelected)
        {
            Assert.That(IsConfigReader(apiCall), Is.EqualTo(expectedSelected));
        }

        /// <summary>
        /// True when the call has a config_user 0 condition and every config key condition is in an object
        /// restricting config_user to 0 or nested in such an object.
        /// </summary>
        private static bool IsRestrictedToGlobalRows(string apiCall)
        {
            HashSet<int> filteredObjects = [];
            foreach (Match globalRowCondition in kGlobalRowCondition.Matches(apiCall))
            {
                List<int> enclosingObjects = GetEnclosingObjects(apiCall, globalRowCondition.Index);
                if (enclosingObjects.Count > 0)
                {
                    filteredObjects.Add(enclosingObjects[^1]);
                }
            }

            return filteredObjects.Count > 0 && kKeyCondition.Matches(apiCall)
                .All(keyCondition => GetEnclosingObjects(apiCall, keyCondition.Index).Any(filteredObjects.Contains));
        }

        /// <summary>
        /// Returns the start positions of the objects open at the given position, outermost first.
        /// </summary>
        private static List<int> GetEnclosingObjects(string text, int position)
        {
            List<int> openObjects = [];
            for (int index = 0; index < position; ++index)
            {
                if (text[index] == kObjectStart)
                {
                    openObjects.Add(index);
                }
                else if (text[index] == kObjectEnd && openObjects.Count > 0)
                {
                    openObjects.RemoveAt(openObjects.Count - 1);
                }
            }
            return openObjects;
        }

        /// <summary>
        /// True for the calls reading the settings of the user given as variable.
        /// </summary>
        private static bool IsUserScoped(string filePath)
        {
            return kUserScopedFiles.Contains(Path.GetFileName(filePath));
        }

        /// <summary>
        /// True when the call has the field config, i.e. it is a query or subscription reading the config table.
        /// </summary>
        private static bool IsConfigReader(string apiCall)
        {
            return kConfigRootField.IsMatch(apiCall);
        }

        /// <summary>
        /// Returns all API calls with the root field config, i.e. the queries and subscriptions reading the config table.
        /// </summary>
        private static List<string> GetConfigReaderFiles()
        {
            return [.. Directory.EnumerateFiles(FindApiCallDirectory(), kGraphQlFilePattern, SearchOption.AllDirectories)
                .Where(filePath => IsConfigReader(File.ReadAllText(filePath)))
                .Order(StringComparer.Ordinal)];
        }

        /// <summary>
        /// Locates the directory of the API calls in the repository or in the installed directory tree, walking up
        /// from the directory of the test assembly.
        /// </summary>
        private static string FindApiCallDirectory()
        {
            DirectoryInfo? currentDirectory = new(AppContext.BaseDirectory);
            while (currentDirectory is not null)
            {
                string repositoryPath = Path.Combine(currentDirectory.FullName, "roles", "common", "files", kApiCallDirectoryName);
                if (Directory.Exists(repositoryPath))
                {
                    return repositoryPath;
                }

                string installedPath = Path.Combine(currentDirectory.FullName, kApiCallDirectoryName);
                if (Directory.Exists(installedPath))
                {
                    return installedPath;
                }
                currentDirectory = currentDirectory.Parent;
            }

            Assert.Fail("Could not locate the repository or installed fwo-api-calls directory from the test output directory.");
            return string.Empty;
        }
    }
}

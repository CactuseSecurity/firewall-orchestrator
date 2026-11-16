using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Settings of a user: saving them (WriteToDatabase on a user config) writes only personal settings as rows of the
    /// user, and a user config applies only the rows of personal settings.
    /// </summary>
    internal partial class ConfigTest
    {
        private const int kPersonalSettingsUserId = 50;
        private const int kEditedElementsPerFetch = 250;
        private const int kEditedUnusedTolerance = 123;
        private const int kGlobalUnusedToleranceAtLoad = 400;
        private const int kGlobalUnusedToleranceChanged = 500;
        private const int kGlobalUnusedToleranceChangedAgain = 600;
        private const int kImportSleepTimeOfUserRow = 77;
        private const int kGlobalImportSleepTime = 88;
        private const string kWelcomeMessageAtLoad = "welcome at load";
        private const string kWelcomeMessageChanged = "welcome changed";
        private const string kWelcomeMessageOfUserRow = "welcome of a user row";
        private const string kElementsPerFetchKey = "elementsPerFetch";
        private const string kUnusedToleranceKey = "unusedTolerance";
        private const string kWelcomeMessageKey = "welcomeMessage";
        private const string kImportSleepTimeKey = "importSleepTime";

        /// <summary>
        /// Rows of the user at load: none, so every personal setting starts with its global value.
        /// </summary>
        private static readonly ConfigItem[] kNoPersonalRows = [];

        /// <summary>
        /// Rows of the user for a personal setting and for two global settings, as written outside the permission checks
        /// of the UI roles (e.g. directly in the database). The global config has a value for welcomeMessage only.
        /// </summary>
        private static readonly ConfigItem[] kRowsOfUserWithGlobalSettings =
        [
            new() { Key = kElementsPerFetchKey, Value = kEditedElementsPerFetch.ToString(), User = kPersonalSettingsUserId },
            new() { Key = kWelcomeMessageKey, Value = kWelcomeMessageOfUserRow, User = kPersonalSettingsUserId },
            new() { Key = kImportSleepTimeKey, Value = kImportSleepTimeOfUserRow.ToString(), User = kPersonalSettingsUserId }
        ];
        private static readonly ConfigItem[] kGlobalRowsOfPersonalAndGlobalSettings =
        [
            new() { Key = kElementsPerFetchKey, Value = kEditedElementsPerFetch.ToString(), User = 0 },
            new() { Key = kWelcomeMessageKey, Value = kWelcomeMessageChanged, User = 0 },
            new() { Key = kImportSleepTimeKey, Value = kGlobalImportSleepTime.ToString(), User = 0 }
        ];
        private static readonly string[] kPersonalRowKeys = [kElementsPerFetchKey];
        private static readonly ConfigItem[] kGlobalItemsAtLoad =
        [
            new() { Key = kWelcomeMessageKey, Value = kWelcomeMessageAtLoad, User = 0 },
            new() { Key = kUnusedToleranceKey, Value = kGlobalUnusedToleranceAtLoad.ToString(), User = 0 }
        ];
        private static readonly ConfigItem[] kGlobalWelcomeMessageChanged =
        [
            new() { Key = kWelcomeMessageKey, Value = kWelcomeMessageChanged, User = 0 },
            new() { Key = kUnusedToleranceKey, Value = kGlobalUnusedToleranceAtLoad.ToString(), User = 0 }
        ];
        private static readonly ConfigItem[] kGlobalUnusedToleranceChangedItems =
        [
            new() { Key = kWelcomeMessageKey, Value = kWelcomeMessageAtLoad, User = 0 },
            new() { Key = kUnusedToleranceKey, Value = kGlobalUnusedToleranceChanged.ToString(), User = 0 }
        ];
        private static readonly ConfigItem[] kGlobalUnusedToleranceChangedAgainItems =
        [
            new() { Key = kWelcomeMessageKey, Value = kWelcomeMessageAtLoad, User = 0 },
            new() { Key = kUnusedToleranceKey, Value = kGlobalUnusedToleranceChangedAgain.ToString(), User = 0 }
        ];
        private static readonly string[] kChangedPersonalSettingKeys =
        [
            kElementsPerFetchKey,
            kUnusedToleranceKey,
            "resolveNetworkAreas",
            "reportingPersonalPreferredCollapseState"
        ];

        /// <summary>
        /// A user config writes the changed personal settings as rows of the user and leaves out the changed global settings.
        /// </summary>
        [Test]
        public async Task WriteToDatabase_UserConfigWritesOnlyPersonalSettings()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);
            ConfigData editable = await userConfig.GetEditableConfig();
            editable.ElementsPerFetch = kEditedElementsPerFetch;
            editable.UnusedTolerance = kEditedUnusedTolerance;
            editable.ResolveNetworkAreas = !editable.ResolveNetworkAreas;
            editable.ReportingPersonalPreferredCollapseState = PreferredCollapseState.Expanded;
            editable.WelcomeMessage = kWelcomeMessageChanged;
            editable.ImportSleepTime += 1;

            await userConfig.WriteToDatabase(editable, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems.Select(item => item.Key), Is.EquivalentTo(kChangedPersonalSettingKeys));
                Assert.That(apiConnection.LastConfigItems.Select(item => item.User), Is.All.EqualTo(kPersonalSettingsUserId));
                Assert.That(userConfig.UnusedTolerance, Is.EqualTo(kEditedUnusedTolerance));
                Assert.That(userConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageAtLoad));
            });
        }

        /// <summary>
        /// A user config sends nothing if only global settings were changed in the editable copy.
        /// </summary>
        [Test]
        public async Task WriteToDatabase_UserConfigWritesNoGlobalSetting()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);
            ConfigData editable = await userConfig.GetEditableConfig();
            editable.WelcomeMessage = kWelcomeMessageChanged;

            await userConfig.WriteToDatabase(editable, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.Zero);
                Assert.That(userConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageAtLoad));
            });
        }

        /// <summary>
        /// A global setting that changes while the personal settings are edited is not written as a row of the user;
        /// the user config keeps the new global value.
        /// </summary>
        [Test]
        public async Task WriteToDatabase_UserConfigSkipsGlobalSettingChangedMeanwhile()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);
            ConfigData editable = await userConfig.GetEditableConfig();

            userConfig.GlobalConfig!.SubscriptionUpdateHandler(kGlobalWelcomeMessageChanged);
            editable.ElementsPerFetch = kEditedElementsPerFetch;
            await userConfig.WriteToDatabase(editable, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo(kElementsPerFetchKey));
                Assert.That(apiConnection.LastConfigItems[0].User, Is.EqualTo(kPersonalSettingsUserId));
                Assert.That(userConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageChanged));
            });
        }

        /// <summary>
        /// Compared with the values from loading, a personal setting the user did not edit is not written although its
        /// global value changed meanwhile, so it keeps following the global value.
        /// </summary>
        [Test]
        public async Task WriteToDatabase_WithLoadedData_SkipsPersonalSettingChangedGloballyMeanwhile()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);
            ConfigData loaded = await userConfig.GetEditableConfig();
            ConfigData editable = (ConfigData)loaded.Clone();

            userConfig.GlobalConfig!.SubscriptionUpdateHandler(kGlobalUnusedToleranceChangedItems);
            editable.ElementsPerFetch = kEditedElementsPerFetch;
            await userConfig.WriteToDatabase(editable, apiConnection, loaded);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo(kElementsPerFetchKey));
                Assert.That(userConfig.UnusedTolerance, Is.EqualTo(kGlobalUnusedToleranceChanged));
            });
        }

        /// <summary>
        /// Compared with the values from loading, a personal setting the user edited is written even if it equals the
        /// global value changed meanwhile, and it then keeps the user's value when the global value changes again.
        /// </summary>
        [Test]
        public async Task WriteToDatabase_WithLoadedData_WritesEditedPersonalSettingEqualToNewGlobalValue()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);
            ConfigData loaded = await userConfig.GetEditableConfig();
            ConfigData editable = (ConfigData)loaded.Clone();

            userConfig.GlobalConfig!.SubscriptionUpdateHandler(kGlobalUnusedToleranceChangedItems);
            editable.UnusedTolerance = kGlobalUnusedToleranceChanged;
            await userConfig.WriteToDatabase(editable, apiConnection, loaded);
            userConfig.GlobalConfig!.SubscriptionUpdateHandler(kGlobalUnusedToleranceChangedAgainItems);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo(kUnusedToleranceKey));
                Assert.That(apiConnection.LastConfigItems[0].User, Is.EqualTo(kPersonalSettingsUserId));
                Assert.That(userConfig.UnusedTolerance, Is.EqualTo(kGlobalUnusedToleranceChanged));
            });
        }

        /// <summary>
        /// A user config loaded once (scheduled reports, report and compliance requests in the middleware) applies the
        /// row of the user for a personal setting and ignores the rows for global settings: a global setting keeps the
        /// global value, or its default if the global config has none.
        /// </summary>
        [Test]
        public void Constructor_UserConfigAppliesOnlyRowsOfPersonalSettings()
        {
            using UserConfigApiConnection apiConnection = new(kRowsOfUserWithGlobalSettings);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(kEditedElementsPerFetch));
                Assert.That(userConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageAtLoad));
                Assert.That(userConfig.ImportSleepTime, Is.EqualTo(new ConfigData().ImportSleepTime));
                Assert.That(userConfig.RawConfigItems.Select(item => item.Key), Is.EqualTo(kPersonalRowKeys));
            });
        }

        /// <summary>
        /// The rows of the user received from the subscription (UI session, applied after the global values) change only
        /// personal settings; global settings keep their global values.
        /// </summary>
        [Test]
        public void SubscriptionUpdate_UserConfigKeepsGlobalValuesOfGlobalSettings()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);

            userConfig.SubscriptionUpdateHandler(kRowsOfUserWithGlobalSettings);

            Assert.Multiple(() =>
            {
                Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(kEditedElementsPerFetch));
                Assert.That(userConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageAtLoad));
                Assert.That(userConfig.ImportSleepTime, Is.EqualTo(new ConfigData().ImportSleepTime));
                Assert.That(userConfig.RawConfigItems.Select(item => item.Key), Is.EqualTo(kPersonalRowKeys));
            });
        }

        /// <summary>
        /// Merging rows of the user received from a subscription also changes only personal settings.
        /// </summary>
        [Test]
        public void MergeSubscriptionUpdate_UserConfigKeepsGlobalValuesOfGlobalSettings()
        {
            using UserConfigApiConnection apiConnection = new(kNoPersonalRows);
            using UserConfig userConfig = CreatePersonalSettingsUserConfig(apiConnection);

            userConfig.MergeSubscriptionUpdateHandler(kRowsOfUserWithGlobalSettings);

            Assert.Multiple(() =>
            {
                Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(kEditedElementsPerFetch));
                Assert.That(userConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageAtLoad));
                Assert.That(userConfig.ImportSleepTime, Is.EqualTo(new ConfigData().ImportSleepTime));
                Assert.That(userConfig.RawConfigItems.Select(item => item.Key), Is.EqualTo(kPersonalRowKeys));
            });
        }

        /// <summary>
        /// The global config (user 0) applies all of its rows, personal and global settings alike.
        /// </summary>
        [Test]
        public void SubscriptionUpdate_GlobalConfigAppliesAllRows()
        {
            SimulatedGlobalConfig globalConfig = new();

            globalConfig.SubscriptionUpdateHandler(kGlobalRowsOfPersonalAndGlobalSettings);

            Assert.Multiple(() =>
            {
                Assert.That(globalConfig.ElementsPerFetch, Is.EqualTo(kEditedElementsPerFetch));
                Assert.That(globalConfig.WelcomeMessage, Is.EqualTo(kWelcomeMessageChanged));
                Assert.That(globalConfig.ImportSleepTime, Is.EqualTo(kGlobalImportSleepTime));
                Assert.That(globalConfig.RawConfigItems, Is.EqualTo(kGlobalRowsOfPersonalAndGlobalSettings));
            });
        }

        /// <summary>
        /// Creates the config of a user with the rows the API connection returns, on top of a global config with the values
        /// at load.
        /// </summary>
        private static UserConfig CreatePersonalSettingsUserConfig(UserConfigApiConnection apiConnection)
        {
            SimulatedGlobalConfig globalConfig = new() { RawConfigItems = kGlobalItemsAtLoad };
            return new UserConfig(globalConfig, apiConnection, new UiUser { DbId = kPersonalSettingsUserId, Language = "English" });
        }
    }
}

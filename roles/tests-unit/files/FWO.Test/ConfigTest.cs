using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ConfigTest
    {
        private sealed class UserConfigApiConnection(ConfigItem[] configItems) : ApiConnection
        {
            public int UpsertConfigCallCount { get; private set; }
            public List<ConfigItem> LastConfigItems { get; private set; } = [];

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)configItems);
                }
                if (typeof(QueryResponseType) == typeof(List<UiText>) && query == ConfigQueries.getCustomTextsPerLanguage)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }
                if (query == ConfigQueries.upsertConfigItems)
                {
                    UpsertConfigCallCount++;
                    PropertyInfo configItemsProperty = variables?.GetType().GetProperty("config_items")
                        ?? throw new ArgumentException("Missing config_items variable.");
                    LastConfigItems = ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>() { }
            protected override void Dispose(bool disposing) { }
        }

        [Test]
        public void Update_KeepsDefaultEnum_WhenConfigContainsUnknownAutoCreateImplTaskOption()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.never;

            InvokeUpdate(userConfig,
            [
                new() { Key = "reqAutoCreateImplTasks", Value = "999", User = 0 }
            ]);

            Assert.That(userConfig.ReqAutoCreateImplTasks, Is.EqualTo(AutoCreateImplTaskOptions.never));
        }

        [Test]
        public void Constructor_AppliesGlobalConfigToScheduledUserConfig()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "reqOwnerBased", Value = "true", User = 0 }
            ];

            using UserConfigApiConnection apiConnection = new([]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ReqOwnerBased, Is.True);
        }

        [Test]
        public void Constructor_DoesNotOverwriteUserSpecificConfigWithGlobalValues()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "reqOwnerBased", Value = "true", User = 0 },
                new() { Key = "elementsPerFetch", Value = "777", User = 0 }
            ];

            using UserConfigApiConnection apiConnection =
                new([new() { Key = "elementsPerFetch", Value = "55", User = 50 }]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ReqOwnerBased, Is.True);
            Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(55));
        }

        [Test]
        public async Task WriteToDatabase_UpdatesCurrentConfigAfterPersistingChanges()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                WelcomeMessage = "old",
                ImportSleepTime = 40,
                RawConfigItems =
                [
                    new() { Key = "welcomeMessage", Value = "old", User = 0 },
                    new() { Key = "importSleepTime", Value = "40", User = 0 }
                ]
            };
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.WelcomeMessage = "new";

            using UserConfigApiConnection apiConnection = new([]);
            await globalConfig.WriteToDatabase(editableConfig, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("welcomeMessage"));
                Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo("new"));
                Assert.That(globalConfig.WelcomeMessage, Is.EqualTo("new"));
                Assert.That(globalConfig.RawConfigItems.First(item => item.Key == "welcomeMessage").Value, Is.EqualTo("new"));
                Assert.That(globalConfig.RawConfigItems.First(item => item.Key == "importSleepTime").Value, Is.EqualTo("40"));
            });
        }

        [Test]
        public async Task WriteToDatabase_NotifiesUserConfigSubscribersAfterPersistingChanges()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                OwnerSoruceMappingID = 0,
                RawConfigItems =
                [
                    new() { Key = "OwnerSoruceMappingID", Value = "0", User = 0 }
                ]
            };
            UserConfig userConfig = new(globalConfig);
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.OwnerSoruceMappingID = 2;

            using UserConfigApiConnection apiConnection = new([]);
            await globalConfig.WriteToDatabase(editableConfig, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(globalConfig.OwnerSoruceMappingID, Is.EqualTo(2));
                Assert.That(userConfig.OwnerSoruceMappingID, Is.EqualTo(2));
            });
        }

        [Test]
        public void DailyCheckSubscription_IncludesNotificationSettingsUsedByRunningJobs()
        {
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("notificationLanguage"));
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("ownerActiveRuleEmailBody"));
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("ruleExpiryEmailBody"));
        }

        [Test]
        public void ConfigData_DefaultsModIntegrationModeToFullyIntegrated()
        {
            ConfigData configData = new();

            Assert.That(configData.ModIntegrationMode, Is.EqualTo(ModIntegrationMode.FullyIntegrated));
        }

        [Test]
        public void ConfigData_DefaultsModIntegrationStatesToEmptyList()
        {
            ConfigData configData = new();

            Assert.That(configData.ModIntegrationStates, Is.EqualTo("[]"));
            Assert.That(ModIntegrationStateConfig.Parse(configData.ModIntegrationStates), Is.Empty);
        }

        [Test]
        public void ConfigData_DefaultsModIntegrationStateMarker()
        {
            ConfigData configData = new();

            Assert.That(configData.ModIntegrationStateMarker, Is.EqualTo(ModIntegrationStateConfig.DefaultMarker));
        }

        [Test]
        public void ConfigData_DefaultsFlowNamingSourceRankingToAnEmptyList()
        {
            ConfigData configData = new();

            Assert.That(configData.FlowNamingSourceManagementRanking, Is.EqualTo("[]"));
        }

        [Test]
        public void ModIntegrationStateConfig_TrimsAndSerializesNamedStates()
        {
            string configValue = ModIntegrationStateConfig.ToConfigValue(
            [
                new() { Name = " Requested ", IncludeIntoRequest = true },
                new() { Name = "", IncludeIntoRequest = true }
            ]);

            List<ModIntegrationState> states = ModIntegrationStateConfig.Parse(configValue);

            Assert.That(states, Has.Count.EqualTo(1));
            Assert.That(states[0].Name, Is.EqualTo("Requested"));
            Assert.That(states[0].IncludeIntoRequest, Is.True);
            Assert.That(states[0].MonitorStatus, Is.EqualTo(ModIntegrationStateStatus.None));
        }

        [Test]
        public void ModIntegrationStateConfig_SerializesConfiguredMonitorStatus()
        {
            string configValue = ModIntegrationStateConfig.ToConfigValue(
            [
                new() { Name = " Done ", MonitorStatus = ModIntegrationStateStatus.Implemented },
                new() { Name = "Broken", MonitorStatus = "unknown" }
            ]);

            List<ModIntegrationState> states = ModIntegrationStateConfig.Parse(configValue);
            Dictionary<string, string> monitorStatusByName = ModIntegrationStateConfig.MonitorStatusByStateName(configValue);

            Assert.Multiple(() =>
            {
                Assert.That(states[0].Name, Is.EqualTo("Done"));
                Assert.That(states[0].MonitorStatus, Is.EqualTo(ModIntegrationStateStatus.Implemented));
                Assert.That(states[1].MonitorStatus, Is.EqualTo(ModIntegrationStateStatus.None));
                Assert.That(monitorStatusByName["Done"], Is.EqualTo(ModIntegrationStateStatus.Implemented));
                Assert.That(ModIntegrationStateConfig.MonitorStatusTextKey(ModIntegrationStateStatus.Implemented), Is.EqualTo("monitor_status_implemented"));
            });
        }

        [Test]
        public void ModIntegrationStateConfig_ReadsMarkerFromSameLineComment()
        {
            string comment = "manual note ImplementationState: Retry | 2026-05-08T10:00:00.0000000Z still manual";

            Assert.That(ModIntegrationStateConfig.GetMarkedCommentValue(comment, "ImplementationState"), Is.EqualTo("Retry"));
            Assert.That(ModIntegrationStateConfig.GetMarkedCommentTimestamp(comment, "ImplementationState"), Is.EqualTo(DateTime.Parse("2026-05-08T10:00:00.0000000Z").ToUniversalTime()));
        }

        [Test]
        public void ModIntegrationStateConfig_ReplacesOnlyMarkerSegmentInSameLineComment()
        {
            string comment = "manual before ImplementationState: Old | 2026-05-08T10:00:00.0000000Z manual after";

            string updatedComment = ModIntegrationStateConfig.ReplaceMarkedComment(comment, "ImplementationState",
                "ImplementationState: Implemented | 2026-05-08T11:00:00.0000000Z");

            Assert.That(updatedComment, Is.EqualTo("manual before ImplementationState: Implemented | 2026-05-08T11:00:00.0000000Z manual after"));
        }

        [Test]
        public void Update_ParsesModIntegrationMode()
        {
            SimulatedUserConfig userConfig = new();

            InvokeUpdate(userConfig,
            [
                new() { Key = "modIntegrationMode", Value = nameof(ModIntegrationMode.WorkflowNotifications), User = 0 },
                new() { Key = "modIntegrationStateMarker", Value = "ticketState", User = 0 }
            ]);

            Assert.That(userConfig.ModIntegrationMode, Is.EqualTo(ModIntegrationMode.WorkflowNotifications));
            Assert.That(userConfig.ModIntegrationStateMarker, Is.EqualTo("ticketState"));
        }

        private static void InvokeUpdate(FWO.Config.Api.Config config, ConfigItem[] configItems)
        {
            MethodInfo updateMethod = typeof(FWO.Config.Api.Config).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(FWO.Config.Api.Config).FullName, "Update");

            updateMethod.Invoke(config, [configItems]);
        }
    }
}

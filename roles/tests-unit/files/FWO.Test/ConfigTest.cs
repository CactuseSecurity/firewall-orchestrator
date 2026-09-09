using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Data.Flow;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal partial class ConfigTest
    {
        private static readonly ConfigItem[] ModIconifyFalseConfigItem =
        [
            new() { Key = "modIconify", Value = "false", User = 50 }
        ];

        private sealed class UserConfigApiConnection(ConfigItem[] configItems) : ApiConnection
        {
            public int UpsertConfigCallCount { get; private set; }
            public List<ConfigItem> LastConfigItems { get; private set; } = [];
            public object? LastGetConfigItemsByUserVariables { get; private set; }
            public bool IsDisposed { get; private set; }

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
                {
                    LastGetConfigItemsByUserVariables = variables;
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
            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
            }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                throw new NotImplementedException();
            }
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
                new() { Key = "reqOwnerBased", Value = "true", User = 0 },
                new() { Key = "reqVisibilityBased", Value = "true", User = 0 }
            ];

            using UserConfigApiConnection apiConnection = new([]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ReqOwnerBased, Is.True);
            Assert.That(userConfig.ReqVisibilityBased, Is.True);
            Assert.That(apiConnection.LastGetConfigItemsByUserVariables?.GetType().GetProperty("user")?.GetValue(apiConnection.LastGetConfigItemsByUserVariables), Is.EqualTo(50));
            Assert.That(apiConnection.LastGetConfigItemsByUserVariables?.GetType().GetProperty("User"), Is.Null);
        }

        [Test]
        public void GlobalConfigChange_DoesNotOverwritePersonalModIconifyOverride()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ModIconify = true
            };

            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            userConfig.RawConfigItems =
            [
                ModIconifyFalseConfigItem[0]
            ];

            InvokeUpdate(userConfig, ModIconifyFalseConfigItem);

            InvokePrivateMethod(userConfig, "OnGlobalConfigChange", globalConfig, ModIconifyFalseConfigItem);

            Assert.That(userConfig.ModIconify, Is.False);
        }

        [Test]
        public void TextOnlyFactory_DoesNotExposePublicGlobalConfigConstructor()
        {
            ConstructorInfo? constructor = typeof(UserConfig).GetConstructor(
                [typeof(GlobalConfig), typeof(bool)]);

            Assert.That(constructor, Is.Null);
        }

        [Test]
        public void TextOnlyFactory_DoesNotApplyDirectConfigValues()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "reqOwnerBased", Value = "true", User = 0 },
                new() { Key = "reqVisibilityBased", Value = "true", User = 0 }
            ];

            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);

            Assert.That(userConfig.ReqOwnerBased, Is.False);
            Assert.That(userConfig.ReqVisibilityBased, Is.False);
        }

        [Test]
        public void GlobalSettingsFactory_LoadsDirectConfigValues()
        {
            SimulatedGlobalConfig globalConfig = new();
            using UserConfigApiConnection apiConnection =
                new([new() { Key = "reqOwnerBased", Value = "true", User = 0 }, new() { Key = "reqVisibilityBased", Value = "true", User = 0 }]);

            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);

            Assert.That(userConfig.ReqOwnerBased, Is.True);
            Assert.That(userConfig.ReqVisibilityBased, Is.True);
        }

        [Test]
        public void Dispose_DoesNotDisposeApiConnection_WhenNotOwned()
        {
            SimulatedGlobalConfig globalConfig = new();
            using UserConfigApiConnection apiConnection = new([]);
            using UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);

            userConfig.Dispose();

            Assert.That(apiConnection.IsDisposed, Is.False);
        }

        [Test]
        public void Dispose_UnsubscribesFromGlobalConfigChange()
        {
            SimulatedGlobalConfig globalConfig = new();
            using UserConfigApiConnection apiConnection = new([]);
            int initialSubscriberCount = GetOnChangeSubscriberCount(globalConfig);
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);

            Assert.That(GetOnChangeSubscriberCount(globalConfig), Is.EqualTo(initialSubscriberCount + 1));

            userConfig.Dispose();

            Assert.That(GetOnChangeSubscriberCount(globalConfig), Is.EqualTo(initialSubscriberCount));
        }

        [Test]
        public void Dispose_DisposesApiConnection_WhenOwned()
        {
            SimulatedGlobalConfig globalConfig = new();
            UserConfigApiConnection apiConnection = new([]);
            using UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, owningApiConnection: true);

            userConfig.Dispose();

            Assert.That(apiConnection.IsDisposed, Is.True);
        }

        [Test]
        public void ImportChangeNotifier_Dispose_UnsubscribesUserConfigWithoutDisposingApiConnection()
        {
            SimulatedGlobalConfig globalConfig = new();
            using UserConfigApiConnection apiConnection = new([]);
            int initialSubscriberCount = GetOnChangeSubscriberCount(globalConfig);
            ImportChangeNotifier notifier = new(apiConnection, globalConfig);

            Assert.That(GetOnChangeSubscriberCount(globalConfig), Is.EqualTo(initialSubscriberCount + 1));

            notifier.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(GetOnChangeSubscriberCount(globalConfig), Is.EqualTo(initialSubscriberCount));
                Assert.That(apiConnection.IsDisposed, Is.False);
            });
        }

        [Test]
        public void AppDataImport_Dispose_UnsubscribesUserConfigWithoutDisposingApiConnection()
        {
            SimulatedGlobalConfig globalConfig = new();
            using UserConfigApiConnection apiConnection = new([]);
            int initialSubscriberCount = GetOnChangeSubscriberCount(globalConfig);
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);
            AppDataImport appDataImport = new(apiConnection, globalConfig);
            SetPrivateField(appDataImport, "userConfig", userConfig);

            Assert.That(GetOnChangeSubscriberCount(globalConfig), Is.EqualTo(initialSubscriberCount + 1));

            appDataImport.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(GetOnChangeSubscriberCount(globalConfig), Is.EqualTo(initialSubscriberCount));
                Assert.That(apiConnection.IsDisposed, Is.False);
            });
        }

        [Test]
        public void Constructor_DoesNotOverwriteUserSpecificConfigWithGlobalValues()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "reqOwnerBased", Value = "true", User = 0 },
                new() { Key = "reqVisibilityBased", Value = "true", User = 0 },
                new() { Key = "elementsPerFetch", Value = "777", User = 0 }
            ];

            using UserConfigApiConnection apiConnection =
                new([new() { Key = "elementsPerFetch", Value = "55", User = 50 }]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ReqOwnerBased, Is.True);
            Assert.That(userConfig.ReqVisibilityBased, Is.True);
            Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(55));
        }

        [Test]
        public void Constructor_UsesGlobalUserConfigValueWhenUserHasNoSpecificConfig()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "elementsPerFetch", Value = "777", User = 0 }
            ];

            using UserConfigApiConnection apiConnection = new([]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(777));
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
        public async Task WriteToDatabase_PersistsDesignatedZoneMatrixSelection()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDesignatedZoneMatrixId = 0,
                RawConfigItems =
                [
                    new() { Key = "complianceDesignatedZoneMatrix", Value = "0", User = 0 }
                ]
            };
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.ComplianceDesignatedZoneMatrixId = 17;

            using UserConfigApiConnection apiConnection = new([]);
            await globalConfig.WriteToDatabase(editableConfig, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("complianceDesignatedZoneMatrix"));
                Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo("17"));
                Assert.That(globalConfig.ComplianceDesignatedZoneMatrixId, Is.EqualTo(17));
            });
        }

        [Test]
        public async Task WriteToDatabase_PersistsComplianceDiffExistingViolationFilter()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = false,
                RawConfigItems =
                [
                    new() { Key = "complianceDiffFilterExistingViolations", Value = "false", User = 0 }
                ]
            };
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.ComplianceDiffFilterExistingViolations = true;

            using UserConfigApiConnection apiConnection = new([]);
            await globalConfig.WriteToDatabase(editableConfig, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("complianceDiffFilterExistingViolations"));
                Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo("True"));
                Assert.That(globalConfig.ComplianceDiffFilterExistingViolations, Is.True);
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
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
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
        public void FlowSyncSubscription_ContainsFlowSyncConfigSettings()
        {
            Assert.That(ConfigQueries.subscribeFlowSyncConfigChanges, Does.Contain("flowSyncSleepTime"));
            Assert.That(ConfigQueries.subscribeFlowSyncConfigChanges, Does.Contain("flowNamingSourceManagementRanking"));
        }

        [Test]
        public void FlowCatalogSubscription_ContainsZoneGroupNamePatterns()
        {
            Assert.That(ConfigQueries.subscribeFlowCatalogConfigChanges, Does.Contain("flowZoneGroupNamePatterns"));
        }

        [Test]
        public void LogDataImportSubscription_ContainsIntervalUnit()
        {
            Assert.That(ConfigQueries.subscribeImportLogDataConfigChanges, Does.Contain("importLogDataSleepTimeUnit"));
        }

        [Test]
        public void LogDataImportSubscription_ContainsSettingsUsedByRunningImports()
        {
            Assert.That(ConfigQueries.subscribeImportLogDataConfigChanges, Does.Contain("importLogDataMaxEntries"));
            Assert.That(ConfigQueries.subscribeImportLogDataConfigChanges, Does.Contain("allowLogDataPortWithoutProtocol"));
            Assert.That(ConfigQueries.subscribeImportLogDataConfigChanges, Does.Contain("replaceExistingLogData"));
            Assert.That(ConfigQueries.subscribeImportLogDataConfigChanges, Does.Contain("logDataRetentionDays"));
        }

        [Test]
        public void ImportSubscriptions_ContainTheScriptTimeoutOfEveryScriptedImport()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ConfigQueries.subscribeImportLogDataConfigChanges, Does.Contain("importScriptTimeout"));
                Assert.That(ConfigQueries.subscribeImportAppDataConfigChanges, Does.Contain("importScriptTimeout"));
                Assert.That(ConfigQueries.subscribeImportIpDataConfigChanges, Does.Contain("importScriptTimeout"));
            });
        }

        [Test]
        public void ConfigData_DefaultsTheImportScriptTimeoutToAnHour()
        {
            ConfigData configData = new();

            Assert.That(configData.ImportScriptTimeout, Is.EqualTo(60));
        }

        [Test]
        public void ConfigData_DefaultsLogDataImportIntervalUnitToHours()
        {
            ConfigData configData = new();

            Assert.That(configData.ImportLogDataSleepTimeUnit, Is.EqualTo(LogDataImportIntervalUnit.Hours));
        }

        [Test]
        public void ConfigData_EnablesLogDataReplacementByDefault()
        {
            ConfigData configData = new();

            Assert.That(configData.ReplaceExistingLogData, Is.True);
        }

        [Test]
        public void ComplianceCheckSubscription_ContainsDesignatedZoneMatrix()
        {
            Assert.That(ConfigQueries.subscribeComplianceCheckConfigChanges, Does.Contain("complianceDesignatedZoneMatrix"));
        }

        [Test]
        public void ComplianceCheckSubscription_LimitCoversAllTrackedConfigKeys()
        {
            string subscription = ConfigQueries.subscribeComplianceCheckConfigChanges;
            MatchCollection configKeyFilters = ConfigKeyFiltersRegex().Matches(subscription);
            int trackedConfigKeyCount = configKeyFilters
                .SelectMany(match => QuotedValueRegex().Matches(match.Groups["body"].Value).Select(quotedValue => quotedValue.Groups[1].Value))
                .Distinct(StringComparer.Ordinal)
                .Count();
            int limitStart = subscription.IndexOf("limit:", StringComparison.Ordinal);

            Assert.That(limitStart, Is.GreaterThanOrEqualTo(0), "Subscription limit not found.");

            string limitText = subscription[(limitStart + "limit:".Length)..].TrimStart();
            int limitEnd = limitText.IndexOfAny(['\r', '\n', ')']);
            string limitValue = limitEnd >= 0 ? limitText[..limitEnd] : limitText;

            Assert.That(int.Parse(limitValue), Is.GreaterThanOrEqualTo(trackedConfigKeyCount));
        }

        [Test]
        public void ChangeIdCustomFieldKeysAreIncludedInRelevantSubscriptions()
        {
            Assert.That(ConfigQueries.subscribeComplianceCheckConfigChanges, Does.Contain("CustomFieldChangeIdKey"));
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("CustomFieldChangeIdKey"));
        }

        [GeneratedRegex(@"config_key\s*:\s*\{(?<body>.*?)\}", RegexOptions.Singleline)]
        private static partial Regex ConfigKeyFiltersRegex();

        [GeneratedRegex("\"([^\"]+)\"")]
        private static partial Regex QuotedValueRegex();

        [Test]
        public void ConfigData_DefaultsFlowSyncSleepTimeToDisabled()
        {
            ConfigData configData = new();

            Assert.That(configData.FlowSyncSleepTime, Is.Zero);
        }

        [Test]
        public void ConfigData_DefaultsReqConsiderBundlingToFalse()
        {
            ConfigData configData = new();

            Assert.That(configData.ReqConsiderBundling, Is.False);
        }

        [Test]
        public void ConfigData_DefaultsReqVisibilityBasedToFalse()
        {
            ConfigData configData = new();

            Assert.That(configData.ReqVisibilityBased, Is.False);
        }

        [Test]
        public void ConfigData_DefaultsComplianceDesignatedZoneMatrixIdToZero()
        {
            ConfigData configData = new();

            Assert.That(configData.ComplianceDesignatedZoneMatrixId, Is.Zero);
        }

        [Test]
        public void ConfigData_DefaultsComplianceDiffExistingViolationFilterToFalse()
        {
            ConfigData configData = new();

            Assert.That(configData.ComplianceDiffFilterExistingViolations, Is.False);
        }

        [Test]
        public void ConfigData_DefaultsChangeIdCustomFieldKeys()
        {
            ConfigData configData = new();

            Assert.That(configData.CustomFieldChangeIdKey, Is.EqualTo("[\"field-2\",\"ChangeID\"]"));
        }

        [Test]
        public void Update_ParsesReqConsiderBundling()
        {
            SimulatedUserConfig userConfig = new();

            InvokeUpdate(userConfig,
            [
                new() { Key = "reqConsiderBundling", Value = "True", User = 0 }
            ]);

            Assert.That(userConfig.ReqConsiderBundling, Is.True);
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
        public void ConfigData_DefaultsFlowZoneGroupNamePatternsToAnEmptyList()
        {
            ConfigData configData = new();

            Assert.That(configData.FlowZoneGroupNamePatterns, Is.EqualTo("[]"));
            Assert.That(FlowZoneGroupMatcher.ParsePatterns(configData.FlowZoneGroupNamePatterns), Is.Empty);
        }

        [Test]
        public void ConfigData_DefaultsReducedProtocolSetProtocolsToCurrentSelection()
        {
            ConfigData configData = new();

            Assert.That(configData.ReducedProtocolSetProtocols, Is.EqualTo("""["tcp","udp","icmp","esp"]"""));
        }

        [Test]
        public void ConfigData_ReducedProtocolSetProtocolsRoundTripAsJson()
        {
            ConfigData configData = new()
            {
                ReducedProtocolSetProtocols = """["tcp","udp","icmp","esp"]"""
            };

            List<string>? parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(configData.ReducedProtocolSetProtocols);

            Assert.That(parsed, Is.EqualTo(["tcp", "udp", "icmp", "esp"]));
        }

        [Test]
        public void RuleRecognitionOption_DefaultsMatchCurrentSelectionLogic()
        {
            RuleRecognitionOption option = new();

            Assert.Multiple(() =>
            {
                Assert.That(option.NwRegardIp, Is.True);
                Assert.That(option.NwRegardName, Is.False);
                Assert.That(option.NwRegardGroupName, Is.False);
                Assert.That(option.NwResolveGroup, Is.False);
                Assert.That(option.NwSeparateGroupAnalysis, Is.True);
                Assert.That(option.SvcRegardPortAndProt, Is.True);
                Assert.That(option.SvcRegardName, Is.False);
                Assert.That(option.SvcRegardGroupName, Is.False);
                Assert.That(option.SvcResolveGroup, Is.True);
                Assert.That(option.SvcSplitPortRanges, Is.False);
            });
        }

        [Test]
        public void RuleRecognitionOption_SerializesAndDeserializesWithoutLoss()
        {
            RuleRecognitionOption option = new()
            {
                NwRegardIp = false,
                NwRegardName = true,
                NwRegardGroupName = true,
                NwResolveGroup = true,
                NwSeparateGroupAnalysis = false,
                SvcRegardPortAndProt = false,
                SvcRegardName = true,
                SvcRegardGroupName = true,
                SvcResolveGroup = false,
                SvcSplitPortRanges = true
            };

            string serialized = System.Text.Json.JsonSerializer.Serialize(option);
            RuleRecognitionOption? parsed = System.Text.Json.JsonSerializer.Deserialize<RuleRecognitionOption>(serialized);

            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed!.NwRegardIp, Is.False);
                Assert.That(parsed.NwRegardName, Is.True);
                Assert.That(parsed.NwRegardGroupName, Is.True);
                Assert.That(parsed.NwResolveGroup, Is.True);
                Assert.That(parsed.NwSeparateGroupAnalysis, Is.False);
                Assert.That(parsed.SvcRegardPortAndProt, Is.False);
                Assert.That(parsed.SvcRegardName, Is.True);
                Assert.That(parsed.SvcRegardGroupName, Is.True);
                Assert.That(parsed.SvcResolveGroup, Is.False);
                Assert.That(parsed.SvcSplitPortRanges, Is.True);
            });
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

        private static int GetOnChangeSubscriberCount(FWO.Config.Api.Config config)
        {
            FieldInfo onChangeField = typeof(FWO.Config.Api.Config).GetField("OnChange", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(FWO.Config.Api.Config).FullName, "OnChange");

            return ((Delegate?)onChangeField.GetValue(config))?.GetInvocationList().Length ?? 0;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(target.GetType().FullName, fieldName);

            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object?[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(target.GetType().FullName, methodName);

            method.Invoke(target, args);
        }
    }
}

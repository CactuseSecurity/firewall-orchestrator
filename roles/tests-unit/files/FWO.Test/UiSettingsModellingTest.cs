using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsModellingTest
    {
        private static readonly object?[] RefreshEmptyArgs = [string.Empty];

        private static SettingsModelling CreateComponent()
        {
            return new SettingsModelling();
        }

        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsModelling).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsModelling).FullName, name);
        }

        private static void SetPrivateField(object component, string fieldName, object? value)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        /// <summary>
        /// Sets a non-public component property for isolated component tests.
        /// </summary>
        private static void SetPrivateProperty(object component, string propertyName, object value)
        {
            PropertyInfo property = component.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(component.GetType().FullName, propertyName);
            property.SetValue(component, value);
        }
        private static void SetMember(object component, string memberName, object? value)
        {
            Type type = component.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static void InvokePrivate(string name, object component, params object?[]? args)
        {
            MethodInfo method = GetPrivateMethod(name);
            method.Invoke(component, args);
        }

        private static async Task InvokePrivateAsync(string name, object component, params object?[]? args)
        {
            MethodInfo method = GetPrivateMethod(name);
            Task task = (Task)(method.Invoke(component, args) ?? throw new InvalidOperationException($"{name} returned null task."));
            await task;
        }

        [Test]
        public void OpenReducedProtocolSelection_SetsPopupFlag()
        {
            SettingsModelling component = CreateComponent();

            InvokePrivate("OpenReducedProtocolSelection", component);

            Assert.That(GetPrivateField<bool>(component, "reducedProtocolSelectionMode"), Is.True);
        }

        [Test]
        public void VarianceOptions_SetsPopupFlag()
        {
            SettingsModelling component = CreateComponent();

            InvokePrivate("VarianceOptions", component);

            Assert.That(GetPrivateField<bool>(component, "varOptMode"), Is.True);
        }

        [Test]
        public void ModIntegrationStates_SetsPopupFlag()
        {
            SettingsModelling component = CreateComponent();

            InvokePrivate("ModIntegrationStates", component);

            Assert.That(GetPrivateField<bool>(component, "modIntegrationStatesMode"), Is.True);
        }

        /// <summary>
        /// Verifies which naming conventions are rejected and which message is reported for them.
        /// </summary>
        [TestCase(true, 1, "NA", "AR", "E5601")]
        [TestCase(true, 0, "NA", "AR", "E5601")]
        [TestCase(true, 2, "NA", "AR", "E5601")]
        [TestCase(true, 0, null, null, "E5601")]
        [TestCase(true, 4, "NA", "ARX", "E5602")]
        [TestCase(true, 4, "NA", "AR", null)]
        [TestCase(true, 3, "NA", "AR", null)]
        [TestCase(true, 1, null, null, null)]
        [TestCase(true, 4, "NA", "A", "E5602")]
        [TestCase(true, 5, "NET", "AR", "E5602")]
        [TestCase(false, 1, "NA", "ARX", null)]
        [TestCase(false, 2, "NA", "AR", null)]
        public void GetNamingConventionError_ChecksPatternLengths(bool networkAreaRequired, int fixedPartLength,
            string? networkAreaPattern, string? appRolePattern, string? expectedKey)
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "namingConvention", new ModellingNamingConvention
            {
                NetworkAreaRequired = networkAreaRequired,
                FixedPartLength = fixedPartLength,
                NetworkAreaPattern = networkAreaPattern!,
                AppRolePattern = appRolePattern!
            });

            string? result = (string?)GetPrivateMethod("GetNamingConventionError").Invoke(component, null);

            Assert.That(result, Is.EqualTo(expectedKey));
        }

        /// <summary>
        /// Verifies that saving rejects a naming convention that would discard the area-specific identifier.
        /// </summary>
        [TestCase(1, "NA", "AR", "E5601", "Invalid fixed part length")]
        [TestCase(2, "NA", "AR", "E5601", "Invalid fixed part length")]
        [TestCase(4, "NA", "ARX", "E5602", "Invalid app role pattern")]
        public async Task Save_WithInvalidNamingConvention_ReportsValidationError(int fixedPartLength,
            string networkAreaPattern, string appRolePattern, string expectedKey, string expectedMessage)
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.LangDict[GlobalConst.kEnglish]["modelling_settings"] = "Modelling Settings";
            globalConfig.LangDict[GlobalConst.kEnglish][expectedKey] = expectedMessage;
            SettingsModelling component = CreateComponent();
            SetPrivateProperty(component, "userConfig", UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false));
            SetPrivateField(component, "namingConvention", new ModellingNamingConvention
            {
                NetworkAreaRequired = true,
                FixedPartLength = fixedPartLength,
                NetworkAreaPattern = networkAreaPattern,
                AppRolePattern = appRolePattern
            });
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            SetPrivateProperty(component, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((exception, title, message, isError) => messages.Add((exception, title, message, isError))));

            await (Task)GetPrivateMethod("Save").Invoke(component, null)!;

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(messages[0].Exception, Is.Null);
                Assert.That(messages[0].Title, Is.EqualTo("Modelling Settings"));
                Assert.That(messages[0].Message, Is.EqualTo(expectedMessage));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        /// <summary>
        /// Verifies that negative lengths are repaired before the naming convention is validated.
        /// The repaired values are observable although the validation aborts the save, which is only
        /// possible if the repair runs ahead of the validation.
        /// </summary>
        [Test]
        public async Task Save_WithNegativeLengths_RepairsThemBeforeValidating()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.LangDict[GlobalConst.kEnglish]["modelling_settings"] = "Modelling Settings";
            globalConfig.LangDict[GlobalConst.kEnglish]["E5601"] = "Invalid fixed part length";
            SettingsModelling component = CreateComponent();
            SetPrivateProperty(component, "userConfig", UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false));
            ModellingNamingConvention namingConvention = new()
            {
                NetworkAreaRequired = true,
                FixedPartLength = -3,
                FreePartLength = -1,
                NetworkAreaPattern = "",
                AppRolePattern = ""
            };
            SetPrivateField(component, "namingConvention", namingConvention);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            SetPrivateProperty(component, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((exception, title, message, isError) => messages.Add((exception, title, message, isError))));

            await (Task)GetPrivateMethod("Save").Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(namingConvention.FixedPartLength, Is.Zero);
                Assert.That(namingConvention.FreePartLength, Is.Zero);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Exception, Is.Null);
                Assert.That(messages[0].Message, Is.EqualTo("Invalid fixed part length"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public void AddExtraConfig_AddsNonEmptyValueAndClearsInput()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "actExtraConfig", "  keep me  ");
            SetPrivateField(component, "ExtraConfigsToAdd", new List<string>());

            InvokePrivate("AddExtraConfig", component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<string>>(component, "ExtraConfigsToAdd"), Is.EqualTo(new List<string> { "  keep me  " }));
                Assert.That(GetPrivateField<string>(component, "actExtraConfig"), Is.Empty);
            });
        }

        [Test]
        public void AddExtraConfig_IgnoresEmptyValue()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "actExtraConfig", "");
            SetPrivateField(component, "ExtraConfigsToAdd", new List<string> { "existing" });

            InvokePrivate("AddExtraConfig", component);

            Assert.That(GetPrivateField<List<string>>(component, "ExtraConfigsToAdd"), Is.EqualTo(new List<string> { "existing" }));
        }

        [Test]
        public void AddAppServerType_AddsUniqueEntryAndResetsInput()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "appServerTypes", new List<AppServerType>());
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "actAppServerType", new AppServerType { Id = 5, Name = "Type 5" });

            InvokePrivate("AddAppServerType", component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypesToAdd"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypesToAdd")[0].Id, Is.EqualTo(5));
                Assert.That(GetPrivateField<AppServerType>(component, "actAppServerType").Id, Is.EqualTo(0));
            });
        }

        [Test]
        public void AddAppServerType_IgnoresDuplicates()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "appServerTypes", new List<AppServerType> { new() { Id = 5, Name = "Existing" } });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "actAppServerType", new AppServerType { Id = 5, Name = "Duplicate" });

            InvokePrivate("AddAppServerType", component);

            Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypesToAdd"), Is.Empty);
        }

        [Test]
        public void PrepareAreas_RemovesDeletedEntriesAndSerializesRemainingAreas()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "allAreas", new List<ModellingNwGroup>
            {
                new() { Id = 1, Name = "Area 1" },
                new() { Id = 2, Name = "Area 2" }
            });
            SetPrivateField(component, "CommonAreas", new List<CommonArea>
            {
                new() { Area = new() { Content = new ModellingNwGroup { Id = 1, Name = "Area 1" } }, UseInSrc = true, UseInDst = false },
                new() { Area = new() { Content = new ModellingNwGroup { Id = 2, Name = "Area 2" } }, UseInSrc = false, UseInDst = true }
            });
            SetPrivateField(component, "CommAreasToDelete", new List<CommonArea>
            {
                new() { Area = new() { Content = new ModellingNwGroup { Id = 2, Name = "Area 2" } } }
            });

            object?[] prepareAreasArgs =
            [
                GetPrivateField<List<CommonArea>>(component, "CommonAreas"),
                GetPrivateField<List<CommonArea>>(component, "CommAreasToDelete")
            ];
            string serialized = (string)GetPrivateMethod("PrepareAreas").Invoke(component, prepareAreasArgs)!;
            List<CommonAreaConfig>? parsed = JsonSerializer.Deserialize<List<CommonAreaConfig>>(serialized);

            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed, Has.Count.EqualTo(1));
                Assert.That(parsed![0].AreaId, Is.EqualTo(1));
                Assert.That(parsed[0].UseInSrc, Is.True);
                Assert.That(parsed[0].UseInDst, Is.False);
            });
        }

        [Test]
        public void PrepareAppServerTypes_UpdatesDefaultEntryAndSerializesRemainingTypes()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "configData", new ConfigData());
            AppServerType typeToRemove = new() { Id = 1, Name = "Remove me" };
            SetPrivateField(component, "appServerTypes", new List<AppServerType>
            {
                new() { Id = 0, Name = "Old default" },
                typeToRemove
            });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>
            {
                new() { Id = 2, Name = "Add me" }
            });
            SetPrivateField(component, "appServerTypesToDelete", new List<AppServerType>
            {
                typeToRemove
            });
            SetPrivateField(component, "appServerDefaultTypeName", "Updated default");

            InvokePrivate("PrepareAppServerTypes", component);

            string serialized = GetPrivateField<ConfigData>(component, "configData").ModAppServerTypes;
            List<AppServerType>? parsed = JsonSerializer.Deserialize<List<AppServerType>>(serialized);

            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed, Has.Count.EqualTo(2));
                Assert.That(parsed!.Any(type => type.Id == 0 && type.Name == "Updated default"), Is.True);
                Assert.That(parsed!.Any(type => type.Id == 1), Is.False);
                Assert.That(parsed!.Any(type => type.Id == 2 && type.Name == "Add me"), Is.True);
            });
        }

        [Test]
        public void PrepareConfigData_SerializesRulesAndTimeFields()
        {
            SettingsModelling component = CreateComponent();
            ConfigData configData = new();
            SetPrivateField(component, "configData", configData);
            SetPrivateField(component, "namingConvention", new ModellingNamingConvention
            {
                NetworkAreaRequired = true,
                UseAppPart = true,
                FixedPartLength = 12,
                FreePartLength = 7,
                NetworkAreaPattern = "net-*",
                AppRolePattern = "app-*",
                AppZone = "zone-*",
                AppServerPrefix = "srv-",
                NetworkPrefix = "net-",
                IpRangePrefix = "range-"
            });
            SetPrivateField(component, "ruleRecognitionOption", new RuleRecognitionOption
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
            });
            SetPrivateField(component, "varAnalysisDate", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
            SetPrivateField(component, "varAnalysisTime", new DateTime(2026, 7, 5, 14, 35, 0, DateTimeKind.Utc));
            SetPrivateField(component, "ExtraConfigs", new List<string> { "first" });
            SetPrivateField(component, "ExtraConfigsToAdd", new List<string> { "second" });
            SetPrivateField(component, "ExtraConfigsToDelete", new List<string>());
            SetPrivateField(component, "appServerTypes", new List<AppServerType> { new() { Id = 0, Name = "Default" } });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "appServerTypesToDelete", new List<AppServerType>());
            SetPrivateField(component, "CommonAreas", new List<CommonArea>());
            SetPrivateField(component, "CommAreasToDelete", new List<CommonArea>());
            SetPrivateField(component, "SpecUserAreas", new List<CommonArea>());
            SetPrivateField(component, "SpecUserAreasToDelete", new List<CommonArea>());
            SetPrivateField(component, "UpdObjAreas", new List<CommonArea>());
            SetPrivateField(component, "UpdObjAreasToDelete", new List<CommonArea>());

            InvokePrivate("PrepareConfigData", component);

            Assert.Multiple(() =>
            {
                Assert.That(configData.ModNamingConvention, Does.Contain("\"networkAreaRequired\":true"));
                Assert.That(configData.RuleRecognitionOption, Does.Contain("\"nwRegardName\":true"));
                Assert.That(configData.VarianceAnalysisStartAt, Is.EqualTo(new DateTime(2026, 7, 5, 14, 35, 0, DateTimeKind.Utc)));
                Assert.That(configData.ModExtraConfigs, Does.Contain("second"));
            });
        }

        [Test]
        public void RefreshAreas_ReturnsEmptyWhenConfigIsEmpty()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "allAreas", new List<ModellingNwGroup> { new() { Id = 1, Name = "Area 1" } });

            List<CommonArea> result = (List<CommonArea>)GetPrivateMethod("RefreshAreas").Invoke(component, RefreshEmptyArgs)!;

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void RefreshAreas_IgnoresUnknownAreaIds()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "allAreas", new List<ModellingNwGroup> { new() { Id = 1, Name = "Area 1" } });
            string config = JsonSerializer.Serialize(new List<CommonAreaConfig>
            {
                new() { AreaId = 99, UseInSrc = true, UseInDst = true }
            });

            object?[] refreshConfigArgs = [config];
            List<CommonArea> result = (List<CommonArea>)GetPrivateMethod("RefreshAreas").Invoke(component, refreshConfigArgs)!;

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task Save_PersistsOverviewChange()
        {
            SettingsModelling component = CreateComponent();
            SimulatedGlobalConfig globalConfig = new()
            {
                OverviewDisplayLines = 10
            };
            RecordingSettingsApiConn apiConnection = new()
            {
                IpProtocols = [],
                ModellingGroups = []
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.OverviewDisplayLines = 11;

            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", userConfig);
            SetPrivateField(component, "configData", editableConfig);
            SetPrivateField(component, "initComplete", true);

            await InvokePrivateAsync("Save", component);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastUpsertConfigItems.Any(item => item.Key == "overviewDisplayLines" && item.Value == "11"), Is.True);
            });
        }

        [Test]
        public async Task SettingsModelling_InitializesLoadedConfig_ForPrivilegedUser()
        {
            SimulatedGlobalConfig globalConfig = CreateLoadedGlobalConfig();
            RecordingSettingsApiConn apiConnection = new()
            {
                IpProtocols =
                [
                    new() { Name = "TCP" },
                    new() { Name = "UDP" }
                ],
                ModellingGroups =
                [
                    new() { Id = 1, Name = "Area 1" },
                    new() { Id = 2, Name = "Area 2" }
                ]
            };
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Admin);
            SettingsModelling component = CreateComponent();

            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, _, _) => { }));

            await InvokePrivateAsync("OnInitializedAsync", component);
            InvokePrivate("PredefServices", component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "initComplete"), Is.True);
                Assert.That(GetPrivateField<string>(component, "appServerDefaultTypeName"), Is.EqualTo("Default server"));
                Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypes"), Has.Count.EqualTo(2));
                Assert.That(GetPrivateField<List<CommonArea>>(component, "CommonAreas"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<CommonArea>>(component, "SpecUserAreas"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<CommonArea>>(component, "UpdObjAreas"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component, "predefServices"), Is.True);
                Assert.That(apiConnection.Queries, Does.Contain(StmQueries.getIpProtocols));
                Assert.That(apiConnection.Queries, Does.Contain(ModellingQueries.getNwGroupObjects));
            });
        }

        [Test]
        public async Task SettingsModelling_RendersLoadingAndReportsInitErrors_WhenConfigLoadFails()
        {
            SimulatedGlobalConfig globalConfig = CreateLoadedGlobalConfig();
            ThrowingSettingsApiConn apiConnection = new(StmQueries.getIpProtocols);
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Admin);
            RecordingMessageSink sink = new();

            await using BunitContext context = CreateContext(globalConfig, apiConnection, userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context, sink.Handler);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(sink.Messages, Has.Count.EqualTo(1));
                Assert.That(wrapper.FindAll("#cbx_allow_server_in_conn"), Is.Empty);
            });

            SettingsModelling component = wrapper.FindComponent<SettingsModelling>().Instance;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "initComplete"), Is.False);
                Assert.That(sink.Messages[0].IsError, Is.False);
            });
        }

        [Test]
        public async Task Save_ReportsErrorWhenConfigDataIsMissing()
        {
            SettingsModelling component = CreateComponent();
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new();
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Admin);
            RecordingMessageSink sink = new();

            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)sink.Handler);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", userConfig);
            SetPrivateField(component, "configData", null);

            await InvokePrivateAsync("Save", component);

            Assert.Multiple(() =>
            {
                Assert.That(sink.Messages, Has.Count.EqualTo(1));
                Assert.That(sink.Messages[0].IsError, Is.True);
                Assert.That(apiConnection.UpsertConfigCallCount, Is.Zero);
            });
        }

        [Test]
        public void PrepareAppServerTypes_AddsDefaultEntryWhenMissing()
        {
            SettingsModelling component = CreateComponent();
            ConfigData configData = new();
            SetPrivateField(component, "configData", configData);
            SetPrivateField(component, "appServerTypes", new List<AppServerType>
            {
                new() { Id = 3, Name = "Existing" }
            });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "appServerTypesToDelete", new List<AppServerType>());
            SetPrivateField(component, "appServerDefaultTypeName", "Default server");

            InvokePrivate("PrepareAppServerTypes", component);

            List<AppServerType>? parsed = JsonSerializer.Deserialize<List<AppServerType>>(configData.ModAppServerTypes);

            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed, Has.Count.EqualTo(2));
                Assert.That(parsed!.Any(type => type.Id == 0 && type.Name == "Default server"), Is.True);
                Assert.That(parsed!.Any(type => type.Id == 3 && type.Name == "Existing"), Is.True);
            });
        }

        private static BunitContext CreateContext(SimulatedGlobalConfig globalConfig, ApiConnection apiConnection, SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context, Action<Exception?, string, string, bool>? displayMessage = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, displayMessage ?? ((_, _, _, _) => { }))
                    .AddChildContent<SettingsModelling>()));
        }

        private static SimulatedGlobalConfig CreateLoadedGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                DefaultLanguage = "English",
                ModIntegrationMode = ModIntegrationMode.WorkflowNotifications,
                ModIntegrationStateMarker = ModIntegrationStateConfig.DefaultMarker,
                ModIntegrationStates = JsonSerializer.Serialize(new List<ModIntegrationState>
                {
                    new() { Name = "ImplementationState", IncludeIntoRequest = true, MonitorStatus = ModIntegrationStateStatus.Implemented }
                }),
                ModAppServerTypes = JsonSerializer.Serialize(new List<AppServerType>
                {
                    new() { Id = 0, Name = "Default server" },
                    new() { Id = 3, Name = "App server" }
                }),
                ModNamingConvention = JsonSerializer.Serialize(new ModellingNamingConvention
                {
                    NetworkAreaRequired = true,
                    UseAppPart = true,
                    FixedPartLength = 12,
                    FreePartLength = 7,
                    NetworkAreaPattern = "net-*",
                    AppRolePattern = "app-*",
                    AppZone = "zone-*",
                    AppServerPrefix = "srv-",
                    NetworkPrefix = "net-",
                    IpRangePrefix = "range-"
                }),
                ModExtraConfigs = JsonSerializer.Serialize(new List<string> { "alpha", "beta" }),
                ModCommonAreas = JsonSerializer.Serialize(new List<CommonAreaConfig>
                {
                    new() { AreaId = 1, UseInSrc = true, UseInDst = false }
                }),
                ModSpecUserAreas = JsonSerializer.Serialize(new List<CommonAreaConfig>
                {
                    new() { AreaId = 2, UseInSrc = false, UseInDst = true }
                }),
                ModUpdatableObjAreas = JsonSerializer.Serialize(new List<CommonAreaConfig>
                {
                    new() { AreaId = 1, UseInSrc = true, UseInDst = true }
                }),
                RuleRecognitionOption = JsonSerializer.Serialize(new RuleRecognitionOption
                {
                    NwRegardName = true,
                    SvcSplitPortRanges = true
                }),
                VarianceAnalysisStartAt = new DateTime(2026, 8, 24, 13, 45, 0, DateTimeKind.Utc)
            };
        }

        private static SimulatedUserConfig CreateUserConfig(params string[] roles)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Language = "English";
            userConfig.User.Roles = [.. roles];
            userConfig.SetExecutionMode(Roles.Admin);
            return userConfig;
        }

        private sealed class RecordingMessageSink
        {
            public List<(Exception? Exception, string Title, string Message, bool IsError)> Messages { get; } = [];

            public void Handler(Exception? exception, string title, string message, bool isError)
            {
                Messages.Add((exception, title, message, isError));
            }
        }

        private sealed class ThrowingSettingsApiConn : SimulatedApiConnection
        {
            private readonly string failedQuery;

            public ThrowingSettingsApiConn(string failedQuery)
            {
                this.failedQuery = failedQuery;
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == failedQuery)
                {
                    throw new InvalidOperationException("Injected load failure.");
                }

                return base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }
        }
    }
}

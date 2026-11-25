using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsReportGeneralTest
    {
        [Test]
        public void BuildReportTypeVisibilityByRole_InitializesAllTypesAndKeepsSavedOptions()
        {
            Dictionary<ReportType, ReportTypeVisibilityOption> savedOptions = new()
            {
                [ReportType.Connections] = ReportTypeVisibilityOption.NotVisible
            };
            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> saved = new()
            {
                [Roles.Admin] = savedOptions
            };

            List<string> roles = [Roles.Admin, Roles.Auditor];
            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> result = InvokeBuild(roles, saved);
            List<string> expectedRoles = [Roles.Admin, Roles.Auditor];

            Assert.Multiple(() =>
            {
                Assert.That(result.Keys, Is.EquivalentTo(expectedRoles));
                Assert.That(result[Roles.Admin][ReportType.Connections], Is.EqualTo(ReportTypeVisibilityOption.NotVisible));
                Assert.That(result[Roles.Admin].Keys, Is.EquivalentTo(ReportTypeGroups.AllReportTypes()));
                Assert.That(result[Roles.Auditor].Values, Has.All.EqualTo(ReportTypeVisibilityOption.Inherited));
            });
        }

        [Test]
        public async Task OnInitializedAsync_WithNoLdapRolesFinishesLoadingAndReportsWarning()
        {
            SettingsReportGeneral component = CreateComponent(new SingleResponseHandler(HttpStatusCode.OK, "[]"), out RecordingMessageSink sink);

            await InvokeAsync(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "rolesLoaded"), Is.True);
                Assert.That(GetField<List<string>>(component, "availableRoles"), Is.Empty);
                Assert.That(sink.Messages, Has.Count.EqualTo(1));
                Assert.That(sink.Messages[0].IsError, Is.True);
                Assert.That(sink.Messages[0].Message, Is.EqualTo("No roles found"));
            });
        }

        [Test]
        public async Task OnInitializedAsync_WithLdapRolesBuildsInheritedVisibilityOptions()
        {
            const string response = "[{\"role\":\"cn=reporter,ou=roles,dc=example\",\"attributes\":[]}]";
            SettingsReportGeneral component = CreateComponent(new SingleResponseHandler(HttpStatusCode.OK, response), out RecordingMessageSink sink);

            await InvokeAsync(component, "OnInitializedAsync");

            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> visibility =
                GetField<Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>>>(component, "reportTypeVisibilityByRole");

            Assert.Multiple(() =>
            {
                Assert.That(sink.Messages, Is.Empty);
                Assert.That(GetField<List<string>>(component, "availableRoles"), Is.EqualTo(new List<string> { "reporter" }));
                Assert.That(visibility["reporter"][ReportType.Rules], Is.EqualTo(ReportTypeVisibilityOption.Inherited));
            });
        }

        [Test]
        public async Task OnInitializedAsync_WhenConfigIsInvalidReportsReadErrorAndFinishesLoading()
        {
            SimulatedGlobalConfig globalConfig = new() { AvailableReportTypes = "not-json" };
            SettingsReportGeneral component = new();
            RecordingMessageSink sink = new();
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)sink.Handler);

            await InvokeAsync(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "rolesLoaded"), Is.True);
                Assert.That(sink.Messages, Has.Count.EqualTo(1));
                Assert.That(sink.Messages[0].Title, Is.EqualTo("Read Config"));
                Assert.That(sink.Messages[0].IsError, Is.False);
            });
        }

        [Test]
        public async Task Render_WithNoRolesShowsWarningAndCoreSettings()
        {
            SimulatedGlobalConfig globalConfig = new();
            await using BunitContext context = CreateRenderContext(new SingleResponseHandler(HttpStatusCode.OK, "[]"), globalConfig);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderReport(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(wrapper.Find("h3").TextContent, Is.EqualTo("report_settings"));
                    Assert.That(wrapper.Markup, Does.Contain("No roles found"));
                    Assert.That(wrapper.Find("#cbx_auto_fill_right_sidebar"), Is.Not.Null);
                    Assert.That(wrapper.FindComponent<RulesViewSelect>(), Is.Not.Null);
                    Assert.That(wrapper.Find("label[for='dropdown-input-defaultRulesView']"), Is.Not.Null);
                    Assert.That(wrapper.Find("#dropdown-input-defaultRulesView"), Is.Not.Null);
                    Assert.That(wrapper.Find("button.btn-primary"), Is.Not.Null);
                });
            });
        }

        [Test]
        public async Task Render_WithRolesShowsRoleVisibilityControls()
        {
            const string response = "[{\"role\":\"cn=reporter,ou=roles,dc=example\",\"attributes\":[]}]";
            await using BunitContext context = CreateRenderContext(new SingleResponseHandler(HttpStatusCode.OK, response), new SimulatedGlobalConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderReport(context);
            IRenderedComponent<SettingsReportGeneral> component = wrapper.FindComponent<SettingsReportGeneral>();
            wrapper.WaitForAssertion(() => Assert.That(component.FindComponents<Dropdown<string>>(), Has.Count.EqualTo(1)));

            SetMember(component.Instance, "selectedRole", "reporter");
            component.Render();

            Assert.That(component.FindAll("input[type='radio']"), Is.Not.Empty);
        }

        [Test]
        public async Task Save_PersistsAvailableReportsAndMergesRoleVisibility()
        {
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new();
            SettingsReportGeneral component = new();
            ConfigData configData = new()
            {
                AvailableReportTypes = JsonSerializer.Serialize(new List<ReportType> { ReportType.Rules }),
                ReportTypeVisibilityByRole = "{}"
            };
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "configData", configData);
            SetMember(component, "reportTypesActiveDict", new Dictionary<ReportType, bool>
            {
                [ReportType.Rules] = true,
                [ReportType.Connections] = false
            });
            SetMember(component, "reportTypeVisibilityByRole", new Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>>
            {
                [Roles.Admin] = new() { [ReportType.Rules] = ReportTypeVisibilityOption.Visible }
            });
            SetMember(component, "savedReportTypeVisibilityByRole", new Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>>
            {
                [Roles.Auditor] = new() { [ReportType.Connections] = ReportTypeVisibilityOption.NotVisible }
            });

            await InvokeAsync(component, "Save");

            ConfigItem reports = apiConnection.LastUpsertConfigItems.Single(item => item.Key == "availableReportTypes");
            ConfigItem visibility = apiConnection.LastUpsertConfigItems.Single(item => item.Key == "reportTypeVisibilityByRole");
            Dictionary<string, Dictionary<int, ReportTypeVisibilityOption>> serializedVisibility =
                JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, ReportTypeVisibilityOption>>>(visibility.Value!)!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(JsonSerializer.Deserialize<List<ReportType>>(reports.Value!)!, Is.EqualTo(new List<ReportType> { ReportType.Rules }));
                Assert.That(serializedVisibility[Roles.Admin][(int)ReportType.Rules], Is.EqualTo(ReportTypeVisibilityOption.Visible));
                Assert.That(serializedVisibility[Roles.Auditor][(int)ReportType.Connections], Is.EqualTo(ReportTypeVisibilityOption.NotVisible));
            });
        }

        [Test]
        public async Task Save_ReportsErrorWhenConfigurationWasNotLoaded()
        {
            SettingsReportGeneral component = new();
            RecordingMessageSink sink = new();
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "globalConfig", new SimulatedGlobalConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)sink.Handler);

            await InvokeAsync(component, "Save");

            Assert.That(sink.Messages, Has.Count.EqualTo(1));
            Assert.That(sink.Messages[0].IsError, Is.True);
        }

        [Test]
        public void GetMigrateSchedulerConfigMessage_UsesCountWhenLegacyConfigExists()
        {
            SettingsReportGeneral component = new();
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "configData", new ConfigData { ReportSchedulerConfig = "{}" });

            string message = (string)Invoke(component, "GetMigrateSchedulerConfigMessage")!;

            Assert.That(message, Does.StartWith("0 "));
        }

        [Test]
        public void GetMigrateSchedulerConfigMessage_UsesDefaultPromptWithoutLegacyConfig()
        {
            SettingsReportGeneral component = new();
            SetMember(component, "userConfig", new SimulatedUserConfig());

            string message = (string)Invoke(component, "GetMigrateSchedulerConfigMessage")!;

            Assert.That(message, Is.EqualTo("confirm_migrate_scheduler_config"));
        }

        [Test]
        public async Task MigrateSchedulerConfig_SkipsMissingSchedulesAndClearsLegacyConfig()
        {
            MigrationApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new() { ReportSchedulerConfig = "legacy" };
            SettingsReportGeneral component = CreateMigrationComponent(apiConnection, globalConfig, out RecordingMessageSink sink);
            SetLegacyConfigs(component, CreateLegacyConfig(99));

            await InvokeAsync(component, "MigrateSchedulerConfig");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.GetSchedulesCalls, Is.EqualTo(1));
                Assert.That(apiConnection.EditScheduleCalls, Is.Zero);
                Assert.That(globalConfig.ReportSchedulerConfig, Is.Empty);
                Assert.That(GetField<bool>(component, "ShowMigrateSchedulerConfigDialog"), Is.False);
                Assert.That(sink.Messages.Single().IsError, Is.False);
            });
        }

        [Test]
        public async Task MigrateSchedulerConfig_UpdatesMatchingSchedule()
        {
            ReportSchedule schedule = new()
            {
                Id = 7,
                Name = "Weekly report",
                ScheduleOwningUser = new UiUser { DbId = 11 },
                Template = new ReportTemplate { Id = 12 },
                Notifications = [new FwoNotification()]
            };
            MigrationApiConnection apiConnection = new() { Schedules = [schedule] };
            SimulatedGlobalConfig globalConfig = new() { ReportSchedulerConfig = "legacy" };
            SettingsReportGeneral component = CreateMigrationComponent(apiConnection, globalConfig, out RecordingMessageSink sink);
            SetLegacyConfigs(component, CreateLegacyConfig(7, toArchive: true));

            await InvokeAsync(component, "MigrateSchedulerConfig");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.EditScheduleCalls, Is.EqualTo(1));
                Assert.That(schedule.Archive, Is.True);
                Assert.That(sink.Messages.Single().IsError, Is.False);
            });
        }

        private static SettingsReportGeneral CreateComponent(HttpMessageHandler handler, out RecordingMessageSink sink)
        {
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(handler);
            sink = new RecordingMessageSink();
            SettingsReportGeneral component = new();
            SetMember(component, "apiConnection", new RecordingSettingsApiConn());
            SetMember(component, "globalConfig", new SimulatedGlobalConfig());
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "middlewareClient", middlewareClient);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)sink.Handler);
            return component;
        }

        private static BunitContext CreateRenderContext(HttpMessageHandler handler, SimulatedGlobalConfig globalConfig)
        {
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(handler);
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new RecordingSettingsApiConn());
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
            context.Services.AddSingleton<DomEventService>();
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderReport(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(parameter => parameter.Value, (_, _, _, _) => { })
                    .AddChildContent<SettingsReportGeneral>()));
        }

        private static SettingsReportGeneral CreateMigrationComponent(MigrationApiConnection apiConnection,
            SimulatedGlobalConfig globalConfig, out RecordingMessageSink sink)
        {
            sink = new RecordingMessageSink();
            SettingsReportGeneral component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "configData", new ConfigData { ReportSchedulerConfig = "legacy" });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)sink.Handler);
            return component;
        }

        private static object CreateLegacyConfig(int reportScheduleId, bool toArchive = false)
        {
            Type legacyType = typeof(SettingsReportGeneral).GetNestedType("LegacyReportSchedulerConfig", BindingFlags.NonPublic)
                ?? throw new MissingMemberException(typeof(SettingsReportGeneral).FullName, "LegacyReportSchedulerConfig");
            object legacyConfig = Activator.CreateInstance(legacyType)!;
            legacyType.GetProperty("ReportScheduleId")!.SetValue(legacyConfig, reportScheduleId);
            legacyType.GetProperty("ToArchive")!.SetValue(legacyConfig, toArchive);
            return legacyConfig;
        }

        private static void SetLegacyConfigs(SettingsReportGeneral component, object legacyConfig)
        {
            Type listType = typeof(List<>).MakeGenericType(legacyConfig.GetType());
            System.Collections.IList configs = (System.Collections.IList)Activator.CreateInstance(listType)!;
            configs.Add(legacyConfig);
            SetMember(component, "legacyReportSchedulerConfigs", configs);
        }

        private static Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> InvokeBuild(
            List<string> roles,
            Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>> saved)
        {
            MethodInfo method = typeof(SettingsReportGeneral).GetMethod("BuildReportTypeVisibilityByRole", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsReportGeneral).FullName, "BuildReportTypeVisibilityByRole");
            object?[] arguments = [roles, saved];
            return (Dictionary<string, Dictionary<ReportType, ReportTypeVisibilityOption>>)method.Invoke(null, arguments)!;
        }

        private static async Task InvokeAsync(object component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            await (Task)(method.Invoke(component, null) ?? throw new InvalidOperationException($"{methodName} returned null task."));
        }

        private static object? Invoke(object component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            return method.Invoke(component, null);
        }

        private static void SetMember(object component, string memberName, object? value)
        {
            Type type = component.GetType();
            FieldInfo? field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static T GetField<T>(object component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            return (T)field.GetValue(component)!;
        }

        private sealed class RecordingMessageSink
        {
            public List<(Exception? Exception, string Title, string Message, bool IsError)> Messages { get; } = [];

            public void Handler(Exception? exception, string title, string message, bool isError)
            {
                Messages.Add((exception, title, message, isError));
            }
        }

        private sealed class MigrationApiConnection : SimulatedApiConnection
        {
            public List<ReportSchedule> Schedules { get; set; } = [];
            public int GetSchedulesCalls { get; private set; }
            public int EditScheduleCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getReportSchedules && typeof(QueryResponseType) == typeof(List<ReportSchedule>))
                {
                    GetSchedulesCalls++;
                    return Task.FromResult((QueryResponseType)(object)Schedules);
                }

                if (query == ReportQueries.editReportSchedule && typeof(QueryResponseType) == typeof(object))
                {
                    EditScheduleCalls++;
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == ConfigQueries.upsertConfigItems)
                {
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new NotImplementedException($"Unhandled query {query}");
            }
        }
    }
}

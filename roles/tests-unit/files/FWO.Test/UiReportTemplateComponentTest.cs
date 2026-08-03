using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportTemplateComponentTest
    {
        private static readonly List<string> kModellerRoles = [Roles.Modeller];
        private static readonly List<string> kAuditorRoles = [Roles.Auditor];
        private static readonly List<string> kReporterRoles = [Roles.Reporter];
        private static readonly List<int> kOwnership11 = [11];
        private static readonly List<int> kEmptyOwnerships = [];
        private static readonly List<ReportTemplate> kEmptyTemplates = [];
        private static readonly List<FwoOwner> kEmptyOwners = [];
        private static readonly List<FwoOwner> kOwnedAppOwners = [new FwoOwner { Id = 11, Name = "Owned App" }];

        private sealed class ReportTemplateComponentTestApiConn(IEnumerable<ReportTemplate> templates, IEnumerable<FwoOwner> owners) : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(ReportTemplate[]))
                {
                    return Task.FromResult((QueryResponseType)(object)templates.ToArray());
                }

                if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)owners);
                }

                throw new NotImplementedException();
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("templates", "Templates");
            SimulatedUserConfig.DummyTranslate.TryAdd("actions", "Actions");
            SimulatedUserConfig.DummyTranslate.TryAdd("comment", "Comment");
            SimulatedUserConfig.DummyTranslate.TryAdd("creation_date", "Creation date");
            SimulatedUserConfig.DummyTranslate.TryAdd("load", "Load");
            SimulatedUserConfig.DummyTranslate.TryAdd("edit", "Edit");
            SimulatedUserConfig.DummyTranslate.TryAdd("delete", "Delete");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_template", "Report template");
            SimulatedUserConfig.DummyTranslate.TryAdd("U1002", "Delete template");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_time", "Report time");
            SimulatedUserConfig.DummyTranslate.TryAdd("devices", "Devices");
            SimulatedUserConfig.DummyTranslate.TryAdd("managements", "Managements");
            SimulatedUserConfig.DummyTranslate.TryAdd("unused_days", "Unused days");
            SimulatedUserConfig.DummyTranslate.TryAdd("diff_interval", "Diff interval");
            SimulatedUserConfig.DummyTranslate.TryAdd("show_non_impact_rules", "Show non impact rules");
            SimulatedUserConfig.DummyTranslate.TryAdd("tenant", "Tenant");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_type", "Report type");
            SimulatedUserConfig.DummyTranslate.TryAdd("select_device", "Select device");
            SimulatedUserConfig.DummyTranslate.TryAdd("select_time", "Select time");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_filters", "Report filters");
            SimulatedUserConfig.DummyTranslate.TryAdd("rule_filters", "Rule filters");
            SimulatedUserConfig.DummyTranslate.TryAdd("variance_filters", "Variance filters");
            SimulatedUserConfig.DummyTranslate.TryAdd("save_template", "Save template");
            SimulatedUserConfig.DummyTranslate.TryAdd("edit_template", "Edit template");
            SimulatedUserConfig.DummyTranslate.TryAdd("delete_template", "Delete template");
            SimulatedUserConfig.DummyTranslate.TryAdd("open", "Open");
            SimulatedUserConfig.DummyTranslate.TryAdd("from", "from");
            SimulatedUserConfig.DummyTranslate.TryAdd("until", "until");
            SimulatedUserConfig.DummyTranslate.TryAdd("Days", "Days");
            SimulatedUserConfig.DummyTranslate.TryAdd("Weeks", "Wochen");
            SimulatedUserConfig.DummyTranslate.TryAdd("Months", "Months");
            SimulatedUserConfig.DummyTranslate.TryAdd("Years", "Years");
        }

        [Test]
        public void ReportTemplateComponent_ModellerOnly_ShowsOnlyAllowedTemplates()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Modeller),
                CreateUserConfig(kModellerRoles, kOwnership11),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Connections template", ReportType.Connections, ownerId: 11),
                    CreateTemplate(2, "Compliance template", ReportType.ComplianceReport),
                    CreateTemplate(3, "Workflow template", ReportType.TicketReport),
                    CreateTemplate(4, "Certificate template", ReportType.RecertificationEvent)
                ],
                kOwnedAppOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());

            wrapper.WaitForAssertion(() =>
            {
                string markup = wrapper.Markup;
                Assert.That(markup, Does.Contain("Connections template"));
                Assert.That(markup, Does.Not.Contain("Compliance template"));
                Assert.That(markup, Does.Not.Contain("Workflow template"));
                Assert.That(markup, Does.Not.Contain("Certificate template"));
            });
        }

        [Test]
        public void ReportTemplateComponent_Auditor_ShowsComplianceAndWorkflowButNotArchiveOnly()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Auditor),
                CreateUserConfig(kAuditorRoles, kOwnership11),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Compliance template", ReportType.ComplianceReport),
                    CreateTemplate(2, "Workflow template", ReportType.TicketReport),
                    CreateTemplate(3, "Certificate template", ReportType.RecertificationEvent)
                ],
                kOwnedAppOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());

            wrapper.WaitForAssertion(() =>
            {
                string markup = wrapper.Markup;
                Assert.That(markup, Does.Contain("Compliance template"));
                Assert.That(markup, Does.Contain("Workflow template"));
                Assert.That(markup, Does.Not.Contain("Certificate template"));
            });
        }

        [Test]
        public void ReportTemplateComponent_Modeller_HidesTemplateForInaccessibleOwner()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Modeller),
                CreateUserConfig(kModellerRoles, kOwnership11),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Owned connections template", ReportType.Connections, ownerId: 11),
                    CreateTemplate(2, "Foreign connections template", ReportType.Connections, ownerId: 12)
                ],
                kOwnedAppOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());

            wrapper.WaitForAssertion(() =>
            {
                string markup = wrapper.Markup;
                Assert.That(markup, Does.Contain("Owned connections template"));
                Assert.That(markup, Does.Not.Contain("Foreign connections template"));
            });
        }

        [Test]
        public void ReportTemplateComponent_Reporter_ShowsRuleTemplateButNotWorkflowTemplate()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Rules template", ReportType.Rules),
                    CreateTemplate(2, "Workflow template", ReportType.TicketReport),
                    CreateTemplate(3, "App rules template", ReportType.AppRules),
                    CreateTemplate(4, "Owners template", ReportType.Owners)
                ],
                kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());

            wrapper.WaitForAssertion(() =>
            {
                string markup = wrapper.Markup;
                Assert.That(markup, Does.Contain("Rules template"));
                Assert.That(markup, Does.Not.Contain("Workflow template"));
                Assert.That(markup, Does.Not.Contain("App rules template"));
                Assert.That(markup, Does.Not.Contain("Owners template"));
            });
        }

        [Test]
        public void ReportTemplateComponent_DecodeAndRecodeComment_PreserveTemplateKey()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            ReportTemplate template = new()
            {
                Comment = "T0100"
            };
            SimulatedUserConfig.DummyTranslate["T0100"] = "Translated template comment";

            ReportTemplate decodedTemplate = component.decodeComment(template);
            string recodedComment = component.recodeComment(decodedTemplate.Comment);

            Assert.That(decodedTemplate.Comment, Is.EqualTo("Translated template comment"));
            Assert.That(recodedComment, Is.EqualTo("T0100"));
        }

        [Test]
        public void ReportTemplateComponent_NewTemplate_OpensAddDialogForSelectedType()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            ReportTemplate template = CreateTemplate(1, "Rules template", ReportType.Rules);

            component.NewTemplate(template);

            Assert.That(component.reportTemplateInEdit, Is.SameAs(template));
            Assert.That(GetPrivateField<bool>(component, "ShowAddTemplateDialog"), Is.True);
            Assert.That(GetPrivateField<ReportType>(component, "reportTypeInEdit"), Is.EqualTo(ReportType.Rules));
        }

        [Test]
        public void ReportTemplateComponent_CancelEdit_RestoresOriginalDeviceFilterAndClosesDialogs()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            DeviceFilter originalFilter = new(
            [
                new()
                {
                    Id = 1,
                    Devices = [new() { Id = 11, Selected = true }]
                }
            ]);
            component.reportTemplateInEdit.ReportParams.DeviceFilter = new DeviceFilter();
            SetPrivateField(component, "DeviceFilterOrig", originalFilter);
            SetPrivateField(component, "ShowAddTemplateDialog", true);
            SetPrivateField(component, "ShowUpdateTemplateDialog", true);

            InvokePrivateMethod(component, "CancelEdit");

            Assert.That(component.reportTemplateInEdit.ReportParams.DeviceFilter.Managements[0].Devices[0].Selected, Is.True);
            Assert.That(GetPrivateField<bool>(component, "ShowAddTemplateDialog"), Is.False);
            Assert.That(GetPrivateField<bool>(component, "ShowUpdateTemplateDialog"), Is.False);
        }

        [Test]
        public void ReportTemplateComponent_DisplayTime_ShowsChangeIntervalDescription()
        {
            SimulatedUserConfig userConfig = CreateUserConfig(kReporterRoles, kEmptyOwnerships);
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                userConfig,
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            SetPrivateField(component, "reportTypeInEdit", ReportType.Changes);
            component.reportTemplateInEdit.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Interval;
            component.reportTemplateInEdit.ReportParams.TimeFilter.Offset = 3;
            component.reportTemplateInEdit.ReportParams.TimeFilter.Interval = SchedulerInterval.Weeks;

            string displayTime = component.DisplayTime();

            Assert.That(displayTime, Is.EqualTo($"{userConfig.GetText("last")} 3 {userConfig.GetText(SchedulerInterval.Weeks.ToString())}"));
        }

        [Test]
        public void ReportTemplateComponent_DisplayTime_ShowsOpenFixedDateRange()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            SetPrivateField(component, "reportTypeInEdit", ReportType.Changes);
            component.reportTemplateInEdit.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Fixeddates;
            component.reportTemplateInEdit.ReportParams.TimeFilter.OpenStart = true;
            component.reportTemplateInEdit.ReportParams.TimeFilter.OpenEnd = true;

            string displayTime = component.DisplayTime();

            Assert.That(displayTime, Is.EqualTo("open").IgnoreCase);
        }

        [Test]
        public void ReportTemplateComponent_GetOwnerAdditionalInfoKeys_DeduplicatesAndSortsKeys()
        {
            MethodInfo? method = typeof(ReportTemplateComponent).GetMethod("GetOwnerAdditionalInfoKeys", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            List<FwoOwner> owners =
            [
                new()
                {
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        ["region"] = "EMEA",
                        ["business_unit"] = "Payments"
                    }
                },
                new()
                {
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        ["service_tier"] = "gold",
                        ["REGION"] = "APAC"
                    }
                }
            ];

            List<string> keys = (List<string>)method!.Invoke(null, [owners])!;

            Assert.That(keys, Is.EqualTo(new List<string> { "business_unit", "region", "service_tier" }));
        }

        [Test]
        public async Task ReportTemplateComponent_DisplayTime_ShowsUntilForOpenStart()
        {
            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTrackingApiConn([]));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            SetPrivateField(component, "reportTypeInEdit", ReportType.Changes);
            component.reportTemplateInEdit.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Fixeddates;
            component.reportTemplateInEdit.ReportParams.TimeFilter.OpenStart = true;
            component.reportTemplateInEdit.ReportParams.TimeFilter.EndTime = new DateTime(2026, 7, 27, 14, 30, 0);

            string displayTime = component.DisplayTime();

            Assert.That(displayTime, Does.StartWith("until "));
            Assert.That(displayTime, Does.Contain("2026"));
        }

        [Test]
        public async Task ReportTemplateComponent_DisplayTime_ShowsFromForOpenEnd()
        {
            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTrackingApiConn([]));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            SetPrivateField(component, "reportTypeInEdit", ReportType.Changes);
            component.reportTemplateInEdit.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Fixeddates;
            component.reportTemplateInEdit.ReportParams.TimeFilter.StartTime = new DateTime(2026, 7, 27, 11, 15, 0);
            component.reportTemplateInEdit.ReportParams.TimeFilter.OpenEnd = true;

            string displayTime = component.DisplayTime();

            Assert.That(displayTime, Does.StartWith("from "));
            Assert.That(displayTime, Does.Contain("2026"));
        }

        [Test]
        public async Task ReportTemplateComponent_Save_AddTemplate_RefreshesAndClosesDialog()
        {
            ReportTemplateComponentTrackingApiConn apiConnection = new(
                [
                    CreateTemplate(1, "Existing template", ReportType.Rules, ownerId: 50)
                ]);

            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                apiConnection);

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            wrapper.WaitForAssertion(() => Assert.That(wrapper.FindComponent<ReportTemplateComponent>().Instance.reportTemplates, Has.Count.EqualTo(1)));

            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            ReportTemplate newTemplate = CreateTemplate(0, "Created template", ReportType.Rules);
            newTemplate.Comment = "New comment";
            component.NewTemplate(newTemplate);

            await (Task)InvokePrivateMethod(component, "Save")!;

            Assert.Multiple(() =>
            {
                Assert.That(component.reportTemplates, Has.Count.EqualTo(2));
                Assert.That(component.reportTemplates.Any(template => template.Name == "Created template"), Is.True);
                Assert.That(GetPrivateField<bool>(component, "ShowAddTemplateDialog"), Is.False);
                Assert.That(apiConnection.AddTemplateCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ReportTemplateComponent_Save_UpdateTemplate_ReplacesEntryAndClosesDialog()
        {
            ReportTemplateComponentTrackingApiConn apiConnection = new(
                [
                    CreateTemplate(1, "Original template", ReportType.Rules, ownerId: 50)
                ]);

            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                apiConnection);

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            wrapper.WaitForAssertion(() => Assert.That(wrapper.FindComponent<ReportTemplateComponent>().Instance.reportTemplates, Has.Count.EqualTo(1)));

            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            ReportTemplate existingTemplate = component.reportTemplates[0];
            existingTemplate.Name = "Updated template";
            existingTemplate.Comment = "T0100";
            SimulatedUserConfig.DummyTranslate["T0100"] = "Translated template comment";

            InvokePrivateMethod(component, "EditTemplate", existingTemplate);
            await (Task)InvokePrivateMethod(component, "Save")!;

            Assert.Multiple(() =>
            {
                Assert.That(component.reportTemplates, Has.Count.EqualTo(1));
                Assert.That(component.reportTemplates[0].Name, Is.EqualTo("Updated template"));
                Assert.That(component.reportTemplates[0].Comment, Is.EqualTo("Translated template comment"));
                Assert.That(GetPrivateField<bool>(component, "ShowUpdateTemplateDialog"), Is.False);
                Assert.That(apiConnection.UpdateTemplateCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ReportTemplateComponent_DeleteTemplate_RemovesEntry()
        {
            ReportTemplateComponentTrackingApiConn apiConnection = new(
                [
                    CreateTemplate(1, "Delete me", ReportType.Rules, ownerId: 50)
                ]);

            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                apiConnection);

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            wrapper.WaitForAssertion(() => Assert.That(wrapper.FindComponent<ReportTemplateComponent>().Instance.reportTemplates, Has.Count.EqualTo(1)));

            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            component.reportTemplateInEdit = component.reportTemplates[0];
            SetPrivateField(component, "ShowDeleteTemplateDialog", true);

            await (Task)InvokePrivateMethod(component, "DeleteTemplate")!;

            Assert.Multiple(() =>
            {
                Assert.That(component.reportTemplates, Is.Empty);
                Assert.That(GetPrivateField<bool>(component, "ShowDeleteTemplateDialog"), Is.False);
                Assert.That(apiConnection.DeleteTemplateCalls, Is.EqualTo(1));
            });
        }

        private static BunitContext CreateContext(AuthenticationStateProvider authStateProvider, UserConfig userConfig, ApiConnection apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton(authStateProvider);
            context.Services.AddSingleton(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton(userConfig);
            return context;
        }

        private static SimulatedUserConfig CreateUserConfig(IEnumerable<string> roles, IEnumerable<int> ownerships)
        {
            return new SimulatedUserConfig
            {
                User =
                {
                    DbId = 50,
                    Language = "English",
                    Roles = roles.ToList(),
                    Ownerships = ownerships.ToList()
                }
            };
        }

        private static ReportTemplate CreateTemplate(int id, string name, ReportType reportType, int ownerId = 0)
        {
            return new ReportTemplate
            {
                Id = id,
                Name = name,
                Filter = "",
                Comment = "",
                ReportParams = new ReportParams
                {
                    ReportType = (int)reportType,
                    ModellingFilter = new()
                    {
                        SelectedOwner = new FwoOwner { Id = ownerId, Name = $"Owner {ownerId}" }
                    }
                }
            };
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            return (T)(instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName));
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(instance, value);
        }

        private static object? InvokePrivateMethod(object instance, string methodName, params object[] parameters)
        {
            return instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(instance, parameters);
        }
    }

    internal sealed class ReportTemplateComponentTrackingApiConn : SimulatedApiConnection
    {
        private readonly List<ReportTemplate> templates;

        public int GetTemplatesCalls { get; private set; }
        public int AddTemplateCalls { get; private set; }
        public int UpdateTemplateCalls { get; private set; }
        public int DeleteTemplateCalls { get; private set; }

        public ReportTemplateComponentTrackingApiConn(List<ReportTemplate> templates)
        {
            this.templates = templates;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
            }

            if (typeof(QueryResponseType) == typeof(ReportTemplate[]) && query == ReportQueries.getReportTemplates)
            {
                GetTemplatesCalls++;
                return Task.FromResult((QueryResponseType)(object)templates.ToArray());
            }

            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ReportQueries.addReportTemplate)
            {
                AddTemplateCalls++;
                string name = GetAnonymousProperty<string>(variables!, "reportTemplateName");
                int ownerId = GetAnonymousProperty<int>(variables!, "reportTemplateOwner");
                int reportType = GetAnonymousProperty<ReportParams>(variables!, "reportParameters").ReportType;
                ReportTemplate createdTemplate = new()
                {
                    Id = 99,
                    Name = name,
                    TemplateOwningUserId = ownerId,
                    ReportParams = new ReportParams { ReportType = reportType }
                };
                templates.Add(createdTemplate);
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = new ReturnId[] { new ReturnId { NewId = 99 } }
                });
            }

            if (typeof(QueryResponseType) == typeof(object) && query == ReportQueries.updateReportTemplate)
            {
                UpdateTemplateCalls++;
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            if (typeof(QueryResponseType) == typeof(ReturnId) && query == ReportQueries.deleteReportTemplate)
            {
                DeleteTemplateCalls++;
                int id = GetAnonymousProperty<int>(variables!, "reportTemplateId");
                templates.RemoveAll(item => item.Id == id);
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }

            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }

        private static T GetAnonymousProperty<T>(object instance, string propertyName)
        {
            System.Reflection.PropertyInfo? property = instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, propertyName);
            }

            return (T)property.GetValue(instance)!;
        }
    }
}

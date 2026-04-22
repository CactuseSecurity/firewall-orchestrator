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

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportTemplateComponentTest
    {
        private sealed class ReportTemplateComponentTestApiConn(List<ReportTemplate> templates, List<FwoOwner> owners) : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
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
            SimulatedUserConfig.DummyTranslate.TryAdd("save_template", "Save template");
            SimulatedUserConfig.DummyTranslate.TryAdd("edit_template", "Edit template");
            SimulatedUserConfig.DummyTranslate.TryAdd("delete_template", "Delete template");
            SimulatedUserConfig.DummyTranslate.TryAdd("open", "Open");
            SimulatedUserConfig.DummyTranslate.TryAdd("Days", "Days");
            SimulatedUserConfig.DummyTranslate.TryAdd("Weeks", "Wochen");
            SimulatedUserConfig.DummyTranslate.TryAdd("Months", "Months");
            SimulatedUserConfig.DummyTranslate.TryAdd("Years", "Years");
        }

        [Test]
        public void ReportTemplateComponent_ModellerOnly_ShowsOnlyAllowedTemplates()
        {
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Modeller),
                CreateUserConfig([Roles.Modeller], [11]),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Connections template", ReportType.Connections, ownerId: 11),
                    CreateTemplate(2, "Compliance template", ReportType.ComplianceReport),
                    CreateTemplate(3, "Workflow template", ReportType.TicketReport),
                    CreateTemplate(4, "Certificate template", ReportType.RecertificationEvent)
                ],
                [new FwoOwner { Id = 11, Name = "Owned App" }]));

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
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Auditor),
                CreateUserConfig([Roles.Auditor], [11]),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Compliance template", ReportType.ComplianceReport),
                    CreateTemplate(2, "Workflow template", ReportType.TicketReport),
                    CreateTemplate(3, "Certificate template", ReportType.RecertificationEvent)
                ],
                [new FwoOwner { Id = 11, Name = "Owned App" }]));

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
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Modeller),
                CreateUserConfig([Roles.Modeller], [11]),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Owned connections template", ReportType.Connections, ownerId: 11),
                    CreateTemplate(2, "Foreign connections template", ReportType.Connections, ownerId: 12)
                ],
                [new FwoOwner { Id = 11, Name = "Owned App" }]));

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
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig([Roles.Reporter], []),
                new ReportTemplateComponentTestApiConn(
                [
                    CreateTemplate(1, "Rules template", ReportType.Rules),
                    CreateTemplate(2, "Workflow template", ReportType.TicketReport)
                ],
                []));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());

            wrapper.WaitForAssertion(() =>
            {
                string markup = wrapper.Markup;
                Assert.That(markup, Does.Contain("Rules template"));
                Assert.That(markup, Does.Not.Contain("Workflow template"));
            });
        }

        [Test]
        public void ReportTemplateComponent_DecodeAndRecodeComment_PreserveTemplateKey()
        {
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig([Roles.Reporter], []),
                new ReportTemplateComponentTestApiConn([], []));

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
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig([Roles.Reporter], []),
                new ReportTemplateComponentTestApiConn([], []));

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
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig([Roles.Reporter], []),
                new ReportTemplateComponentTestApiConn([], []));

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
            SimulatedUserConfig userConfig = CreateUserConfig([Roles.Reporter], []);
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                userConfig,
                new ReportTemplateComponentTestApiConn([], []));

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
            using Bunit.TestContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig([Roles.Reporter], []),
                new ReportTemplateComponentTestApiConn([], []));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            SetPrivateField(component, "reportTypeInEdit", ReportType.Changes);
            component.reportTemplateInEdit.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Fixeddates;
            component.reportTemplateInEdit.ReportParams.TimeFilter.OpenStart = true;
            component.reportTemplateInEdit.ReportParams.TimeFilter.OpenEnd = true;

            string displayTime = component.DisplayTime();

            Assert.That(displayTime, Is.EqualTo("Open"));
        }

        private static Bunit.TestContext CreateContext(AuthenticationStateProvider authStateProvider, UserConfig userConfig, ApiConnection apiConnection)
        {
            Bunit.TestContext context = new();
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

        private static SimulatedUserConfig CreateUserConfig(List<string> roles, List<int> ownerships)
        {
            return new SimulatedUserConfig
            {
                User =
                {
                    DbId = 50,
                    Language = "English",
                    Roles = roles,
                    Ownerships = ownerships
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
}

using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
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
        private static readonly List<ReportTemplate> kSingleExistingRulesTemplate = [CreateTemplate(1, "Existing template", ReportType.Rules, ownerId: 50)];
        private static readonly List<ReportTemplate> kSingleOriginalRulesTemplate = [CreateTemplate(1, "Original template", ReportType.Rules, ownerId: 50, templateOwnerId: 50)];
        private static readonly List<ReportTemplate> kSingleDeleteRulesTemplate = [CreateTemplate(1, "Delete me", ReportType.Rules, ownerId: 50, templateOwnerId: 50)];

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

        [Test]
        public void ReportTemplateComponent_ModellerOnly_ShowsOnlyAllowedTemplates()
        {
            List<ReportTemplate> templates =
            [
                CreateTemplate(1, "Connections template", ReportType.Connections, ownerId: 11),
                CreateTemplate(2, "Compliance template", ReportType.ComplianceReport),
                CreateTemplate(3, "Workflow template", ReportType.TicketReport),
                CreateTemplate(4, "Certificate template", ReportType.RecertificationEvent)
            ];
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Modeller),
                CreateUserConfig(kModellerRoles, kOwnership11),
                new ReportTemplateComponentTestApiConn(templates, kOwnedAppOwners));

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
            List<ReportTemplate> templates =
            [
                CreateTemplate(1, "Compliance template", ReportType.ComplianceReport),
                CreateTemplate(2, "Workflow template", ReportType.TicketReport),
                CreateTemplate(3, "Certificate template", ReportType.RecertificationEvent)
            ];
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Auditor),
                CreateUserConfig(kAuditorRoles, kOwnership11),
                new ReportTemplateComponentTestApiConn(templates, kOwnedAppOwners));

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
            List<ReportTemplate> templates =
            [
                CreateTemplate(1, "Owned connections template", ReportType.Connections, ownerId: 11),
                CreateTemplate(2, "Foreign connections template", ReportType.Connections, ownerId: 12)
            ];
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Modeller),
                CreateUserConfig(kModellerRoles, kOwnership11),
                new ReportTemplateComponentTestApiConn(templates, kOwnedAppOwners));

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
            List<ReportTemplate> templates =
            [
                CreateTemplate(1, "Rules template", ReportType.Rules),
                CreateTemplate(2, "Workflow template", ReportType.TicketReport),
                CreateTemplate(3, "App rules template", ReportType.AppRules),
                CreateTemplate(4, "Owners template", ReportType.Owners)
            ];
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(templates, kEmptyOwners));

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
        public void ReportTemplateComponent_RefreshAvailableAddInfoNames_DeduplicatesAndSortsKeys()
        {
            MethodInfo? method = typeof(ReportTemplateComponent).GetMethod("RefreshAvailableAddInfoNames", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            ReportTemplateComponent component = new();
            SetPrivateField(component, "recertOwnerList", new List<FwoOwner>
            {
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
            });

            method!.Invoke(component, null);

            List<string>? availableAddInfoNames = GetPrivateField<List<string>?>(component, "availableAddInfoNames");

            Assert.That(availableAddInfoNames, Is.EqualTo(new List<string> { "business_unit", "region", "service_tier" }));
        }

        [Test]
        public async Task ReportTemplateComponent_UsesDedicatedWorkflowAddInfoPrefixInTemplateDialog()
        {
            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            IRenderedComponent<ReportTemplateComponent> component = wrapper.FindComponent<ReportTemplateComponent>();
            ReportTemplate workflowTemplate = CreateTemplate(1, "Workflow template", ReportType.TicketReport);

            component.Instance.NewTemplate(workflowTemplate);
            component.Render();

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("templateWorkflowAddInfo-summary"));
                Assert.That(component.Markup, Does.Not.Contain("workflowAddInfo-summary"));
            });
        }

        [Test]
        public void ReportTemplateComponent_RefreshAvailableAddInfoNames_KeepsNullWhenNoNamesExist()
        {
            using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTestApiConn(kEmptyTemplates, kEmptyOwners));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>());
            IRenderedComponent<ReportTemplateComponent> component = wrapper.FindComponent<ReportTemplateComponent>();
            SetPrivateField(component.Instance, "recertOwnerList", new List<FwoOwner>());
            InvokePrivateMethod(component.Instance, "RefreshAvailableAddInfoNames");

            Assert.That(GetPrivateField<List<string>?>(component.Instance, "availableAddInfoNames"), Is.Null);
        }

        [Test]
        public async Task ReportTemplateComponent_DisplayTime_ShowsUntilForOpenStart()
        {
            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                CreateUserConfig(kReporterRoles, kEmptyOwnerships),
                new ReportTemplateComponentTrackingApiConn(kEmptyTemplates));

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
                new ReportTemplateComponentTrackingApiConn(kEmptyTemplates));

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
            ReportTemplateComponentTrackingApiConn apiConnection = new(kSingleExistingRulesTemplate);

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
            ReportTemplateComponentTrackingApiConn apiConnection = new(kSingleOriginalRulesTemplate);

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
            ReportTemplateComponentTrackingApiConn apiConnection = new(kSingleDeleteRulesTemplate);

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

        [Test]
        public async Task ReportTemplateComponent_AuditorCanOpenForeignTemplateButCannotSaveOrDeleteIt()
        {
            ReportTemplateComponentTrackingApiConn apiConnection = new(kForeignAndOwnRulesTemplates);
            SimulatedUserConfig userConfig = CreateUserConfig(kAuditorRoles, kEmptyOwnerships);
            userConfig.ModIconify = false;

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Auditor), userConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportTemplateComponent>(child => child.Add(component => component.OnTemplateLoad, _ => { })));
            wrapper.WaitForAssertion(() => Assert.That(wrapper.FindComponent<ReportTemplateComponent>().Instance.reportTemplates, Has.Count.EqualTo(2)));

            ReportTemplateComponent component = wrapper.FindComponent<ReportTemplateComponent>().Instance;
            ReportTemplate foreignTemplate = component.reportTemplates.Single(template => template.TemplateOwningUserId == kForeignOwnerId);
            List<AngleSharp.Dom.IElement> editButtons = [.. wrapper.FindAll("button").Where(button => button.TextContent.Trim() == userConfig.GetText("edit"))];
            List<AngleSharp.Dom.IElement> deleteButtons = [.. wrapper.FindAll("button").Where(button => button.TextContent.Trim() == userConfig.GetText("delete"))];

            Assert.Multiple(() =>
            {
                Assert.That(editButtons, Has.Count.EqualTo(2));
                Assert.That(editButtons.All(button => !button.HasAttribute("disabled")), Is.True);
                Assert.That(deleteButtons.Count(button => button.HasAttribute("disabled")), Is.EqualTo(1));
                Assert.That(InvokePrivateMethod(component, "CanModifyTemplate", foreignTemplate), Is.False);
            });

            await wrapper.InvokeAsync(() => InvokePrivateMethod(component, "EditTemplate", foreignTemplate));

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "ShowUpdateTemplateDialog"), Is.True);
                Assert.That(InvokePrivateMethod(component, "CanSaveTemplateInEdit"), Is.False);
            });

            await (Task)InvokePrivateMethod(component, "Save")!;
            component.reportTemplateInEdit = foreignTemplate;
            await (Task)InvokePrivateMethod(component, "DeleteTemplate")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateTemplateCalls, Is.EqualTo(0));
                Assert.That(apiConnection.DeleteTemplateCalls, Is.EqualTo(0));
            });
        }

        [Test]
        public void ReportTemplateComponent_OwnerAndAdminCanModifyAndNewTemplateCanBeSaved()
        {
            ReportTemplateComponent ownerComponent = new();
            ReportTemplateComponent adminComponent = new();
            SetInjectedUserConfig(ownerComponent, CreateUserConfig(kAuditorRoles, kEmptyOwnerships));
            SetInjectedUserConfig(adminComponent, CreateUserConfig(kAdminRoles, kEmptyOwnerships));
            ReportTemplate ownTemplate = CreateTemplate(1, "Own", ReportType.Rules, templateOwnerId: 50);
            ReportTemplate foreignTemplate = CreateTemplate(2, "Foreign", ReportType.Rules, templateOwnerId: kForeignOwnerId);

            SetPrivateField(ownerComponent, "ShowAddTemplateDialog", true);
            ownerComponent.reportTemplateInEdit = foreignTemplate;

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateMethod(ownerComponent, "CanModifyTemplate", ownTemplate), Is.True);
                Assert.That(InvokePrivateMethod(ownerComponent, "CanModifyTemplate", foreignTemplate), Is.False);
                Assert.That(InvokePrivateMethod(ownerComponent, "CanSaveTemplateInEdit"), Is.True);
                Assert.That(InvokePrivateMethod(adminComponent, "CanModifyTemplate", foreignTemplate), Is.True);
            });
        }

        private const string kTranslatedCommentKey = "T0199";
        private const string kTranslatedComment = "Shipped template description";
        private const int kTenantId = 4;

        private sealed record DisplayedMessage(Exception? Exception, string Title, string Text);

        /// <summary>
        /// User config that translates the comment key of a shipped template, without touching the
        /// translations shared by all tests.
        /// </summary>
        private sealed class TranslatingUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key == kTranslatedCommentKey ? kTranslatedComment : base.GetText(key);
            }
        }

        private sealed class TemplateDialogApiConnection : SimulatedApiConnection
        {
            public List<ReportTemplate> Templates { get; set; } = [];
            public List<FwoOwner> Owners { get; set; } = [];
            public HashSet<string> FailingQueries { get; } = [];
            public bool AddReturnsIds { get; set; } = true;
            public int DeleteAffectedRows { get; set; } = 1;
            public object? AddVariables { get; private set; }
            public object? DeleteVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException("simulated api failure");
                }
                if (query == ReportQueries.getReportTemplates)
                {
                    return Task.FromResult((QueryResponseType)(object)Templates.ToArray());
                }
                if (query == OwnerQueries.getOwners)
                {
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }
                if (query == ReportQueries.addReportTemplate)
                {
                    AddVariables = variables;
                    ReturnIdWrapper wrapper = new() { ReturnIds = AddReturnsIds ? [new ReturnId { NewId = 99 }] : null };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
                }
                if (query == ReportQueries.deleteReportTemplate)
                {
                    DeleteVariables = variables;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = DeleteAffectedRows });
                }
                return Task.FromResult(CreateEmptyResponse<QueryResponseType>());
            }

            private static QueryResponseType CreateEmptyResponse<QueryResponseType>()
            {
                Type responseType = typeof(QueryResponseType);
                if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return Activator.CreateInstance<QueryResponseType>();
                }
                if (responseType.IsArray)
                {
                    return (QueryResponseType)(object)Array.CreateInstance(responseType.GetElementType()!, 0);
                }
                return default!;
            }
        }

        private readonly List<DisplayedMessage> displayedMessages = [];
        private readonly List<ReportTemplate> loadedTemplates = [];

        private IRenderedComponent<ReportTemplateComponent> RenderTemplates(BunitContext context, bool withLoadAction = true)
        {
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (exception, title, text, _) => displayedMessages.Add(new DisplayedMessage(exception, title, text)))
                    .AddChildContent<ReportTemplateComponent>(component =>
                    {
                        if (withLoadAction)
                        {
                            component.Add(p => p.OnTemplateLoad, loadedTemplates.Add);
                        }
                    })));
            return wrapper.FindComponent<ReportTemplateComponent>();
        }

        private static BunitContext CreateAdminContext(TemplateDialogApiConnection apiConnection, SimulatedUserConfig? userConfig = null)
        {
            return CreateContext(new MonitoringTestAuthStateProvider(Roles.Admin), userConfig ?? CreateUserConfig(kAdminRoles, kEmptyOwnerships), apiConnection);
        }

        private static async Task OpenDialog(IRenderedComponent<ReportTemplateComponent> component, ReportTemplate template)
        {
            await component.InvokeAsync(() => component.Instance.NewTemplate(template));
            component.Render();
        }

        private static IElement ButtonWithIcon(IRenderedComponent<ReportTemplateComponent> component, string icon)
        {
            return component.FindAll("button").First(button => button.QuerySelector("span")?.ClassName == icon);
        }

        private static bool ShowsComponentOfType<TComponent>(IRenderedComponent<ReportTemplateComponent> component) where TComponent : IComponent
        {
            return component.FindComponents<TComponent>().Count > 0;
        }

        /// <summary>
        /// Whether the dialog currently shows a component of the given type, for test cases that name it as data.
        /// </summary>
        private static bool ShowsComponent(IRenderedComponent<ReportTemplateComponent> component, Type componentType)
        {
            MethodInfo showsComponentOfType = typeof(UiReportTemplateComponentTest).GetMethod(nameof(ShowsComponentOfType), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{nameof(ShowsComponentOfType)} not found");
            return (bool)showsComponentOfType.MakeGenericMethod(componentType).Invoke(null, [component])!;
        }

        /// <summary>
        /// The row of the template dialog whose first label carries the given title.
        /// </summary>
        private static IElement DialogRow(BunitContext context, IRenderedComponent<ReportTemplateComponent> component, string labelKey)
        {
            string labelText = Text(context, labelKey);
            IElement label = component.FindAll("form label").First(candidate => candidate.TextContent.Trim() == labelText + ":");
            return label.ParentElement!;
        }

        /// <summary>
        /// Text shown next to the label with the given title in the template dialog.
        /// </summary>
        private static string DialogValue(BunitContext context, IRenderedComponent<ReportTemplateComponent> component, string labelKey)
        {
            return DialogRow(context, component, labelKey).QuerySelectorAll("label")[1].TextContent.Trim();
        }

        private static string Text(BunitContext context, string key)
        {
            return context.Services.GetRequiredService<UserConfig>().GetText(key);
        }

        [Test]
        public async Task LoadingATemplateHandsItToThePage()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            ButtonWithIcon(component, Icons.Load).Click();

            Assert.That(loadedTemplates.Select(template => template.Name), Is.EqualTo((List<string>)["Rules template"]));
        }

        [Test]
        public async Task WithoutLoadActionTheTemplatesOfferNoActions()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            await using BunitContext context = CreateAdminContext(apiConnection);

            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context, withLoadAction: false);

            Assert.That(component.FindAll("tbody button"), Is.Empty);
            Assert.That(component.Markup, Does.Contain("Rules template"));
        }

        [Test]
        public async Task EditingATemplateOpensTheDialogWithItsValues()
        {
            ReportTemplate template = CreateTemplate(1, "Rules template", ReportType.Rules);
            template.Filter = "src=1.2.3.4";
            TemplateDialogApiConnection apiConnection = new() { Templates = [template] };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            ButtonWithIcon(component, Icons.Edit).Click();

            Assert.That(component.FindComponent<PopUp>().Instance.Show, Is.True);
            Assert.That(component.Find("#saveTemplateName").GetAttribute("value"), Is.EqualTo("Rules template"));
            Assert.That(component.Find("#saveTemplateFilterLineTextarea").GetAttribute("value"), Is.EqualTo("src=1.2.3.4"));
        }

        [Test]
        public async Task CancellingTheDialogClosesIt()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ButtonWithIcon(component, Icons.Edit).Click();

            component.FindComponent<PopUp>().FindAll("button.btn-secondary")
                .Last(button => button.QuerySelector("span")?.ClassName == Icons.Cancel).Click();

            Assert.That(component.FindComponent<PopUp>().Instance.Show, Is.False);
        }

        [Test]
        public async Task DeletingATemplateAsksForConfirmationFirst()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            ButtonWithIcon(component, Icons.Delete).Click();

            ConfirmDelete confirmDelete = component.FindComponent<ConfirmDelete>().Instance;
            Assert.That(confirmDelete.Display, Is.True);
            Assert.That(confirmDelete.DeleteMessage, Does.Contain("Rules template"));
            Assert.That(apiConnection.DeleteVariables, Is.Null);
        }

        [Test]
        public async Task ConfirmingTheDeletionRemovesTheTemplate()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ButtonWithIcon(component, Icons.Delete).Click();

            component.FindComponent<ConfirmDelete>().Find("button.btn-danger").Click();

            component.WaitForAssertion(() => Assert.That(component.Instance.reportTemplates, Is.Empty));
            Assert.That(displayedMessages, Is.Empty);
        }

        [Test]
        public async Task ADeletionWithoutEffectIsReported()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)], DeleteAffectedRows = 0 };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ButtonWithIcon(component, Icons.Delete).Click();

            component.FindComponent<ConfirmDelete>().Find("button.btn-danger").Click();

            component.WaitForAssertion(() => Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[Text(context, "E1005")])));
            Assert.That(component.Instance.reportTemplates, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AFailingDeletionIsReported()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            apiConnection.FailingQueries.Add(ReportQueries.deleteReportTemplate);
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ButtonWithIcon(component, Icons.Delete).Click();

            component.FindComponent<ConfirmDelete>().Find("button.btn-danger").Click();

            component.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Title, Is.EqualTo(Text(context, "delete_template")));
        }

        [Test]
        public async Task ShippedTemplatesShowTheirTranslatedCommentAndAreHighlighted()
        {
            ReportTemplate template = CreateTemplate(1, "Shipped template", ReportType.Rules);
            template.Comment = kTranslatedCommentKey;
            ReportTemplate ownTemplate = CreateTemplate(2, "Own template", ReportType.Rules, templateOwnerId: 50);
            ownTemplate.Comment = "my own words";
            TemplateDialogApiConnection apiConnection = new() { Templates = [template, ownTemplate] };
            TranslatingUserConfig userConfig = new() { User = { DbId = 50, Roles = [Roles.Admin] } };
            await using BunitContext context = CreateAdminContext(apiConnection, userConfig);

            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            Assert.That(component.Markup, Does.Contain(kTranslatedComment));
            Assert.That(component.Markup, Does.Not.Contain(kTranslatedCommentKey));
            Assert.That(component.Markup, Does.Contain("my own words"));
            // shipped templates are owned by nobody
            Assert.That(component.FindAll("tbody b").Select(name => name.TextContent), Is.EqualTo((List<string>)["Shipped template"]));
        }

        [TestCase(ReportType.Recertification, typeof(ReportRecertParamSelection))]
        [TestCase(ReportType.Owners, typeof(ReportOwnerParamSelection))]
        [TestCase(ReportType.OwnerRecertification, typeof(ReportOwnerRecertParamSelection))]
        [TestCase(ReportType.OwnerRecertification, typeof(ReportRecertParamSelection))]
        [TestCase(ReportType.Connections, typeof(Dropdown<FwoOwner>))]
        [TestCase(ReportType.AppRules, typeof(ReportAppRuleParamSelection))]
        [TestCase(ReportType.VarianceAnalysis, typeof(ReportVarianceParamSelection))]
        [TestCase(ReportType.RecertEventReport, typeof(Dropdown<FwoOwner>))]
        public async Task TheDialogShowsTheParametersOfTheTemplatesReportType(ReportType reportType, Type expectedComponent)
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            await OpenDialog(component, CreateTemplate(0, "new", reportType));

            Assert.That(ShowsComponent(component, expectedComponent), Is.True);
        }

        [TestCase(ReportType.UnusedRules, "#saveTemplateUnused")]
        [TestCase(ReportType.ComplianceReport, "#saveShowNonImpactRules")]
        [TestCase(ReportType.ComplianceDiffReport, "#saveDiffReference")]
        [TestCase(ReportType.ComplianceDiffReport, "#saveShowNonImpactRules")]
        [TestCase(ReportType.Changes, "#saveTemplateIncludeObjectChanges")]
        public async Task TheDialogShowsTheSettingsOfTheTemplatesReportType(ReportType reportType, string selector)
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            await OpenDialog(component, CreateTemplate(0, "new", reportType));

            Assert.That(component.FindAll(selector), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task TheComplianceReportHasNoDiffInterval()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            await OpenDialog(component, CreateTemplate(0, "new", ReportType.ComplianceReport));

            Assert.That(component.FindAll("#saveDiffReference"), Is.Empty);
        }

        [Test]
        public async Task TheOwnerChooserOffersTheOwnApps()
        {
            TemplateDialogApiConnection apiConnection = new() { Owners = [new FwoOwner { Id = 11, Name = "Owned App", ExtAppId = "APP-11" }] };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            await OpenDialog(component, CreateTemplate(0, "new", ReportType.Connections));

            component.FindComponent<Dropdown<FwoOwner>>().Find("input").Focus();

            Assert.That(component.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim()), Does.Contain("Owned App (APP-11)"));
        }

        [Test]
        public async Task ATemplateWithTenantFilterShowsTheTenant()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Rules);
            template.ReportParams.TenantFilter = new TenantFilter(new Tenant { Id = kTenantId });

            await OpenDialog(component, template);

            Assert.That(DialogValue(context, component, "tenant"), Is.EqualTo(kTenantId.ToString()));
        }

        [Test]
        public async Task ADeviceReportTemplateListsItsSelectedDevices()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Rules);
            template.ReportParams.DeviceFilter = new DeviceFilter(
                [new ManagementSelect { Id = 1, Name = "management", Devices = [new DeviceSelect { Id = 11, Name = "gw1", Selected = true }, new DeviceSelect { Id = 12, Name = "gw2" }] }]);

            await OpenDialog(component, template);

            Assert.That(DialogValue(context, component, "devices"), Is.EqualTo("gw1"));
        }

        [TestCase(ReportType.AppRules)]
        [TestCase(ReportType.Recertification)]
        public async Task AManagementLevelTemplateListsItsSelectedManagements(ReportType reportType)
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", reportType);
            template.ReportParams.DeviceFilter = new DeviceFilter(
            [
                new ManagementSelect { Id = 1, Name = "first management", Devices = [new DeviceSelect { Id = 11, Selected = true }] },
                new ManagementSelect { Id = 2, Name = "second management", Devices = [new DeviceSelect { Id = 21 }] }
            ]);

            await OpenDialog(component, template);

            Assert.That(DialogValue(context, component, "managements"), Is.EqualTo("first management"));
        }

        [Test]
        public async Task AManagementRulebasesViewTemplateListsItsSelectedRulebases()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Rules);
            template.ReportParams.ManagementRulebaseView = true;
            template.ReportParams.SelectedRulebases =
            [
                new SelectedRulebase { ManagementName = "management", RulebaseName = "first" },
                new SelectedRulebase { ManagementName = "management", RulebaseName = "second" }
            ];

            await OpenDialog(component, template);

            Assert.That(DialogValue(context, component, "rulebases"), Is.EqualTo("management: first, management: second"));
        }

        [Test]
        public async Task TheDeviceSelectionOfATemplateCanBeChanged()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            await OpenDialog(component, CreateTemplate(0, "new", ReportType.Rules));

            DialogRow(context, component, "devices").QuerySelector("button")!.Click();

            Assert.That(component.FindComponent<ReportTemplateSelectDevice>().Instance.Display, Is.True);
        }

        [Test]
        public async Task TheReportTimeOfATemplateIsChangedOnACopy()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Rules);
            await OpenDialog(component, template);

            DialogRow(context, component, "report_time").QuerySelector("button")!.Click();

            ReportSelectTime selectTime = component.FindComponent<ReportSelectTime>().Instance;
            Assert.That(selectTime.Display, Is.True);
            Assert.That(selectTime.ActTimeFilter, Is.Not.SameAs(template.ReportParams.TimeFilter));
            Assert.That(selectTime.ActTimeFilter.TimeShortcut, Is.EqualTo(template.ReportParams.TimeFilter.TimeShortcut));
            Assert.That(await component.InvokeAsync(() => selectTime.DisplayTime()), Is.True);
        }

        [Test]
        public async Task TheTemplateListCanBeCollapsedAndExpanded()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            await component.InvokeAsync(component.Instance.Collapse);
            bool collapsed = component.FindAll("div.collapse").Count == 1;
            await component.InvokeAsync(component.Instance.Uncollapse);

            Assert.That(collapsed, Is.True);
            Assert.That(component.FindAll("div.collapse"), Is.Empty);
        }

        [Test]
        public async Task AFailingOwnerFetchLeavesTheTemplatesEmptyAndIsReported()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            apiConnection.FailingQueries.Add(OwnerQueries.getOwners);
            await using BunitContext context = CreateAdminContext(apiConnection);

            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            Assert.That(component.Instance.reportTemplates, Is.Empty);
            Assert.That(displayedMessages.Select(message => message.Title), Is.EqualTo((List<string>)[Text(context, "template_fetch")]));
        }

        [Test]
        public async Task AFailingTemplateFetchIsReported()
        {
            TemplateDialogApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(ReportQueries.getReportTemplates);
            await using BunitContext context = CreateAdminContext(apiConnection);

            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);

            Assert.That(component.Instance.reportTemplates, Is.Empty);
            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task SavingANewTemplateWithSurroundingBlanksSaysItWasShortened()
        {
            TemplateDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            await OpenDialog(component, CreateTemplate(0, "  new template  ", ReportType.Rules));

            ButtonWithIcon(component, Icons.Save).Click();

            component.WaitForAssertion(() => Assert.That(apiConnection.AddVariables, Is.Not.Null));
            Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[Text(context, "U0001")]));
            Assert.That(component.Instance.reportTemplateInEdit.Name, Is.EqualTo("new template"));
            Assert.That(component.Instance.reportTemplateInEdit.Id, Is.EqualTo(99));
        }

        [Test]
        public async Task SavingANewTemplateWithoutReturnedIdIsReportedAndKeepsTheDialogOpen()
        {
            TemplateDialogApiConnection apiConnection = new() { AddReturnsIds = false };
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            await OpenDialog(component, CreateTemplate(0, "new template", ReportType.Rules));

            ButtonWithIcon(component, Icons.Save).Click();

            component.WaitForAssertion(() => Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[Text(context, "E1004")])));
            Assert.That(component.FindComponent<PopUp>().Instance.Show, Is.True);
        }

        [Test]
        public async Task AFailingSaveOfANewTemplateIsReported()
        {
            TemplateDialogApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(ReportQueries.addReportTemplate);
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            await OpenDialog(component, CreateTemplate(0, "new template", ReportType.Rules));

            ButtonWithIcon(component, Icons.Save).Click();

            component.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Title, Is.EqualTo(Text(context, "save_template")));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AFailingUpdateIsReportedAndKeepsTheDialogOpen()
        {
            TemplateDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(1, "Rules template", ReportType.Rules)] };
            apiConnection.FailingQueries.Add(ReportQueries.updateReportTemplate);
            await using BunitContext context = CreateAdminContext(apiConnection);
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ButtonWithIcon(component, Icons.Edit).Click();

            ButtonWithIcon(component, Icons.Save).Click();

            component.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Title, Is.EqualTo(Text(context, "edit_template")));
            Assert.That(component.FindComponent<PopUp>().Instance.Show, Is.True);
        }

        [Test]
        public async Task TheReportTimeOfAChangesTemplateShowsItsShortcut()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Changes);
            template.ReportParams.TimeFilter.TimeRangeType = TimeRangeType.Shortcut;
            template.ReportParams.TimeFilter.TimeRangeShortcut = "yesterday";

            await OpenDialog(component, template);

            Assert.That(component.Instance.DisplayTime(), Is.EqualTo("yesterday"));
        }

        [Test]
        public async Task TheReportTimeOfATicketChangesTemplateShowsItsFixedDates()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.TicketChangeReport);
            TimeFilter timeFilter = template.ReportParams.TimeFilter;
            timeFilter.TimeRangeType = TimeRangeType.Fixeddates;
            timeFilter.OpenStart = false;
            timeFilter.OpenEnd = false;
            timeFilter.StartTime = new DateTime(2026, 1, 1, 8, 0, 0);
            timeFilter.EndTime = new DateTime(2026, 2, 1, 8, 0, 0);

            await OpenDialog(component, template);

            Assert.That(component.Instance.DisplayTime(), Is.EqualTo($"{timeFilter.StartTime} - {timeFilter.EndTime}"));
        }

        [Test]
        public async Task AnUnknownTimeRangeTypeShowsNoTime()
        {
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Changes);
            template.ReportParams.TimeFilter.TimeRangeType = (TimeRangeType)99;

            await OpenDialog(component, template);

            Assert.That(component.Instance.DisplayTime(), Is.Empty);
        }

        [Test]
        public async Task TheReportTimeOfARulesTemplateShowsItsShortcutOrTime()
        {
            DateTime reportTime = new(2026, 3, 4, 5, 6, 0);
            await using BunitContext context = CreateAdminContext(new TemplateDialogApiConnection());
            IRenderedComponent<ReportTemplateComponent> component = RenderTemplates(context);
            ReportTemplate template = CreateTemplate(0, "new", ReportType.Rules);
            template.ReportParams.TimeFilter.IsShortcut = true;
            template.ReportParams.TimeFilter.TimeShortcut = "now";
            await OpenDialog(component, template);
            string shortcutText = component.Instance.DisplayTime();

            template.ReportParams.TimeFilter.IsShortcut = false;
            template.ReportParams.TimeFilter.ReportTime = reportTime;

            Assert.That(shortcutText, Is.EqualTo("now"));
            Assert.That(component.Instance.DisplayTime(), Is.EqualTo(reportTime.ToString()));
        }

        private const int kForeignOwnerId = 77;
        private static readonly List<string> kAdminRoles = [Roles.Admin];
        private static readonly List<ReportTemplate> kForeignAndOwnRulesTemplates =
        [
            CreateTemplate(1, "Own template", ReportType.Rules, templateOwnerId: 50),
            CreateTemplate(2, "Foreign template", ReportType.Rules, templateOwnerId: kForeignOwnerId)
        ];

        private static void SetInjectedUserConfig(ReportTemplateComponent component, UserConfig userConfig)
        {
            PropertyInfo property = typeof(ReportTemplateComponent).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .First(candidate => candidate.PropertyType == typeof(UserConfig));
            property.SetValue(component, userConfig);
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

        private static ReportTemplate CreateTemplate(int id, string name, ReportType reportType, int ownerId = 0, int templateOwnerId = 0)
        {
            return new ReportTemplate
            {
                Id = id,
                Name = name,
                TemplateOwningUserId = templateOwnerId,
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
            Type originalType = instance.GetType();
            Type? type = originalType;
            bool found = false;
            object? value = null;

            while (type != null && !found)
            {
                FieldInfo? fieldInfo = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    value = fieldInfo.GetValue(instance);
                    found = true;
                }
                else
                {
                    PropertyInfo? propertyInfo = type.GetProperty(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (propertyInfo != null)
                    {
                        value = propertyInfo.GetValue(instance);
                        found = true;
                    }
                }
                type = type.BaseType;
            }

            return found ? (T)value! : throw new MissingFieldException(originalType.FullName, fieldName);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            Type? type = instance.GetType();
            while (type != null)
            {
                FieldInfo? fieldInfo = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(instance, value);
                    return;
                }
                type = type.BaseType;
            }
        }

        private static object? InvokePrivateMethod(object instance, string methodName, params object[] parameters)
        {
            Type? type = instance.GetType();
            while (type != null)
            {
                MethodInfo? methodInfo = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (methodInfo != null)
                {
                    return methodInfo.Invoke(instance, parameters);
                }
                type = type.BaseType;
            }
            return null;
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

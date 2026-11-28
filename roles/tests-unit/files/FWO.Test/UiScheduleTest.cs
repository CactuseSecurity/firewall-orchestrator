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
    internal class UiScheduleTest
    {
        [Test]
        public async Task Schedule_LoadsVisibleSchedulesAndAllowedTemplates()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [
                    CreateSchedule(1, "Own schedule", 50, ReportType.Rules, "Rules template"),
                    CreateSchedule(2, "Foreign schedule", 51, ReportType.Rules, "Rules template")
                ],
                [
                    CreateTemplate(11, "Rules template", ReportType.Rules),
                    CreateTemplate(12, "Connections template", ReportType.Connections)
                ],
                []);

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), [Roles.Reporter], apiConnection, out _);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, null);

            wrapper.WaitForAssertion(() =>
            {
                Schedule component = wrapper.FindComponent<Schedule>().Instance;
                List<ReportSchedule> schedules = GetPrivateField<List<ReportSchedule>>(component, "reportSchedules");
                List<ReportTemplate> templates = GetPrivateField<List<ReportTemplate>>(component, "reportTemplates");
                List<ReportSchedule> visibleSchedules = (List<ReportSchedule>)InvokePrivateMethod(component, "FilterVisibleReportSchedules", schedules)!;

                Assert.Multiple(() =>
                {
                    Assert.That(schedules, Has.Count.EqualTo(2));
                    Assert.That(visibleSchedules, Has.Count.EqualTo(1));
                    Assert.That(visibleSchedules[0].Name, Is.EqualTo("Own schedule"));
                    Assert.That(templates, Has.Count.EqualTo(1));
                    Assert.That(templates[0].Name, Is.EqualTo("Rules template"));
                    Assert.That(apiConnection.GetSchedulesCalls, Is.EqualTo(1));
                    Assert.That(apiConnection.GetTemplatesCalls, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public async Task Schedule_AddReportSchedule_UsesDefaultsAndSavesSchedule()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), [Roles.Reporter], apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, null);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportTemplate>>(wrapper.FindComponent<Schedule>().Instance, "reportTemplates"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;

            await (Task)InvokePrivateMethod(component, "OnAddReportScheduleButtonClick")!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "ShowAddReportScheduleDialog"), Is.True);
                Assert.That(GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit").RepeatInterval, Is.EqualTo(SchedulerInterval.Never));
                Assert.That(GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit").Archive, Is.False);
                Assert.That(GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit").ScheduleOwningUser.DbId, Is.EqualTo(userConfig.User.DbId));
            });

            ReportSchedule reportScheduleInEdit = GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit");
            reportScheduleInEdit.Name = "Nightly schedule";
            reportScheduleInEdit.Template = GetPrivateField<List<ReportTemplate>>(component, "reportTemplates")[0];
            reportScheduleInEdit.OutputFormat =
            [
                new FileFormat { Name = GlobalConst.kJson }
            ];
            SetPrivateField(component, "actDate", new DateTime(2026, 7, 27));
            SetPrivateField(component, "actTime", new DateTime(2026, 7, 27, 13, 45, 0));

            await (Task)InvokePrivateMethod(component, "AddReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.AddScheduleCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component, "ShowAddReportScheduleDialog"), Is.False);
                Assert.That(GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit").Id, Is.EqualTo(99));
                Assert.That(GetAnonymousProperty<string>(apiConnection.LastAddVariables!, "report_schedule_name"), Is.EqualTo("Nightly schedule"));
                Assert.That(GetAnonymousProperty<int>(apiConnection.LastAddVariables!, "report_schedule_owner_id"), Is.EqualTo(userConfig.User.DbId));
                Assert.That(GetAnonymousProperty<int>(apiConnection.LastAddVariables!, "report_schedule_every"), Is.EqualTo((int)SchedulerInterval.Never));
                Assert.That(GetAnonymousProperty<bool>(apiConnection.LastAddVariables!, "report_schedule_active"), Is.True);
                Assert.That(GetAnonymousProperty<bool>(apiConnection.LastAddVariables!, "archive"), Is.False);
            });
        }

        [Test]
        public async Task Schedule_AddReportSchedule_WithoutTemplate_ShowsValidationError()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), [Roles.Reporter], apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, (exception, title, message, isError) => messages.Add((exception, title, message, isError)));
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportTemplate>>(wrapper.FindComponent<Schedule>().Instance, "reportTemplates"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;
            await (Task)InvokePrivateMethod(component, "OnAddReportScheduleButtonClick")!;
            SetPrivateField(component, "reportScheduleInEdit", new ReportSchedule
            {
                Name = "Broken schedule",
                Template = new ReportTemplate()
            });

            await (Task)InvokePrivateMethod(component, "AddReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.AddScheduleCalls, Is.EqualTo(0));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Exception, Is.Null);
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("edit_scheduled_report")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E2001")));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(GetPrivateField<bool>(component, "ShowAddReportScheduleDialog"), Is.True);
            });
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task Schedule_AddReportSchedule_WithNonPositiveRepeatOffset_ShowsValidationError(int repeatOffset)
        {
            TrackingScheduleApiConnection apiConnection = new(
                [],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), [Roles.Reporter], apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, (exception, title, message, isError) => messages.Add((exception, title, message, isError)));
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportTemplate>>(wrapper.FindComponent<Schedule>().Instance, "reportTemplates"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;
            await (Task)InvokePrivateMethod(component, "OnAddReportScheduleButtonClick")!;
            ReportSchedule reportScheduleInEdit = GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit");
            reportScheduleInEdit.Name = "Endless schedule";
            reportScheduleInEdit.Template = GetPrivateField<List<ReportTemplate>>(component, "reportTemplates")[0];
            reportScheduleInEdit.RepeatInterval = SchedulerInterval.Days;
            reportScheduleInEdit.RepeatOffset = repeatOffset;

            await (Task)InvokePrivateMethod(component, "AddReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.AddScheduleCalls, Is.EqualTo(0));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E2003")));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(GetPrivateField<bool>(component, "ShowAddReportScheduleDialog"), Is.True);
            });
        }

        [Test]
        public async Task Schedule_EditReportSchedule_WithZeroRepeatOffset_IsNotSaved()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [
                    CreateSchedule(1, "Own schedule", 50, ReportType.Rules, "Rules template")
                ],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), [Roles.Reporter], apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, (exception, title, message, isError) => messages.Add((exception, title, message, isError)));
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportSchedule>>(wrapper.FindComponent<Schedule>().Instance, "reportSchedules"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;
            await wrapper.InvokeAsync(() => wrapper.Find("button.btn.btn-sm.btn-warning").Click());
            ReportSchedule reportScheduleInEdit = GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit");
            reportScheduleInEdit.RepeatInterval = SchedulerInterval.Weeks;
            reportScheduleInEdit.RepeatOffset = 0;

            await (Task)InvokePrivateMethod(component, "UpdateReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateScheduleCalls, Is.EqualTo(0));
                Assert.That(messages.Select(message => message.Message), Does.Contain(userConfig.GetText("E2003")));
                Assert.That(GetPrivateField<bool>(component, "ShowEditReportScheduleDialog"), Is.True);
            });
        }

        [Test]
        public async Task Schedule_EditReportSchedule_ShowsAvailabilityAndUpdatesSchedule()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [
                    CreateSchedule(1, "Own schedule", 50, ReportType.Connections, "Connections template", true)
                ],
                [
                    CreateTemplate(31, "Connections template", ReportType.Connections)
                ],
                [
                    new FwoOwner { Id = 0, Name = "All" }
                ]);

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Admin), [Roles.Admin], apiConnection, out _);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, null);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportSchedule>>(wrapper.FindComponent<Schedule>().Instance, "reportSchedules"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;
            await wrapper.InvokeAsync(() => wrapper.Find("button.btn.btn-sm.btn-warning").Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(GetPrivateField<bool>(component, "ShowEditReportScheduleDialog"), Is.True);
                    Assert.That(GetPrivateField<bool>(component, "scheduleToEmail"), Is.True);
                    Assert.That(GetPrivateField<string>(component, "scheduleEmailRecipients"), Is.EqualTo("report@example.org"));
                    Assert.That(GetPrivateField<string>(component, "scheduleEmailSubject"), Is.EqualTo("Subject"));
                    Assert.That(GetPrivateField<string>(component, "scheduleEmailBody"), Is.EqualTo("Body"));
                    Assert.That(GetPrivateField<DateTime>(component, "actDate"), Is.EqualTo(new DateTime(2026, 7, 27, 10, 30, 0)));
                    Assert.That(GetPrivateField<DateTime>(component, "actTime"), Is.EqualTo(new DateTime(2026, 7, 27, 10, 30, 0)));
                    Assert.That(wrapper.Find("#outputFormatHtml").HasAttribute("disabled"), Is.False);
                    Assert.That(wrapper.Find("#outputFormatPdf").HasAttribute("disabled"), Is.False);
                    Assert.That(wrapper.FindAll("#outputFormatCsv"), Is.Empty);
                });
            });

            SetPrivateField(component, "scheduleToEmail", false);
            ReportSchedule reportScheduleInEdit = GetPrivateField<ReportSchedule>(component, "reportScheduleInEdit");
            reportScheduleInEdit.Name = "Updated schedule";

            await (Task)InvokePrivateMethod(component, "UpdateReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateScheduleCalls, Is.EqualTo(1));
                Assert.That(apiConnection.DeleteNotificationCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component, "ShowEditReportScheduleDialog"), Is.False);
                Assert.That(GetAnonymousProperty<string>(apiConnection.LastUpdateVariables!, "report_schedule_name"), Is.EqualTo("Updated schedule"));
                Assert.That(GetAnonymousProperty<int>(apiConnection.LastUpdateVariables!, "report_schedule_id"), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Schedule_DeleteReportSchedule_SendsDeleteAndClosesDialog()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [
                    CreateSchedule(1, "Own schedule", 50, ReportType.Rules, "Rules template")
                ],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), [Roles.Reporter], apiConnection, out _);
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, null);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportSchedule>>(wrapper.FindComponent<Schedule>().Instance, "reportSchedules"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;
            await wrapper.InvokeAsync(() => wrapper.Find("button.btn.btn-sm.btn-danger").Click());

            Assert.That(GetPrivateField<bool>(component, "ShowDeleteReportScheduleDialog"), Is.True);

            await (Task)InvokePrivateMethod(component, "DeleteReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.DeleteScheduleCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component, "ShowDeleteReportScheduleDialog"), Is.False);
                Assert.That(GetAnonymousProperty<int>(apiConnection.LastDeleteVariables!, "report_schedule_id"), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Schedule_AuditorCanOpenForeignScheduleButCannotSaveOrDeleteIt()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [
                    CreateSchedule(1, "Foreign schedule", 77, ReportType.Rules, "Rules template")
                ],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Auditor), [Roles.Auditor], apiConnection, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, null);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportSchedule>>(wrapper.FindComponent<Schedule>().Instance, "reportSchedules"), Has.Count.EqualTo(1)));

            Schedule component = wrapper.FindComponent<Schedule>().Instance;
            Assert.Multiple(() =>
            {
                Assert.That(wrapper.Find("button.btn.btn-sm.btn-warning").HasAttribute("disabled"), Is.False);
                Assert.That(wrapper.Find("button.btn.btn-sm.btn-danger").HasAttribute("disabled"), Is.True);
            });

            await wrapper.InvokeAsync(() => wrapper.Find("button.btn.btn-sm.btn-warning").Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "ShowEditReportScheduleDialog"), Is.True);
                AngleSharp.Dom.IElement saveButton = wrapper.FindAll("button").Single(button => button.TextContent.Trim() == userConfig.GetText("save"));
                Assert.That(saveButton.HasAttribute("disabled"), Is.True);
            });

            await (Task)InvokePrivateMethod(component, "UpdateReportSchedule")!;
            await (Task)InvokePrivateMethod(component, "DeleteReportSchedule")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateScheduleCalls, Is.EqualTo(0));
                Assert.That(apiConnection.DeleteScheduleCalls, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Schedule_AuditorCanSaveNewSchedule()
        {
            TrackingScheduleApiConnection apiConnection = new(
                [],
                [
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                ],
                []);

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Auditor), [Roles.Auditor], apiConnection, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context, null);
            Schedule component = wrapper.FindComponent<Schedule>().Instance;

            await wrapper.InvokeAsync(() => wrapper.Find("button.btn.btn-sm.btn-success").Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "ShowAddReportScheduleDialog"), Is.True);
                AngleSharp.Dom.IElement saveButton = wrapper.FindAll("button").Single(button => button.TextContent.Trim() == userConfig.GetText("save"));
                Assert.That(saveButton.HasAttribute("disabled"), Is.False);
            });
        }

        private const int kOwnUserDbId = 50;
        private const int kNewScheduleId = 99;

        private sealed record DisplayedMessage(Exception? Exception, string Title, string Text, bool Error);

        /// <summary>
        /// Api fake for the schedule dialog: counts the mutations it receives, can fail chosen queries
        /// and hands the subscription callbacks to the test.
        /// </summary>
        private sealed class ScheduleDialogApiConnection : SimulatedApiConnection
        {
            public List<ReportSchedule> Schedules { get; set; } = [];
            public List<ReportTemplate> Templates { get; set; } = [];
            public HashSet<string> FailingQueries { get; } = [];
            public List<string> SentQueries { get; } = [];
            public object? AddVariables { get; private set; }
            public object? DeleteVariables { get; private set; }
            public Action<Exception>? SubscriptionErrorHandler { get; private set; }
            public GraphQlApiSubscription<List<ReportSchedule>>.SubscriptionUpdate? SubscriptionUpdateHandler { get; private set; }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                SubscriptionErrorHandler = exceptionHandler;
                SubscriptionUpdateHandler = subscriptionUpdateHandler as GraphQlApiSubscription<List<ReportSchedule>>.SubscriptionUpdate;
                return null!;
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                SentQueries.Add(query);
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException("simulated api failure");
                }
                if (query == ReportQueries.getReportSchedules)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ReportSchedule>(Schedules));
                }
                if (query == ReportQueries.getReportTemplates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ReportTemplate>(Templates));
                }
                if (query == ReportQueries.addReportSchedule || query == NotificationQueries.addNotification || query == NotificationQueries.updateNotification)
                {
                    if (query == ReportQueries.addReportSchedule)
                    {
                        AddVariables = variables;
                    }
                    ReturnIdWrapper returnIds = new() { ReturnIds = [new ReturnId { NewId = kNewScheduleId }] };
                    return Task.FromResult((QueryResponseType)(object)returnIds);
                }
                if (query == ReportQueries.deleteReportSchedule)
                {
                    DeleteVariables = variables;
                }
                if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }
                return Task.FromResult((QueryResponseType)new object());
            }

            public int CountQuery(string query)
            {
                return SentQueries.Count(sentQuery => sentQuery == query);
            }
        }

        private readonly List<DisplayedMessage> displayedMessages = [];

        private IRenderedComponent<Schedule> RenderSchedulePage(BunitContext context)
        {
            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderSchedule(context,
                (exception, title, text, error) => displayedMessages.Add(new DisplayedMessage(exception, title, text, error)));
            return wrapper.FindComponent<Schedule>();
        }

        private static BunitContext CreateAdminContext(ScheduleDialogApiConnection apiConnection, out SimulatedUserConfig userConfig)
        {
            return CreateContext(new MonitoringTestAuthStateProvider(Roles.Admin), [Roles.Admin], apiConnection, out userConfig);
        }

        private static IElement ButtonWithIcon(IRenderedComponent<Schedule> page, string icon)
        {
            return page.FindAll("button").First(button => button.QuerySelector("span")?.ClassName == icon);
        }

        private static ReportSchedule InEdit(IRenderedComponent<Schedule> page)
        {
            return GetPrivateField<ReportSchedule>(page.Instance, "reportScheduleInEdit");
        }

        private static List<string> FormatNames(IRenderedComponent<Schedule> page)
        {
            return [.. InEdit(page).OutputFormat.Select(format => format.Name)];
        }

        /// <summary>
        /// Opens the add dialog and fills in what a valid schedule needs.
        /// </summary>
        private static async Task OpenFilledAddDialog(IRenderedComponent<Schedule> page, ReportTemplate template, string name = "Nightly")
        {
            ButtonWithIcon(page, Icons.Add).Click();
            page.Find("#scheduleName").Change(name);
            Dropdown<ReportTemplate> templateDropdown = page.FindComponent<Dropdown<ReportTemplate>>().Instance;
            await page.InvokeAsync(() => templateDropdown.SelectedElementChanged.InvokeAsync(template));
        }

        private static void ClickSave(IRenderedComponent<Schedule> page)
        {
            page.FindComponent<PopUp>().FindAll("button").First(button => button.QuerySelector("span")?.ClassName == Icons.Save).Click();
        }

        [Test]
        public async Task TheIntervalChooserOffersTheIntervalsAvailableForReports()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Add).Click();

            IRenderedComponent<Dropdown<SchedulerInterval>> intervalDropdown = page.FindComponent<Dropdown<SchedulerInterval>>();
            intervalDropdown.Find("input").Focus();

            List<SchedulerInterval> offered = [.. intervalDropdown.Instance.Elements];
            Assert.That(offered, Is.EqualTo(Enum.GetValues<SchedulerInterval>().Where(interval => interval.OfferedForReportOrNever()).ToList()));
            Assert.That(intervalDropdown.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim()),
                Does.Contain(userConfig.GetText(SchedulerInterval.Days.ToString())));
        }

        [Test]
        public async Task TheTemplateChooserOffersTheAllowedTemplates()
        {
            ScheduleDialogApiConnection apiConnection = new() { Templates = [CreateTemplate(21, "Rules template", ReportType.Rules)] };
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Add).Click();

            IRenderedComponent<Dropdown<ReportTemplate>> templateDropdown = page.FindComponent<Dropdown<ReportTemplate>>();
            templateDropdown.Find("input").Focus();

            Assert.That(templateDropdown.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim()), Does.Contain("Rules template"));
        }

        [Test]
        public async Task EveryOutputFormatCanBeSwitchedOnAndOffAgain()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Resolved rules", ReportType.ResolvedRules));

            foreach (string checkbox in (List<string>)["#outputFormatHtml", "#outputFormatPdf", "#outputFormatJson", "#outputFormatCsv"])
            {
                page.Find(checkbox).Change(true);
            }
            List<string> allFormats = FormatNames(page);
            foreach (string checkbox in (List<string>)["#outputFormatHtml", "#outputFormatPdf", "#outputFormatJson", "#outputFormatCsv"])
            {
                page.Find(checkbox).Change(false);
            }

            Assert.That(allFormats, Is.EquivalentTo((List<string>)[GlobalConst.kHtml, GlobalConst.kPdf, GlobalConst.kJson, GlobalConst.kCsv]));
            Assert.That(FormatNames(page), Is.Empty);
        }

        [Test]
        public async Task AComplianceTemplateDropsHtmlAndPdfButOffersCsv()
        {
            ReportSchedule schedule = CreateSchedule(1, "Compliance schedule", kOwnUserDbId, ReportType.ComplianceReport, "Compliance template");
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);

            ButtonWithIcon(page, Icons.Edit).Click();

            Assert.That(page.Find("#outputFormatHtml").HasAttribute("disabled"), Is.True);
            Assert.That(page.Find("#outputFormatPdf").HasAttribute("disabled"), Is.True);
            Assert.That(page.FindAll("#outputFormatCsv"), Has.Count.EqualTo(1));
            Assert.That(FormatNames(page), Is.EquivalentTo((List<string>)[GlobalConst.kJson, GlobalConst.kCsv]));
        }

        [Test]
        public async Task ATemplateWithoutParametersDropsCsv()
        {
            ReportSchedule schedule = CreateSchedule(1, "Odd schedule", kOwnUserDbId, ReportType.Rules, "Template without parameters");
            schedule.Template.ReportParams = null!;
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);

            ButtonWithIcon(page, Icons.Edit).Click();

            Assert.That(page.FindAll("#outputFormatCsv"), Is.Empty);
            Assert.That(page.Find("#outputFormatHtml").HasAttribute("disabled"), Is.False);
            Assert.That(FormatNames(page), Does.Not.Contain(GlobalConst.kCsv));
        }

        [Test]
        public async Task TheListShowsTheScheduleDetails()
        {
            ReportSchedule schedule = CreateSchedule(1, "Weekly schedule", kOwnUserDbId, ReportType.Rules, "Rules template");
            ReportSchedule once = CreateSchedule(2, "Once schedule", kOwnUserDbId, ReportType.Rules, "Rules template");
            once.RepeatInterval = SchedulerInterval.Never;
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule, once] };
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);

            IRenderedComponent<Schedule> page = RenderSchedulePage(context);

            List<string> rows = [.. page.FindAll("tbody tr").Select(row => row.TextContent)];
            Assert.That(rows[0], Does.Contain("2026-07-27 10:30:00"));
            Assert.That(rows[0], Does.Contain($"2 {userConfig.GetText(SchedulerInterval.Weeks.ToString())}"));
            Assert.That(rows[0], Does.Contain("HTML, PDF, JSON, CSV"));
            Assert.That(rows[1], Does.Not.Contain($"2 {userConfig.GetText(SchedulerInterval.Never.ToString())}"));
        }

        [Test]
        public async Task SavingTheAddDialogCreatesTheSchedule()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules));
            page.Find("#outputFormatJson").Change(true);
            page.Find("#operationArchive").Change(true);

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(apiConnection.AddVariables, Is.Not.Null));
            Assert.That(GetAnonymousProperty<int>(apiConnection.AddVariables!, "report_schedule_template_id"), Is.EqualTo(21));
            Assert.That(GetAnonymousProperty<bool>(apiConnection.AddVariables!, "archive"), Is.True);
            Assert.That(InEdit(page).Id, Is.EqualTo(kNewScheduleId));
            Assert.That(page.FindComponent<PopUp>().Instance.Show, Is.False);
            Assert.That(apiConnection.CountQuery(NotificationQueries.addNotification), Is.Zero);
            Assert.That(displayedMessages, Is.Empty);
        }

        [Test]
        public async Task CancellingTheDialogClosesItWithoutSaving()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules));

            page.FindComponent<PopUp>().FindAll("button").First(button => button.QuerySelector("span")?.ClassName == Icons.Cancel).Click();

            Assert.That(page.FindComponent<PopUp>().Instance.Show, Is.False);
            Assert.That(apiConnection.AddVariables, Is.Null);
        }

        [Test]
        public async Task ANameWithSurroundingBlanksIsShortenedAndSaidSo()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules), "  Nightly  ");

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(apiConnection.AddVariables, Is.Not.Null));
            Assert.That(GetAnonymousProperty<string>(apiConnection.AddVariables!, "report_schedule_name"), Is.EqualTo("Nightly"));
            Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[userConfig.GetText("U0001")]));
        }

        [Test]
        public async Task AScheduleSentByEmailGetsOneNotificationPerFormat()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules));
            page.Find("#outputFormatJson").Change(true);
            page.Find("#outputFormatHtml").Change(true);
            page.Find("#operationEmail").Change(true);

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(apiConnection.CountQuery(NotificationQueries.addNotification), Is.EqualTo(2)));
            Assert.That(InEdit(page).Notifications, Has.Count.EqualTo(2));
            Assert.That(page.FindComponent<PopUp>().Instance.Show, Is.False);
        }

        [Test]
        public async Task ANewScheduleIsRemovedAgainWhenItsNotificationsCannotBeCreated()
        {
            ScheduleDialogApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(NotificationQueries.addNotification);
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules));
            page.Find("#outputFormatJson").Change(true);
            page.Find("#operationEmail").Change(true);

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(GetAnonymousProperty<int>(apiConnection.DeleteVariables!, "report_schedule_id"), Is.EqualTo(kNewScheduleId));
            Assert.That(InEdit(page).Id, Is.Zero);
            Assert.That(displayedMessages[0].Title, Is.EqualTo(userConfig.GetText("save_scheduled_report")));
            Assert.That(page.FindComponent<PopUp>().Instance.Show, Is.True);
        }

        [Test]
        public async Task AFailingRemovalOfTheNewScheduleIsOnlyLogged()
        {
            ScheduleDialogApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(NotificationQueries.addNotification);
            apiConnection.FailingQueries.Add(ReportQueries.deleteReportSchedule);
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules));
            page.Find("#outputFormatJson").Change(true);
            page.Find("#operationEmail").Change(true);

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(apiConnection.CountQuery(ReportQueries.deleteReportSchedule), Is.EqualTo(1));
            Assert.That(InEdit(page).Id, Is.EqualTo(kNewScheduleId));
        }

        [Test]
        public async Task AScheduleThatCouldNotBeSavedIsNotRemoved()
        {
            ScheduleDialogApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(ReportQueries.addReportSchedule);
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            await OpenFilledAddDialog(page, CreateTemplate(21, "Rules template", ReportType.Rules));

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(apiConnection.CountQuery(ReportQueries.deleteReportSchedule), Is.Zero);
        }

        [Test]
        public async Task SavingAnEditedScheduleUpdatesItsNotifications()
        {
            ReportSchedule schedule = CreateSchedule(1, "Own schedule", kOwnUserDbId, ReportType.Rules, "Rules template", withNotification: true);
            schedule.Template.Id = 21;
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Edit).Click();

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(apiConnection.CountQuery(ReportQueries.editReportSchedule), Is.EqualTo(1)));
            Assert.That(apiConnection.CountQuery(NotificationQueries.addNotification) + apiConnection.CountQuery(NotificationQueries.updateNotification),
                Is.EqualTo(schedule.OutputFormat.Count));
            Assert.That(page.FindComponent<PopUp>().Instance.Show, Is.False);
        }

        [Test]
        public async Task AnEditedNameWithSurroundingBlanksIsShortenedAndSaidSo()
        {
            ReportSchedule schedule = CreateSchedule(1, "Own schedule", kOwnUserDbId, ReportType.Rules, "Rules template");
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Edit).Click();
            page.Find("#scheduleName").Change(" Renamed ");

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(apiConnection.CountQuery(ReportQueries.editReportSchedule), Is.EqualTo(1)));
            Assert.That(schedule.Name, Is.EqualTo("Renamed"));
            Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[userConfig.GetText("U0001")]));
        }

        [Test]
        public async Task AnUpdateWhoseNotificationsCannotBeSyncedKeepsTheDialogOpen()
        {
            ReportSchedule schedule = CreateSchedule(1, "Own schedule", kOwnUserDbId, ReportType.Rules, "Rules template", withNotification: true);
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            apiConnection.FailingQueries.Add(NotificationQueries.updateNotification);
            apiConnection.FailingQueries.Add(NotificationQueries.addNotification);
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Edit).Click();

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(apiConnection.CountQuery(ReportQueries.editReportSchedule), Is.EqualTo(1));
            Assert.That(displayedMessages[0].Text, Is.EqualTo(userConfig.GetText("E2002")));
            Assert.That(page.FindComponent<PopUp>().Instance.Show, Is.True);
        }

        [Test]
        public async Task AFailingUpdateIsReported()
        {
            ReportSchedule schedule = CreateSchedule(1, "Own schedule", kOwnUserDbId, ReportType.Rules, "Rules template");
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            apiConnection.FailingQueries.Add(ReportQueries.editReportSchedule);
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Edit).Click();

            ClickSave(page);

            page.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Title, Is.EqualTo(userConfig.GetText("edit_scheduled_report")));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AFailingDeletionIsReported()
        {
            ReportSchedule schedule = CreateSchedule(1, "Own schedule", kOwnUserDbId, ReportType.Rules, "Rules template");
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [schedule] };
            apiConnection.FailingQueries.Add(ReportQueries.deleteReportSchedule);
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            ButtonWithIcon(page, Icons.Delete).Click();

            page.FindComponent<ConfirmDelete>().Find("button.btn-danger").Click();

            page.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Title, Is.EqualTo(userConfig.GetText("delete_scheduled_report")));
        }

        [Test]
        public async Task AFailingScheduleFetchIsReported()
        {
            ScheduleDialogApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(ReportQueries.getReportSchedules);
            await using BunitContext context = CreateAdminContext(apiConnection, out SimulatedUserConfig userConfig);

            RenderSchedulePage(context);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Title, Is.EqualTo(userConfig.GetText("schedule_fetch")));
        }

        [Test]
        public async Task ASubscriptionUpdateReplacesTheListedSchedules()
        {
            ScheduleDialogApiConnection apiConnection = new() { Schedules = [CreateSchedule(1, "Old schedule", kOwnUserDbId, ReportType.Rules, "Rules template")] };
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);

            await page.InvokeAsync(() => apiConnection.SubscriptionUpdateHandler!([CreateSchedule(2, "New schedule", kOwnUserDbId, ReportType.Rules, "Rules template")]));

            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("New schedule")));
            Assert.That(page.Markup, Does.Not.Contain("Old schedule"));
        }

        [Test]
        public async Task ASubscriptionErrorIsReported()
        {
            ScheduleDialogApiConnection apiConnection = new();
            await using BunitContext context = CreateAdminContext(apiConnection, out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);
            InvalidOperationException subscriptionError = new("subscription lost");

            await page.InvokeAsync(() => apiConnection.SubscriptionErrorHandler!(subscriptionError));

            page.WaitForAssertion(() => Assert.That(displayedMessages.Select(message => message.Exception), Does.Contain(subscriptionError)));
        }

        [Test]
        public async Task DisposingThePageWithoutSubscriptionDoesNotThrow()
        {
            await using BunitContext context = CreateAdminContext(new ScheduleDialogApiConnection(), out _);
            IRenderedComponent<Schedule> page = RenderSchedulePage(context);

            Assert.DoesNotThrow(page.Instance.Dispose);
        }

        private static BunitContext CreateContext(AuthenticationStateProvider authStateProvider, List<string> roles, ApiConnection apiConnection, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton(authStateProvider);
            context.Services.AddSingleton(apiConnection);
            context.Services.AddScoped<DomEventService>();
            userConfig = new SimulatedUserConfig();
            userConfig.User.DbId = 50;
            userConfig.User.Name = "report.user";
            userConfig.User.Language = "English";
            userConfig.User.Roles = [.. roles];
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> RenderSchedule(BunitContext context, Action<Exception?, string, string, bool>? displayMessage)
        {
            Action<Exception?, string, string, bool> displayMessageInUi = displayMessage ?? ((_, _, _, _) => { });

            return context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                .Add(p => p.Value, displayMessageInUi)
                .AddChildContent<CascadingAuthenticationState>(authParameters => authParameters
                    .AddChildContent<Schedule>()));
        }

        private static ReportTemplate CreateTemplate(int id, string name, ReportType reportType)
        {
            ReportTemplate template = new()
            {
                Id = id,
                Name = name,
                ReportParams = new ReportParams
                {
                    ReportType = (int)reportType
                }
            };
            template.ReportParams.ModellingFilter.SelectedOwner = new FwoOwner { Id = 0, Name = "All" };
            return template;
        }

        private static ReportSchedule CreateSchedule(int id, string name, int ownerId, ReportType reportType, string templateName, bool withNotification = false)
        {
            ReportSchedule schedule = new()
            {
                Id = id,
                Name = name,
                ScheduleOwningUser = new UiUser
                {
                    DbId = ownerId,
                    Name = ownerId == 50 ? "report.user" : "other.user"
                },
                StartTime = new DateTime(2026, 7, 27, 10, 30, 0),
                RepeatOffset = 2,
                RepeatInterval = SchedulerInterval.Weeks,
                Template = CreateTemplate(id + 100, templateName, reportType),
                OutputFormat =
                [
                    new FileFormat { Name = GlobalConst.kHtml },
                    new FileFormat { Name = GlobalConst.kPdf },
                    new FileFormat { Name = GlobalConst.kJson },
                    new FileFormat { Name = GlobalConst.kCsv }
                ],
                Active = true,
                Counter = 3,
                Archive = false
            };

            if (withNotification)
            {
                schedule.Notifications =
                [
                    new FwoNotification
                    {
                        Id = 77,
                        EmailAddressTo = "report@example.org",
                        EmailSubject = "Subject",
                        EmailBody = "Body"
                    }
                ];
            }

            return schedule;
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(instance.GetType().FullName, fieldName);
            }

            return (T)field.GetValue(instance)!;
        }

        private static void SetPrivateField(object instance, string fieldName, object? value)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(instance.GetType().FullName, fieldName);
            }

            field.SetValue(instance, value);
        }

        private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }

            return method.Invoke(instance, args);
        }

        private static T GetAnonymousProperty<T>(object instance, string propertyName)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, propertyName);
            }

            return (T)property.GetValue(instance)!;
        }
    }

    internal sealed class TrackingScheduleApiConnection : SimulatedApiConnection
    {
        private readonly List<ReportSchedule> schedules;
        private readonly List<ReportTemplate> templates;
        private readonly List<FwoOwner> owners;

        public int GetSchedulesCalls { get; private set; }
        public int GetTemplatesCalls { get; private set; }
        public int AddScheduleCalls { get; private set; }
        public int UpdateScheduleCalls { get; private set; }
        public int DeleteScheduleCalls { get; private set; }
        public int DeleteNotificationCalls { get; private set; }
        public object? LastAddVariables { get; private set; }
        public object? LastUpdateVariables { get; private set; }
        public object? LastDeleteVariables { get; private set; }
        public object? LastDeleteNotificationVariables { get; private set; }

        public TrackingScheduleApiConnection(List<ReportSchedule> schedules, List<ReportTemplate> templates, List<FwoOwner> owners)
        {
            this.schedules = schedules;
            this.templates = templates;
            this.owners = owners;
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            return null!;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<ReportSchedule>) && query == ReportQueries.getReportSchedules)
            {
                GetSchedulesCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<ReportSchedule>(schedules));
            }

            if (typeof(QueryResponseType) == typeof(List<ReportTemplate>) && query == ReportQueries.getReportTemplates)
            {
                GetTemplatesCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<ReportTemplate>(templates));
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>(owners));
            }

            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ReportQueries.addReportSchedule)
            {
                AddScheduleCalls++;
                LastAddVariables = variables;
                ReturnId[] returnIds = new ReturnId[1];
                returnIds[0] = new ReturnId { NewId = 99 };
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = returnIds });
            }

            if (typeof(QueryResponseType) == typeof(object) && query == ReportQueries.editReportSchedule)
            {
                UpdateScheduleCalls++;
                LastUpdateVariables = variables;
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            if (typeof(QueryResponseType) == typeof(object) && query == ReportQueries.deleteReportSchedule)
            {
                DeleteScheduleCalls++;
                LastDeleteVariables = variables;
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            if (typeof(QueryResponseType) == typeof(object) && query == NotificationQueries.deleteNotification)
            {
                DeleteNotificationCalls++;
                LastDeleteNotificationVariables = variables;
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }
    }
}

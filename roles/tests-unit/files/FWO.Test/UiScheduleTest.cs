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
                new List<ReportSchedule>
                {
                    CreateSchedule(1, "Own schedule", 50, ReportType.Rules, "Rules template"),
                    CreateSchedule(2, "Foreign schedule", 51, ReportType.Rules, "Rules template")
                },
                new List<ReportTemplate>
                {
                    CreateTemplate(11, "Rules template", ReportType.Rules),
                    CreateTemplate(12, "Connections template", ReportType.Connections)
                },
                new List<FwoOwner>());

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), new List<string> { Roles.Reporter }, apiConnection, out _);
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
                new List<ReportSchedule>(),
                new List<ReportTemplate>
                {
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                },
                new List<FwoOwner>());

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), new List<string> { Roles.Reporter }, apiConnection, out SimulatedUserConfig userConfig);
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
            reportScheduleInEdit.OutputFormat = new List<FileFormat>
            {
                new FileFormat { Name = GlobalConst.kJson }
            };
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
                new List<ReportSchedule>(),
                new List<ReportTemplate>
                {
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                },
                new List<FwoOwner>());
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new List<(Exception? Exception, string Title, string Message, bool IsError)>();

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), new List<string> { Roles.Reporter }, apiConnection, out SimulatedUserConfig userConfig);
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

        [Test]
        public async Task Schedule_EditReportSchedule_ShowsAvailabilityAndUpdatesSchedule()
        {
            TrackingScheduleApiConnection apiConnection = new(
                new List<ReportSchedule>
                {
                    CreateSchedule(1, "Own schedule", 50, ReportType.Connections, "Connections template", true)
                },
                new List<ReportTemplate>
                {
                    CreateTemplate(31, "Connections template", ReportType.Connections)
                },
                new List<FwoOwner>
                {
                    new FwoOwner { Id = 0, Name = "All" }
                });

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Admin), new List<string> { Roles.Admin }, apiConnection, out _);
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
                new List<ReportSchedule>
                {
                    CreateSchedule(1, "Own schedule", 50, ReportType.Rules, "Rules template")
                },
                new List<ReportTemplate>
                {
                    CreateTemplate(21, "Rules template", ReportType.Rules)
                },
                new List<FwoOwner>());

            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), new List<string> { Roles.Reporter }, apiConnection, out _);
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
            userConfig.User.Roles = new List<string>(roles);
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
                OutputFormat = new List<FileFormat>
                {
                    new FileFormat { Name = GlobalConst.kHtml },
                    new FileFormat { Name = GlobalConst.kPdf },
                    new FileFormat { Name = GlobalConst.kJson },
                    new FileFormat { Name = GlobalConst.kCsv }
                },
                Active = true,
                Counter = 3,
                Archive = false
            };

            if (withNotification)
            {
                schedule.Notifications = new List<FwoNotification>
                {
                    new FwoNotification
                    {
                        Id = 77,
                        EmailAddressTo = "report@example.org",
                        EmailSubject = "Subject",
                        EmailBody = "Body"
                    }
                };
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

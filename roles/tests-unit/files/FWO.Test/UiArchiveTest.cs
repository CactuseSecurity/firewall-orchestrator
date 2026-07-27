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
    internal class UiArchiveTest
    {
        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("report_type", "Report type");
            SimulatedUserConfig.DummyTranslate.TryAdd("owner", "Owner");
            SimulatedUserConfig.DummyTranslate.TryAdd("all", "All");
            SimulatedUserConfig.DummyTranslate.TryAdd("actions", "Actions");
            SimulatedUserConfig.DummyTranslate.TryAdd("name", "Name");
            SimulatedUserConfig.DummyTranslate.TryAdd("template", "Template");
            SimulatedUserConfig.DummyTranslate.TryAdd("generation_date", "Generation date");
            SimulatedUserConfig.DummyTranslate.TryAdd("user", "User");
            SimulatedUserConfig.DummyTranslate.TryAdd("description", "Description");
            SimulatedUserConfig.DummyTranslate.TryAdd("generated_report", "Generated report");
            SimulatedUserConfig.DummyTranslate.TryAdd("archive_fetch", "Archive fetch");
            SimulatedUserConfig.DummyTranslate.TryAdd("archive_upd_err_msg", "Archive update failed");
            SimulatedUserConfig.DummyTranslate.TryAdd("fetch_report", "Fetch report");
            SimulatedUserConfig.DummyTranslate.TryAdd("delete_report", "Delete report");
            SimulatedUserConfig.DummyTranslate.TryAdd("U3002", "Delete report");
        }

        [Test]
        public async Task Archive_LoadsOnlyOwnReportsForReporter()
        {
            TrackingArchiveApiConnection apiConnection = new(
                new List<ReportFile>
                {
                    CreateReport(1, "Own report", ReportType.Rules, 50, "Own user", "Template A"),
                    CreateReport(2, "Foreign report", ReportType.Rules, 51, "Other user", "Template B")
                },
                CreateOwners());

            List<string> roles = new List<string> { Roles.Reporter };
            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), roles, apiConnection, out SimulatedUserConfig userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderArchive(context);

            wrapper.WaitForAssertion(() =>
            {
                Archive component = wrapper.FindComponent<Archive>().Instance;
                List<ReportFile> visibleReports = GetPrivateField<List<ReportFile>>(component, "visibleReports");
                List<ReportType> visibleReportTypes = GetPrivateField<List<ReportType>>(component, "visibleReportTypes");
                FwoOwner? selectedOwner = GetPrivateField<FwoOwner?>(component, "selectedOwner");

                Assert.Multiple(() =>
                {
                    Assert.That(visibleReports, Has.Count.EqualTo(1));
                    Assert.That(visibleReports[0].Name, Is.EqualTo("Own report"));
                    Assert.That(visibleReportTypes, Has.Count.EqualTo(1));
                    Assert.That(visibleReportTypes[0], Is.EqualTo(ReportType.Rules));
                    Assert.That(selectedOwner, Is.Not.Null);
                    Assert.That(selectedOwner!.Id, Is.EqualTo(0));
                    Assert.That(selectedOwner.Name, Is.EqualTo(userConfig.GetText("all")));
                    Assert.That(apiConnection.GeneratedReportsQueryCalls, Is.EqualTo(1));
                    Assert.That(apiConnection.EditableOwnersQueryCalls, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public async Task Archive_RecertificationSelection_FiltersVisibleReportsByOwner()
        {
            TrackingArchiveApiConnection apiConnection = new(
                new List<ReportFile>
                {
                    CreateReport(10, "app-a recert report", ReportType.RecertificationEvent, 50, "Own user", "Template A"),
                    CreateReport(11, "app-b recert report", ReportType.RecertificationEvent, 50, "Own user", "Template B"),
                    CreateReport(12, "other report", ReportType.Rules, 50, "Own user", "Template C")
                },
                CreateOwners());

            List<string> roles = new List<string> { Roles.Admin };
            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Admin), roles, apiConnection, out _);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderArchive(context);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportFile>>(wrapper.FindComponent<Archive>().Instance, "archivedReports"), Has.Count.EqualTo(3)));

            Archive component = wrapper.FindComponent<Archive>().Instance;
            InvokePrivateMethod(component, "ReportTypeChanged", ReportType.RecertificationEvent);

            List<FwoOwner> ownerList = GetPrivateField<List<FwoOwner>>(component, "ownerList");
            List<ReportFile> visibleReports = GetPrivateField<List<ReportFile>>(component, "visibleReports");

            Assert.Multiple(() =>
            {
                Assert.That(ownerList.Any(owner => owner.Id == 0), Is.True);
                Assert.That(ownerList.Any(owner => owner.Id == 10), Is.True);
                Assert.That(ownerList.Any(owner => owner.Id == 11), Is.True);
                Assert.That(ownerList.Any(owner => owner.Id == 13), Is.True);
                Assert.That(ownerList.Any(owner => owner.Id == 12), Is.False);
                Assert.That(visibleReports, Has.Count.EqualTo(2));
            });

            InvokePrivateMethod(component, "OwnerChanged", ownerList.First(owner => owner.Id == 10));

            visibleReports = GetPrivateField<List<ReportFile>>(component, "visibleReports");

            Assert.That(visibleReports, Has.Count.EqualTo(1));
            Assert.That(visibleReports[0].Name, Does.Contain("app-a"));
        }

        [Test]
        public async Task Archive_DownloadButton_OpensPopupAndFetchesReportContent()
        {
            TrackingArchiveApiConnection apiConnection = new(
                new List<ReportFile>
                {
                    CreateReport(1, "Own report", ReportType.Rules, 50, "Own user", "Template A")
                },
                CreateOwners());

            List<string> roles = new List<string> { Roles.Reporter };
            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), roles, apiConnection, out _);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderArchive(context);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportFile>>(wrapper.FindComponent<Archive>().Instance, "visibleReports"), Has.Count.EqualTo(1)));

            Archive component = wrapper.FindComponent<Archive>().Instance;
            IElement downloadButton = wrapper.Find("button.btn-sm.btn-primary");

            await wrapper.InvokeAsync(() => downloadButton.Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.GeneratedReportContentCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component, "ShowDownloadReportFileDialog"), Is.True);
                Assert.That(GetPrivateField<ReportFile>(component, "reportFileContext").Id, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Archive_DeleteButton_DeletesReportAndClosesDialog()
        {
            TrackingArchiveApiConnection apiConnection = new(
                new List<ReportFile>
                {
                    CreateReport(1, "Own report", ReportType.Rules, 50, "Own user", "Template A")
                },
                CreateOwners());

            List<string> roles = new List<string> { Roles.Reporter };
            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), roles, apiConnection, out _);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderArchive(context);
            wrapper.WaitForAssertion(() => Assert.That(GetPrivateField<List<ReportFile>>(wrapper.FindComponent<Archive>().Instance, "visibleReports"), Has.Count.EqualTo(1)));

            Archive component = wrapper.FindComponent<Archive>().Instance;
            IElement deleteButton = wrapper.Find("button.btn-sm.btn-danger");

            await wrapper.InvokeAsync(() => deleteButton.Click());

            Assert.That(GetPrivateField<bool>(component, "ShowDeleteReportFileDialog"), Is.True);
            Assert.That(GetPrivateField<ReportFile>(component, "reportFileContext").Name, Is.EqualTo("Own report"));

            await (Task)InvokePrivateMethod(component, "DeleteGeneratedReport")!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.DeleteGeneratedReportCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<List<ReportFile>>(component, "archivedReports"), Is.Empty);
                Assert.That(GetPrivateField<bool>(component, "ShowDeleteReportFileDialog"), Is.False);
            });
        }

        [Test]
        public async Task Archive_LoadFailure_ShowsErrorMessage()
        {
            List<string> roles = new List<string> { Roles.Reporter };
            await using BunitContext context = CreateContext(new MonitoringTestAuthStateProvider(Roles.Reporter), roles, new ThrowingArchiveApiConnection(), out SimulatedUserConfig userConfig);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new List<(Exception? Exception, string Title, string Message, bool IsError)>();
            Action<Exception?, string, string, bool> displayMessage = (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            };

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                .Add(p => p.Value, displayMessage)
                .AddChildContent<CascadingAuthenticationState>(authParameters => authParameters
                    .AddChildContent<Archive>()));

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Exception, Is.Not.Null);
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("archive_fetch")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("archive_upd_err_msg")));
                Assert.That(messages[0].IsError, Is.True);
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
            userConfig.User.Language = "English";
            userConfig.User.Roles = new List<string>(roles);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderArchive(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<Archive>());
        }

        private static List<FwoOwner> CreateOwners()
        {
            return new List<FwoOwner>
            {
                new FwoOwner { Id = 0, Name = "All owners" },
                new FwoOwner { Id = 10, Name = "App A owner", ExtAppId = "app-a" },
                new FwoOwner { Id = 11, Name = "App B owner", ExtAppId = "app-b" },
                new FwoOwner { Id = 12, Name = "Unused owner", ExtAppId = "app-x" },
                new FwoOwner { Id = 13, Name = "No external id owner" }
            };
        }

        private static ReportFile CreateReport(int id, string name, ReportType reportType, int owningUserId, string owningUserName, string templateName)
        {
            return new ReportFile
            {
                Id = id,
                Name = name,
                Type = (int)reportType,
                OwningUserId = owningUserId,
                ReportOwningUser = new UiUser
                {
                    DbId = owningUserId,
                    Name = owningUserName
                },
                Template = new ReportTemplate
                {
                    Name = templateName
                },
                GenerationDateStart = new DateTime(2026, 7, 27, 9, 0, 0),
                Description = $"{name} description"
            };
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

        private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }

            return method.Invoke(instance, args);
        }
    }

    internal sealed class ThrowingArchiveApiConnection : SimulatedApiConnection
    {
        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            return null!;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            throw new InvalidOperationException("Archive load failed");
        }
    }

    internal sealed class TrackingArchiveApiConnection : SimulatedApiConnection
    {
        private readonly List<ReportFile> archivedReports;
        private readonly List<FwoOwner> owners;

        public int GeneratedReportsQueryCalls { get; private set; }
        public int EditableOwnersQueryCalls { get; private set; }
        public int GeneratedReportContentCalls { get; private set; }
        public int DeleteGeneratedReportCalls { get; private set; }

        public TrackingArchiveApiConnection(List<ReportFile> archivedReports, List<FwoOwner> owners)
        {
            this.archivedReports = archivedReports;
            this.owners = owners;
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            return null!;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<ReportFile>) && query == ReportQueries.getGeneratedReports)
            {
                GeneratedReportsQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<ReportFile>(archivedReports));
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getEditableOwners)
            {
                EditableOwnersQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>(owners));
            }

            if (typeof(QueryResponseType) == typeof(ReportFile[]) && query == ReportQueries.getGeneratedReport)
            {
                GeneratedReportContentCalls++;
                int reportId = GetAnonymousProperty<int>(variables!, "report_id");
                ReportFile? report = archivedReports.FirstOrDefault(item => item.Id == reportId);
                if (report == null)
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ReportFile>());
                }

                return Task.FromResult((QueryResponseType)(object)new List<ReportFile> { report }.ToArray());
            }

            if (typeof(QueryResponseType) == typeof(object) && query == ReportQueries.deleteGeneratedReport)
            {
                DeleteGeneratedReportCalls++;
                int reportId = GetAnonymousProperty<int>(variables!, "report_id");
                archivedReports.RemoveAll(item => item.Id == reportId);
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
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
}

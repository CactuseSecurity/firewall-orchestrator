using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportExportTest
    {
        /// <summary>
        /// A report whose html export is cheap to build but distinguishable per call, so that a
        /// released export cache is visible as a freshly rendered string.
        /// </summary>
        private sealed class ExportableTestReport() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return "a;b;c";
            }

            public override string ExportToJson()
            {
                return "{}";
            }

            public override string ExportToHtml()
            {
                return GenerateHtmlFrameBase("Title", "", DateTime.Parse("2026-01-01T00:00:00Z"), new StringBuilder("<p>body</p>"));
            }

            public override string SetDescription()
            {
                return "";
            }
        }

        private sealed class AppRulesExportTestReport() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.AppRules)
        {
            public string RoleUsedForObjectFetch { get; private set; } = "";

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
            {
                RoleUsedForObjectFetch = ((ReportExportTrackingApiConnection)apiConnection).ActiveRole;
                return await base.GetObjectsInReport(objectsPerFetch, apiConnection, callback);
            }

            public override string ExportToCsv()
            {
                return "";
            }

            public override string ExportToJson()
            {
                return "";
            }

            public override string ExportToHtml()
            {
                return "";
            }

            public override string SetDescription()
            {
                return "";
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("export_report", "Export report");
            SimulatedUserConfig.DummyTranslate.TryAdd("export_report_download", "Download export");
            SimulatedUserConfig.DummyTranslate.TryAdd("E1002", "No report to export");
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        [Test]
        public void ClosingTheDownloadDialogReleasesTheHtmlTheExportLeftCachedOnTheReport()
        {
            using BunitContext context = CreateContext();
            ExportableTestReport report = new();
            string firstExport = report.ExportToHtml();
            IRenderedComponent<ReportExport> exportComponent = context.Render<ReportExport>(parameters => parameters
                .Add(p => p.ReportToExport, report));

            IRenderedComponent<ReportDownloadPopUp> downloadPopUp = exportComponent.FindComponent<ReportDownloadPopUp>();
            exportComponent.InvokeAsync(() => downloadPopUp.Instance.OnClose()).GetAwaiter().GetResult();

            // a released cache means the next export renders the report again instead of handing back
            // the multi megabyte string that was kept alive since the last export
            Assert.That(report.ExportToHtml(), Is.Not.SameAs(firstExport));
        }

        [Test]
        public void ClosingTheDownloadDialogWithoutAReportDoesNotThrow()
        {
            using BunitContext context = CreateContext();
            IRenderedComponent<ReportExport> exportComponent = context.Render<ReportExport>(parameters => parameters
                .Add(p => p.ReportToExport, null));

            IRenderedComponent<ReportDownloadPopUp> downloadPopUp = exportComponent.FindComponent<ReportDownloadPopUp>();

            Assert.DoesNotThrow(() =>
                exportComponent.InvokeAsync(() => downloadPopUp.Instance.OnClose()).GetAwaiter().GetResult());
        }

        [Test]
        public async Task GettingObjectsForAppRulesExportUsesModellingRole()
        {
            await using BunitContext context = CreateContext();
            ReportExportTrackingApiConnection apiConnection = new();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<AuthenticationStateProvider>(new ReportExportAuthStateProvider(Roles.Modeller));
            AppRulesExportTestReport report = new();
            IRenderedComponent<ReportExport> exportComponent = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ReportExport>(child => child.Add(p => p.ReportToExport, report)))
                .FindComponent<ReportExport>();

            MethodInfo getReportObjectsForExport = typeof(ReportExport).GetMethod("GetReportObjectsForExport", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await exportComponent.InvokeAsync(async () => await (Task)getReportObjectsForExport.Invoke(exportComponent.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(report.RoleUsedForObjectFetch, Is.EqualTo(Roles.Modeller));
                Assert.That(apiConnection.ActiveRole, Is.Empty);
                Assert.That(apiConnection.SwitchBackCount, Is.EqualTo(1));
            });
        }

        private sealed class ReportExportAuthStateProvider(string role) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(new List<Claim> { new(ClaimTypes.Role, role) }, "test", ClaimTypes.Name, ClaimTypes.Role));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class ReportExportTrackingApiConnection : SimulatedApiConnection
        {
            private readonly Stack<string> previousRoles = new();

            public string ActiveRole { get; private set; } = "";
            public int SwitchBackCount { get; private set; }

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                SetRole(targetRoleList.First(user.IsInRole));
            }

            public override void SetRole(string role)
            {
                previousRoles.Push(ActiveRole);
                ActiveRole = role;
            }

            public override void SwitchBack()
            {
                SwitchBackCount++;
                ActiveRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }
        }
    }
}

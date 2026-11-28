using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
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

        private const string kHtmlExport = "<p>exported html</p>";
        private const string kCsvExport = "a;b";
        private const string kJsonExport = "{\"a\":1}";
        private const string kPdfExport = "cGRm";
        private const string kDescription = "report description";
        private const int kCustomWidth = 200;
        private const int kCustomHeight = 300;
        // the export runs on a worker thread, so the page renders its result only once that is done
        private static readonly TimeSpan kExportTimeout = TimeSpan.FromSeconds(10);

        private sealed record DisplayedMessage(Exception? Exception, string Title, string Text);

        /// <summary>
        /// Report of a chosen type that records how it was exported, without rendering a pdf through a browser.
        /// </summary>
        private sealed class RecordingTestReport : ReportBase
        {
            public RecordingTestReport(ReportType reportType, bool detailedView = false) : base(new DynGraphqlQuery(""), new SimulatedUserConfig(), reportType)
            {
                ReportData.WorkflowFilter.DetailedView = detailedView;
            }

            public TaskCompletionSource? ObjectFetchGate { get; set; }
            public bool FailHtmlExport { get; set; }
            public int ObjectFetches { get; private set; }
            public PaperFormat? PdfFormat { get; private set; }
            public string? PdfSource { get; private set; }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
            {
                ObjectFetches++;
                if (ObjectFetchGate != null)
                {
                    await ObjectFetchGate.Task;
                }
                GotObjectsInReport = true;
                return true;
            }

            public override string ExportToHtml()
            {
                return FailHtmlExport ? throw new InvalidOperationException("html export failed") : kHtmlExport;
            }

            public override string ExportToCsv()
            {
                return kCsvExport;
            }

            public override string ExportToJson()
            {
                return kJsonExport;
            }

            public override Task<string?> ToPdf(string html, PaperFormat format)
            {
                PdfSource = html;
                PdfFormat = format;
                return Task.FromResult<string?>(kPdfExport);
            }

            public override string SetDescription()
            {
                return kDescription;
            }
        }

        private sealed class ArchiveTrackingApiConnection : SimulatedApiConnection
        {
            public object? ArchiveVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.addGeneratedReport)
                {
                    ArchiveVariables = variables;
                    return Task.FromResult((QueryResponseType)new object());
                }
                throw new NotImplementedException($"Unhandled query {query}");
            }
        }

        private readonly List<DisplayedMessage> displayedMessages = [];

        private static BunitContext CreateContext(ApiConnection apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton(apiConnection);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private IRenderedComponent<ReportExport> RenderExport(BunitContext context, ReportBase? report)
        {
            return context.Render<ReportExport>(parameters => parameters
                .AddCascadingValue<Action<Exception?, string, string, bool>>((exception, title, text, _) =>
                    displayedMessages.Add(new DisplayedMessage(exception, title, text)))
                .Add(p => p.ReportToExport, report));
        }

        private static string Text(BunitContext context, string key)
        {
            return context.Services.GetRequiredService<UserConfig>().GetText(key);
        }

        private static void OpenExportDialog(IRenderedComponent<ReportExport> component)
        {
            component.Find("button.btn-dark").Click();
        }

        private static IRenderedComponent<PopUp> ExportDialog(IRenderedComponent<ReportExport> component)
        {
            return component.FindComponents<PopUp>()[0];
        }

        private static void ClickExport(IRenderedComponent<ReportExport> component)
        {
            ExportDialog(component).Find("button.btn-primary").Click();
        }

        /// <summary>
        /// Waits for the export, which runs on a worker thread, to open the download dialog. A plain
        /// condition is used instead of an assertion, because NUnit records a failed assertion even when
        /// the wait catches it and tries again.
        /// </summary>
        private static void WaitForDownloadDialog(IRenderedComponent<ReportExport> component)
        {
            component.WaitForState(() => DownloadDialog(component).Show, kExportTimeout);
        }

        private static ReportDownloadPopUp DownloadDialog(IRenderedComponent<ReportExport> component)
        {
            return component.FindComponent<ReportDownloadPopUp>().Instance;
        }

        [Test]
        public async Task ExportingWithoutAReportIsRefused()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, null);

            OpenExportDialog(component);

            Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[Text(context, "E1002")]));
            Assert.That(ExportDialog(component).Instance.Show, Is.False);
        }

        [Test]
        public async Task OpeningTheExportDialogSuggestsAReportName()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(ReportType.Rules));

            OpenExportDialog(component);

            Assert.That(ExportDialog(component).Instance.Show, Is.True);
            Assert.That(component.Find("input.form-control").GetAttribute("value"), Is.EqualTo("Report"));
        }

        [TestCase(ReportType.Rules, false, true, true, false)]
        [TestCase(ReportType.ResolvedRules, false, true, true, true)]
        [TestCase(ReportType.ComplianceReport, false, false, false, true)]
        [TestCase(ReportType.TicketReport, false, true, true, true)]
        [TestCase(ReportType.TicketReport, true, true, true, false)]
        public async Task OnlyTheFormatsTheReportTypeSupportsCanBeChosen(ReportType reportType, bool detailedView, bool pdf, bool html, bool csv)
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(reportType, detailedView));

            OpenExportDialog(component);

            Assert.That(component.Find("#reportExportPdf").HasAttribute("disabled"), Is.EqualTo(!pdf));
            Assert.That(component.Find("#reportExportHtml").HasAttribute("disabled"), Is.EqualTo(!html));
            Assert.That(component.Find("#reportExportCsv").HasAttribute("disabled"), Is.EqualTo(!csv));
            Assert.That(component.Find("#reportExportJson").HasAttribute("disabled"), Is.False);
        }

        [Test]
        public async Task ExportingProducesTheChosenFormatsAndOffersThemForDownload()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            RecordingTestReport report = new(ReportType.ResolvedRules);
            IRenderedComponent<ReportExport> component = RenderExport(context, report);
            OpenExportDialog(component);
            component.Find("#reportExportHtml").Change(true);
            component.Find("#reportExportCsv").Change(true);
            component.Find("#reportExportJson").Change(true);

            ClickExport(component);

            WaitForDownloadDialog(component);
            ReportFile exportedFile = DownloadDialog(component).ReportFile!;
            Assert.That(exportedFile.Html, Is.EqualTo(kHtmlExport));
            Assert.That(exportedFile.Csv, Is.EqualTo(kCsvExport));
            Assert.That(exportedFile.Json, Is.EqualTo(kJsonExport));
            Assert.That(exportedFile.Pdf, Is.Null);
            Assert.That(DownloadDialog(component).ShowJson, Is.True);
            Assert.That(ExportDialog(component).Instance.Show, Is.False);
            Assert.That(report.ObjectFetches, Is.EqualTo(1));
            Assert.That(displayedMessages, Is.Empty);
        }

        [Test]
        public async Task TheReportObjectsAreFetchedOnlyForTheFirstExport()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            RecordingTestReport report = new(ReportType.Rules);
            IRenderedComponent<ReportExport> component = RenderExport(context, report);

            for (int export = 0; export < 2; export++)
            {
                OpenExportDialog(component);
                ClickExport(component);
                WaitForDownloadDialog(component);
                await component.InvokeAsync(() => DownloadDialog(component).OnClose());
            }

            Assert.That(report.ObjectFetches, Is.EqualTo(1));
        }

        [Test]
        public async Task APdfIsRenderedInTheChosenPaperFormat()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            RecordingTestReport report = new(ReportType.Rules);
            IRenderedComponent<ReportExport> component = RenderExport(context, report);
            OpenExportDialog(component);
            component.Find("#reportExportPdf").Change(true);
            Dropdown<PaperFormat> paperFormat = component.FindComponent<Dropdown<PaperFormat>>().Instance;
            await component.InvokeAsync(() => paperFormat.SelectedElementChanged.InvokeAsync(PaperFormat.Letter));

            ClickExport(component);

            WaitForDownloadDialog(component);
            Assert.That(report.PdfFormat, Is.EqualTo(PaperFormat.Letter));
            Assert.That(report.PdfSource, Is.EqualTo(kHtmlExport));
            Assert.That(DownloadDialog(component).ReportFile!.Pdf, Is.EqualTo(kPdfExport));
            Assert.That(DownloadDialog(component).ReportFile!.Html, Is.Null);
        }

        [Test]
        public async Task ACustomPaperFormatAsksForItsSize()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            RecordingTestReport report = new(ReportType.Rules);
            IRenderedComponent<ReportExport> component = RenderExport(context, report);
            OpenExportDialog(component);
            Assert.That(component.FindComponents<Dropdown<PaperFormat>>(), Is.Empty);
            component.Find("#reportExportPdf").Change(true);
            Dropdown<PaperFormat> paperFormat = component.FindComponent<Dropdown<PaperFormat>>().Instance;
            await component.InvokeAsync(() => paperFormat.SelectedElementChanged.InvokeAsync(PaperFormat.Custom));

            component.FindAll("input[type=number]")[0].Change(kCustomWidth);
            component.FindAll("input[type=number]")[1].Change(kCustomHeight);
            ClickExport(component);

            WaitForDownloadDialog(component);
            Assert.That(report.PdfFormat, Is.EqualTo(PaperFormat.Custom));
            Assert.That(report.CustomWidth, Is.EqualTo(kCustomWidth));
            Assert.That(report.CustomHeight, Is.EqualTo(kCustomHeight));
        }

        [Test]
        public async Task ArchivingStoresTheExportWithItsJson()
        {
            ArchiveTrackingApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(ReportType.Rules));
            OpenExportDialog(component);
            component.Find("#reportExportHtml").Change(true);
            component.Find("#reportExportArchive").Change(true);

            ClickExport(component);

            component.WaitForState(() => apiConnection.ArchiveVariables != null, kExportTimeout);
            object archived = apiConnection.ArchiveVariables!;
            Assert.That(GetProperty<string>(archived, "report_name"), Is.EqualTo("Report"));
            Assert.That(GetProperty<string>(archived, "report_html"), Is.EqualTo(kHtmlExport));
            Assert.That(GetProperty<string>(archived, "report_json"), Is.EqualTo(kJsonExport));
            Assert.That(GetProperty<int?>(archived, "report_type"), Is.EqualTo((int)ReportType.Rules));
            Assert.That(GetProperty<string>(archived, "description"), Is.EqualTo(kDescription));
            // json was not chosen for download, only for the archive
            Assert.That(DownloadDialog(component).ShowJson, Is.False);
        }

        [Test]
        public async Task WhileExportingTheButtonsAreReplacedByAProgressIndicator()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            RecordingTestReport report = new(ReportType.Rules) { ObjectFetchGate = new TaskCompletionSource() };
            IRenderedComponent<ReportExport> component = RenderExport(context, report);
            OpenExportDialog(component);

            ClickExport(component);

            component.WaitForState(() => component.FindComponents<Exporting>().Count > 0, kExportTimeout);
            Assert.That(ExportDialog(component).FindAll("button.btn-primary"), Is.Empty);
            report.ObjectFetchGate.SetResult();
            WaitForDownloadDialog(component);
            Assert.That(component.FindComponents<Exporting>(), Is.Empty);
        }

        [Test]
        public async Task AFailingExportIsReported()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            RecordingTestReport report = new(ReportType.Rules) { FailHtmlExport = true };
            IRenderedComponent<ReportExport> component = RenderExport(context, report);
            OpenExportDialog(component);
            component.Find("#reportExportHtml").Change(true);

            ClickExport(component);

            component.WaitForState(() => displayedMessages.Count > 0, kExportTimeout);
            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Title, Is.EqualTo(Text(context, "export_report")));
        }

        [Test]
        public async Task AReportNameWithSurroundingBlanksIsShortenedAndSaidSo()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(ReportType.Rules));
            OpenExportDialog(component);
            component.Find("input.form-control").Input("  monthly  ");

            ClickExport(component);

            WaitForDownloadDialog(component);
            Assert.That(DownloadDialog(component).ReportFile!.Name, Is.EqualTo("monthly"));
            Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[Text(context, "U0001")]));
        }

        [Test]
        public async Task CancellingTheExportDialogClosesIt()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(ReportType.Rules));
            OpenExportDialog(component);

            ExportDialog(component).Find("button.btn-secondary").Click();

            Assert.That(ExportDialog(component).Instance.Show, Is.False);
            Assert.That(DownloadDialog(component).Show, Is.False);
        }

        [Test]
        public async Task ClosingTheExportDialogHidesIt()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(ReportType.Rules));
            OpenExportDialog(component);

            await component.InvokeAsync(() => ExportDialog(component).Instance.OnClose());
            component.Render();

            Assert.That(ExportDialog(component).Instance.Show, Is.False);
        }

        [Test]
        public async Task AReportThatDisappearedWhileTheDialogWasOpenIsNotExported()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, new RecordingTestReport(ReportType.Rules));
            OpenExportDialog(component);
            component.Render(parameters => parameters.Add(p => p.ReportToExport, null));

            ClickExport(component);

            component.WaitForState(() => displayedMessages.Count > 0, kExportTimeout);
            Assert.That(displayedMessages.Select(message => message.Text), Is.EqualTo((List<string>)[Text(context, "E1002")]));
        }

        [Test]
        public async Task GettingObjectsWithoutAReportDoesNothing()
        {
            await using BunitContext context = CreateContext(new ArchiveTrackingApiConnection());
            IRenderedComponent<ReportExport> component = RenderExport(context, null);

            MethodInfo getReportObjectsForExport = typeof(ReportExport).GetMethod("GetReportObjectsForExport", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.DoesNotThrowAsync(async () => await component.InvokeAsync(async () => await (Task)getReportObjectsForExport.Invoke(component.Instance, null)!));
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            return (T)instance.GetType().GetProperty(propertyName)!.GetValue(instance)!;
        }

        private sealed class ReportExportAuthStateProvider(string role) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity([new(ClaimTypes.Role, role)], "test", ClaimTypes.Name, ClaimTypes.Role));

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

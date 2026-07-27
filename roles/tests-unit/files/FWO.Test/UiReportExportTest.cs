using Bunit;
using Bunit.TestDoubles;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportExportTest
    {
        [Test]
        public async Task ReportExport_NoReportSelected_ShowsErrorMessage()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new List<(Exception? Exception, string Title, string Message, bool IsError)>();

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderComponent(
                context,
                null,
                messages);

            await wrapper.InvokeAsync(() => wrapper.Find("button.btn-dark").Click());

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(messages[0].Exception, Is.Null);
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("export_report")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E1002")));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task ReportExport_ConnectionsReport_DisablesCsvExport()
        {
            await using BunitContext context = CreateContext(out _, out _);
            TrackingReport report = new(ReportType.Connections);

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderComponent(
                context,
                report,
                null);

            await wrapper.InvokeAsync(() => wrapper.Find("button.btn-dark").Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#reportExportPdf").HasAttribute("disabled"), Is.False);
                Assert.That(wrapper.Find("#reportExportHtml").HasAttribute("disabled"), Is.False);
                Assert.That(wrapper.Find("#reportExportCsv").HasAttribute("disabled"), Is.True);
                Assert.That(wrapper.Find("#reportExportJson").HasAttribute("disabled"), Is.False);
            });
        }

        [TestCase(ReportType.VarianceAnalysis)]
        [TestCase(ReportType.RecertEventReport)]
        public async Task ReportExport_ConnectionsLikeReports_EnablePdfAndHtmlButDisableCsv(ReportType reportType)
        {
            await using BunitContext context = CreateContext(out _, out _);
            TrackingReport report = new(reportType);

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderComponent(
                context,
                report,
                null);

            await wrapper.InvokeAsync(() => wrapper.Find("button.btn-dark").Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#reportExportPdf").HasAttribute("disabled"), Is.False);
                Assert.That(wrapper.Find("#reportExportHtml").HasAttribute("disabled"), Is.False);
                Assert.That(wrapper.Find("#reportExportCsv").HasAttribute("disabled"), Is.True);
            });
        }

        [Test]
        public async Task ReportExport_ArchiveExport_WritesGeneratedReportAndShowsDownloadedFiles()
        {
            await using BunitContext context = CreateContext(out TrackingReportExportApiConnection apiConnection, out SimulatedUserConfig userConfig);
            userConfig.User.DbId = 77;
            TrackingReport report = new(ReportType.OwnerRecertification);
            JSRuntimeInvocationHandler downloadInvocation = context.JSInterop.SetupVoid("DownloadFile", _ => true).SetVoidResult();

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> wrapper = RenderComponent(
                context,
                report,
                null);

            await wrapper.InvokeAsync(() => wrapper.Find("button.btn-dark").Click());

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#reportExportPdf"), Is.Not.Null);
                Assert.That(wrapper.Find("#reportExportJson"), Is.Not.Null);
                Assert.That(wrapper.Find("#reportExportArchive"), Is.Not.Null);
            });

            await wrapper.InvokeAsync(() => wrapper.Find("#reportExportPdf").Change(true));
            await wrapper.InvokeAsync(() => wrapper.Find("#reportExportJson").Change(true));
            await wrapper.InvokeAsync(() => wrapper.Find("#reportExportArchive").Change(true));
            await wrapper.InvokeAsync(() => wrapper.Find("button.btn.btn-sm.btn-primary").Click());

            await WaitForConditionAsync(() => apiConnection.AddGeneratedReportCalls == 1);
            Assert.Multiple(() =>
            {
                Assert.That(report.GetObjectsInReportCalls, Is.EqualTo(1));
                Assert.That(report.HtmlExportCalls, Is.EqualTo(1));
                Assert.That(report.PdfExportCalls, Is.EqualTo(1));
                Assert.That(report.CsvExportCalls, Is.EqualTo(0));
                Assert.That(report.JsonExportCalls, Is.EqualTo(1));
            });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(ReportQueries.addGeneratedReport));
                Assert.That(GetAnonymousProperty<string>(apiConnection.LastVariables!, "report_name"), Is.EqualTo("Report"));
                Assert.That(GetAnonymousProperty<int>(apiConnection.LastVariables!, "report_owner_id"), Is.EqualTo(77));
                Assert.That(GetAnonymousProperty<int?>(apiConnection.LastVariables!, "report_type"), Is.EqualTo((int)ReportType.OwnerRecertification));
                Assert.That(GetAnonymousProperty<string?>(apiConnection.LastVariables!, "report_pdf"), Is.Not.Null);
                Assert.That(GetAnonymousProperty<string?>(apiConnection.LastVariables!, "report_json"), Is.Not.Null);
                Assert.That(GetAnonymousProperty<string?>(apiConnection.LastVariables!, "report_html"), Is.Null);
                Assert.That(GetAnonymousProperty<string?>(apiConnection.LastVariables!, "report_csv"), Is.Null);
                Assert.That(GetAnonymousProperty<bool>(apiConnection.LastVariables!, "read_only"), Is.False);
                Assert.That(GetAnonymousProperty<string>(apiConnection.LastVariables!, "description"), Is.EqualTo("export-description"));
            });

            Assert.That(wrapper.FindAll("btn.btn-sm.btn-info"), Is.Not.Empty);
            var downloadButton = wrapper.FindAll("btn.btn-sm.btn-info")
                .Single(element => element.TextContent.Contains("download_pdf"));
            await wrapper.InvokeAsync(() => downloadButton.Click());
            await WaitForConditionAsync(() => downloadInvocation.Invocations.Count > 0);
            JSRuntimeInvocation invocation = downloadInvocation.Invocations.First();
            Assert.That(invocation.Arguments[0], Is.EqualTo("Report.pdf"));
            Assert.That(invocation.Arguments[1], Is.EqualTo("application/octet-stream"));
        }

        private static BunitContext CreateContext(out TrackingReportExportApiConnection apiConnection, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            userConfig = new SimulatedUserConfig
            {
                User =
                {
                    DbId = 0
                }
            };
            context.Services.AddSingleton<UserConfig>(userConfig);
            apiConnection = new TrackingReportExportApiConnection();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> RenderComponent(
            BunitContext context,
            TrackingReport? report,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages)
        {
            Action<Exception?, string, string, bool> displayMessage = (exception, title, message, isError) =>
            {
                if (messages != null)
                {
                    messages.Add((exception, title, message, isError));
                }
            };

            return context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                .Add(p => p.Value, displayMessage)
                .AddChildContent<ReportExport>(childParameters => childParameters
                    .Add(p => p.ReportToExport, report)));
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

        private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int pollMs = 25)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                {
                    Assert.Fail("Timed out waiting for asynchronous export work to complete.");
                }

                await Task.Delay(pollMs);
            }
        }

    }

    internal sealed class TrackingReportExportApiConnection : SimulatedApiConnection
    {
        public int AddGeneratedReportCalls { get; private set; }
        public string? LastQuery { get; private set; }
        public object? LastVariables { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(object) && query == ReportQueries.addGeneratedReport)
            {
                AddGeneratedReportCalls++;
                LastQuery = query;
                LastVariables = variables;
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            throw new NotImplementedException($"Unexpected query {query} for {typeof(QueryResponseType).Name}");
        }
    }

    internal sealed class TrackingReport : ReportBase
    {
        public int GetObjectsInReportCalls { get; private set; }
        public int HtmlExportCalls { get; private set; }
        public int CsvExportCalls { get; private set; }
        public int JsonExportCalls { get; private set; }
        public int PdfExportCalls { get; private set; }

        public TrackingReport(ReportType reportType)
            : base(new DynGraphqlQuery("export-test"), new SimulatedUserConfig(), reportType)
        {
        }

        public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            GetObjectsInReportCalls++;
            GotObjectsInReport = true;
            await callback(ReportData);
            return true;
        }

        public override string ExportToCsv()
        {
            CsvExportCalls++;
            return "csv-content";
        }

        public override string ExportToJson()
        {
            JsonExportCalls++;
            return "json-content";
        }

        public override string ExportToHtml()
        {
            HtmlExportCalls++;
            return "html-content";
        }

        public override string SetDescription()
        {
            return "export-description";
        }

        public override async Task<string?> ToPdf(string html, PaperFormat format)
        {
            PdfExportCalls++;
            await Task.CompletedTask;
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("pdf-content"));
        }
    }
}

using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
    }
}
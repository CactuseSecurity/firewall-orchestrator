using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Server.Jobs;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportJobTest
    {
        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(ReportJob).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(ReportJob).FullName, name);
        }

        [Test]
        public void RoundDown_RoundsToMinuteBoundary()
        {
            MethodInfo roundDown = GetPrivateStaticMethod("RoundDown");
            DateTime input = new(2026, 3, 27, 10, 15, 42, 987, DateTimeKind.Utc);

            DateTime rounded = (DateTime)roundDown.Invoke(null, [input, TimeSpan.FromMinutes(1)])!;

            ClassicAssert.AreEqual(new DateTime(2026, 3, 27, 10, 15, 0, DateTimeKind.Utc), rounded);
        }

        [Test]
        public async Task WriteReportFile_CsvCapableReport_WritesRequestedFormats()
        {
            MethodInfo writeReportFile = GetPrivateStaticMethod("WriteReportFile");
            TestReport report = new(ReportType.TicketChangeReport, detailedView: false);
            ReportFile reportFile = new();
            List<FileFormat> fileFormats =
            [
                new() { Name = GlobalConst.kCsv },
                new() { Name = GlobalConst.kHtml },
                new() { Name = GlobalConst.kPdf },
                new() { Name = GlobalConst.kJson }
            ];

            await (Task)writeReportFile.Invoke(null, [report, fileFormats, reportFile])!;

            ClassicAssert.AreEqual("json-content", reportFile.Json);
            ClassicAssert.AreEqual("csv-content", reportFile.Csv);
            ClassicAssert.AreEqual("html-content", reportFile.Html);
            ClassicAssert.AreEqual("pdf-content", reportFile.Pdf);
            Assert.That(report.PdfInputs, Has.Count.EqualTo(1));
            ClassicAssert.AreEqual("html-content", report.PdfInputs[0]);
            Assert.That(reportFile.GenerationDateEnd, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task WriteReportFile_DetailedWorkflowReport_SkipsCsvExport()
        {
            MethodInfo writeReportFile = GetPrivateStaticMethod("WriteReportFile");
            TestReport report = new(ReportType.TicketChangeReport, detailedView: true);
            ReportFile reportFile = new();
            List<FileFormat> fileFormats =
            [
                new() { Name = GlobalConst.kCsv },
                new() { Name = GlobalConst.kJson }
            ];

            await (Task)writeReportFile.Invoke(null, [report, fileFormats, reportFile])!;

            ClassicAssert.AreEqual("json-content", reportFile.Json);
            ClassicAssert.IsTrue(string.IsNullOrEmpty(reportFile.Csv));
            Assert.That(report.PdfInputs, Is.Empty);
        }

        private sealed class TestReport : ReportBase
        {
            internal List<string> PdfInputs { get; } = [];

            internal TestReport(ReportType reportType, bool detailedView)
                : base(new DynGraphqlQuery(""), new SimulatedUserConfig(), reportType)
            {
                ReportData = new()
                {
                    WorkflowFilter = new WorkflowFilter { DetailedView = detailedView }
                };
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return "csv-content";
            }

            public override string ExportToJson()
            {
                return "json-content";
            }

            public override string ExportToHtml()
            {
                return "html-content";
            }

            public override string SetDescription()
            {
                return "";
            }

            public override Task<string?> ToPdf(string html)
            {
                PdfInputs.Add(html);
                return Task.FromResult<string?>("pdf-content");
            }
        }
    }
}

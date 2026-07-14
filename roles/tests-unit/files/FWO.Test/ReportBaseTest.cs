using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report.Data;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;
using PuppeteerSharp.Media;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class ReportBaseTest
    {
        private sealed class TestReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public static string OutputCsvPublic(string? input)
            {
                return OutputCsv(input);
            }

            public static string ToUtcStringPublic(string? input)
            {
                return ToUtcString(input);
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                return string.Empty;
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class FrameReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public string BuildHtmlFrame()
            {
                return GenerateHtmlFrameBase("Frame Title", "", DateTime.Parse("2026-01-01T00:00:00Z"), new StringBuilder("<p>frame body</p>"));
            }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                return BuildHtmlFrame();
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class LazyBodyReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public int ExportCalls { get; private set; }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                ExportCalls++;
                htmlBodyExport = "<p>lazy frame body</p>";
                htmlBodyExportValid = true;
                return "<html><body><p>lazy frame body</p></body></html>";
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        private sealed class HtmlOnlyReportBase() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport)
        {
            public int ExportCalls { get; private set; }

            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return string.Empty;
            }

            public override string ExportToJson()
            {
                return string.Empty;
            }

            public override string ExportToHtml()
            {
                ExportCalls++;
                htmlExport = "<html><body><p>html only</p></body></html>";
                return htmlExport;
            }

            public override string SetDescription()
            {
                return string.Empty;
            }
        }

        [Test]
        public void OutputCsvEscapesQuotesAndNull()
        {
            Assert.That(TestReportBase.OutputCsvPublic("a\"b"), Is.EqualTo("\"a\"\"b\","));
            Assert.That(TestReportBase.OutputCsvPublic(null), Is.EqualTo("\"\","));
        }

        [Test]
        public void GetLinkAddressUsesReportPrefixForNonChangeReports()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.report, "rep42", "rule", 3, 99, ReportType.Rules);

            Assert.That(link, Is.EqualTo($"{PageName.ReportGeneration}#goto-report-rep42-rule3x99"));
        }

        [Test]
        public void GetLinkAddressUsesAllPrefixForChangeReports()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.report, "rep42", "rule", 3, 99, ReportType.Changes);

            Assert.That(link, Is.EqualTo($"{PageName.ReportGeneration}#goto-all-rep42-rule3x99"));
        }

        [Test]
        public void GetLinkAddressUsesCertificationPageOutsideReportLocation()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.certification, "rep42", "svc", 1, 5, ReportType.Rules);

            Assert.That(link, Is.EqualTo($"{PageName.Certification}#goto-report-rep42-svc1x5"));
        }

        [Test]
        public void GetLinkAddressUsesHashOnlyForExportLocation()
        {
            string link = ReportBase.GetLinkAddress(OutputLocation.export, "rep42", "nwobj", 2, 7, ReportType.Changes);

            Assert.That(link, Is.EqualTo("#nwobj2x7"));
        }

        [Test]
        public void ConstructLinkBuildsExpectedAnchorHtml()
        {
            string link = ReportBase.ConstructLink("icon-test", "Test Name", "color:red;", "#dest");

            Assert.That(link, Is.EqualTo("<span class=\"icon-test\">&nbsp;</span><a onclick=\"event.stopPropagation();\" href=\"#dest\" target=\"_top\" style=\"color:red;\">Test Name</a>"));
        }

        [Test]
        public void ExportToHtmlBodyReturnsBodyFragmentForFramedReports()
        {
            FrameReportBase report = new();

            string html = report.ExportToHtml();
            string body = report.ExportToHtmlBody();

            Assert.That(html, Does.Contain("<html>"));
            Assert.That(body, Does.Contain("<style>"));
            Assert.That(body, Does.Contain("border-collapse"));
            Assert.That(body, Does.Not.Contain("<html>"));
            Assert.That(body, Does.Not.Contain("<body>"));
            Assert.That(body, Does.Contain("<p>frame body</p>"));
        }

        [Test]
        public void ExportToHtmlBodyTriggersExportWhenBodyIsNotYetCached()
        {
            LazyBodyReportBase report = new();

            string body = report.ExportToHtmlBody();

            Assert.That(report.ExportCalls, Is.EqualTo(1));
            Assert.That(body, Is.EqualTo("<p>lazy frame body</p>"));
        }

        [Test]
        public void ExportToHtmlBodyFallsBackToFullHtmlWhenBodyIsNeverPopulated()
        {
            HtmlOnlyReportBase report = new();

            string firstBody = report.ExportToHtmlBody();
            string secondBody = report.ExportToHtmlBody();

            Assert.That(report.ExportCalls, Is.EqualTo(1));
            Assert.That(firstBody, Is.EqualTo("<html><body><p>html only</p></body></html>"));
            Assert.That(secondBody, Is.EqualTo(firstBody));
        }

        [Test]
        public void GetPuppeteerPaperFormatReturnsCustomFormatFromDimensions()
        {
            TestReportBase report = new()
            {
                CustomWidth = 210,
                CustomHeight = 297
            };

            var format = report.GetPuppeteerPaperFormat(FWO.Report.PaperFormat.Custom);

            Assert.That(format, Is.Not.Null);
            Assert.That(format!.Width, Is.EqualTo(210));
            Assert.That(format.Height, Is.EqualTo(297));
        }

        [Test]
        public void GetPuppeteerPaperFormatReturnsNullForUnknownFormat()
        {
            TestReportBase report = new();

            var format = report.GetPuppeteerPaperFormat((FWO.Report.PaperFormat)999);

            Assert.That(format, Is.Null);
        }

        [Test]
        public void ToUtcStringConvertsValidDatesAndKeepsInvalidText()
        {
            Assert.That(TestReportBase.ToUtcStringPublic("2026-07-08T10:30:00+02:00"), Does.Contain("2026-07-08T08:30:00"));
            Assert.That(TestReportBase.ToUtcStringPublic("not-a-date"), Is.EqualTo("not-a-date"));
            Assert.That(TestReportBase.ToUtcStringPublic(null), Is.EqualTo(""));
        }

        [Test]
        public void CreateTOCContentBuildsNestedEntries()
        {
            List<ToCHeader> toc = ReportBase.CreateTOCContent("<h2>Top</h2><h4>Child</h4><h5>Grandchild</h5><h6>GreatGrandchild</h6>");

            Assert.That(toc, Has.Count.EqualTo(1));
            Assert.That(toc[0].Title, Is.EqualTo("Top"));
            Assert.That(toc[0].Items, Has.Count.EqualTo(1));
            Assert.That(toc[0].Items[0].Title, Is.EqualTo("Child"));
            Assert.That(toc[0].Items[0].SubItems, Has.Count.EqualTo(1));
            Assert.That(toc[0].Items[0].SubItems[0].Title, Is.EqualTo("Grandchild"));
            Assert.That(toc[0].Items[0].SubItems[0].SubItems, Has.Count.EqualTo(1));
            Assert.That(toc[0].Items[0].SubItems[0].SubItems[0].Title, Is.EqualTo("GreatGrandchild"));
        }

        [Test]
        public void BuildHTMLToCReturnsEmptyWithoutHeadings()
        {
            FrameReportBase report = new();

            string toc = report.BuildHTMLToC("<p>No headings here</p>");

            Assert.That(toc, Is.EqualTo(""));
        }

        [Test]
        public void BuildHTMLToCRendersTableOfContentsWithHeadings()
        {
            FrameReportBase report = new();

            string toc = report.BuildHTMLToC("<h2>Top</h2><h4>Child</h4>");

            Assert.That(toc, Does.Contain("Table of content"));
            Assert.That(toc, Does.Contain("Top"));
            Assert.That(toc, Does.Contain("Child"));
            Assert.That(toc, Does.Contain("<ul class=\"toc_list\">"));
        }

        [Test]
        public void GetIconClassMapsKnownObjectTypes()
        {
            Assert.That(ReportBase.GetIconClass(ObjCategory.user, ObjectType.Group), Is.EqualTo(Icons.UserGroup));
            Assert.That(ReportBase.GetIconClass(ObjCategory.nobj, ObjectType.Host), Is.EqualTo(Icons.Host));
            Assert.That(ReportBase.GetIconClass(ObjCategory.nsrv, ObjectType.AccessRole), Is.EqualTo(Icons.User));
            Assert.That(ReportBase.GetIconClass(null, null), Is.EqualTo(""));
        }
    }
}

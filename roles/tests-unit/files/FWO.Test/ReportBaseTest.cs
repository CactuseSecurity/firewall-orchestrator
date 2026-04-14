using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;

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
    }
}

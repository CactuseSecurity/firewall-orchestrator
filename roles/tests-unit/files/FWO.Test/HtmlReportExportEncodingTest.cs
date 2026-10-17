using AngleSharp;
using AngleSharp.Dom;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Data.ViewData;
using FWO.Report.Filter;
using FWO.Test.Mocks;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The html exports of the connection, variance, change and compliance reports place stored modelling,
    /// owner and firewall data into markup. Each of them is fed a payload with an element, an event handler
    /// attribute and quotes, and the export is checked for elements that payload would have created
    /// (GHSA-mm29-m6vq-gwgg). The compliance csv export is checked for cells a quote could end early.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HtmlReportExportEncodingTest
    {
        private const string kPayload = "<img src=x onerror=console.log(1)>\"'&amp;";
        private const string kReasonPayload = "<b onclick=console.log(1)>bold</b><img src=x onerror=console.log(1)>";
        private const string kQuoteBreakout = "x\";\"=1+1";

        private static readonly List<ModellingAppServer> PayloadAppServers = [PayloadAppServer()];
        private static readonly List<string> PayloadUnusedObjects = [kPayload];
        private static readonly List<ComplianceViolation> PayloadViolations = [new() { Details = kPayload }];

        private readonly SimulatedUserConfig userConfig = new();
        private readonly DynGraphqlQuery query = new("TestFilter") { ReportTimeString = "2023-04-20T17:50:04" };
        private readonly TimeFilter timeFilter = new()
        {
            TimeRangeType = TimeRangeType.Fixeddates,
            StartTime = DateTime.Parse("2023-04-19T17:00:04"),
            EndTime = DateTime.Parse("2023-04-20T17:00:04")
        };

        private static ModellingAppServer PayloadAppServer()
        {
            return new() { Id = 11, Number = 1, Name = kPayload, Ip = "10.0.0.1/32", IpEnd = "10.0.0.1/32" };
        }

        private static ModellingConnection PayloadConnection(int id)
        {
            ModellingAppServer appServer = PayloadAppServer();
            ModellingService service = new() { Id = 31, Number = 1, Name = kPayload, Port = 1234, Protocol = new() { Id = 6, Name = kPayload } };
            return new()
            {
                Id = id,
                Name = kPayload,
                Reason = kReasonPayload,
                App = new() { Name = kPayload },
                SourceAppServers = [new() { Content = appServer }],
                DestinationAppServers = [new() { Content = appServer }],
                Services = [new() { Content = service }]
            };
        }

        private static OwnerConnectionReport PayloadOwnerReport()
        {
            ModellingConnection connection = PayloadConnection(101);
            ModellingConnection commonService = PayloadConnection(102);
            ModellingAppRole appRole = new() { Id = 21, Number = 3, Name = kPayload, IdString = "AR1", AppServers = [new() { Content = PayloadAppServer() }] };
            Rule implementedRule = new()
            {
                ManagementName = kPayload,
                DeviceName = kPayload,
                UnusedSpecialUserObjects = PayloadUnusedObjects
            };
            return new()
            {
                Name = kPayload,
                Owner = new() { Name = kPayload, ExtAppId = kPayload },
                ImplementationState = kPayload,
                Connections = [connection, commonService],
                RegularConnections = [connection],
                CommonServices = [commonService],
                MissingAppRoles = new() { [0] = [appRole] },
                DifferingAppRoles = new() { [0] = [appRole] },
                RuleDifferences = [new() { ModelledConnection = PayloadConnection(103), ImplementedRules = [implementedRule] }]
            };
        }

        private static IDocument Parse(string html)
        {
            return BrowsingContext.New(Configuration.Default).OpenAsync(request => request.Content(html)).GetAwaiter().GetResult();
        }

        private static void AssertNoInjectedMarkup(string html)
        {
            using IDocument document = Parse(html);
            Assert.Multiple(() =>
            {
                Assert.That(document.QuerySelectorAll("img"), Is.Empty, "a stored value created an <img> element");
                Assert.That(document.QuerySelectorAll("[onerror], b[onclick]"), Is.Empty, "a stored value created an event handler");
                Assert.That(html, Does.Not.Contain(kPayload), "a stored value reached the document unencoded");
                Assert.That(document.Body!.TextContent, Does.Contain(kPayload), "the value is no longer shown as text");
            });
        }

        [Test]
        public void ReportConnections_ExportToHtml_EncodesConnectionData()
        {
            ReportConnections report = new(query, userConfig, ReportType.Connections)
            {
                ReportData = new() { OwnerData = [PayloadOwnerReport()] }
            };

            string html = report.ExportToHtml();

            AssertNoInjectedMarkup(html);
            using IDocument document = Parse(html);
            Assert.That(document.QuerySelectorAll("b").Select(element => element.TextContent), Does.Contain("bold"),
                "the formatting of the reason is kept");
        }

        [Test]
        public void ReportConnections_ExportToHtml_EncodesGlobalCommonServiceApp()
        {
            ReportConnections report = new(query, userConfig, ReportType.Connections)
            {
                ReportData = new() { GlobalComSvc = [new() { GlobalComSvcs = [PayloadConnection(104)] }] }
            };

            AssertNoInjectedMarkup(report.ExportToHtml());
        }

        [Test]
        public void ReportVariances_ExportToHtml_EncodesOwnerAndDifferenceData()
        {
            ReportVariances report = new(query, userConfig, ReportType.VarianceAnalysis)
            {
                ReportData = new() { OwnerData = [PayloadOwnerReport()] }
            };

            AssertNoInjectedMarkup(report.ExportToHtml());
        }

        [Test]
        public void ReportChanges_ExportToHtml_EncodesManagementName()
        {
            ReportChanges report = new(query, userConfig, ReportType.Changes, timeFilter, false)
            {
                ReportData = new() { ManagementData = [new() { Name = kPayload }] }
            };

            AssertNoInjectedMarkup(report.ExportToHtml());
        }

        [Test]
        public void ReportCompliance_SetComplianceData_EncodesViolationDetails()
        {
            MockReportCompliance report = new(new(""), userConfig, ReportType.ComplianceReport);
            Rule rule = new() { Violations = PayloadViolations };

            report.SetComplianceDataForRulePublic(rule);

            Assert.That(rule.ViolationDetails, Is.EqualTo(HtmlOutputEncoder.EncodeText(kPayload)));
        }

        [Test]
        public void ReportCompliance_ExportToCsv_DecodesOnlyHtmlColumnsAndEscapesQuotes()
        {
            MockReportCompliance report = new(new(""), userConfig, ReportType.ComplianceReport);
            report.RuleViewData =
            [
                new RuleViewData
                {
                    Name = "R&amp;D",
                    Source = $"<span>{HtmlOutputEncoder.EncodeText(kQuoteBreakout)}</span>",
                    ViolationDetails = HtmlOutputEncoder.EncodeText("a & b"),
                    Comment = kQuoteBreakout,
                    Show = true
                }
            ];

            string csv = report.ExportToCsv();

            Assert.Multiple(() =>
            {
                Assert.That(csv, Does.Contain("\"R&amp;D\""), "a plain column is exported as it is");
                Assert.That(csv, Does.Contain("\"<span>x\"\";\"\"=1+1</span>\""), "an html column is decoded and its quotes doubled");
                Assert.That(csv, Does.Contain("\"a & b\""), "the encoded violation details are decoded");
                Assert.That(csv, Does.Contain("\"x\"\";\"\"=1+1\""), "quotes in a plain column are doubled");
                Assert.That(csv, Does.Not.Contain("\"x\";\""), "no quote ends a cell early");
            });
        }

        [Test]
        public void ModellingNwObject_DisplayHtml_EncodesTooltipOfDeletedObject()
        {
            ModellingAppRole appRole = new() { Name = kPayload, IdString = "AR1", IsDeleted = true, TooltipText = kPayload };

            string html = appRole.DisplayHtml();

            using IDocument document = Parse(html);
            Assert.Multiple(() =>
            {
                Assert.That(document.QuerySelectorAll("img"), Is.Empty);
                Assert.That(document.QuerySelector("span[title]")?.GetAttribute("title"), Is.EqualTo(kPayload),
                    "the tooltip keeps the whole value inside its attribute");
            });
        }

        [Test]
        public void ConnectionReport_ListAppServers_EncodesNamesInDiffMode()
        {
            string html = ConnectionReport.ListAppServers(PayloadAppServers, PayloadAppServers, true, true);

            AssertNoInjectedMarkup(html);
        }
    }
}

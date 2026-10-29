using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;
using System.Net;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Data values written into report HTML exports must arrive as text, not as markup.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ReportHtmlEncodingTest
    {
        // element, attribute breakout and quote payloads in one value
        private const string kPayload = "<b>x</b>\" onmouseover=\"x'";
        private static readonly string kEncodedPayload = WebUtility.HtmlEncode(kPayload);

        [Test]
        public void TicketReport_EncodesTicketTaskAndApprovalValues()
        {
            WorkflowFilter workflowFilter = new() { DetailedView = true, ShowFullTicket = true };
            workflowFilter.AddInfoFilter.Name = kPayload;
            ReportTickets report = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.TicketReport, workflowFilter);
            // Generate copies the filter into the report data; this test fills the report data directly
            report.ReportData.WorkflowFilter = new(workflowFilter);
            report.ReportData.WorkflowStateNames[1] = kPayload;
            report.ReportData.Tickets.Add(CreateTicket());

            string html = report.ExportToHtml();

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain("<b>x</b>"));
                Assert.That(html, Does.Not.Contain("\" onmouseover"));
                Assert.That(html, Does.Contain($"<th>{kEncodedPayload}</th>"), "add-info column header");
                // ticket title, requester, ticket state, add-info value, task title, task state,
                // implementation task title and state, approver group and approval state
                Assert.That(CountCells(html), Is.EqualTo(10));
            });
        }

        [Test]
        public void StatisticsReport_EncodesDeviceName()
        {
            ReportStatistics report = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Statistics);
            ManagementReport management = new() { Name = "mgmt" };
            management.Devices = [new DeviceReport { Name = kPayload, RuleStatistics = new ObjectStatistics() }];
            report.ReportData.ManagementData.Add(management);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Not.Contain("<b>x</b>"));
            Assert.That(CountCells(html), Is.EqualTo(1));
        }

        private static int CountCells(string html)
        {
            return html.Split($"<td>{kEncodedPayload}</td>").Length - 1;
        }

        private static WfTicket CreateTicket()
        {
            string addInfo = JsonSerializer.Serialize(new Dictionary<string, string> { [kPayload] = kPayload });
            return new WfTicket
            {
                Id = 1,
                Title = kPayload,
                StateId = 1,
                Requester = new UiUser { Name = kPayload },
                Tasks =
                [
                    new WfReqTask
                    {
                        Id = 2,
                        TaskNumber = 1,
                        Title = kPayload,
                        StateId = 1,
                        AdditionalInfo = addInfo,
                        ImplementationTasks = [new WfImplTask { Id = 3, TaskNumber = 1, Title = kPayload, StateId = 1 }],
                        Approvals = [new WfApproval { Id = 4, ApproverGroup = kPayload, StateId = 1 }]
                    }
                ]
            };
        }
    }
}

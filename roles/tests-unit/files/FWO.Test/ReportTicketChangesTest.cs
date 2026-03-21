using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class ReportTicketChangesTest
    {
        private class ReportTicketChangesApiConnection(List<WfTicket> tickets) : ApiConnection
        {
            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (typeof(QueryResponseType) == typeof(List<WfTicket>))
                {
                    return Task.FromResult((QueryResponseType)(object)tickets);
                }
                if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WfState> { new() { Id = 9, Name = "done" } });
                }
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>() { }
            protected override void Dispose(bool disposing) { }
        }

        [Test]
        [Parallelizable]
        public async Task TicketChangeReport_GenerateStoresReportedTickets()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            ReportBase report = ReportBase.ConstructReport(template, new UserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1001,
                    Title = "Closed ticket",
                    StateId = 9,
                    Priority = 2,
                    Tasks = [new WfReqTask()],
                    Requester = new UiUser { Name = "reporter" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.Tickets, Has.Count.EqualTo(1));
            Assert.That(report.ReportData.Tickets[0].Id, Is.EqualTo(1001));
            Assert.That(report.ReportData.ElementsCount, Is.EqualTo(1));
            Assert.That(report.ReportData.WorkflowStateNames[9], Is.EqualTo("done"));
        }

        [Test]
        [Parallelizable]
        public void TicketChangeReport_ConstructReportReturnsTicketChangeReport()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;

            ReportBase report = ReportBase.ConstructReport(template, new UserConfig());

            Assert.That(report, Is.TypeOf<ReportTicketChanges>());
        }

        [Test]
        [Parallelizable]
        public async Task TicketChangeReport_ExportToHtml_UsesCurrentColumnsAndStateNames()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.ShowFullTicket = true;
            ReportBase report = ReportBase.ConstructReport(template, new UserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1001,
                    Title = "Closed ticket",
                    StateId = 9,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 201,
                            TaskNumber = "REQ-1",
                            Title = "Request task",
                            StateId = 9
                        }
                    ],
                    Requester = new UiUser { Name = "reporter" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            StringAssert.Contains("<th>name</th>", html);
            StringAssert.Contains("<th>created</th>", html);
            StringAssert.Contains("<th>closed</th>", html);
            StringAssert.DoesNotContain("<th>priority</th>", html);
            StringAssert.DoesNotContain("<th>deadline</th>", html);
            StringAssert.Contains("<td>done</td>", html);
            StringAssert.Contains("REQ-1", html);
        }
    }
}

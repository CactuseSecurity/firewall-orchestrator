using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using FWO.Services.Workflow;
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
            public Dictionary<string, object>? LastQueryVariables { get; private set; }

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
                    LastQueryVariables = variables as Dictionary<string, object>;
                    return Task.FromResult((QueryResponseType)(object)tickets);
                }
                if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WfState> { new() { Id = 9, Name = "done" } });
                }
                if (typeof(QueryResponseType) == typeof(List<GlobalStateMatrixHelper>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<GlobalStateMatrixHelper>
                    {
                        new()
                        {
                            ConfData = """
                                {"config_value":{"request":{"matrix":{},"derived_states":{},"lowest_input_state":0,"lowest_start_state":0,"lowest_end_state":49,"active":true},"implementation":{"matrix":{},"derived_states":{},"lowest_input_state":99,"lowest_start_state":210,"lowest_end_state":249,"active":true}}}
                                """
                        }
                    });
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
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
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

            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());

            Assert.That(report, Is.TypeOf<ReportTicketChanges>());
        }

        [Test]
        [Parallelizable]
        public void TicketReport_ConstructReportReturnsReportTickets()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketReport;

            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());

            Assert.That(report, Is.TypeOf<ReportTickets>());
        }

        [Test]
        [Parallelizable]
        public async Task TicketReport_GenerateResolvesPhaseToStateRange()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.Phase = "implementation";
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            ReportTicketChangesApiConnection apiConnection = new([]);

            await report.Generate(0, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(apiConnection.LastQueryVariables, Is.Not.Null);
            Assert.That(apiConnection.LastQueryVariables!["phase_lowest_input_state"], Is.EqualTo(99));
            Assert.That(apiConnection.LastQueryVariables!["phase_lowest_end_state"], Is.EqualTo(249));
        }

        [Test]
        [Parallelizable]
        public async Task TicketChangeReport_GenerateResolvesPhaseToStateRange()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.Phase = "implementation";
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            ReportTicketChangesApiConnection apiConnection = new([]);

            await report.Generate(0, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(apiConnection.LastQueryVariables, Is.Not.Null);
            Assert.That(apiConnection.LastQueryVariables!["phase_lowest_input_state"], Is.EqualTo(99));
            Assert.That(apiConnection.LastQueryVariables!["phase_lowest_end_state"], Is.EqualTo(249));
        }

        [Test]
        [Parallelizable]
        public async Task TicketReport_GenerateResolvesClosedPhaseToOpenEndedStateRange()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.Phase = GlobalConst.kClosed;
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            ReportTicketChangesApiConnection apiConnection = new([]);

            await report.Generate(0, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(apiConnection.LastQueryVariables, Is.Not.Null);
            Assert.That(apiConnection.LastQueryVariables!["phase_lowest_input_state"], Is.EqualTo(249));
            Assert.That(apiConnection.LastQueryVariables.ContainsKey("phase_lowest_end_state"), Is.False);
        }

        [Test]
        [Parallelizable]
        public async Task TicketChangeReport_ExportToHtml_UsesCurrentColumnsAndStateNames()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.DetailedView = true;
            template.ReportParams.WorkflowFilter.ShowFullTicket = true;
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
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
                            TaskNumber = 1,
                            Title = "Request task",
                            StateId = 9
                        }
                    ],
                    Requester = new UiUser { Name = "reporter" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain("<th>Name</th>"));
            Assert.That(html, Does.Contain("<th>Created</th>"));
            Assert.That(html, Does.Contain("<th>Closed</th>"));
            Assert.That(html, Does.Not.Contain("<th>Priority</th>"));
            Assert.That(html, Does.Not.Contain("<th>Deadline</th>"));
            Assert.That(html, Does.Contain("<td>done</td>"));
            Assert.That(html, Does.Contain(">1<"));
            Assert.That(html, Does.Contain(">Tasks<"));
            Assert.That(html, Does.Contain(">Request task<"));
        }

        [Test]
        [Parallelizable]
        public async Task TicketChangeReport_ExportToHtml_ShowsRequestTasksForTicketCreationReferenceDate()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.DetailedView = true;
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.TicketCreation;
            template.ReportParams.WorkflowFilter.ShowFullTicket = false;
            template.ReportParams.WorkflowFilter.TaskTypes = [WfTaskType.access];
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1002,
                    Title = "Created ticket",
                    StateId = 9,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 301,
                            TaskNumber = 7,
                            Title = "Visible request task",
                            StateId = 9,
                            TaskType = WfTaskType.access.ToString()
                        }
                    ],
                    Requester = new UiUser { Name = "creator" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain(">Visible request task<"));
            Assert.That(html, Does.Contain(">7<"));
        }

        [Test]
        [Parallelizable]
        public async Task TicketChangeReport_ExportToHtml_FiltersRequestTasksByTaskType()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketChangeReport;
            template.ReportParams.WorkflowFilter.DetailedView = true;
            template.ReportParams.WorkflowFilter.ReferenceDate = WorkflowReferenceDate.TicketCreation;
            template.ReportParams.WorkflowFilter.ShowFullTicket = false;
            template.ReportParams.WorkflowFilter.TaskTypes = [WfTaskType.access];
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1003,
                    Title = "Task type filtered ticket",
                    StateId = 9,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 401,
                            TaskNumber = 1,
                            Title = "Access task",
                            StateId = 9,
                            TaskType = WfTaskType.access.ToString()
                        },
                        new WfReqTask
                        {
                            Id = 402,
                            TaskNumber = 2,
                            Title = "Rule delete task",
                            StateId = 9,
                            TaskType = WfTaskType.rule_delete.ToString()
                        }
                    ],
                    Requester = new UiUser { Name = "creator" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain(">Access task<"));
            Assert.That(html, Does.Not.Contain(">Rule delete task<"));
        }

        [Test]
        [Parallelizable]
        public async Task TicketReport_ExportToHtml_FiltersRequestTasksByTaskType()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.DetailedView = true;
            template.ReportParams.WorkflowFilter.ShowFullTicket = false;
            template.ReportParams.WorkflowFilter.TaskTypes = [WfTaskType.access];
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1004,
                    Title = "Ticket report task filter",
                    StateId = 9,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 501,
                            TaskNumber = 1,
                            Title = "Shown access task",
                            StateId = 9,
                            TaskType = WfTaskType.access.ToString()
                        },
                        new WfReqTask
                        {
                            Id = 502,
                            TaskNumber = 2,
                            Title = "Hidden rule modify task",
                            StateId = 9,
                            TaskType = WfTaskType.rule_modify.ToString()
                        }
                    ],
                    Requester = new UiUser { Name = "reporter" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain(">Shown access task<"));
            Assert.That(html, Does.Not.Contain(">Hidden rule modify task<"));
        }

        [Test]
        [Parallelizable]
        public async Task TicketReport_ExportToHtml_HidesRequestTasksWhenDetailedViewIsDisabled()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.DetailedView = false;
            template.ReportParams.WorkflowFilter.ShowFullTicket = true;
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1005,
                    Title = "Compact ticket report",
                    StateId = 9,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 601,
                            TaskNumber = 1,
                            Title = "Hidden request task",
                            StateId = 9,
                            TaskType = WfTaskType.access.ToString()
                        }
                    ],
                    Requester = new UiUser { Name = "reporter" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain(">Compact ticket report<"));
            Assert.That(html, Does.Not.Contain(">Hidden request task<"));
            Assert.That(html, Does.Not.Contain(">Task Number<"));
        }

        [Test]
        [Parallelizable]
        public async Task TicketReport_ExportToHtml_ShowsSelectedLabelAsColumn()
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)ReportType.TicketReport;
            template.ReportParams.WorkflowFilter.LabelFilter = new() { Name = "policy_check", Mode = WorkflowLabelFilterMode.existing };
            ReportBase report = ReportBase.ConstructReport(template, new SimulatedUserConfig());
            List<WfTicket> tickets =
            [
                new()
                {
                    Id = 1006,
                    Title = "Label ticket report",
                    StateId = 9,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 701,
                            TaskNumber = 1,
                            Title = "Request task",
                            StateId = 9,
                            AdditionalInfo = "{\"policy_check\":\"true\"}"
                        }
                    ],
                    Requester = new UiUser { Name = "reporter" }
                }
            ];

            await report.Generate(0, new ReportTicketChangesApiConnection(tickets), _ => Task.CompletedTask, CancellationToken.None);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain("<th>policy_check</th>"));
            Assert.That(html, Does.Contain("<td>true</td>"));
        }
    }
}

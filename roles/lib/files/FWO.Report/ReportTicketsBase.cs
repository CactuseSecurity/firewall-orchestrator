using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Report.Filter;
using FWO.Services.Workflow;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    /// <summary>
    /// Shared base for workflow ticket reports.
    /// </summary>
    public abstract class ReportTicketsBase(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, WorkflowFilter workflowFilter)
        : ReportBase(query, userConfig, reportType)
    {
        /// <inheritdoc />
        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await ResolvePhaseFilterAsync(apiConnection);
            List<WfTicket> tickets = await apiConnection.SendQueryAsync<List<WfTicket>>(Query.FullQuery, Query.QueryVariables);
            tickets = await FilterVisibleTicketsAsync(apiConnection, tickets);
            List<WfState> workflowStates = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
            ReportData.WorkflowStateNames = workflowStates.ToDictionary(state => state.Id, state => state.Name);
            ReportData.WorkflowFilter = new(workflowFilter);
            ReportData.Tickets = tickets;
            ReportData.ElementsCount = tickets.Count;
            await callback(ReportData);
        }

        /// <summary>
        /// Applies the same owner-based workflow visibility rule used by the request pages.
        /// </summary>
        private async Task<List<WfTicket>> FilterVisibleTicketsAsync(ApiConnection apiConnection, List<WfTicket> tickets)
        {
            int ticketCountBeforeFilter = tickets.Count;
            if (!userConfig.ReqOwnerBased
                || userConfig.User.Roles.Contains(Roles.Admin)
                || userConfig.User.Roles.Contains(Roles.Auditor))
            {
                Log.WriteDebug("Workflow Report Filter", $"Skipping owner-based filtering: reqOwnerBased={userConfig.ReqOwnerBased}, userId={userConfig.User.DbId}, roles=[{string.Join(", ", userConfig.User.Roles)}], ticketCount={ticketCountBeforeFilter}");
                return tickets;
            }

            if (userConfig.User.Ownerships.Count == 0)
            {
                Log.WriteDebug("Workflow Report Filter", $"Owner-based filtering removed all tickets because no ownerships were available: reqOwnerBased={userConfig.ReqOwnerBased}, userId={userConfig.User.DbId}, roles=[{string.Join(", ", userConfig.User.Roles)}], ticketCountBefore={ticketCountBeforeFilter}");
                return [];
            }

            List<long> registeredTickets = (await apiConnection.SendQueryAsync<List<TicketId>>(
                RequestQueries.getOwnerTicketIds,
                new { ownerIds = userConfig.User.Ownerships }))
                .ConvertAll(ticket => ticket.Id);

            List<WfTicket> visibleTickets = [.. tickets.Where(ticket => ticket.IsVisibleForOwner(registeredTickets, userConfig.User.Ownerships, userConfig.User.DbId))];
            Log.WriteDebug("Workflow Report Filter", $"Applied owner-based filtering: reqOwnerBased={userConfig.ReqOwnerBased}, userId={userConfig.User.DbId}, roles=[{string.Join(", ", userConfig.User.Roles)}], ownershipCount={userConfig.User.Ownerships.Count}, ownerIds=[{string.Join(", ", userConfig.User.Ownerships)}], registeredTicketCount={registeredTickets.Count}, ticketCountBefore={ticketCountBeforeFilter}, ticketCountAfter={visibleTickets.Count}");
            return visibleTickets;
        }

        /// <inheritdoc />
        public override string ExportToCsv()
        {
            if (ReportData.WorkflowFilter.DetailedView)
            {
                throw new NotImplementedException("CSV export is only supported for workflow reports with detailed view disabled.");
            }

            StringBuilder report = new();
            report.Append(DisplayWorkflowReportHeaderCsv());
            report.Append($"\"{userConfig.GetText("id")}\",");
            report.Append($"\"{userConfig.GetText("name")}\",");
            report.Append($"\"{userConfig.GetText("tasks")}\",");
            report.Append($"\"{userConfig.GetText("requester")}\",");
            report.Append($"\"{userConfig.GetText("state")}\",");
            report.Append($"\"{userConfig.GetText("created")}\",");
            report.Append($"\"{userConfig.GetText("closed")}\"");
            if (HasLabelColumn())
            {
                report.Append($",\"{workflowFilter.LabelFilter.Name}\"");
            }
            report.AppendLine("");

            foreach (WfTicket ticket in ReportData.Tickets.OrderBy(ticket => ticket.Id))
            {
                report.Append(OutputCsv(ticket.Id.ToString()));
                report.Append(OutputCsv(ticket.Title));
                report.Append(OutputCsv(ticket.Tasks.Count.ToString()));
                report.Append(OutputCsv(ticket.Requester?.Name));
                report.Append(OutputCsv(ResolveStateName(ticket.StateId)));
                report.Append(OutputCsv(ticket.CreationDate.ToString()));
                report.Append(OutputCsv(ticket.CompletionDate.ToString()));
                if (HasLabelColumn())
                {
                    report.Append(OutputCsv(GetLabelValue(ticket)));
                }
                report.Length--;
                report.AppendLine("");
            }

            return report.ToString();
        }

        /// <inheritdoc />
        public override string ExportToJson()
        {
            return JsonSerializer.Serialize(ReportData.Tickets, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <inheritdoc />
        public override string ExportToHtml()
        {
            StringBuilder report = new();
            report.AppendLine("<table>");
            AppendTicketTableHeader(report);

            foreach (WfTicket ticket in ReportData.Tickets.OrderBy(ticket => ticket.Id))
            {
                AppendTicketRow(report, ticket);
                AppendTaskDetailsSection(report, ticket);
            }

            report.AppendLine("</table>");
            return GenerateHtmlFrameBase(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report, BuildWorkflowFilterSummary());
        }

        /// <inheritdoc />
        public override string SetDescription()
        {
            return $"{ReportData.Tickets.Count} {userConfig.GetText("tickets")}";
        }

        /// <summary>
        /// Selects the request tasks to display for one ticket.
        /// </summary>
        protected abstract List<WfReqTask> GetDisplayedTasks(WfTicket ticket);

        /// <summary>
        /// Indicates whether implementation tasks should be rendered.
        /// </summary>
        protected abstract bool ShowImplementationTasks();

        /// <summary>
        /// Indicates whether approvals should be rendered.
        /// </summary>
        protected abstract bool ShowApprovals();

        /// <summary>
        /// Selects the implementation tasks to display for one request task.
        /// </summary>
        protected abstract List<WfImplTask> GetDisplayedImplementationTasks(WfReqTask task);

        /// <summary>
        /// Selects the approvals to display for one request task.
        /// </summary>
        protected abstract List<WfApproval> GetDisplayedApprovals(WfReqTask task);

        private string ResolveStateName(int stateId)
        {
            return ReportData.WorkflowStateNames.TryGetValue(stateId, out string? stateName) ? stateName : stateId.ToString();
        }

        private void AppendTicketTableHeader(StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("tasks")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("requester")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("created")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("closed")}</th>");
            if (HasLabelColumn())
            {
                report.AppendLine($"<th>{workflowFilter.LabelFilter.Name}</th>");
            }
            report.AppendLine("</tr>");
        }

        private void AppendTicketRow(StringBuilder report, WfTicket ticket)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{ticket.Id}</td>");
            report.AppendLine($"<td>{ticket.Title}</td>");
            report.AppendLine($"<td>{ticket.Tasks.Count}</td>");
            report.AppendLine($"<td>{ticket.Requester?.Name}</td>");
            report.AppendLine($"<td>{ResolveStateName(ticket.StateId)}</td>");
            report.AppendLine($"<td>{ticket.CreationDate}</td>");
            report.AppendLine($"<td>{ticket.CompletionDate}</td>");
            if (HasLabelColumn())
            {
                report.AppendLine($"<td>{GetLabelValue(ticket)}</td>");
            }
            report.AppendLine("</tr>");
        }

        private void AppendTaskDetailsSection(StringBuilder report, WfTicket ticket)
        {
            List<WfReqTask> displayedTasks = GetDisplayedTasks(ticket);
            if (displayedTasks.Count == 0)
            {
                return;
            }

            report.AppendLine($"<tr><td colspan=\"{GetTicketColumnCount()}\">");
            report.AppendLine($"<b>{userConfig.GetText("tasks")}</b>");
            report.AppendLine("<table>");
            AppendTaskTableHeader(report);

            foreach (WfReqTask task in displayedTasks)
            {
                AppendTaskRow(report, task);
                AppendImplementationTasks(report, task);
                AppendApprovals(report, task);
            }

            report.AppendLine("</table>");
            report.AppendLine("</td></tr>");
        }

        private void AppendTaskTableHeader(StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("task_number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("start_time")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("end_time")}</th>");
            report.AppendLine("</tr>");
        }

        private void AppendTaskRow(StringBuilder report, WfReqTask task)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{task.Id}</td>");
            report.AppendLine($"<td>{task.TaskNumber}</td>");
            report.AppendLine($"<td>{task.Title}</td>");
            report.AppendLine($"<td>{ResolveStateName(task.StateId)}</td>");
            report.AppendLine($"<td>{task.Start}</td>");
            report.AppendLine($"<td>{task.Stop}</td>");
            report.AppendLine("</tr>");
        }

        private void AppendImplementationTasks(StringBuilder report, WfReqTask task)
        {
            if (!ShowImplementationTasks())
            {
                return;
            }

            List<WfImplTask> implementationTasks = GetDisplayedImplementationTasks(task);
            if (implementationTasks.Count == 0)
            {
                return;
            }

            report.AppendLine($"<tr><td colspan=\"6\"><b>{userConfig.GetText("implementation")}</b>");
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("task_number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("start_time")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("end_time")}</th>");
            report.AppendLine("</tr>");

            foreach (WfImplTask implTask in implementationTasks)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{implTask.Id}</td>");
                report.AppendLine($"<td>{implTask.TaskNumber}</td>");
                report.AppendLine($"<td>{implTask.Title}</td>");
                report.AppendLine($"<td>{ResolveStateName(implTask.StateId)}</td>");
                report.AppendLine($"<td>{implTask.Start}</td>");
                report.AppendLine($"<td>{implTask.Stop}</td>");
                report.AppendLine("</tr>");
            }

            report.AppendLine("</table></td></tr>");
        }

        private void AppendApprovals(StringBuilder report, WfReqTask task)
        {
            if (!ShowApprovals())
            {
                return;
            }

            List<WfApproval> approvals = GetDisplayedApprovals(task);
            if (approvals.Count == 0)
            {
                return;
            }

            report.AppendLine($"<tr><td colspan=\"6\"><b>{userConfig.GetText("approval")}</b>");
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("opened")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("approved")}</th>");
            report.AppendLine("</tr>");

            foreach (WfApproval approval in approvals)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{approval.Id}</td>");
                report.AppendLine($"<td>{approval.ApproverGroup}</td>");
                report.AppendLine($"<td>{ResolveStateName(approval.StateId)}</td>");
                report.AppendLine($"<td>{approval.DateOpened}</td>");
                report.AppendLine($"<td>{approval.ApprovalDate}</td>");
                report.AppendLine("</tr>");
            }

            report.AppendLine("</table></td></tr>");
        }

        private bool HasLabelColumn()
        {
            return !string.IsNullOrWhiteSpace(workflowFilter.LabelFilter.Name);
        }

        private int GetTicketColumnCount()
        {
            return HasLabelColumn() ? 8 : 7;
        }

        private string BuildWorkflowFilterSummary()
        {
            List<string> filterParts = [];
            int allRealTaskTypesCount = Enum.GetValues(typeof(WfTaskType)).Cast<WfTaskType>().Count(taskType => taskType != WfTaskType.master);

            if (ReportType == ReportType.TicketChangeReport && ReportData.WorkflowFilter.ReferenceDate != WorkflowReferenceDate.AnyActivity)
            {
                filterParts.Add($"{userConfig.GetText("reference_date")}: {userConfig.GetText(ReportData.WorkflowFilter.ReferenceDate.ToString())}");
            }

            if (ReportData.WorkflowFilter.TaskTypes.Count > 0 && ReportData.WorkflowFilter.TaskTypes.Count < allRealTaskTypesCount)
            {
                string taskTypes = string.Join(", ", ReportData.WorkflowFilter.TaskTypes.Select(taskType => userConfig.GetText(taskType.ToString())));
                filterParts.Add($"{userConfig.GetText("task_type")}: {taskTypes}");
            }

            if (!string.IsNullOrWhiteSpace(ReportData.WorkflowFilter.Phase))
            {
                filterParts.Add($"{userConfig.GetText("phase")}: {userConfig.GetText(ReportData.WorkflowFilter.Phase)}");
            }

            if (ReportData.WorkflowFilter.StateIds.Count > 0)
            {
                string states = string.Join(", ", ReportData.WorkflowFilter.StateIds.Select(ResolveStateName));
                filterParts.Add($"{userConfig.GetText("state")}: {states}");
            }

            if (!string.IsNullOrWhiteSpace(ReportData.WorkflowFilter.LabelFilter.Name))
            {
                string labelFilter = ReportData.WorkflowFilter.LabelFilter.Mode == WorkflowLabelFilterMode.value
                    ? $"{ReportData.WorkflowFilter.LabelFilter.Name}={ReportData.WorkflowFilter.LabelFilter.Value}"
                    : $"{ReportData.WorkflowFilter.LabelFilter.Name} ({userConfig.GetText(ReportData.WorkflowFilter.LabelFilter.Mode.ToString())})";
                filterParts.Add($"{userConfig.GetText("label")}: {labelFilter}");
            }

            return string.Join("; ", filterParts);
        }

        private string DisplayWorkflowReportHeaderCsv()
        {
            StringBuilder report = new();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime():yyyy-MM-ddTHH:mm:ssK} (UTC)");
            if (ReportType == ReportType.TicketChangeReport
                && Query.QueryVariables.TryGetValue("ticket_time_start", out object? startObj)
                && Query.QueryVariables.TryGetValue("ticket_time_end", out object? endObj))
            {
                report.AppendLine($"# change time: from {ToUtcString(startObj?.ToString())}, until {ToUtcString(endObj?.ToString())}");
            }
            string workflowFilters = BuildWorkflowFilterSummary();
            if (!string.IsNullOrWhiteSpace(workflowFilters))
            {
                report.AppendLine($"# workflow filter: {workflowFilters}");
            }
            if (!string.IsNullOrWhiteSpace(Query.RawFilter))
            {
                report.AppendLine($"# other filters: {Query.RawFilter}");
            }
            report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
            report.AppendLine($"# data protection level: For internal use only");
            report.AppendLine("#");
            return report.ToString();
        }

        private string GetLabelValue(WfTicket ticket)
        {
            if (!HasLabelColumn())
            {
                return "";
            }

            List<string> labelValues =
            [
                .. ticket.Tasks
                    .Select(task => task.GetAddInfoValue(workflowFilter.LabelFilter.Name))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct()
            ];

            return string.Join(", ", labelValues);
        }

        /// <summary>
        /// Resolves the selected workflow phase to the current master ticket state range.
        /// </summary>
        private async Task ResolvePhaseFilterAsync(ApiConnection apiConnection)
        {
            if ((ReportType != ReportType.TicketReport && ReportType != ReportType.TicketChangeReport) || string.IsNullOrWhiteSpace(workflowFilter.Phase))
            {
                return;
            }

            string normalizedPhase = workflowFilter.Phase.Trim();

            if (string.Equals(normalizedPhase, GlobalConst.kClosed, StringComparison.OrdinalIgnoreCase))
            {
                StateMatrix stateMatrix = new();
                await stateMatrix.Init(WorkflowPhases.request, apiConnection, WfTaskType.master);
                Query.QueryVariables["phase_lowest_input_state"] = stateMatrix.MinTicketCompleted;
                return;
            }

            if (!Enum.TryParse(normalizedPhase, true, out WorkflowPhases phase))
            {
                throw new ArgumentException($"Unknown workflow phase '{normalizedPhase}'.");
            }

            GlobalStateMatrix glbStateMatrix = GlobalStateMatrix.Create();
            await glbStateMatrix.Init(apiConnection, WfTaskType.master);
            if (!glbStateMatrix.GlobalMatrix.TryGetValue(phase, out StateMatrix? phaseMatrix))
            {
                throw new InvalidOperationException($"Workflow phase '{normalizedPhase}' is missing in the master state matrix.");
            }

            Query.QueryVariables["phase_lowest_input_state"] = phaseMatrix.LowestInputState;
            Query.QueryVariables["phase_lowest_end_state"] = phaseMatrix.LowestEndState;
        }
    }
}

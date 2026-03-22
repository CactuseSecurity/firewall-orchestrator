using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Data.Workflow;
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
        public override async Task Generate(int _, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await ResolvePhaseFilterAsync(apiConnection);
            List<WfTicket> tickets = await apiConnection.SendQueryAsync<List<WfTicket>>(Query.FullQuery, Query.QueryVariables);
            List<WfState> workflowStates = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
            ReportData.WorkflowStateNames = workflowStates.ToDictionary(state => state.Id, state => state.Name);
            ReportData.WorkflowFilter = new(workflowFilter);
            ReportData.Tickets = tickets;
            ReportData.ElementsCount = tickets.Count;
            await callback(ReportData);
        }

        /// <inheritdoc />
        public override string ExportToCsv()
        {
            throw new NotImplementedException();
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
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("tasks")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("requester")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("created")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("closed")}</th>");
            report.AppendLine("</tr>");

            foreach (WfTicket ticket in ReportData.Tickets.OrderBy(ticket => ticket.Id))
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{ticket.Id}</td>");
                report.AppendLine($"<td>{ticket.Title}</td>");
                report.AppendLine($"<td>{ticket.Tasks.Count}</td>");
                report.AppendLine($"<td>{ticket.Requester?.Name}</td>");
                report.AppendLine($"<td>{ResolveStateName(ticket.StateId)}</td>");
                report.AppendLine($"<td>{ticket.CreationDate}</td>");
                report.AppendLine($"<td>{ticket.CompletionDate}</td>");
                report.AppendLine("</tr>");

                List<WfReqTask> displayedTasks = GetDisplayedTasks(ticket);
                if (displayedTasks.Count > 0)
                {
                    report.AppendLine("<tr><td colspan=\"7\">");
                    report.AppendLine($"<b>{userConfig.GetText("tasks")}</b>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("task_number")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("start_time")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("end_time")}</th>");
                    report.AppendLine("</tr>");

                    foreach (WfReqTask task in displayedTasks)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{task.Id}</td>");
                        report.AppendLine($"<td>{task.TaskNumber}</td>");
                        report.AppendLine($"<td>{task.Title}</td>");
                        report.AppendLine($"<td>{ResolveStateName(task.StateId)}</td>");
                        report.AppendLine($"<td>{task.Start}</td>");
                        report.AppendLine($"<td>{task.Stop}</td>");
                        report.AppendLine("</tr>");

                        if (ShowImplementationTasks())
                        {
                            List<WfImplTask> implementationTasks = GetDisplayedImplementationTasks(task);
                            if (implementationTasks.Count > 0)
                            {
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
                        }

                        if (ShowApprovals())
                        {
                            List<WfApproval> approvals = GetDisplayedApprovals(task);
                            if (approvals.Count > 0)
                            {
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
                        }
                    }

                    report.AppendLine("</table>");
                    report.AppendLine("</td></tr>");
                }
            }

            report.AppendLine("</table>");
            return GenerateHtmlFrameBase(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
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

        /// <summary>
        /// Resolves the selected workflow phase to the current master ticket state range.
        /// </summary>
        private async Task ResolvePhaseFilterAsync(ApiConnection apiConnection)
        {
            if ((ReportType != ReportType.TicketReport && ReportType != ReportType.TicketChangeReport) || string.IsNullOrWhiteSpace(workflowFilter.Phase))
            {
                return;
            }

            if (!Enum.TryParse(workflowFilter.Phase, true, out WorkflowPhases phase))
            {
                throw new ArgumentException($"Unknown workflow phase '{workflowFilter.Phase}'.");
            }

            GlobalStateMatrix stateMatrix = GlobalStateMatrix.Create();
            await stateMatrix.Init(apiConnection, WfTaskType.master);
            if (!stateMatrix.GlobalMatrix.TryGetValue(phase, out StateMatrix? phaseMatrix))
            {
                throw new InvalidOperationException($"Workflow phase '{workflowFilter.Phase}' is missing in the master state matrix.");
            }

            Query.QueryVariables["phase_lowest_input_state"] = phaseMatrix.LowestInputState;
            Query.QueryVariables["phase_lowest_end_state"] = phaseMatrix.LowestEndState;
        }
    }
}

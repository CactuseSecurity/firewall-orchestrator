using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report.Filter;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    /// <summary>
    /// Generates workflow ticket change reports.
    /// </summary>
    public class ReportTicketChanges(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, WorkflowFilter workflowFilter) : ReportBase(query, userConfig, reportType)
    {
        /// <inheritdoc />
        public override async Task Generate(int _, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
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

        private string ResolveStateName(int stateId)
        {
            return ReportData.WorkflowStateNames.TryGetValue(stateId, out string? stateName) ? stateName : stateId.ToString();
        }

        private List<WfReqTask> GetDisplayedTasks(WfTicket ticket)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return ticket.Tasks.OrderBy(task => task.Id).ToList();
            }

            List<WfReqTask> tasks = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.TaskStart => ticket.Tasks.Where(task => IsInSelectedTimeRange(task.Start)).ToList(),
                WorkflowReferenceDate.TaskEnd => ticket.Tasks.Where(task => IsInSelectedTimeRange(task.Stop)).ToList(),
                WorkflowReferenceDate.ApprovalOpened => ticket.Tasks.Where(task => GetDisplayedApprovals(task).Count > 0).ToList(),
                WorkflowReferenceDate.Approved => ticket.Tasks.Where(task => GetDisplayedApprovals(task).Count > 0).ToList(),
                WorkflowReferenceDate.ImplementationStart => ticket.Tasks.Where(task => GetDisplayedImplementationTasks(task).Count > 0).ToList(),
                WorkflowReferenceDate.ImplementationEnd => ticket.Tasks.Where(task => GetDisplayedImplementationTasks(task).Count > 0).ToList(),
                WorkflowReferenceDate.AnyActivity => ticket.Tasks.Where(task =>
                    IsInSelectedTimeRange(task.Start) || IsInSelectedTimeRange(task.Stop) ||
                    GetDisplayedImplementationTasks(task).Count > 0 || GetDisplayedApprovals(task).Count > 0).ToList(),
                _ => []
            };

            return tasks.OrderBy(task => task.Id).ToList();
        }

        private bool ShowImplementationTasks()
        {
            return ReportData.WorkflowFilter.ShowFullTicket
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ImplementationStart
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ImplementationEnd
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.AnyActivity;
        }

        private bool ShowApprovals()
        {
            return ReportData.WorkflowFilter.ShowFullTicket
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ApprovalOpened
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.Approved
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.AnyActivity;
        }

        private List<WfImplTask> GetDisplayedImplementationTasks(WfReqTask task)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return task.ImplementationTasks.OrderBy(implTask => implTask.Id).ToList();
            }

            List<WfImplTask> implementationTasks = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.ImplementationStart => task.ImplementationTasks.Where(implTask => IsInSelectedTimeRange(implTask.Start)).ToList(),
                WorkflowReferenceDate.ImplementationEnd => task.ImplementationTasks.Where(implTask => IsInSelectedTimeRange(implTask.Stop)).ToList(),
                WorkflowReferenceDate.AnyActivity => task.ImplementationTasks.Where(implTask => IsInSelectedTimeRange(implTask.Start) || IsInSelectedTimeRange(implTask.Stop)).ToList(),
                _ => []
            };

            return implementationTasks.OrderBy(implTask => implTask.Id).ToList();
        }

        private List<WfApproval> GetDisplayedApprovals(WfReqTask task)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return task.Approvals.OrderBy(approval => approval.Id).ToList();
            }

            List<WfApproval> approvals = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.ApprovalOpened => task.Approvals.Where(approval => IsInSelectedTimeRange(approval.DateOpened)).ToList(),
                WorkflowReferenceDate.Approved => task.Approvals.Where(approval => IsInSelectedTimeRange(approval.ApprovalDate)).ToList(),
                WorkflowReferenceDate.AnyActivity => task.Approvals.Where(approval => IsInSelectedTimeRange(approval.DateOpened) || IsInSelectedTimeRange(approval.ApprovalDate)).ToList(),
                _ => []
            };

            return approvals.OrderBy(approval => approval.Id).ToList();
        }

        private bool IsInSelectedTimeRange(DateTime? date)
        {
            if (!date.HasValue)
            {
                return false;
            }

            return IsInSelectedTimeRange(date.Value);
        }

        private bool IsInSelectedTimeRange(DateTime date)
        {
            if (date == default)
            {
                return false;
            }

            if (!Query.QueryVariables.TryGetValue("ticket_time_start", out object? startObj) || !DateTime.TryParse(startObj?.ToString(), out DateTime start))
            {
                return true;
            }

            if (!Query.QueryVariables.TryGetValue("ticket_time_end", out object? endObj) || !DateTime.TryParse(endObj?.ToString(), out DateTime end))
            {
                return true;
            }

            return date >= start && date <= end;
        }
    }
}

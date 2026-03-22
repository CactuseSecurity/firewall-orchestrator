using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report.Filter;

namespace FWO.Report
{
    /// <summary>
    /// Generates workflow ticket change reports.
    /// </summary>
    public class ReportTicketChanges(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, WorkflowFilter workflowFilter)
        : ReportTicketsBase(query, userConfig, reportType, workflowFilter)
    {
        /// <inheritdoc />
        protected override List<WfReqTask> GetDisplayedTasks(WfTicket ticket)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return ticket.Tasks.OrderBy(task => task.Id).ToList();
            }

            List<WfReqTask> tasks = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.TicketCreation => ticket.Tasks.ToList(),
                WorkflowReferenceDate.TicketClosure => ticket.Tasks.ToList(),
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

            return FilterTasksByTaskType(tasks);
        }

        /// <inheritdoc />
        protected override bool ShowImplementationTasks()
        {
            return ReportData.WorkflowFilter.ShowFullTicket
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ImplementationStart
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ImplementationEnd
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.AnyActivity;
        }

        /// <inheritdoc />
        protected override bool ShowApprovals()
        {
            return ReportData.WorkflowFilter.ShowFullTicket
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ApprovalOpened
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.Approved
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.AnyActivity;
        }

        /// <inheritdoc />
        protected override List<WfImplTask> GetDisplayedImplementationTasks(WfReqTask task)
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

        /// <inheritdoc />
        protected override List<WfApproval> GetDisplayedApprovals(WfReqTask task)
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

        /// <summary>
        /// Checks whether a nullable date is within the selected report range.
        /// </summary>
        protected bool IsInSelectedTimeRange(DateTime? date)
        {
            if (!date.HasValue)
            {
                return false;
            }

            return IsInSelectedTimeRange(date.Value);
        }

        /// <summary>
        /// Checks whether a date is within the selected report range.
        /// </summary>
        protected bool IsInSelectedTimeRange(DateTime date)
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

        private List<WfReqTask> FilterTasksByTaskType(IEnumerable<WfReqTask> tasks)
        {
            return tasks
                .Where(task => ReportData.WorkflowFilter.TaskTypes.Count == 0 || ReportData.WorkflowFilter.TaskTypes.Any(taskType => task.TaskType == taskType.ToString()))
                .OrderBy(task => task.Id)
                .ToList();
        }
    }
}

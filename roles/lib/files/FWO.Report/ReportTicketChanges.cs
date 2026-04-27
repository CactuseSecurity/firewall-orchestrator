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
            if (!ReportData.WorkflowFilter.DetailedView)
            {
                return [];
            }

            return GetReferenceTasks(ticket);
        }

        /// <inheritdoc />
        protected override List<WfReqTask> GetReferenceTasks(WfTicket ticket)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return FilterTasksByTaskType(ticket.Tasks);
            }

            List<WfReqTask> tasks = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.TicketCreation => ticket.Tasks.ToList(),
                WorkflowReferenceDate.TicketClosure => ticket.Tasks.ToList(),
                WorkflowReferenceDate.TaskStart => ticket.Tasks.Where(task => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(task.Start, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                WorkflowReferenceDate.TaskEnd => ticket.Tasks.Where(task => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(task.Stop, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                WorkflowReferenceDate.ApprovalOpened => ticket.Tasks.Where(task => GetReferenceApprovals(task).Count > 0).ToList(),
                WorkflowReferenceDate.Approved => ticket.Tasks.Where(task => GetReferenceApprovals(task).Count > 0).ToList(),
                WorkflowReferenceDate.ImplementationStart => ticket.Tasks.Where(task => GetReferenceImplementationTasks(task).Count > 0).ToList(),
                WorkflowReferenceDate.ImplementationEnd => ticket.Tasks.Where(task => GetReferenceImplementationTasks(task).Count > 0).ToList(),
                WorkflowReferenceDate.AnyActivity => ticket.Tasks.Where(task =>
                    WorkflowTicketReferenceHelper.IsInSelectedTimeRange(task.Start, GetTicketTimeStart(), GetTicketTimeEnd())
                    || WorkflowTicketReferenceHelper.IsInSelectedTimeRange(task.Stop, GetTicketTimeStart(), GetTicketTimeEnd()) ||
                    GetReferenceImplementationTasks(task).Count > 0 || GetReferenceApprovals(task).Count > 0).ToList(),
                _ => []
            };

            return FilterTasksByTaskType(tasks);
        }

        /// <inheritdoc />
        protected override bool ShowImplementationTasks()
        {
            if (!ReportData.WorkflowFilter.DetailedView)
            {
                return false;
            }

            return ReportData.WorkflowFilter.ShowFullTicket
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ImplementationStart
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ImplementationEnd
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.AnyActivity;
        }

        /// <inheritdoc />
        protected override bool ShowApprovals()
        {
            if (!ReportData.WorkflowFilter.DetailedView)
            {
                return false;
            }

            return ReportData.WorkflowFilter.ShowFullTicket
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.ApprovalOpened
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.Approved
                || ReportData.WorkflowFilter.ReferenceDate == WorkflowReferenceDate.AnyActivity;
        }

        /// <inheritdoc />
        protected override List<WfImplTask> GetDisplayedImplementationTasks(WfReqTask task)
        {
            if (!ReportData.WorkflowFilter.DetailedView)
            {
                return [];
            }

            return GetReferenceImplementationTasks(task);
        }

        /// <inheritdoc />
        protected override List<WfImplTask> GetReferenceImplementationTasks(WfReqTask task)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return task.ImplementationTasks.OrderBy(implTask => implTask.Id).ToList();
            }

            List<WfImplTask> implementationTasks = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.ImplementationStart => task.ImplementationTasks.Where(implTask => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(implTask.Start, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                WorkflowReferenceDate.ImplementationEnd => task.ImplementationTasks.Where(implTask => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(implTask.Stop, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                WorkflowReferenceDate.AnyActivity => task.ImplementationTasks.Where(implTask =>
                    WorkflowTicketReferenceHelper.IsInSelectedTimeRange(implTask.Start, GetTicketTimeStart(), GetTicketTimeEnd())
                    || WorkflowTicketReferenceHelper.IsInSelectedTimeRange(implTask.Stop, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                _ => []
            };

            return implementationTasks.OrderBy(implTask => implTask.Id).ToList();
        }

        /// <inheritdoc />
        protected override List<WfApproval> GetDisplayedApprovals(WfReqTask task)
        {
            if (!ReportData.WorkflowFilter.DetailedView)
            {
                return [];
            }

            return GetReferenceApprovals(task);
        }

        /// <inheritdoc />
        protected override List<WfApproval> GetReferenceApprovals(WfReqTask task)
        {
            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return task.Approvals.OrderBy(approval => approval.Id).ToList();
            }

            List<WfApproval> approvals = ReportData.WorkflowFilter.ReferenceDate switch
            {
                WorkflowReferenceDate.ApprovalOpened => task.Approvals.Where(approval => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(approval.DateOpened, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                WorkflowReferenceDate.Approved => task.Approvals.Where(approval => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(approval.ApprovalDate, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                WorkflowReferenceDate.AnyActivity => task.Approvals.Where(approval =>
                    WorkflowTicketReferenceHelper.IsInSelectedTimeRange(approval.DateOpened, GetTicketTimeStart(), GetTicketTimeEnd())
                    || WorkflowTicketReferenceHelper.IsInSelectedTimeRange(approval.ApprovalDate, GetTicketTimeStart(), GetTicketTimeEnd())).ToList(),
                _ => []
            };

            return approvals.OrderBy(approval => approval.Id).ToList();
        }

        /// <inheritdoc />
        protected override IEnumerable<DateTime?> GetReferenceActivityDates(WfReqTask task)
        {
            return WorkflowTicketReferenceHelper.GetActivityDates(
                task,
                false,
                date => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(date, GetTicketTimeStart(), GetTicketTimeEnd()));
        }

        /// <inheritdoc />
        protected override IEnumerable<DateTime?> GetTicketReferenceActivityDates(WfTicket ticket)
        {
            return new DateTime?[] { ticket.CreationDate, ticket.CompletionDate }
                .Where(date => WorkflowTicketReferenceHelper.IsInSelectedTimeRange(date, GetTicketTimeStart(), GetTicketTimeEnd()));
        }

        private string? GetTicketTimeStart()
        {
            return Query.QueryVariables.TryGetValue("ticket_time_start", out object? startObj) ? startObj?.ToString() : null;
        }

        private string? GetTicketTimeEnd()
        {
            return Query.QueryVariables.TryGetValue("ticket_time_end", out object? endObj) ? endObj?.ToString() : null;
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

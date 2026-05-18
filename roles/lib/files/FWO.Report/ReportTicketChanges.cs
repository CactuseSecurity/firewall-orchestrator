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
            return WorkflowTicketSelectionHelper.GetReferenceTasks(
                ticket,
                ReportData.WorkflowFilter.ShowFullTicket,
                ReportData.WorkflowFilter.ReferenceDate,
                GetTicketTimeStart(),
                GetTicketTimeEnd(),
                ReportData.WorkflowFilter.TaskTypes);
        }

        /// <inheritdoc />
        protected override bool ShowImplementationTasks()
        {
            return WorkflowTicketSelectionHelper.ShowImplementationTasks(
                ReportData.WorkflowFilter.DetailedView,
                ReportData.WorkflowFilter.ShowFullTicket,
                ReportType,
                ReportData.WorkflowFilter.ReferenceDate);
        }

        /// <inheritdoc />
        protected override bool ShowApprovals()
        {
            return WorkflowTicketSelectionHelper.ShowApprovals(
                ReportData.WorkflowFilter.DetailedView,
                ReportData.WorkflowFilter.ShowFullTicket,
                ReportType,
                ReportData.WorkflowFilter.ReferenceDate);
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
            return WorkflowTicketSelectionHelper.GetReferenceImplementationTasks(
                task,
                ReportData.WorkflowFilter.ShowFullTicket,
                ReportData.WorkflowFilter.ReferenceDate,
                GetTicketTimeStart(),
                GetTicketTimeEnd());
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
            return WorkflowTicketSelectionHelper.GetReferenceApprovals(
                task,
                ReportData.WorkflowFilter.ShowFullTicket,
                ReportData.WorkflowFilter.ReferenceDate,
                GetTicketTimeStart(),
                GetTicketTimeEnd());
        }

        /// <inheritdoc />
        protected override IEnumerable<DateTime?> GetReferenceActivityDates(WfReqTask task)
        {
            return WorkflowTicketSelectionHelper.GetActivityDates(
                task,
                false,
                date => WorkflowTicketSelectionHelper.IsInSelectedTimeRange(date, GetTicketTimeStart(), GetTicketTimeEnd()));
        }

        /// <inheritdoc />
        protected override IEnumerable<DateTime?> GetTicketReferenceActivityDates(WfTicket ticket)
        {
            return new DateTime?[] { ticket.CreationDate, ticket.CompletionDate }
                .Where(date => WorkflowTicketSelectionHelper.IsInSelectedTimeRange(date, GetTicketTimeStart(), GetTicketTimeEnd()));
        }

        private string? GetTicketTimeStart()
        {
            return Query.QueryVariables.TryGetValue("ticket_time_start", out object? startObj) ? startObj?.ToString() : null;
        }

        private string? GetTicketTimeEnd()
        {
            return Query.QueryVariables.TryGetValue("ticket_time_end", out object? endObj) ? endObj?.ToString() : null;
        }

    }
}

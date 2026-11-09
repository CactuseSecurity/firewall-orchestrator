using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report.Filter;

namespace FWO.Report
{
    /// <summary>
    /// Generates workflow ticket reports.
    /// </summary>
    public class ReportTickets(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, WorkflowFilter workflowFilter)
        : ReportTicketsBase(query, userConfig, reportType, workflowFilter)
    {
        /// <inheritdoc />
        protected override List<WfReqTask> GetDisplayedTasks(WfTicket ticket)
        {
            if (!ReportData.WorkflowFilter.DetailedView)
            {
                return [];
            }

            if (ReportData.WorkflowFilter.ShowFullTicket)
            {
                return ticket.Tasks.OrderBy(task => task.Id).ToList();
            }

            return ticket.Tasks
                .Where(task => ReportData.WorkflowFilter.TaskTypes.Count == 0 || ReportData.WorkflowFilter.TaskTypes.Any(taskType => task.TaskType == taskType.ToString()))
                .OrderBy(task => task.Id)
                .ToList();
        }

        /// <inheritdoc />
        protected override bool ShowImplementationTasks()
        {
            return true;
        }

        /// <inheritdoc />
        protected override bool ShowApprovals()
        {
            return true;
        }

        /// <inheritdoc />
        protected override List<WfImplTask> GetDisplayedImplementationTasks(WfReqTask task)
        {
            return task.ImplementationTasks.OrderBy(implTask => implTask.Id).ToList();
        }

        /// <inheritdoc />
        protected override List<WfApproval> GetDisplayedApprovals(WfReqTask task)
        {
            return task.Approvals.OrderBy(approval => approval.Id).ToList();
        }
    }
}

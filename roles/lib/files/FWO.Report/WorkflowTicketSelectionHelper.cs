using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using System.Globalization;

namespace FWO.Report
{
    /// <summary>
    /// Selects workflow ticket display data and resolves reference dates for ticket reports.
    /// </summary>
    public static class WorkflowTicketSelectionHelper
    {
        /// <summary>
        /// Computes the reference date for one workflow ticket.
        /// </summary>
        public static DateTime? GetTicketReferenceDate(
            WfTicket ticket,
            WorkflowReferenceDate referenceDate,
            Func<WfTicket, List<WfReqTask>> getReferenceTasks,
            Func<WfReqTask, List<WfApproval>> getReferenceApprovals,
            Func<WfReqTask, List<WfImplTask>> getReferenceImplementationTasks,
            Func<WfTicket, IEnumerable<DateTime?>> getTicketReferenceActivityDates,
            Func<WfReqTask, IEnumerable<DateTime?>> getReferenceActivityDates)
        {
            List<WfReqTask> referenceTasks = getReferenceTasks(ticket);
            return referenceDate switch
            {
                WorkflowReferenceDate.TicketCreation => ticket.CreationDate,
                WorkflowReferenceDate.TicketClosure => ticket.CompletionDate,
                WorkflowReferenceDate.TaskStart => referenceTasks
                    .Select(task => task.Start)
                    .Where(date => date.HasValue)
                    .Min(),
                WorkflowReferenceDate.TaskEnd => referenceTasks
                    .Select(task => task.Stop)
                    .Where(date => date.HasValue)
                    .Min(),
                WorkflowReferenceDate.ApprovalOpened => referenceTasks
                    .SelectMany(task => getReferenceApprovals(task))
                    .Select(approval => approval.DateOpened)
                    .Cast<DateTime?>()
                    .Min(),
                WorkflowReferenceDate.Approved => referenceTasks
                    .SelectMany(task => getReferenceApprovals(task))
                    .Select(approval => approval.ApprovalDate)
                    .Where(date => date.HasValue)
                    .Min(),
                WorkflowReferenceDate.ImplementationStart => referenceTasks
                    .SelectMany(task => getReferenceImplementationTasks(task))
                    .Select(task => task.Start)
                    .Where(date => date.HasValue)
                    .Min(),
                WorkflowReferenceDate.ImplementationEnd => referenceTasks
                    .SelectMany(task => getReferenceImplementationTasks(task))
                    .Select(task => task.Stop)
                    .Where(date => date.HasValue)
                    .Min(),
                WorkflowReferenceDate.AnyActivity => GetAnyActivityReferenceDate(ticket, referenceTasks, getTicketReferenceActivityDates, getReferenceActivityDates),
                _ => ticket.CompletionDate
            };
        }

        /// <summary>
        /// Selects all activity timestamps for one request task, optionally constrained by a date filter.
        /// </summary>
        public static IEnumerable<DateTime?> GetActivityDates(WfReqTask task, bool includeAllDates, Func<DateTime?, bool> isInSelectedTimeRange)
        {
            IEnumerable<DateTime?> activityDates =
            [
                task.Start,
                task.Stop,
                .. task.ImplementationTasks.SelectMany(implTask => new DateTime?[] { implTask.Start, implTask.Stop }),
                .. task.Approvals.SelectMany(approval => new DateTime?[] { approval.DateOpened, approval.ApprovalDate })
            ];

            return includeAllDates ? activityDates : activityDates.Where(isInSelectedTimeRange);
        }

        /// <summary>
        /// Checks whether a nullable date lies within the given half-open time range.
        /// </summary>
        public static bool IsInSelectedTimeRange(DateTime? date, string? startText, string? endText)
        {
            if (!date.HasValue || date.Value == default)
            {
                return false;
            }

            if (!DateTime.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime start))
            {
                return true;
            }

            if (!DateTime.TryParse(endText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime end))
            {
                return true;
            }

            return date.Value >= start && date.Value < end;
        }

        /// <summary>
        /// Selects request tasks for a ticket change report using the configured reference date.
        /// </summary>
        public static List<WfReqTask> GetReferenceTasks(
            WfTicket ticket,
            bool showFullTicket,
            WorkflowReferenceDate referenceDate,
            string? timeRangeStart,
            string? timeRangeEnd,
            IReadOnlyCollection<WfTaskType> taskTypes)
        {
            if (showFullTicket)
            {
                return ticket.Tasks.OrderBy(task => task.Id).ToList();
            }

            List<WfReqTask> tasks = referenceDate switch
            {
                WorkflowReferenceDate.TicketCreation => ticket.Tasks.ToList(),
                WorkflowReferenceDate.TicketClosure => ticket.Tasks.ToList(),
                WorkflowReferenceDate.TaskStart => ticket.Tasks
                    .Where(task => IsInSelectedTimeRange(task.Start, timeRangeStart, timeRangeEnd))
                    .ToList(),
                WorkflowReferenceDate.TaskEnd => ticket.Tasks
                    .Where(task => IsInSelectedTimeRange(task.Stop, timeRangeStart, timeRangeEnd))
                    .ToList(),
                WorkflowReferenceDate.ApprovalOpened => ticket.Tasks
                    .Where(task => GetReferenceApprovals(task, false, referenceDate, timeRangeStart, timeRangeEnd).Count > 0)
                    .ToList(),
                WorkflowReferenceDate.Approved => ticket.Tasks
                    .Where(task => GetReferenceApprovals(task, false, referenceDate, timeRangeStart, timeRangeEnd).Count > 0)
                    .ToList(),
                WorkflowReferenceDate.ImplementationStart => ticket.Tasks
                    .Where(task => GetReferenceImplementationTasks(task, false, referenceDate, timeRangeStart, timeRangeEnd).Count > 0)
                    .ToList(),
                WorkflowReferenceDate.ImplementationEnd => ticket.Tasks
                    .Where(task => GetReferenceImplementationTasks(task, false, referenceDate, timeRangeStart, timeRangeEnd).Count > 0)
                    .ToList(),
                WorkflowReferenceDate.AnyActivity => ticket.Tasks
                    .Where(task => IsInSelectedTimeRange(task.Start, timeRangeStart, timeRangeEnd)
                        || IsInSelectedTimeRange(task.Stop, timeRangeStart, timeRangeEnd)
                        || GetReferenceImplementationTasks(task, false, referenceDate, timeRangeStart, timeRangeEnd).Count > 0
                        || GetReferenceApprovals(task, false, referenceDate, timeRangeStart, timeRangeEnd).Count > 0)
                    .ToList(),
                _ => []
            };

            return FilterTasksByTaskType(tasks, taskTypes);
        }

        /// <summary>
        /// Selects implementation tasks for a request task using the configured reference date.
        /// </summary>
        public static List<WfImplTask> GetReferenceImplementationTasks(
            WfReqTask task,
            bool showAll,
            WorkflowReferenceDate referenceDate,
            string? timeRangeStart,
            string? timeRangeEnd)
        {
            if (showAll)
            {
                return task.ImplementationTasks.OrderBy(implTask => implTask.Id).ToList();
            }

            List<WfImplTask> implementationTasks = referenceDate switch
            {
                WorkflowReferenceDate.ImplementationStart => task.ImplementationTasks
                    .Where(implTask => IsInSelectedTimeRange(implTask.Start, timeRangeStart, timeRangeEnd))
                    .ToList(),
                WorkflowReferenceDate.ImplementationEnd => task.ImplementationTasks
                    .Where(implTask => IsInSelectedTimeRange(implTask.Stop, timeRangeStart, timeRangeEnd))
                    .ToList(),
                WorkflowReferenceDate.AnyActivity => task.ImplementationTasks
                    .Where(implTask => IsInSelectedTimeRange(implTask.Start, timeRangeStart, timeRangeEnd)
                        || IsInSelectedTimeRange(implTask.Stop, timeRangeStart, timeRangeEnd))
                    .ToList(),
                _ => []
            };

            return implementationTasks.OrderBy(implTask => implTask.Id).ToList();
        }

        /// <summary>
        /// Selects approvals for a request task using the configured reference date.
        /// </summary>
        public static List<WfApproval> GetReferenceApprovals(
            WfReqTask task,
            bool showAll,
            WorkflowReferenceDate referenceDate,
            string? timeRangeStart,
            string? timeRangeEnd)
        {
            if (showAll)
            {
                return task.Approvals.OrderBy(approval => approval.Id).ToList();
            }

            List<WfApproval> approvals = referenceDate switch
            {
                WorkflowReferenceDate.ApprovalOpened => task.Approvals
                    .Where(approval => IsInSelectedTimeRange(approval.DateOpened, timeRangeStart, timeRangeEnd))
                    .ToList(),
                WorkflowReferenceDate.Approved => task.Approvals
                    .Where(approval => IsInSelectedTimeRange(approval.ApprovalDate, timeRangeStart, timeRangeEnd))
                    .ToList(),
                WorkflowReferenceDate.AnyActivity => task.Approvals
                    .Where(approval => IsInSelectedTimeRange(approval.DateOpened, timeRangeStart, timeRangeEnd)
                        || IsInSelectedTimeRange(approval.ApprovalDate, timeRangeStart, timeRangeEnd))
                    .ToList(),
                _ => []
            };

            return approvals.OrderBy(approval => approval.Id).ToList();
        }

        /// <summary>
        /// Indicates whether implementation tasks are visible for the selected report options.
        /// </summary>
        public static bool ShowImplementationTasks(
            bool detailedView,
            bool showFullTicket,
            ReportType selectedReportType,
            WorkflowReferenceDate referenceDate)
        {
            if (!detailedView)
            {
                return false;
            }

            if (showFullTicket || selectedReportType == ReportType.TicketReport)
            {
                return true;
            }

            return referenceDate == WorkflowReferenceDate.ImplementationStart
                || referenceDate == WorkflowReferenceDate.ImplementationEnd
                || referenceDate == WorkflowReferenceDate.AnyActivity;
        }

        /// <summary>
        /// Indicates whether approvals are visible for the selected report options.
        /// </summary>
        public static bool ShowApprovals(
            bool detailedView,
            bool showFullTicket,
            ReportType selectedReportType,
            WorkflowReferenceDate referenceDate)
        {
            if (!detailedView)
            {
                return false;
            }

            if (showFullTicket || selectedReportType == ReportType.TicketReport)
            {
                return true;
            }

            return referenceDate == WorkflowReferenceDate.ApprovalOpened
                || referenceDate == WorkflowReferenceDate.Approved
                || referenceDate == WorkflowReferenceDate.AnyActivity;
        }

        /// <summary>
        /// Filters request tasks by selected task types and orders them by task id.
        /// </summary>
        public static List<WfReqTask> FilterTasksByTaskType(IEnumerable<WfReqTask> tasks, IReadOnlyCollection<WfTaskType> taskTypes)
        {
            return tasks
                .Where(task => taskTypes.Count == 0 || taskTypes.Any(taskType => task.TaskType == taskType.ToString()))
                .OrderBy(task => task.Id)
                .ToList();
        }

        /// <summary>
        /// Builds the comma-separated distinct workflow label values for one ticket.
        /// </summary>
        public static string GetLabelValue(WfTicket ticket, string labelName)
        {
            if (string.IsNullOrWhiteSpace(labelName))
            {
                return "";
            }

            List<string> labelValues =
            [
                .. ticket.Tasks
                    .Select(task => task.GetAddInfoValue(labelName))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct()
            ];

            return string.Join(", ", labelValues);
        }

        /// <summary>
        /// Computes the latest matching activity date for one workflow ticket.
        /// </summary>
        private static DateTime? GetAnyActivityReferenceDate(
            WfTicket ticket,
            List<WfReqTask> referenceTasks,
            Func<WfTicket, IEnumerable<DateTime?>> getTicketReferenceActivityDates,
            Func<WfReqTask, IEnumerable<DateTime?>> getReferenceActivityDates)
        {
            List<DateTime> dates =
            [
                .. getTicketReferenceActivityDates(ticket)
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value),
                .. referenceTasks
                    .SelectMany(task => getReferenceActivityDates(task))
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value),
            ];
            return dates.Count > 0 ? dates.Max() : null;
        }
    }
}

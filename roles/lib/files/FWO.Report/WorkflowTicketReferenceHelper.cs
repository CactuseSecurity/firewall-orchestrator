using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using System.Globalization;

namespace FWO.Report
{
    /// <summary>
    /// Resolves workflow ticket reference dates and ordering for ticket change reports.
    /// </summary>
    public static class WorkflowTicketReferenceHelper
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
        /// Sorts tickets by reference date descending and falls back to descending ticket id.
        /// </summary>
        public static IEnumerable<WfTicket> SortTicketsByReferenceDate(
            IEnumerable<WfTicket> tickets,
            WorkflowReferenceDate referenceDate,
            Func<WfTicket, List<WfReqTask>> getReferenceTasks,
            Func<WfReqTask, List<WfApproval>> getReferenceApprovals,
            Func<WfReqTask, List<WfImplTask>> getReferenceImplementationTasks,
            Func<WfTicket, IEnumerable<DateTime?>> getTicketReferenceActivityDates,
            Func<WfReqTask, IEnumerable<DateTime?>> getReferenceActivityDates)
        {
            return tickets
                .Select(ticket => new
                {
                    Ticket = ticket,
                    ReferenceDate = GetTicketReferenceDate(ticket, referenceDate, getReferenceTasks, getReferenceApprovals, getReferenceImplementationTasks, getTicketReferenceActivityDates, getReferenceActivityDates)
                })
                .OrderByDescending(item => item.ReferenceDate.HasValue)
                .ThenByDescending(item => item.ReferenceDate)
                .ThenByDescending(item => item.Ticket.Id)
                .Select(item => item.Ticket);
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
            return date.HasValue && IsInSelectedTimeRange(date.Value, startText, endText);
        }

        /// <summary>
        /// Checks whether a date lies within the given half-open time range.
        /// </summary>
        public static bool IsInSelectedTimeRange(DateTime date, string? startText, string? endText)
        {
            if (date == default)
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

            return date >= start && date < end;
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

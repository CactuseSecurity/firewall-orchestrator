using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Result of the rollout check of a ticket.
    /// </summary>
    /// <param name="Passed">True when every involved management has been imported after the implementation.</param>
    /// <param name="ReviewStart">Time after which the imports have to have started (completion of the implementation),
    /// null when it cannot be determined.</param>
    /// <param name="InvolvedManagementIds">Managements the ticket was implemented on.</param>
    /// <param name="MissingManagementIds">Involved managements without a successful import since the review start.</param>
    public sealed record RolloutCheckResult(bool Passed, DateTime? ReviewStart, List<int> InvolvedManagementIds, List<int> MissingManagementIds);

    /// <summary>
    /// Management hierarchy and latest successful imports used by the rollout check.
    /// Since 9.0 sub-managements are imported together with their super management, so the import of a
    /// sub-management is the one of its super management.
    /// </summary>
    public sealed class RolloutCheckImportData
    {
        private readonly Dictionary<int, Management> managementsById;
        private readonly Dictionary<int, DateTime> latestImportStartByManagementId;

        /// <summary>
        /// Creates the import data.
        /// </summary>
        /// <param name="managements">All managements, with their super management ids.</param>
        /// <param name="latestImports">Latest successful import per (super) management.</param>
        public RolloutCheckImportData(List<Management> managements, List<ImportControl> latestImports)
        {
            managementsById = managements
                .GroupBy(management => management.Id)
                .ToDictionary(group => group.Key, group => group.First());
            latestImportStartByManagementId = latestImports
                .Where(import => import.MgmId.HasValue && import.StartTime.HasValue)
                .GroupBy(import => import.MgmId!.Value)
                .ToDictionary(group => group.Key, group => group.Max(import => import.StartTime!.Value));
        }

        /// <summary>
        /// Returns the id of the management whose import covers the given management.
        /// </summary>
        /// <param name="managementId">Id of the management.</param>
        /// <returns>The id of the super management, or the id itself for a stand-alone management.</returns>
        public int GetImportManagementId(int managementId)
        {
            return managementsById.TryGetValue(managementId, out Management? management) && management.SuperManagerId.HasValue
                ? management.SuperManagerId.Value
                : managementId;
        }

        /// <summary>
        /// Checks whether the management has been imported successfully by an import started after the given time.
        /// </summary>
        /// <param name="managementId">Id of the management.</param>
        /// <param name="since">Time after which the import must have started.</param>
        /// <returns>True when such an import exists.</returns>
        public bool IsImportedAfter(int managementId, DateTime since)
        {
            return latestImportStartByManagementId.TryGetValue(GetImportManagementId(managementId), out DateTime importStart)
                && importStart > since;
        }

        /// <summary>
        /// Returns the name of the management, or its id when it is unknown.
        /// </summary>
        /// <param name="managementId">Id of the management.</param>
        /// <returns>Name of the management.</returns>
        public string GetManagementName(int managementId)
        {
            return managementsById.TryGetValue(managementId, out Management? management) && !string.IsNullOrWhiteSpace(management.Name)
                ? management.Name
                : managementId.ToString();
        }
    }

    /// <summary>
    /// Decides whether the rollout of a ticket has been confirmed by the imports of all involved managements.
    /// </summary>
    public static class RolloutCheckEvaluator
    {
        /// <summary>
        /// Evaluates the rollout check of a ticket.
        /// </summary>
        /// <param name="ticket">Ticket with its request and implementation tasks.</param>
        /// <param name="importData">Management hierarchy and latest imports.</param>
        /// <param name="stateEntry">Time the ticket entered its current state, used when the completion of the
        /// implementation is not known from the tasks.</param>
        /// <returns>The result of the check.</returns>
        public static RolloutCheckResult Evaluate(WfTicket ticket, RolloutCheckImportData importData, DateTime? stateEntry)
        {
            List<int> involvedManagementIds = GetInvolvedManagementIds(ticket);
            DateTime? reviewStart = GetReviewStart(ticket, stateEntry);
            List<int> missingManagementIds = reviewStart is DateTime since
                ? [.. involvedManagementIds.Where(managementId => !importData.IsImportedAfter(managementId, since))]
                : [.. involvedManagementIds];
            return new RolloutCheckResult(missingManagementIds.Count == 0, reviewStart, involvedManagementIds, missingManagementIds);
        }

        /// <summary>
        /// Returns the managements the ticket was implemented on: those of the implementation tasks, or that of
        /// the request task when it has no implementation tasks.
        /// </summary>
        /// <param name="ticket">Ticket with its request and implementation tasks.</param>
        /// <returns>Distinct, ascending management ids.</returns>
        public static List<int> GetInvolvedManagementIds(WfTicket ticket)
        {
            SortedSet<int> managementIds = [];
            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                if (reqTask.ImplementationTasks.Count == 0)
                {
                    AddManagementId(managementIds, reqTask.ManagementId);
                    continue;
                }
                foreach (WfImplTask implTask in reqTask.ImplementationTasks)
                {
                    AddManagementId(managementIds, implTask.ManagementId ?? reqTask.ManagementId);
                }
            }
            return [.. managementIds];
        }

        /// <summary>
        /// Returns the time the implementation of the ticket was completed: the latest stop date of its
        /// implementation tasks, or of the request task when it has no implementation tasks. When a task has
        /// no stop date, the time the ticket entered its state is used, since the ticket only reached the
        /// checked state after all tasks were done.
        /// </summary>
        /// <param name="ticket">Ticket with its request and implementation tasks.</param>
        /// <param name="stateEntry">Time the ticket entered its current state, if known.</param>
        /// <returns>The review start date, or null when it cannot be determined.</returns>
        public static DateTime? GetReviewStart(WfTicket ticket, DateTime? stateEntry)
        {
            List<DateTime?> completionDates = [];
            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                if (reqTask.ImplementationTasks.Count == 0)
                {
                    completionDates.Add(reqTask.Stop);
                }
                else
                {
                    completionDates.AddRange(reqTask.ImplementationTasks.Select(implTask => implTask.Stop));
                }
            }

            if (completionDates.Count == 0 || completionDates.Any(completionDate => completionDate == null))
            {
                return stateEntry;
            }
            return completionDates.Max();
        }

        private static void AddManagementId(SortedSet<int> managementIds, int? managementId)
        {
            if (managementId is int id && id > 0)
            {
                managementIds.Add(id);
            }
        }
    }
}

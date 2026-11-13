using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        private const string kRolloutCheckLogTitle = "Rollout Check";
        private static readonly List<int> kRolloutCheckImportTypeIds = [ImportType.RULE, ImportType.ADMIN_VIA_REINITIALIZE_BTN];
        private static readonly List<Management> kNoManagements = [];
        private static readonly List<ImportControl> kNoImports = [];

        /// <summary>
        /// Checks whether every management involved in the calling ticket has been imported successfully by an
        /// import started after the implementation of the ticket was completed.
        /// </summary>
        /// <param name="checkResultLabel">Optional label under which the result is stored in the request tasks.</param>
        /// <param name="statefulObject">Object the action is performed for.</param>
        /// <param name="scope">Scope of the object.</param>
        /// <returns>The result, with a comment listing the imported resp. missing managements.</returns>
        private async Task<ConditionalCheckResult> ExecuteRolloutCheck(string checkResultLabel, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            try
            {
                WfTicket? ticket = await GetCallingTicket(statefulObject, scope);
                if (ticket == null)
                {
                    Log.WriteWarning(kRolloutCheckLogTitle, $"No ticket found for {scope} {GetStatefulObjectId(statefulObject, scope)}; the rollout check is not passed.");
                    return new ConditionalCheckResult(false);
                }

                DateTime? stateEntry = await GetStateEntryTime(statefulObject, scope);
                RolloutCheckImportData importData = await LoadRolloutCheckImportData(ticket);
                RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, importData, stateEntry);
                LogRolloutCheckResult(ticket, result);
                await AttachCheckResultLabel(ticket.Tasks, checkResultLabel, result.Passed);
                return new ConditionalCheckResult(result.Passed, RolloutCheckComment.Build(wfHandler.userConfig, result, importData));
            }
            catch (Exception exception)
            {
                Log.WriteError(kRolloutCheckLogTitle, "Rollout check failed.", exception);
                return new ConditionalCheckResult(false);
            }
        }

        private async Task<RolloutCheckImportData> LoadRolloutCheckImportData(WfTicket ticket)
        {
            List<int> involvedManagementIds = RolloutCheckEvaluator.GetInvolvedManagementIds(ticket);
            if (involvedManagementIds.Count == 0)
            {
                return new RolloutCheckImportData(kNoManagements, kNoImports);
            }

            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementHierarchy) ?? [];
            RolloutCheckImportData hierarchy = new(managements, kNoImports);
            List<int> importManagementIds = [.. involvedManagementIds.Select(hierarchy.GetImportManagementId).Distinct().Order()];
            List<ImportControl> latestImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getLatestSuccessfulImports,
                new { mgmIds = importManagementIds, importTypeIds = kRolloutCheckImportTypeIds }) ?? [];
            return new RolloutCheckImportData(managements, latestImports);
        }

        private static void LogRolloutCheckResult(WfTicket ticket, RolloutCheckResult result)
        {
            if (result.Passed)
            {
                Log.WriteInfo(kRolloutCheckLogTitle, $"Ticket {ticket.Id}: all involved managements imported after {result.ReviewStart}.");
                return;
            }
            Log.WriteDebug(kRolloutCheckLogTitle, $"Ticket {ticket.Id} waits for the import of managements " +
                $"{string.Join(", ", result.MissingManagementIds)} after {result.ReviewStart?.ToString() ?? "an unknown implementation end"}.");
        }
    }
}

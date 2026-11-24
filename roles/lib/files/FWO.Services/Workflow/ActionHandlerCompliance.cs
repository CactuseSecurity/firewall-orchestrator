using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        private static readonly IReadOnlyList<string> kNoComplianceReasons = [];
        private static readonly List<bool> kStoredComplianceValues = [true, false];
        private string? lastComplianceKey;
        private IReadOnlyDictionary<long, RequestTaskComplianceResult>? lastComplianceResults;

        /// <summary>
        /// Returns the policy ids checked by the conditional auto-promote actions of the workflow.
        /// </summary>
        public List<int> GetConditionalPolicyCheckIds()
        {
            return [.. states
                .SelectMany(state => state.Actions.Select(stateAction => stateAction.Action))
                .Where(action => action.ActionType == StateActionTypes.AutoPromote.ToString())
                .Select(action => WfStateAction.TryParseAutoPromoteParams(action.ExternalParams, out _, out ConditionalAutoPromoteParams? conditionalParams)
                    ? conditionalParams
                    : null)
                .Where(conditionalParams => conditionalParams?.ToBeCalled == ToBeCalled.PolicyCheck)
                .SelectMany(conditionalParams => conditionalParams!.PolicyIds)
                .Where(policyId => policyId > 0)
                .Distinct()];
        }

        /// <summary>
        /// Returns the compliance status of the ticket's access tasks against the policies of the conditional auto-promote actions,
        /// the same check that decides the conditional state transition.
        /// Tasks with a stored positive result are not checked again unless <paramref name="recheckAll"/> is set;
        /// tasks with a negative or without a stored result are checked, so their violation reasons are available.
        /// The result of the last check is kept as long as the checked content of the ticket does not change.
        /// </summary>
        /// <param name="ticket">The ticket whose access tasks are checked.</param>
        /// <param name="recheckAll">Checks all access tasks, ignoring stored results and the last check.</param>
        /// <param name="storeResults">Stores the result in the additional info of each checked task whose stored result differs.</param>
        /// <returns>The results per access task, or null if no policy check is configured, no checker is available or the check failed.</returns>
        public async Task<IReadOnlyDictionary<long, RequestTaskComplianceResult>?> CheckRequestTaskCompliance(WfTicket ticket,
            bool recheckAll = false, bool storeResults = false)
        {
            List<int> policyIds = GetConditionalPolicyCheckIds();
            IRequestedRulePolicyChecker? checker = policyIds.Count > 0 ? ResolvePolicyChecker() : null;
            if (checker == null)
            {
                return null;
            }

            string complianceKey = ComplianceKey(ticket, policyIds);
            if (!recheckAll && complianceKey == lastComplianceKey && lastComplianceResults != null)
            {
                return lastComplianceResults;
            }

            List<WfReqTask> checkedTasks = [.. ticket.Tasks.Where(RequestTaskCompliance.IsCheckedTask)];
            List<WfReqTask> tasksToCheck = recheckAll
                ? checkedTasks
                : [.. checkedTasks.Where(task => RequestTaskCompliance.StoredResult(task) != true)];
            Dictionary<long, RequestTaskComplianceResult> results = [];
            foreach (WfReqTask task in checkedTasks.Except(tasksToCheck))
            {
                results[task.Id] = new RequestTaskComplianceResult(task.Id, true, kNoComplianceReasons);
            }

            if (tasksToCheck.Count > 0)
            {
                // the other tasks define requested objects and groups used by the checked tasks
                List<WfReqTask> contextTasks = [.. PolicyCheckTasks(ticket).Except(checkedTasks)];
                IReadOnlyDictionary<long, RequestTaskComplianceResult>? checkResults = await CheckTasks(checker, policyIds, tasksToCheck, contextTasks, storeResults);
                if (checkResults == null)
                {
                    return null;
                }
                foreach (WfReqTask task in tasksToCheck.Where(task => checkResults.ContainsKey(task.Id)))
                {
                    results[task.Id] = checkResults[task.Id];
                }
            }

            lastComplianceKey = complianceKey;
            lastComplianceResults = results;
            return results;
        }

        /// <summary>
        /// Checks the given tasks and, if requested, stores the results that changed.
        /// If the check fails while storing, the stored results of the tasks are removed, as they may be outdated
        /// (e.g. after a change of the ticket); without them the tasks are checked when displayed.
        /// </summary>
        /// <returns>The results per task, or null if the check failed.</returns>
        private async Task<IReadOnlyDictionary<long, RequestTaskComplianceResult>?> CheckTasks(IRequestedRulePolicyChecker checker,
            List<int> policyIds, List<WfReqTask> tasksToCheck, List<WfReqTask> contextTasks, bool storeResults)
        {
            List<WfReqTask> tasksWithContext = [.. tasksToCheck, .. contextTasks];
            IReadOnlyDictionary<long, RequestTaskComplianceResult> checkResults;
            try
            {
                checkResults = await checker.CheckRequestTasks(policyIds, tasksWithContext);
            }
            catch (Exception exc)
            {
                Log.WriteError("Policy Check", "Compliance check of the request tasks failed.", exc);
                if (storeResults)
                {
                    await RemoveStoredComplianceResults(tasksToCheck);
                }
                return null;
            }

            if (storeResults)
            {
                await StoreComplianceResults(tasksToCheck, checkResults);
            }
            return checkResults;
        }

        /// <summary>
        /// Whether the checked content of the ticket is the same as at the last compliance check.
        /// </summary>
        /// <param name="ticket">The ticket.</param>
        /// <returns>Null if there was no check yet.</returns>
        public bool? MatchesLastComplianceCheck(WfTicket ticket)
        {
            return lastComplianceKey == null ? null : ComplianceKey(ticket, GetConditionalPolicyCheckIds()) == lastComplianceKey;
        }

        private static string ComplianceKey(WfTicket ticket, IEnumerable<int> policyIds)
        {
            return $"{string.Join(",", policyIds.Order())}/{RequestTaskCompliance.Fingerprint(ticket)}";
        }

        /// <summary>
        /// Removes the stored check result of the given tasks, including the copy in the active request task of the workflow handler.
        /// </summary>
        private async Task RemoveStoredComplianceResults(IEnumerable<WfReqTask> tasks)
        {
            foreach (WfReqTask task in tasks.Where(task => RequestTaskCompliance.StoredResult(task) != null))
            {
                await wfHandler.RemoveAddInfoInReqTask(task, AdditionalInfoKeys.ComplianceOk);
                WfReqTask activeTask = wfHandler.ActReqTask;
                if (activeTask.Id == task.Id && !ReferenceEquals(task, activeTask))
                {
                    activeTask.RemoveAddInfo(AdditionalInfoKeys.ComplianceOk);
                }
            }
        }

        /// <summary>
        /// Stores the check result in the additional info of each task whose stored result differs.
        /// The active request task of the workflow handler is a copy of its ticket task, so it gets the new result as well;
        /// otherwise saving it later would write back the outdated result.
        /// </summary>
        private async Task StoreComplianceResults(IEnumerable<WfReqTask> tasks, IReadOnlyDictionary<long, RequestTaskComplianceResult> results)
        {
            foreach (bool isCompliant in kStoredComplianceValues)
            {
                List<WfReqTask> changedTasks = [.. tasks.Where(task => task.Id > 0
                    && results.TryGetValue(task.Id, out RequestTaskComplianceResult? result)
                    && result.IsCompliant == isCompliant
                    && RequestTaskCompliance.StoredResult(task) != isCompliant)];
                if (changedTasks.Count == 0)
                {
                    continue;
                }

                string value = isCompliant.ToString().ToLowerInvariant();
                await wfHandler.SetAddInfoInReqTasks(changedTasks, AdditionalInfoKeys.ComplianceOk, value);
                WfReqTask activeTask = wfHandler.ActReqTask;
                if (changedTasks.Any(task => task.Id == activeTask.Id && !ReferenceEquals(task, activeTask)))
                {
                    activeTask.SetAddInfo(AdditionalInfoKeys.ComplianceOk, value);
                }
            }
        }
    }
}

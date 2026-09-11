using FWO.Basics;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems;
using FWO.Logging;
using FWO.Services;
using FWO.Services.Workflow;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Internal work part of the external request handling: deciding whether a request task is internal
    /// work, promoting it into the approval or planning phase, and delivering the workflow emails its
    /// state change bundled up.
    /// </summary>
    public partial class ExternalRequestHandler
    {
        private bool IsInternalWorkConfiguredForTask(WfReqTask task)
        {
            try
            {
                string changeCategory = GetChangeCategory(task);

                var managementSettings = JsonSerializer.Deserialize<List<ManagementFwConfigChangeState>>(UserConfig.FwConfigChangeMgmSettings) ?? [];
                ManagementFwConfigChangeState? managementSetting = managementSettings.FirstOrDefault(m => m.Id == task.ManagementId);

                return managementSetting?.Enabled == true
                    && managementSetting.SelectedChanges.TryGetValue(changeCategory, out string? selectedSystemValue)
                    && selectedSystemValue == ManagementFwConfigChangeTargets.InternalWork;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInternalWorkTask(WfReqTask task)
        {
            return task.GetAddInfoValue(AdditionalInfoKeys.FwConfigChangeTarget) == ManagementFwConfigChangeTargets.InternalWork;
        }

        /// <summary>
        /// Continues the external request chain after an internal work request task has completed.
        /// </summary>
        /// <param name="ticketId">The ID of the workflow ticket that contains the internal work task.</param>
        /// <param name="reqTaskId">The ID of the completed internal work request task.</param>
        /// <returns>
        /// <c>true</c> if the request chain was continued or a following task was started; otherwise <c>false</c>.
        /// </returns>
        public async Task<bool> ContinueAfterInternalWorkCompletion(long ticketId, long reqTaskId)
        {
            WfTicket? ticket = await InitAndResolve(ticketId);
            if (ticket == null)
            {
                return false;
            }

            WfReqTask? changedTask = ticket.Tasks.FirstOrDefault(task => task.Id == reqTaskId);
            if (changedTask == null || !IsInternalWorkTask(changedTask))
            {
                return false;
            }

            List<WfReqTask> batch = GetInternalWorkBatch(ticket, changedTask);
            if (batch.Count == 0 || !await InternalWorkBatchIsCompleted(batch))
            {
                return false;
            }

            int lastInternalTaskNumber = batch.Max(task => task.TaskNumber);
            Log.WriteInfo("Internal Work", $"Internal work batch for ticket {ticket.Id} completed through task {lastInternalTaskNumber}. Continuing external request chain.");

            return await CreateNextRequest(ticket, lastInternalTaskNumber, null);
        }

        private static List<WfReqTask> GetInternalWorkBatch(WfTicket ticket, WfReqTask task)
        {
            List<WfReqTask> orderedTasks = [.. ticket.Tasks.OrderBy(task => task.TaskNumber)];
            int taskIndex = orderedTasks.FindIndex(candidate => candidate.Id == task.Id);
            if (taskIndex < 0 || !IsInternalWorkTask(orderedTasks[taskIndex]))
            {
                return [];
            }

            int firstIndex = taskIndex;
            while (firstIndex > 0 && IsInternalWorkTask(orderedTasks[firstIndex - 1]))
            {
                firstIndex--;
            }

            int lastIndex = taskIndex;
            while (lastIndex + 1 < orderedTasks.Count && IsInternalWorkTask(orderedTasks[lastIndex + 1]))
            {
                lastIndex++;
            }

            return orderedTasks.GetRange(firstIndex, lastIndex - firstIndex + 1);
        }

        private async Task<bool> InternalWorkBatchIsCompleted(List<WfReqTask> batch)
        {
            WfHandler implementationHandler = new(UserConfig, ApiConnection, WorkflowPhases.implementation, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection))
            { SystemContext = true };

            if (!await implementationHandler.Init())
            {
                throw new InvalidOperationException("Could not initialize implementation workflow handler.");
            }

            foreach (WfReqTask task in batch)
            {
                if (IsFailedInternalWorkState(task.StateId))
                {
                    Log.WriteWarning("Internal Work", $"Internal work task {task.Id} in ticket {task.TicketId} reached failure state {task.StateId}. Request chain will not continue.");
                    return false;
                }

                if (task.StateId < implementationHandler.StateMatrix(task.TaskType).LowestEndState)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task RunInternalWorkStateChangeActions(long ticketId, WorkflowEmailBundleCollector emailBundleCollector)
        {
            if (emailBundleCollector.PendingItems.Count == 0)
            {
                return;
            }

            WfHandler approvalHandler = new(UserConfig, ApiConnection, WorkflowPhases.approval, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection))
            { SystemContext = true };

            if (!await approvalHandler.InitForActionExecution())
            {
                throw new InvalidOperationException("Could not initialize approval workflow handler for bundled internal work emails.");
            }

            WfTicket approvalTicket = await approvalHandler.ResolveTicket(ticketId) ?? throw new InvalidOperationException($"Ticket {ticketId} not found.");
            approvalHandler.SetTicketEnv(approvalTicket);
            approvalHandler.ActionHandler!.EmailBundleCollector = emailBundleCollector;

            await approvalHandler.ActionHandler.FlushEmailBundleCollector();
        }

        /// <summary>
        /// Sends the captured internal work emails and never lets a delivery problem escape into the
        /// external request chain. A failure of both delivery paths is reported through an alert, so
        /// this method has no result for a caller to act on.
        /// </summary>
        /// <param name="ticketId">Ticket the captured emails belong to</param>
        /// <param name="emailBundleCollector">Collector holding the captured emails</param>
        private async Task RunInternalWorkStateChangeActionsSafe(long ticketId, WorkflowEmailBundleCollector emailBundleCollector)
        {
            if (emailBundleCollector.PendingItems.Count == 0)
            {
                return;
            }

            try
            {
                await RunInternalWorkStateChangeActions(ticketId, emailBundleCollector);
            }
            catch (Exception exception)
            {
                Log.WriteError("RunInternalWorkStateChangeActions", $"Could not send bundled internal work emails for ticket {ticketId}.", exception);
                if (!await TrySendPendingInternalWorkEmailsIndividually(ticketId, emailBundleCollector))
                {
                    await AlertUndeliveredInternalWorkEmails(ticketId, emailBundleCollector.PendingItems.Count);
                }

                emailBundleCollector.PendingItems.Clear();
            }
        }

        /// <summary>
        /// Raises an alert for bundled internal work emails that could not be delivered by either path.
        /// The state changes themselves are committed, so this must be visible instead of log-only.
        /// </summary>
        /// <param name="ticketId">Ticket the emails belonged to</param>
        /// <param name="pendingItemCount">Number of emails that are discarded unsent</param>
        private async Task AlertUndeliveredInternalWorkEmails(long ticketId, int pendingItemCount)
        {
            string description = $"{pendingItemCount} bundled internal work approval email(s) for ticket {ticketId} " +
                $"could not be sent by either the bundled or the individual path. No automatic retry is available.";
            Log.WriteError("RunInternalWorkStateChangeActions", description);
            try
            {
                await AlertHelper.SetAlert(ApiConnection, UserConfig.GetText("send_email"), description,
                    GlobalConst.kWorkflow, AlertCode.WorkflowAlert, new AlertHelper.AdditionalAlertData());
            }
            catch (Exception alertException)
            {
                Log.WriteError("RunInternalWorkStateChangeActions", $"Could not write alert for ticket {ticketId}.", alertException);
            }
        }

        private async Task<bool> TrySendPendingInternalWorkEmailsIndividually(long ticketId, WorkflowEmailBundleCollector emailBundleCollector)
        {
            if (emailBundleCollector.PendingItems.Count == 0)
            {
                return true;
            }

            try
            {
                if (wfHandler.ActionHandler == null && !await wfHandler.Init())
                {
                    return false;
                }

                if (wfHandler.ActionHandler == null)
                {
                    return false;
                }

                wfHandler.ActionHandler.EmailBundleCollector = null;
                WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);

                foreach (WorkflowEmailBundleItem item in emailBundleCollector.PendingItems
                    .GroupBy(pendingItem => pendingItem.BundleKey)
                    .Select(group => group.OrderBy(bundleItem => bundleItem.RequestTask.TaskNumber).First()))
                {
                    WfReqTask requestTask = ticket?.Tasks.FirstOrDefault(task => task.Id == item.RequestTask.Id)
                        ?? ticket?.Tasks.FirstOrDefault(task => task.TaskNumber == item.RequestTask.TaskNumber)
                        ?? item.RequestTask;
                    await wfHandler.ActionHandler.SendEmail(item.Action, requestTask, WfObjectScopes.RequestTask, item.Owner, item.UserGrpDn);
                }

                return true;
            }
            catch (Exception fallbackException)
            {
                Log.WriteError("RunInternalWorkStateChangeActions", $"Fallback email delivery failed for ticket {ticketId}.", fallbackException);
                return false;
            }
        }

        private async Task PromoteInternalWorkTaskToPlanning(WfTicket ticket, WfReqTask task, WorkflowEmailBundleCollector emailBundleCollector)
        {
            WfHandler planningHandler = new(UserConfig, ApiConnection, WorkflowPhases.planning, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection))
            { SystemContext = true };

            if (!await planningHandler.Init())
            {
                throw new InvalidOperationException("Could not initialize planning workflow handler.");
            }

            WfTicket planningTicket = await planningHandler.ResolveTicket(ticket.Id) ?? throw new InvalidOperationException($"Ticket {ticket.Id} not found.");

            WfReqTask planningTask = planningTicket.Tasks.FirstOrDefault(ta => ta.TaskNumber == task.TaskNumber) ?? throw new InvalidOperationException($"Task {task.TaskNumber} not found in ticket {ticket.Id}.");

            StateMatrix planningMatrix = planningHandler.StateMatrix(planningTask.TaskType);
            planningTask.StateId = planningMatrix.LowestInputState;
            planningHandler.ActionHandler!.EmailBundleCollector = emailBundleCollector;

            planningHandler.SetTicketEnv(planningTicket);
            planningHandler.SetReqTaskEnv(planningTask);

            await planningHandler.SetAddInfoInReqTask(planningTask, AdditionalInfoKeys.FwConfigChangeTarget, ManagementFwConfigChangeTargets.InternalWork);

            if (!IsInternalWorkTask(planningTask))
            {
                throw new InvalidOperationException($"Internal work marker could not be set for task {task.TaskNumber} in ticket {ticket.Id}.");
            }

            await planningHandler.PromoteReqTask(planningTask);

            await LogRequestTasks([planningTask], ticket.Requester?.Name, ModellingTypes.ChangeType.Request);
        }

        private async Task<WorkflowPhases> PromoteInternalWorkTaskToApproval(WfTicket ticket, WfReqTask task, WorkflowEmailBundleCollector emailBundleCollector)
        {
            WfHandler approvalHandler = new(UserConfig, ApiConnection, WorkflowPhases.approval, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection))
            { SystemContext = true };

            if (!await approvalHandler.Init())
            {
                throw new InvalidOperationException("Could not initialize approval workflow handler.");
            }

            WfTicket approvalTicket = await approvalHandler.ResolveTicket(ticket.Id) ?? throw new InvalidOperationException($"Ticket {ticket.Id} not found.");

            WfReqTask approvalTask = approvalTicket.Tasks.FirstOrDefault(ta => ta.TaskNumber == task.TaskNumber) ?? throw new InvalidOperationException($"Task {task.TaskNumber} not found in ticket {ticket.Id}.");

            StateMatrix approvalMatrix = approvalHandler.StateMatrix(approvalTask.TaskType);
            if (!PhaseIsActive(approvalHandler.MasterStateMatrix, WorkflowPhases.approval) || !PhaseIsActive(approvalMatrix, WorkflowPhases.approval))
            {
                await PromoteInternalWorkTaskToPlanning(ticket, task, emailBundleCollector);
                return WorkflowPhases.planning;
            }

            approvalHandler.ActionHandler!.EmailBundleCollector = emailBundleCollector;

            approvalHandler.SetTicketEnv(approvalTicket);
            approvalHandler.SetReqTaskEnv(approvalTask);

            await approvalHandler.SetAddInfoInReqTask(approvalTask, AdditionalInfoKeys.FwConfigChangeTarget, ManagementFwConfigChangeTargets.InternalWork);

            if (!IsInternalWorkTask(approvalTask))
            {
                throw new InvalidOperationException($"Internal work marker could not be set for task {task.TaskNumber} in ticket {ticket.Id}.");
            }

            approvalHandler.SetReqTaskEnv(approvalTask);

            approvalTask.StateId = approvalMatrix.LowestStartedState;
            await approvalHandler.PromoteReqTask(approvalTask, setStartedHandler: false);

            await LogRequestTasks([approvalTask], ticket.Requester?.Name, ModellingTypes.ChangeType.Request);
            return WorkflowPhases.approval;
        }

        private static bool PhaseIsActive(StateMatrix stateMatrix, WorkflowPhases phase)
        {
            return stateMatrix.PhaseActive.TryGetValue(phase, out bool active) && active;
        }

        private bool IsFailedInternalWorkState(int stateId)
        {
            List<int?> failedStates =
            [
                extStateHandler?.GetInternalStateId(ExtStates.Rejected),
                extStateHandler?.GetInternalStateId(ExtStates.ExtReqRejected),
                extStateHandler?.GetInternalStateId(ExtStates.ExtReqAckRejected),
                extStateHandler?.GetInternalStateId(ExtStates.ExtReqDiscarded)
            ];

            return failedStates.Any(failedState => failedState == stateId);
        }
    }
}

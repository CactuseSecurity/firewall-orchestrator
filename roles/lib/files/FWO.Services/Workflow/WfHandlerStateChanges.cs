using FWO.Basics;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Defines how an explicit workflow monitoring state change is applied.
    /// </summary>
    public enum MonitoringStateChangeMode
    {
        /// <summary>
        /// Updates only the selected object state and suppresses state actions.
        /// </summary>
        LocalOnly,

        /// <summary>
        /// Updates the selected object and recalculates parent object states without state actions.
        /// </summary>
        CascadeParents,

        /// <summary>
        /// Updates the selected object, recalculates parent states, and triggers state actions.
        /// </summary>
        TriggerActions
    }

    public partial class WfHandler
    {
        // promote the different objects

        public async Task<bool> PromoteTicket(WfStatefulObject ticket)
        {
            try
            {
                ActTicket.StateId = ticket.StateId;
                await UpdateActTicketState();
                ResetTicketActions();
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_ticket"), "", true);
            }
            return false;
        }

        public async Task<bool> PromoteTicketAndTasks(WfStatefulObject ticket)
        {
            try
            {
                if (await PromoteTicket(ticket) && await UpdateRequestTasksFromTicket(false))
                {
                    await UpdateActTicketStateFromReqTasks();
                }
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_ticket"), "", true);
            }
            return false;
        }

        public async Task PromoteReqTask(WfStatefulObject reqTask)
        {
            try
            {
                ActReqTask.StateId = reqTask.StateId;
                if (ActReqTask.Start == null && ActReqTask.StateId >= ActStateMatrix.LowestStartedState)
                {
                    ActReqTask.Start = DateTime.Now;
                    ActReqTask.CurrentHandler = userConfig.User;
                }
                await UpdateActReqTaskState();

                if (Phase == WorkflowPhases.planning)
                {
                    await UpgradeImplTaskStatesToReqTask(ActReqTask);
                }

                await UpdateActTicketStateFromReqTasks();
                DisplayPromoteReqTaskMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_task"), "", true);
            }
        }

        public async Task PromoteImplTask(WfStatefulObject implTask)
        {
            try
            {
                ActImplTask.StateId = implTask.StateId;
                ActImplTask.CurrentHandler = userConfig.User;
                if (Phase == WorkflowPhases.implementation && ActImplTask.Stop == null && ActImplTask.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActImplTask.Stop = DateTime.Now;
                }
                await UpdateActImplTaskState();
                ResetImplTaskList();
                await UpdateReqTaskStatesFromActImplTask();
                await UpdateActTicketStateFromReqTasks();
                DisplayPromoteImplTaskMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_task"), "", true);
            }
        }

        public async Task AutoPromote(WfStatefulObject statefulObject, WfObjectScopes scope, int? toStateId)
        {
            bool promotePossible = false;
            if (toStateId != null)
            {
                statefulObject.StateId = (int)toStateId;
                promotePossible = true;
            }
            else
            {
                List<int> possibleStates = ActStateMatrix.getAllowedTransitions(statefulObject.StateId, true);
                if (possibleStates.Count >= 1)
                {
                    statefulObject.StateId = possibleStates[0];
                    promotePossible = true;
                }
            }

            if (promotePossible)
            {
                switch (scope)
                {
                    case WfObjectScopes.Ticket:
                        SetTicketEnv((WfTicket)statefulObject);
                        await PromoteTicket(statefulObject);
                        break;
                    case WfObjectScopes.RequestTask:
                        SetReqTaskEnv((WfReqTask)statefulObject);
                        ActReqTask.StateId = statefulObject.StateId;
                        ActReqTask.CurrentHandler = statefulObject.CurrentHandler;
                        await UpdateActReqTaskState();
                        break;
                    case WfObjectScopes.ImplementationTask:
                        SetImplTaskEnv((WfImplTask)statefulObject);
                        ActImplTask.StateId = statefulObject.StateId;
                        ActImplTask.CurrentHandler = statefulObject.CurrentHandler;
                        await UpdateActImplTaskState();
                        break;
                    case WfObjectScopes.Approval:
                        if (SetReqTaskEnv(((WfApproval)statefulObject).TaskId))
                        {
                            await SetApprovalEnv();
                            await ApproveTask(statefulObject);
                        }
                        break;
                    default:
                        break;
                }
                Log.WriteDebug("AutoPromote", $"Done for {scope} id {GetStatefulObjectId(statefulObject)} with state {statefulObject.StateId}.");
            }
        }

        /// <summary>
        /// Applies an explicit ticket state change from workflow monitoring.
        /// </summary>
        public async Task ChangeTicketStateForMonitoring(WfTicket ticket, int targetStateId, MonitoringStateChangeMode mode)
        {
            SetTicketEnv(ticket);
            int oldStateId = ActTicket.StateId;
            ActTicket.StateId = targetStateId;
            await UpdateActTicketState(mode == MonitoringStateChangeMode.TriggerActions, false);
            LogMonitoringStateChange(WfObjectScopes.Ticket, ActTicket.Id, ActTicket.Id, oldStateId, targetStateId, mode);
        }

        /// <summary>
        /// Applies an explicit request task state change from workflow monitoring.
        /// </summary>
        public async Task ChangeReqTaskStateForMonitoring(WfTicket ticket, WfReqTask reqTask, int targetStateId, MonitoringStateChangeMode mode)
        {
            SetTicketEnv(ticket);
            SetReqTaskEnv(reqTask);
            int oldStateId = ActReqTask.StateId;
            ActReqTask.StateId = targetStateId;
            await UpdateActReqTaskState(mode == MonitoringStateChangeMode.TriggerActions);

            if (mode != MonitoringStateChangeMode.LocalOnly)
            {
                await UpdateActTicketStateFromReqTasks(mode == MonitoringStateChangeMode.TriggerActions, false);
            }
            LogMonitoringStateChange(WfObjectScopes.RequestTask, ActReqTask.Id, ActTicket.Id, oldStateId, targetStateId, mode);
        }

        /// <summary>
        /// Applies an explicit implementation task state change from workflow monitoring.
        /// </summary>
        public async Task ChangeImplTaskStateForMonitoring(WfTicket ticket, WfReqTask reqTask, WfImplTask implTask, int targetStateId, MonitoringStateChangeMode mode)
        {
            SetTicketEnv(ticket);
            SetReqTaskEnv(reqTask);
            SetImplTaskEnv(implTask);
            int oldStateId = ActImplTask.StateId;
            ActImplTask.StateId = targetStateId;
            await UpdateActImplTaskState(mode == MonitoringStateChangeMode.TriggerActions);
            ResetImplTaskList();

            if (mode != MonitoringStateChangeMode.LocalOnly)
            {
                await UpdateReqTaskStatesFromActImplTask(mode == MonitoringStateChangeMode.TriggerActions);
                await UpdateActTicketStateFromReqTasks(mode == MonitoringStateChangeMode.TriggerActions, false);
            }
            LogMonitoringStateChange(WfObjectScopes.ImplementationTask, ActImplTask.Id, ActTicket.Id, oldStateId, targetStateId, mode);
        }

        /// <summary>
        /// Applies an explicit approval state change from workflow monitoring.
        /// </summary>
        public async Task ChangeApprovalStateForMonitoring(WfTicket ticket, WfReqTask reqTask, WfApproval approval, int targetStateId, MonitoringStateChangeMode mode)
        {
            SetTicketEnv(ticket);
            SetReqTaskEnv(reqTask);
            await SetApprovalEnv(approval, false);
            int oldStateId = ActApproval.StateId;
            ActApproval.StateId = targetStateId;
            await UpdateActApproval(mode == MonitoringStateChangeMode.TriggerActions);

            if (mode != MonitoringStateChangeMode.LocalOnly)
            {
                await UpdateActReqTaskStateFromApprovals(mode == MonitoringStateChangeMode.TriggerActions);
                SyncActTicketFromReqTask(ActReqTask);
                await UpdateActTicketStateFromReqTasks(mode == MonitoringStateChangeMode.TriggerActions, false);
            }
            LogMonitoringStateChange(WfObjectScopes.Approval, ActApproval.Id, ActTicket.Id, oldStateId, targetStateId, mode);
        }

        private void LogMonitoringStateChange(WfObjectScopes scope, long objectId, long ticketId, int oldStateId, int newStateId, MonitoringStateChangeMode mode)
        {
            Log.WriteWarning("Workflow Monitoring",
                $"Admin monitoring state change by {MonitoringActor()}: {scope} {objectId} on ticket {ticketId} changed from state {oldStateId} to {newStateId} with mode {mode}.");
        }

        private void LogMonitoringImplementationTasksCreated(long ticketId, long reqTaskId, int createdCount)
        {
            Log.WriteWarning("Workflow Monitoring",
                $"Admin monitoring implementation task creation by {MonitoringActor()}: created {createdCount} implementation task(s) for request task {reqTaskId} on ticket {ticketId}.");
        }

        private string MonitoringActor()
        {
            foreach (string? actor in new[] { AuthUser?.FindFirst("x-hasura-uuid")?.Value, AuthUser?.Identity?.Name, userConfig.User.Name, userConfig.User.Dn })
            {
                if (!string.IsNullOrWhiteSpace(actor))
                {
                    return actor;
                }
            }
            return userConfig.User.DbId > 0 ? userConfig.User.DbId.ToString() : "unknown";
        }

        private static string GetStatefulObjectId(WfStatefulObject statefulObject)
        {
            return statefulObject switch
            {
                WfTicket ticket => ticket.Id.ToString(),
                WfReqTask reqTask => reqTask.Id.ToString(),
                WfImplTask implTask => implTask.Id.ToString(),
                WfApproval approval => approval.Id.ToString(),
                _ => ""
            };
        }

        private static void AuditUnexpectedStateTransition(WfStatefulObject statefulObject, WfObjectScopes scope, StateMatrix stateMatrix)
        {
            if (!statefulObject.StateChanged() || statefulObject.StateChangedByCreation())
            {
                return;
            }

            int oldStateId = statefulObject.ChangedFrom();
            int newStateId = statefulObject.StateId;
            List<int> allowedTransitions = stateMatrix.getAllowedTransitions(oldStateId, allowAutomaticOnlyStates: true);
            if (allowedTransitions.Contains(newStateId))
            {
                return;
            }

            string configuredTransitions = allowedTransitions.Count == 0 ? "none" : string.Join(", ", allowedTransitions);
            Log.WriteWarning("Workflow State",
                $"Persisting workflow state transition {oldStateId}->{newStateId} for {scope} {GetStatefulObjectId(statefulObject)} that is not configured in the state matrix. Configured transitions: {configuredTransitions}.");
        }


        // synchronization between the different objects

        private async Task UpdateActTicketState(bool triggerActions = true, bool syncImplementationTasks = true)
        {
            if (ActTicket.StateId >= MasterStateMatrix.MinTicketCompleted)
            {
                ActTicket.CompletionDate = DateTime.Now;
            }
            if (syncImplementationTasks)
            {
                await AutoCreateOrUpdateImplTasks();
            }
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActTicket, WfObjectScopes.Ticket, MasterStateMatrix);
                await dbAcc.UpdateTicketStateInDb(ActTicket, triggerActions);
            }
            int idx = TicketList.FindIndex(x => x.Id == ActTicket.Id);
            if (idx >= 0)
            {
                TicketList[idx] = ActTicket;
            }
        }

        private void SyncActTicketFromReqTask(WfReqTask reqTask)
        {
            int idx = ActTicket.Tasks.FindIndex(x => x.Id == reqTask.Id);
            if (idx >= 0)
            {
                ActTicket.Tasks[idx] = reqTask;
            }
        }

        private async Task UpdateActTicketStateFromReqTasks(bool triggerActions = true, bool syncImplementationTasks = true)
        {
            if (ActTicket.Tasks.Count > 0)
            {
                List<int> taskStates = [];
                foreach (WfReqTask tsk in ActTicket.Tasks)
                {
                    taskStates.Add(tsk.StateId);
                }
                int derivedState = MasterStateMatrix.getDerivedStateFromSubStates(taskStates);
                Log.WriteDebug("UpdateActTicketStateFromReqTasks", $"Ticket {ActTicket.Id}: derived state {derivedState} from request task states {string.Join(", ", taskStates)}.");
                ActTicket.StateId = derivedState;
            }
            await UpdateActTicketState(triggerActions, syncImplementationTasks);
        }

        public async Task UpdateActReqTaskState(bool triggerActions = true)
        {
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActReqTask, WfObjectScopes.RequestTask, ActStateMatrix);
                await dbAcc.UpdateReqTaskStateInDb(ActReqTask, triggerActions);
            }
            SyncActTicketFromReqTask(ActReqTask);
        }

        private async Task<bool> UpdateRequestTasksFromTicket(bool createImplTasks = true, bool triggerActions = true)
        {
            bool requestTaskActionsChangedState = false;
            List<WfReqTask> requestTasks = [.. ActTicket.Tasks];
            List<WfReqTask> requestTasksNeedingInitialImplTasks = [];
            foreach (WfReqTask reqtask in requestTasks)
            {
                StateMatrix reqTaskMatrix = stateMatrixDict.Matrices[reqtask.TaskType];
                int newReqTaskState = reqTaskMatrix.getDerivedStateFromSubStates([ActTicket.StateId]);
                Log.WriteDebug("UpdateRequestTasksFromTicket", $"Ticket {ActTicket.Id} state {ActTicket.StateId}: request task {reqtask.Id} ({reqtask.TaskType}) state {reqtask.StateId} -> {newReqTaskState}.");
                await UpdateReqTaskAndApprovalStatesFromTicket(reqtask, reqTaskMatrix, newReqTaskState, triggerActions);
                if (reqtask.StateId != newReqTaskState)
                {
                    requestTaskActionsChangedState = true;
                    Log.WriteDebug("UpdateRequestTasksFromTicket", $"Request task {reqtask.Id} changed by actions from synced state {newReqTaskState} to {reqtask.StateId}.");
                }
                if (createImplTasks && reqtask.ImplementationTasks.Count == 0 && !stateMatrixDict.Matrices[reqtask.TaskType].PhaseActive[WorkflowPhases.planning]
                    && RequestTaskNeedsInitialImplTasks(reqtask))
                {
                    requestTasksNeedingInitialImplTasks.Add(reqtask);
                }
            }
            foreach (WfReqTask reqtask in await RequestTasksForInitialImplCreation(requestTasksNeedingInitialImplTasks))
            {
                await AutoCreateImplTasks(reqtask);
            }
            return requestTaskActionsChangedState;
        }

        private async Task UpdateReqTaskAndApprovalStatesFromTicket(WfReqTask reqTask, StateMatrix reqTaskMatrix, int newReqTaskState, bool triggerActions = true)
        {
            reqTask.StateId = newReqTaskState;
            List<WfApproval> approvalsToUpdate = reqTask.Approvals
                .Where(x => x.StateId < reqTaskMatrix.ApprovalLowestEndState).ToList();
            foreach (WfApproval approval in approvalsToUpdate)
            {
                approval.StateId = reqTask.StateId;
            }
            SyncActTicketFromReqTask(reqTask);

            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(reqTask, WfObjectScopes.RequestTask, reqTaskMatrix);
                await dbAcc.UpdateReqTaskStateInDb(reqTask, triggerActions);
                foreach (WfApproval approval in approvalsToUpdate)
                {
                    AuditUnexpectedStateTransition(approval, WfObjectScopes.Approval, reqTaskMatrix);
                    await dbAcc.UpdateApprovalInDb(approval, triggerActions);
                }
            }
        }

        private async Task UpdateReqTaskStateFromImplTasks(WfReqTask reqTask, bool triggerActions = true)
        {
            if (reqTask.ImplementationTasks.Count > 0)
            {
                List<int> implTaskStates = [];
                foreach (var implTask in reqTask.ImplementationTasks)
                {
                    implTaskStates.Add(implTask.StateId);
                }
                reqTask.StateId = ActStateMatrix.getDerivedStateFromSubStates(implTaskStates);
            }
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(reqTask, WfObjectScopes.RequestTask, stateMatrixDict.Matrices[reqTask.TaskType]);
                await dbAcc.UpdateReqTaskStateInDb(reqTask, triggerActions);
            }
            SyncActTicketFromReqTask(reqTask);
        }

        private async Task UpdateReqTaskStatesFromActImplTask(bool triggerActions = true)
        {
            SyncReqTaskStopTime(ActReqTask);
            await UpdateReqTaskStateFromImplTasks(ActReqTask, triggerActions);

            List<WfReqTask> bundledTasks = [.. GetBundledRequestTasks(ActReqTask).Where(task => task.Id != ActReqTask.Id)];
            foreach (WfReqTask bundledTask in bundledTasks)
            {
                bundledTask.StateId = ActReqTask.StateId;
                if (bundledTask.Stop == null && ActReqTask.Stop != null)
                {
                    bundledTask.Stop = ActReqTask.Stop;
                }
                if (dbAcc != null)
                {
                    AuditUnexpectedStateTransition(bundledTask, WfObjectScopes.RequestTask, stateMatrixDict.Matrices[bundledTask.TaskType]);
                    await dbAcc.UpdateReqTaskStateInDb(bundledTask, triggerActions);
                }
                SyncActTicketFromReqTask(bundledTask);
            }
        }

        private List<WfReqTask> GetBundledRequestTasks(WfReqTask reqTask)
        {
            if (!userConfig.ReqConsiderBundling)
            {
                return [reqTask];
            }

            string bundleId = reqTask.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
            return string.IsNullOrWhiteSpace(bundleId)
                ? [reqTask]
                : [.. ActTicket.Tasks.Where(task => task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId) == bundleId)];
        }

        private async Task UpdateActImplTaskState(bool triggerActions = true)
        {
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActImplTask, WfObjectScopes.ImplementationTask, ActStateMatrix);
                await dbAcc.UpdateImplTaskStateInDb(ActImplTask, triggerActions);
            }
            int index = ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id);
            if (index >= 0)
            {
                ActReqTask.ImplementationTasks[index] = ActImplTask;
            }
            else
            {
                // due to actions the impl task may not be assigned
                ActReqTask.ImplementationTasks.Add(ActImplTask);
            }
        }

        private async Task UpgradeImplTaskStatesToReqTask(WfReqTask reqTask)
        {
            if (dbAcc != null)
            {
                foreach (var impltask in reqTask.ImplementationTasks)
                {
                    if (impltask.StateId < reqTask.StateId)
                    {
                        impltask.StateId = reqTask.StateId;
                        AuditUnexpectedStateTransition(impltask, WfObjectScopes.ImplementationTask, stateMatrixDict.Matrices[reqTask.TaskType]);
                        await dbAcc.UpdateImplTaskStateInDb(impltask);
                    }
                }
            }
        }

        private static void SyncReqTaskStopTime(WfReqTask reqTask)
        {
            bool openImplTask = false;
            foreach (var impltask in reqTask.ImplementationTasks)
            {
                if (impltask.Stop == null)
                {
                    openImplTask = true;
                }
            }
            if (!openImplTask && reqTask.Stop == null)
            {
                reqTask.Stop = reqTask.ImplementationTasks.FirstOrDefault(task => task.Stop != null)?.Stop;
            }
        }
    }
}

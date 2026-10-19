using FWO.Basics;
using FWO.Data;
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
        public string? WorkflowEmailBundleId { get; private set; }

        private void BeginWorkflowEmailBundle()
        {
            WorkflowEmailBundleId = Guid.NewGuid().ToString("N");
            ActionHandler?.ResetBundledDelegations();
        }

        private void ClearWorkflowEmailBundle()
        {
            WorkflowEmailBundleId = null;
        }

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
            bool emailBundleStarted = false;
            bool emailBundleFlushAttempted = false;
            try
            {
                if (!await PromoteTicket(ticket))
                {
                    return false;
                }

                BeginWorkflowEmailBundle();
                emailBundleStarted = true;

                // Creation itself is guarded by the implementation phase's
                // configured lowest input state. Do not suppress it merely
                // because this caller is in the request phase.
                await UpdateRequestTasksFromTicket(true, approvalComment: ticket.OptComment());
                // In the request phase, task actions execute in middleware and
                // may promote the task and ticket further. The UI still holds
                // the pre-action task state here, so deriving the ticket from
                // it would overwrite the middleware result with stale data.
                if (Phase != WorkflowPhases.request)
                {
                    await UpdateActTicketStateFromReqTasks(syncImplementationTasks: false);
                }

                // Set before the call so a throw inside it still counts as attempted and the finally does
                // not report the same delivery failure a second time.
                emailBundleFlushAttempted = true;
                await FlushWorkflowEmailBundle();
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_ticket"), "", true);
            }
            finally
            {
                if (emailBundleStarted)
                {
                    // Captured emails were suppressed at their state action, so an aborted promote must
                    // still flush what was collected - those request tasks did change state.
                    if (!emailBundleFlushAttempted)
                    {
                        await FlushWorkflowEmailBundle();
                    }
                    ClearWorkflowEmailBundle();
                }
            }
            return false;
        }

        /// <summary>
        /// Promotes lower request tasks and approvals before persisting the requested ticket state.
        /// </summary>
        public async Task<bool> PromoteTasksAndTicket(WfStatefulObject ticket)
        {
            bool emailBundleStarted = false;
            bool emailBundleFlushAttempted = false;
            try
            {
                int targetTicketStateId = ticket.StateId;
                BeginWorkflowEmailBundle();
                emailBundleStarted = true;

                await UpdateRequestTasksFromTicket(false, targetTicketStateId: targetTicketStateId,
                    approvalComment: ticket.OptComment());
                ActTicket.StateId = targetTicketStateId;
                await UpdateActTicketState();

                emailBundleFlushAttempted = true;
                await FlushWorkflowEmailBundle();
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_ticket"), "", true);
            }
            finally
            {
                if (emailBundleStarted)
                {
                    if (!emailBundleFlushAttempted)
                    {
                        await FlushWorkflowEmailBundle();
                    }
                    ClearWorkflowEmailBundle();
                }
            }
            return false;
        }

        /// <summary>
        /// Ends the active workflow email bundle and asks the middleware to send it. Delivery problems are
        /// reported to the promoting user and never turn a completed state change into a failed one. The
        /// flush itself decides whether a round trip is needed, so an empty bundle reports nothing.
        /// </summary>
        private async Task FlushWorkflowEmailBundle()
        {
            if (ActionHandler == null)
            {
                // Without an action handler no state action ran, so nothing was captured. Reporting an
                // email problem here would be misleading.
                return;
            }

            try
            {
                await ActionHandler.FlushWorkflowEmailBundleInMiddleware(ActTicket.Id);
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("send_email"),
                    $"Could not send bundled workflow emails for ticket {ActTicket.Id}.", exception);
                DisplayMessageInUi(exception, userConfig.GetText("send_email"), userConfig.GetText("E9105"), true);
            }
        }

        public async Task PromoteReqTask(WfStatefulObject reqTask, bool setStartedHandler = true)
        {
            try
            {
                List<WfReqTask> requestTasks = GetBundledRequestTasks(ActReqTask, ActReqTask.StateId);
                foreach (WfReqTask requestTask in requestTasks)
                {
                    // ActReqTask is a working copy; use it for the selected task and the
                    // ticket instance for its bundled siblings so every task is persisted
                    // and its request-task actions are executed independently.
                    WfReqTask taskToPromote = requestTask.Id == ActReqTask.Id ? ActReqTask : requestTask;
                    await PromoteSingleReqTask(taskToPromote, reqTask.StateId, setStartedHandler);
                }
                await UpdateActTicketStateFromReqTasks();
                DisplayPromoteReqTaskMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_task"), "", true);
            }
        }

        private async Task PromoteSingleReqTask(WfReqTask reqTask, int targetStateId, bool setStartedHandler)
        {
            StateMatrix reqTaskMatrix = stateMatrixDict.Matrices.TryGetValue(reqTask.TaskType, out StateMatrix? configuredMatrix)
                ? configuredMatrix
                : ActStateMatrix;
            reqTask.StateId = targetStateId;
            if (setStartedHandler && reqTask.Start == null && reqTask.StateId >= reqTaskMatrix.LowestStartedState)
            {
                reqTask.Start = DateTime.Now;
                reqTask.CurrentHandler = userConfig.User;
            }

            if (reqTask.Id == ActReqTask.Id)
            {
                await UpdateActReqTaskState();
            }
            else
            {
                if (dbAcc != null)
                {
                    AuditUnexpectedStateTransition(reqTask, WfObjectScopes.RequestTask, reqTaskMatrix);
                    await dbAcc.UpdateReqTaskStateInDb(reqTask);
                }
                SyncActTicketFromReqTask(reqTask);
            }

            if (Phase == WorkflowPhases.planning)
            {
                await UpgradeImplTaskStatesToReqTask(reqTask);
            }
        }

        public async Task PromoteImplTask(WfStatefulObject implTask, NotificationPlaceholderData? placeholderData = null)
        {
            try
            {
                ActImplTask.StateId = implTask.StateId;
                ActImplTask.CurrentHandler = userConfig.User;
                if (Phase == WorkflowPhases.implementation && ActImplTask.Stop == null && ActImplTask.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActImplTask.Stop = DateTime.Now;
                }
                await UpdateActImplTaskState(placeholderData: placeholderData);
                ResetImplTaskList();
                await UpdateReqTaskStatesFromActImplTask(placeholderData: placeholderData);
                await UpdateActTicketStateFromReqTasks(placeholderData: placeholderData);
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
                Log.WriteDebug("AutoPromote", $"Using configured target state {statefulObject.StateId} for {scope} id {GetStatefulObjectId(statefulObject)}.");
                promotePossible = true;
            }
            else
            {
                List<int> possibleStates = ActStateMatrix.getAllowedTransitions(statefulObject.StateId, true);
                if (possibleStates.Count >= 1)
                {
                    statefulObject.StateId = possibleStates[0];
                    Log.WriteDebug("AutoPromote", $"Using fallback target state {statefulObject.StateId} for {scope} id {GetStatefulObjectId(statefulObject)} from allowed transitions [{string.Join(", ", possibleStates)}].");
                    promotePossible = true;
                }
            }

            if (promotePossible)
            {
                switch (scope)
                {
                    case WfObjectScopes.Ticket:
                        SetTicketEnv((WfTicket)statefulObject);
                        if (Phase == WorkflowPhases.request)
                        {
                            await PromoteTicket(statefulObject);
                        }
                        else
                        {
                            await PromoteTasksAndTicket(statefulObject);
                        }
                        break;
                    case WfObjectScopes.RequestTask:
                        SetReqTaskEnv((WfReqTask)statefulObject);
                        ActReqTask.StateId = statefulObject.StateId;
                        ActReqTask.CurrentHandler = statefulObject.CurrentHandler;
                        await UpdateActReqTaskState();
                        // A request-task auto-promote can bypass the approval
                        // phase. Synchronize pending approvals to their own
                        // approval outcome before deriving the parent ticket.
                        StateMatrix reqTaskMatrix = stateMatrixDict.Matrices[ActReqTask.TaskType];
                        await UpdateReqTaskAndApprovalStatesFromTicket(ActReqTask, reqTaskMatrix, ActReqTask.StateId);
                        await UpdateActTicketStateFromReqTasks();
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
                            StateMatrix approvalTaskMatrix = stateMatrixDict.Matrices[ActReqTask.TaskType];
                            statefulObject.StateId = GetApprovalStateForRequestTask(statefulObject.StateId, approvalTaskMatrix);
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
            StateMatrix approvalTaskMatrix = stateMatrixDict.Matrices[ActReqTask.TaskType];
            int oldStateId = ActApproval.StateId;
            ActApproval.StateId = approvalTaskMatrix.ApprovalLowestEndState > 0
                ? GetApprovalStateForRequestTask(targetStateId, approvalTaskMatrix)
                : targetStateId;
            if (approvalTaskMatrix.ApprovalLowestEndState > 0
                && ActApproval.StateId >= approvalTaskMatrix.ApprovalLowestEndState)
            {
                ActApproval.ApprovalDate = DateTime.Now;
                ActApproval.ApproverDn = CurrentUserDn();
            }
            await UpdateActApproval(mode == MonitoringStateChangeMode.TriggerActions);

            if (mode != MonitoringStateChangeMode.LocalOnly)
            {
                await UpdateActReqTaskStateFromApprovals(mode == MonitoringStateChangeMode.TriggerActions);
                SyncActTicketFromReqTask(ActReqTask);
                await UpdateActTicketStateFromReqTasks(mode == MonitoringStateChangeMode.TriggerActions, false);
            }
            LogMonitoringStateChange(WfObjectScopes.Approval, ActApproval.Id, ActTicket.Id, oldStateId, ActApproval.StateId, mode);
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

        private string CurrentUserDn()
        {
            string? authenticatedUserDn = AuthUser?.FindFirst("x-hasura-uuid")?.Value;
            string userDn = string.IsNullOrWhiteSpace(authenticatedUserDn) ? userConfig.User.Dn : authenticatedUserDn;
            return string.IsNullOrWhiteSpace(userDn) ? GlobalConst.kSystemUser : userDn;
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

        private async Task UpdateActTicketState(bool triggerActions = true, bool syncImplementationTasks = true,
            NotificationPlaceholderData? placeholderData = null)
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
                await dbAcc.UpdateTicketStateInDb(ActTicket, triggerActions, placeholderData: placeholderData);
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

        private async Task UpdateActTicketStateFromReqTasks(bool triggerActions = true, bool syncImplementationTasks = true,
            NotificationPlaceholderData? placeholderData = null)
        {
            if (ActTicket.Tasks.Count > 0)
            {
                List<int> taskStates = [.. ActTicket.Tasks.Select(task => task.StateId)];
                if (dbAcc != null && ActTicket.Id > 0)
                {
                    WfTicket? persistedTicket = await dbAcc.LoadPreviousTicket(ActTicket.Id);
                    if (persistedTicket?.Tasks.Count > 0)
                    {
                        taskStates = [.. persistedTicket.Tasks.Select(task => task.StateId)];
                        Log.WriteDebug("UpdateActTicketStateFromReqTasks",
                            $"Ticket {ActTicket.Id}: using persisted request-task states {string.Join(", ", taskStates)} for ticket derivation.");
                    }
                }
                int derivedState = MasterStateMatrix.getDerivedStateFromSubStates(taskStates);
                Log.WriteDebug("UpdateActTicketStateFromReqTasks", $"Ticket {ActTicket.Id}: derived state {derivedState} from request task states {string.Join(", ", taskStates)}.");
                bool mixedTaskPhases = taskStates.Any(state => state < MasterStateMatrix.LowestStartedState)
                    && taskStates.Any(state => state >= MasterStateMatrix.LowestStartedState);
                if (!mixedTaskPhases || derivedState >= ActTicket.StateId)
                {
                    ActTicket.StateId = derivedState;
                }
                else
                {
                    Log.WriteDebug("UpdateActTicketStateFromReqTasks", $"Keeping ticket {ActTicket.Id} in state {ActTicket.StateId}; mixed request-task phases would derive a transient lower state {derivedState}.");
                }
            }
            await UpdateActTicketState(triggerActions, syncImplementationTasks, placeholderData);
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

        private async Task<bool> UpdateRequestTasksFromTicket(bool createImplTasks = true, bool triggerActions = true,
            int? targetTicketStateId = null, string? approvalComment = null)
        {
            bool requestTaskActionsChangedState = false;
            List<WfReqTask> requestTasks = [.. ActTicket.Tasks];
            List<WfReqTask> requestTasksNeedingInitialImplTasks = [];
            int synchronizationTicketStateId = targetTicketStateId ?? ActTicket.StateId;
            // Read the stored ticket once for the whole loop: every task and approval below needs the same
            // pre-change snapshot for the change history, and none of them is affected by another's write.
            WfTicket? storedTicket = dbAcc != null && requestTasks.Count > 0 ? await dbAcc.LoadPreviousTicket(ActTicket.Id) : null;
            foreach (WfReqTask reqtask in requestTasks)
            {
                StateMatrix reqTaskMatrix = stateMatrixDict.Matrices[reqtask.TaskType];
                List<int> synchronizationTicketStates = new() { synchronizationTicketStateId };
                int newReqTaskState = reqTaskMatrix.getDerivedStateFromSubStates(synchronizationTicketStates);
                if (Phase == WorkflowPhases.approval && synchronizationTicketStateId >= reqTaskMatrix.LowestEndState)
                {
                    newReqTaskState = synchronizationTicketStateId;
                }
                int oldReqTaskState = reqtask.StateId;
                Log.WriteDebug("UpdateRequestTasksFromTicket", $"Ticket {ActTicket.Id} state {synchronizationTicketStateId}: request task {reqtask.Id} ({reqtask.TaskType}) state {reqtask.StateId} -> {newReqTaskState}.");
                await UpdateReqTaskAndApprovalStatesFromTicket(reqtask, reqTaskMatrix, newReqTaskState, triggerActions,
                    storedTicket, approvalComment);
                if (reqtask.StateId != oldReqTaskState)
                {
                    requestTaskActionsChangedState = true;
                    Log.WriteDebug("UpdateRequestTasksFromTicket", $"Request task {reqtask.Id} changed from {oldReqTaskState} to {reqtask.StateId}.");
                }
                if (createImplTasks && reqtask.ImplementationTasks.Count == 0 && !IsPlanningPhaseActive(stateMatrixDict.Matrices[reqtask.TaskType])
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

        private async Task UpdateReqTaskAndApprovalStatesFromTicket(WfReqTask reqTask, StateMatrix reqTaskMatrix, int newReqTaskState,
            bool triggerActions = true, WfTicket? storedTicket = null, string? approvalComment = null)
        {
            if (reqTask.StateId > newReqTaskState)
            {
                Log.WriteDebug("UpdateRequestTasksFromTicket", $"Keeping request task {reqTask.Id} in state {reqTask.StateId}; ticket-derived state is {newReqTaskState}.");
                return;
            }

            bool requestTaskStateChanged = reqTask.StateId < newReqTaskState;
            if (requestTaskStateChanged)
            {
                reqTask.StateId = newReqTaskState;
            }
            List<WfApproval> approvalsToUpdate = GetApprovalsToUpdate(reqTask, reqTaskMatrix, newReqTaskState);
            if (!requestTaskStateChanged && approvalsToUpdate.Count == 0)
            {
                return;
            }

            await UpdateApprovalsFromTicket(approvalsToUpdate, reqTask, reqTaskMatrix, approvalComment);
            SyncActTicketFromReqTask(reqTask);
            await PersistReqTaskAndApprovals(reqTask, reqTaskMatrix, approvalsToUpdate, requestTaskStateChanged,
                triggerActions, storedTicket);
        }

        private static List<WfApproval> GetApprovalsToUpdate(WfReqTask reqTask, StateMatrix reqTaskMatrix, int newReqTaskState)
        {
            bool approvalPhaseActive = reqTaskMatrix.PhaseActive.TryGetValue(WorkflowPhases.approval, out bool active)
                && active;
            if (!approvalPhaseActive)
            {
                return [];
            }
            return reqTask.Approvals
                .Where(approval => approval.StateId < reqTaskMatrix.ApprovalLowestEndState
                    && approval.StateId < newReqTaskState).ToList();
        }

        private async Task UpdateApprovalsFromTicket(List<WfApproval> approvals, WfReqTask reqTask,
            StateMatrix reqTaskMatrix, string? approvalComment)
        {
            int approvalState = GetApprovalStateForRequestTask(reqTask.StateId, reqTaskMatrix);
            bool implicitApproval = approvalState == reqTaskMatrix.ApprovalLowestEndState
                && reqTask.StateId > reqTaskMatrix.ApprovalLowestEndState;
            foreach (WfApproval approval in approvals)
            {
                approval.StateId = approvalState;
                if (approval.StateId >= reqTaskMatrix.ApprovalLowestEndState)
                {
                    approval.ApprovalDate = DateTime.Now;
                    // The comment identifies this as an implicit approval. Keep the
                    // actual initiating user as approver so the approval remains
                    // resolvable and visible in the UI.
                    approval.ApproverDn = CurrentUserDn();
                    await AddApprovalPromotionComment(approval, approvalComment, implicitApproval);
                }
            }
        }

        private async Task AddApprovalPromotionComment(WfApproval approval, string? approvalComment, bool implicitApproval)
        {
            if (!string.IsNullOrWhiteSpace(approvalComment))
            {
                await AddApprovalComment(approval, approvalComment);
            }
            else if (implicitApproval)
            {
                await AddImplicitApprovalComment(approval);
            }
        }

        private async Task PersistReqTaskAndApprovals(WfReqTask reqTask, StateMatrix reqTaskMatrix,
            List<WfApproval> approvals, bool requestTaskStateChanged, bool triggerActions, WfTicket? storedTicket)
        {
            if (dbAcc == null)
            {
                return;
            }
            if (requestTaskStateChanged)
            {
                AuditUnexpectedStateTransition(reqTask, WfObjectScopes.RequestTask, reqTaskMatrix);
                await dbAcc.UpdateReqTaskStateInDb(reqTask, triggerActions, storedTicket);
            }
            foreach (WfApproval approval in approvals)
            {
                AuditUnexpectedStateTransition(approval, WfObjectScopes.Approval, reqTaskMatrix);
                await dbAcc.UpdateApprovalInDb(approval, ActTicket.Id, ActTicket.Requester, triggerActions, storedTicket);
            }
        }

        private static int GetApprovalStateForRequestTask(int requestTaskState, StateMatrix reqTaskMatrix)
        {
            if (requestTaskState < reqTaskMatrix.ApprovalLowestEndState)
            {
                return requestTaskState;
            }

            // Preserve a configured terminal rejection state such as 610. Any
            // later workflow state (planning, implementation, etc.) maps to
            // the approval phase's approved end state instead of leaking into
            // the approval object.
            if (reqTaskMatrix.Matrix.TryGetValue(requestTaskState, out List<int>? transitions)
                && transitions.Count > 0
                && transitions.All(state => state == requestTaskState)
                && requestTaskState != reqTaskMatrix.ApprovalLowestEndState)
            {
                return requestTaskState;
            }

            return reqTaskMatrix.ApprovalLowestEndState;
        }

        private async Task AddImplicitApprovalComment(WfApproval approval)
        {
            string commentText = userConfig.ReqImplicitApprovalComment;
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return;
            }
            await AddApprovalComment(approval, commentText, SystemCommentUser());
        }

        private static UiUser SystemCommentUser()
        {
            return new UiUser
            {
                DbId = 0,
                Name = GlobalConst.kSystemUser,
                Dn = GlobalConst.kSystemUser
            };
        }

        private async Task AddApprovalComment(WfApproval approval, string commentText, UiUser? creator = null)
        {
            WfComment comment = new()
            {
                Scope = WfObjectScopes.Approval.ToString(),
                CreationDate = DateTime.Now,
                Creator = creator ?? userConfig.User,
                CommentText = commentText
            };

            if (dbAcc != null && approval.Id > 0)
            {
                long commentId = await dbAcc.AddCommentToDb(comment);
                if (commentId != 0)
                {
                    await dbAcc.AssignCommentToApprovalInDb(approval.Id, commentId);
                }
            }
            approval.Comments.Add(new WfCommentDataHelper(comment) { });
        }

        private async Task UpdateReqTaskStateFromImplTasks(WfReqTask reqTask, bool triggerActions = true, WfTicket? storedTicket = null,
            NotificationPlaceholderData? placeholderData = null)
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
                await dbAcc.UpdateReqTaskStateInDb(reqTask, triggerActions, storedTicket, placeholderData);
            }
            SyncActTicketFromReqTask(reqTask);
        }

        private async Task UpdateReqTaskStatesFromActImplTask(bool triggerActions = true,
            NotificationPlaceholderData? placeholderData = null)
        {
            int selectedRequestTaskState = ActReqTask.StateId;
            SyncReqTaskStopTime(ActReqTask);
            WfTicket? storedTicket = dbAcc != null ? await dbAcc.LoadPreviousTicket(ActReqTask.TicketId) : null;
            await UpdateReqTaskStateFromImplTasks(ActReqTask, triggerActions, storedTicket, placeholderData);

            List<WfReqTask> bundledTasks = [.. GetBundledRequestTasks(ActReqTask, selectedRequestTaskState)
                .Where(task => task.Id != ActReqTask.Id)];
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
                    await dbAcc.UpdateReqTaskStateInDb(bundledTask, triggerActions, storedTicket, placeholderData);
                }
                SyncActTicketFromReqTask(bundledTask);
            }
        }

        private List<WfReqTask> GetBundledRequestTasks(WfReqTask reqTask, int? stateId = null)
        {
            if (!userConfig.ReqConsiderBundling)
            {
                return [reqTask];
            }

            string bundleId = reqTask.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
            return string.IsNullOrWhiteSpace(bundleId)
                ? [reqTask]
                : [.. ActTicket.Tasks.Where(task => task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId) == bundleId
                    && (!stateId.HasValue || task.StateId == stateId.Value))];
        }

        private async Task UpdateActImplTaskState(bool triggerActions = true, NotificationPlaceholderData? placeholderData = null)
        {
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActImplTask, WfObjectScopes.ImplementationTask, ActStateMatrix);
                await dbAcc.UpdateImplTaskStateInDb(ActImplTask, triggerActions, placeholderData: placeholderData);
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
                List<WfImplTask> tasksToUpgrade = [.. reqTask.ImplementationTasks.Where(task => task.StateId < reqTask.StateId)];
                // only read the stored ticket when there is something to write, and then only once
                WfTicket? storedTicket = tasksToUpgrade.Count > 0 ? await dbAcc.LoadPreviousTicket(reqTask.TicketId) : null;
                foreach (WfImplTask impltask in tasksToUpgrade)
                {
                    impltask.StateId = reqTask.StateId;
                    AuditUnexpectedStateTransition(impltask, WfObjectScopes.ImplementationTask, stateMatrixDict.Matrices[reqTask.TaskType]);
                    await dbAcc.UpdateImplTaskStateInDb(impltask, true, storedTicket);
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

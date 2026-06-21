using FWO.Basics;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
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

        private async Task UpdateActTicketState()
        {
            if (ActTicket.StateId >= MasterStateMatrix.MinTicketCompleted)
            {
                ActTicket.CompletionDate = DateTime.Now;
            }
            await AutoCreateOrUpdateImplTasks();
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActTicket, WfObjectScopes.Ticket, MasterStateMatrix);
                await dbAcc.UpdateTicketStateInDb(ActTicket);
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

        private async Task UpdateActTicketStateFromReqTasks()
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
            await UpdateActTicketState();
        }

        private async Task UpdateActTicketStateFromImplTasks()
        {
            List<WfReqTask> tasks = [.. ActTicket.Tasks];
            foreach (WfReqTask reqTask in tasks)
            {
                await UpdateReqTaskStateFromImplTasks(reqTask);
            }
            await UpdateActTicketStateFromReqTasks();
        }

        public async Task UpdateActReqTaskState()
        {
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActReqTask, WfObjectScopes.RequestTask, ActStateMatrix);
                await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
            }
            SyncActTicketFromReqTask(ActReqTask);
        }

        private async Task<bool> UpdateRequestTasksFromTicket(bool createImplTasks = true)
        {
            bool requestTaskActionsChangedState = false;
            List<WfReqTask> requestTasks = [.. ActTicket.Tasks];
            List<WfReqTask> requestTasksNeedingInitialImplTasks = [];
            foreach (WfReqTask reqtask in requestTasks)
            {
                StateMatrix reqTaskMatrix = stateMatrixDict.Matrices[reqtask.TaskType];
                int newReqTaskState = reqTaskMatrix.getDerivedStateFromSubStates([ActTicket.StateId]);
                Log.WriteDebug("UpdateRequestTasksFromTicket", $"Ticket {ActTicket.Id} state {ActTicket.StateId}: request task {reqtask.Id} ({reqtask.TaskType}) state {reqtask.StateId} -> {newReqTaskState}.");
                await UpdateReqTaskAndApprovalStatesFromTicket(reqtask, reqTaskMatrix, newReqTaskState);
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

        private async Task UpdateReqTaskAndApprovalStatesFromTicket(WfReqTask reqTask, StateMatrix reqTaskMatrix, int newReqTaskState)
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
                await dbAcc.UpdateReqTaskStateInDb(reqTask);
                foreach (WfApproval approval in approvalsToUpdate)
                {
                    AuditUnexpectedStateTransition(approval, WfObjectScopes.Approval, reqTaskMatrix);
                    await dbAcc.UpdateApprovalInDb(approval);
                }
            }
        }

        private async Task UpdateReqTaskStateFromImplTasks(WfReqTask reqTask)
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
                await dbAcc.UpdateReqTaskStateInDb(reqTask);
            }
            SyncActTicketFromReqTask(reqTask);
        }

        private async Task UpdateReqTaskStatesFromActImplTask()
        {
            SyncReqTaskStopTime(ActReqTask);
            await UpdateReqTaskStateFromImplTasks(ActReqTask);

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
                    await dbAcc.UpdateReqTaskStateInDb(bundledTask);
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

        private async Task UpdateActImplTaskState()
        {
            if (dbAcc != null)
            {
                AuditUnexpectedStateTransition(ActImplTask, WfObjectScopes.ImplementationTask, ActStateMatrix);
                await dbAcc.UpdateImplTaskStateInDb(ActImplTask);
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

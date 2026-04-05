using FWO.Basics;
using FWO.Data.Workflow;

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
                if (await PromoteTicket(ticket))
                {
                    await UpdateRequestTasksFromTicket(false);
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
                SyncReqTaskStopTime();
                await UpdateReqTaskStateFromImplTasks(ActReqTask);
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
            }
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
                ActTicket.StateId = MasterStateMatrix.getDerivedStateFromSubStates(taskStates);
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
                await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
            }
            SyncActTicketFromReqTask(ActReqTask);
        }

        private async Task UpdateRequestTasksFromTicket(bool createImplTasks = true)
        {
            List<WfReqTask> requestTasks = [.. ActTicket.Tasks];
            foreach (WfReqTask reqtask in requestTasks)
            {
                if (reqtask.StateId <= ActTicket.StateId)
                {
                    List<int> ticketStateList = [ActTicket.StateId];
                    reqtask.StateId = stateMatrixDict.Matrices[reqtask.TaskType].getDerivedStateFromSubStates(ticketStateList);
                    List<WfApproval> approvalsToUpdate = reqtask.Approvals.Where(x => x.StateId <= reqtask.StateId).ToList();
                    foreach (WfApproval approval in approvalsToUpdate)
                    {
                        approval.StateId = reqtask.StateId;
                    }
                    if (dbAcc != null)
                    {
                        await dbAcc.UpdateReqTaskStateInDb(reqtask);
                        foreach (WfApproval approval in approvalsToUpdate)
                        {
                            await dbAcc.UpdateApprovalInDb(approval);
                        }
                    }
                }
                if (createImplTasks && reqtask.ImplementationTasks.Count == 0 && !stateMatrixDict.Matrices[reqtask.TaskType].PhaseActive[WorkflowPhases.planning]
                    && reqtask.StateId >= stateMatrixDict.Matrices[reqtask.TaskType].MinImplTasksNeeded)
                {
                    await AutoCreateImplTasks(reqtask);
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
                await dbAcc.UpdateReqTaskStateInDb(reqTask);
            }
            SyncActTicketFromReqTask(reqTask);
        }

        private async Task UpdateActImplTaskState()
        {
            if (dbAcc != null)
            {
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
                        await dbAcc.UpdateImplTaskStateInDb(impltask);
                    }
                }
            }
        }

        private void SyncReqTaskStopTime()
        {
            bool openImplTask = false;
            foreach (var impltask in ActReqTask.ImplementationTasks)
            {
                if (impltask.Stop == null)
                {
                    openImplTask = true;
                }
            }
            if (!openImplTask && ActReqTask.Stop == null)
            {
                ActReqTask.Stop = ActImplTask.Stop;
            }
        }
    }
}

using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        public bool DisplayImplTaskMode = false;
        public bool EditImplTaskMode = false;
        public bool AddImplTaskMode = false;
        public bool ImplementImplTaskMode = false;
        public bool ReviewImplTaskMode = false;
        public bool DisplayAssignImplTaskMode = false;
        public bool DisplayApprovalImplMode = false;
        public bool DisplayDeleteImplTaskMode = false;
        public bool DisplayCleanupMode = false;
        public bool DisplayImplTaskCommentMode = false;

        // Implementation Tasks

        public void SelectImplTask(WfImplTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            SetImplTaskOpt(action);
        }

        public void SelectImplTaskPopUp(WfImplTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            SetImplTaskPopUpOpt(action);
        }

        public void SetImplTaskEnv(WfImplTask implTask)
        {
            ActImplTask = new WfImplTask(implTask);
            WfTicket? tick = TicketList.FirstOrDefault(x => x.Id == ActImplTask.TicketId);
            if (tick != null)
            {
                ActTicket = tick;
                WfReqTask? reqTask = ActTicket.Tasks.FirstOrDefault(x => x.Id == ActImplTask.ReqTaskId);
                if (reqTask != null)
                {
                    ActReqTask = reqTask;
                }
            }
            ActStateMatrix = stateMatrixDict.Matrices[implTask.TaskType];
        }

        public void SetImplTaskOpt(ObjAction action)
        {
            ResetImplTaskActions();
            DisplayImplTaskMode = action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add || action == ObjAction.implement || action == ObjAction.review;
            EditImplTaskMode = action == ObjAction.edit || action == ObjAction.add;
            AddImplTaskMode = action == ObjAction.add;
            ImplementImplTaskMode = action == ObjAction.implement;
            ReviewImplTaskMode = action == ObjAction.review;
        }

        public void SetImplTaskPopUpOpt(ObjAction action)
        {
            DisplayPromoteImplTaskMode = action == ObjAction.displayPromote;
            DisplayDeleteImplTaskMode = action == ObjAction.displayDelete;
            DisplayCleanupMode = action == ObjAction.displayCleanup;
            DisplayAssignImplTaskMode = action == ObjAction.displayAssign;
            DisplayImplTaskCommentMode = action == ObjAction.displayComment;
            DisplayApprovalImplMode = action == ObjAction.displayApprovals;
        }

        public void ResetImplTaskActions()
        {
            DisplayImplTaskMode = false;
            EditImplTaskMode = false;
            AddImplTaskMode = false;
            ImplementImplTaskMode = false;
            ReviewImplTaskMode = false;

            DisplayPromoteImplTaskMode = false;
            DisplayDeleteImplTaskMode = false;
            DisplayCleanupMode = false;
            DisplayAssignImplTaskMode = false;
            DisplayImplTaskCommentMode = false;
            DisplayApprovalImplMode = false;
        }

        public async Task StartWorkOnImplTask(WfImplTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            ActImplTask.CurrentHandler = userConfig.User;
            List<int> actPossibleStates = ActStateMatrix.getAllowedTransitions(ActImplTask.StateId);
            if (actPossibleStates.Count == 1 && actPossibleStates[0] >= ActStateMatrix.LowestStartedState && actPossibleStates[0] < ActStateMatrix.LowestEndState)
            {
                ActImplTask.StateId = actPossibleStates[0];
            }
            await UpdateActImplTaskState();
            if (!ActStateMatrix.PhaseActive[WorkflowPhases.planning] && ActReqTask.Start == null)
            {
                ActReqTask.Start = ActImplTask.Start;
            }
            await UpdateActTicketStateFromImplTasks();
            SetImplTaskOpt(action);
        }

        public async Task ContinueImplPhase(WfImplTask implTask)
        {
            SelectImplTask(implTask, contOption);
            if (ActImplTask.CurrentHandler != userConfig.User)
            {
                ActImplTask.CurrentHandler = userConfig.User;
                await UpdateActImplTaskState();
            }
        }

        public bool SelectOwnerImplTasks(FwoOwner selectedOwnerOpt)
        {
            try
            {
                AllVisibleImplTasks = [];
                if (selectedOwnerOpt.Id != -3)
                {
                    foreach (var ticket in TicketList)
                    {
                        SelectFromTicketByOwner(ticket, selectedOwnerOpt);
                    }
                }
                return selectedOwnerOpt.Id == -3;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("select_owner"), "", true);
            }
            return true;
        }

        public bool SelectDeviceImplTasks(Device selectedDeviceOpt)
        {
            try
            {
                AllVisibleImplTasks = [];
                if (selectedDeviceOpt.Id != -1)
                {
                    foreach (var ticket in TicketList)
                    {
                        SelectFromTicketByDevice(ticket, selectedDeviceOpt);
                    }
                }
                return selectedDeviceOpt.Id == -1;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("select_device"), "", true);
            }
            return true;
        }

        public async Task AssignImplTaskGroup(WfStatefulObject statefulObject)
        {
            ActImplTask.RecentHandler = ActImplTask.CurrentHandler ?? userConfig.User;
            if (ActionHandler != null)
            {
                await UpdateActImplTaskState();
                await ActionHandler.DoOnAssignmentActions(statefulObject, ActImplTask.AssignedGroup);
            }
            DisplayAssignImplTaskMode = false;
        }

        public async Task AssignImplTaskBack()
        {
            ActImplTask.AssignedGroup = ActImplTask.RecentHandler?.Dn;
            ActImplTask.RecentHandler = ActImplTask.CurrentHandler ?? userConfig.User;
            await UpdateActImplTaskState();
            if (ActionHandler != null)
            {
                await ActionHandler.DoOnAssignmentActions(ActImplTask, ActImplTask.AssignedGroup);
            }
            DisplayAssignImplTaskMode = false;
        }

        public async Task AddImplTask()
        {
            if (dbAcc != null)
            {
                ActImplTask.Id = await dbAcc.AddImplTaskToDb(ActImplTask);
            }
            ActReqTask.ImplementationTasks.Add(ActImplTask);
        }

        public async Task ChangeImplTask()
        {
            if (dbAcc != null)
            {
                await dbAcc.UpdateImplTaskInDb(ActImplTask, ActReqTask);
            }
            ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.TaskNumber == ActImplTask.TaskNumber)] = ActImplTask;
        }

        public async Task ConfAddCommentToImplTask(string commentText)
        {
            WfComment comment = new()
            {
                Scope = WfObjectScopes.ImplementationTask.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            if (dbAcc != null)
            {
                long commentId = await dbAcc.AddCommentToDb(comment);
                if (commentId != 0)
                {
                    await dbAcc.AssignCommentToImplTaskInDb(ActImplTask.Id, commentId);
                }
            }
            ActImplTask.Comments.Add(new WfCommentDataHelper(comment) { });
            DisplayImplTaskCommentMode = false;
        }

        public async Task ConfDeleteImplTask()
        {
            if (dbAcc != null)
            {
                await dbAcc.DeleteImplTaskFromDb(ActImplTask);
            }
            ActReqTask.ImplementationTasks.RemoveAt(ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id));
            DisplayDeleteImplTaskMode = false;
        }

        public async Task ConfCleanupImplTasks()
        {
            if (dbAcc != null)
            {
                foreach (var impltask in ActReqTask.ImplementationTasks)
                {
                    await dbAcc.DeleteImplTaskFromDb(impltask);
                }
            }
            ActReqTask.ImplementationTasks.Clear();
            DisplayCleanupMode = false;
        }

        public async Task CreateAccessImplTasksFromPathAnalysis(WfReqTask reqTask)
        {
            if (apiConnection != null)
            {
                foreach (var device in await PathAnalysis.GetAllDevices(reqTask.Elements, apiConnection))
                {
                    if (reqTask.ImplementationTasks.FirstOrDefault(x => x.DeviceId == device.Id) == null)
                    {
                        await CreateAccessImplTask(reqTask, device.Id);
                    }
                }
            }
        }

        private async Task AutoCreateOrUpdateImplTasks()
        {
            if (Phase <= WorkflowPhases.approval && !MasterStateMatrix.PhaseActive[WorkflowPhases.planning]
                && ActTicket.StateId >= MasterStateMatrix.LowestEndState)
            {
                foreach (var reqTask in ActTicket.Tasks)
                {
                    // Todo: further analysis how many impl tasks currently have to be there and create or update where needed
                    if (reqTask.ImplementationTasks.Count == 0
                        && reqTask.StateId >= stateMatrixDict.Matrices[reqTask.TaskType].MinImplTasksNeeded)
                    {
                        await AutoCreateImplTasks(reqTask);
                    }
                    else
                    {
                        await UpgradeImplTaskStatesToReqTask(reqTask);
                    }
                }
            }
        }

        private async Task AutoCreateImplTasks(WfReqTask reqTask)
        {
            if (reqTask.TaskType == WfTaskType.access.ToString())
            {
                await AutoCreateAccessImplTasks(reqTask);
                return;
            }

            await CreateGenericImplTask(reqTask);
        }

        private async Task AutoCreateAccessImplTasks(WfReqTask reqTask)
        {
            switch (userConfig.ReqAutoCreateImplTasks)
            {
                case AutoCreateImplTaskOptions.never:
                    return;
                case AutoCreateImplTaskOptions.onlyForOneDevice:
                    await CreateImplTaskForFirstDevice(reqTask);
                    return;
                case AutoCreateImplTaskOptions.forEachDevice:
                    await CreateImplTasksForDevices(reqTask, [.. Devices.Select(device => device.Id)]);
                    return;
                case AutoCreateImplTaskOptions.enterInReqTask:
                    await CreateImplTasksForDevices(reqTask, reqTask.GetResolvedDeviceList(Devices));
                    return;
                case AutoCreateImplTaskOptions.oneTaskForAllDevices:
                    await CreateImplTasksForCombinedOrSelectedDevices(reqTask);
                    return;
                case AutoCreateImplTaskOptions.afterPathAnalysis:
                    await CreateAccessImplTasksFromPathAnalysis(reqTask);
                    return;
                default:
                    return;
            }
        }

        private async Task CreateImplTaskForFirstDevice(WfReqTask reqTask)
        {
            if (Devices.Count > 0)
            {
                await CreateAccessImplTask(reqTask, Devices[0].Id, false);
            }
        }

        private async Task CreateImplTasksForCombinedOrSelectedDevices(WfReqTask reqTask)
        {
            List<int> deviceIds = reqTask.GetDeviceList();
            if (deviceIds.Count == 0)
            {
                return;
            }

            if (reqTask.HasAllDevicesSelected())
            {
                await CreateAccessImplTask(reqTask, null, false);
                return;
            }

            await CreateImplTasksForDevices(reqTask, deviceIds);
        }

        private async Task CreateImplTasksForDevices(WfReqTask reqTask, List<int> deviceIds)
        {
            foreach (int deviceId in deviceIds)
            {
                await CreateAccessImplTask(reqTask, deviceId);
            }
        }

        private async Task CreateGenericImplTask(WfReqTask reqTask)
        {
            WfImplTask newImplTask = new(reqTask) { TaskNumber = reqTask.HighestImplTaskNumber() + 1, StateId = reqTask.StateId };
            if (dbAcc != null)
            {
                newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
            }
            reqTask.ImplementationTasks.Add(newImplTask);
        }

        private async Task CreateAccessImplTask(WfReqTask reqTask, int? deviceId, bool adaptTitle = true)
        {
            WfImplTask newImplTask;
            newImplTask = new WfImplTask(reqTask)
            { TaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = deviceId, StateId = reqTask.StateId };
            if (adaptTitle && deviceId != null)
            {
                Device? device = Devices.FirstOrDefault(x => x.Id == deviceId);
                if (device != null)
                {
                    newImplTask.Title += ": " + device.Name;
                }
            }
            if (dbAcc != null)
            {
                newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
            }
            reqTask.ImplementationTasks.Add(newImplTask);
        }

        private void SelectFromTicketByDevice(WfTicket ticket, Device selectedDeviceOpt)
        {
            foreach (var reqTask in ticket.Tasks)
            {
                foreach (var implTask in reqTask.ImplementationTasks)
                {
                    if (selectedDeviceOpt.Id == 0 || implTask.DeviceId == selectedDeviceOpt.Id)
                    {
                        implTask.TicketId = ticket.Id;
                        implTask.ReqTaskId = reqTask.Id;
                        AllVisibleImplTasks.Add(implTask);
                    }
                }
            }
        }

        private void SelectFromTicketByOwner(WfTicket ticket, FwoOwner selectedOwnerOpt)
        {
            foreach (var reqTask in ticket.Tasks)
            {
                foreach (var implTask in reqTask.ImplementationTasks)
                {
                    bool assignedToMe = implTask.CurrentHandler?.DbId == userConfig.User.DbId || implTask.AssignedGroup == userConfig.User.Dn;  // todo: resolve group membership?
                    if (selectedOwnerOpt.Id == -1 || (selectedOwnerOpt.Id == -2 && assignedToMe)
                        || (selectedOwnerOpt.Id > 0 && reqTask.Owners.FirstOrDefault(o => o.Owner.Id == selectedOwnerOpt.Id) != null))
                    {
                        implTask.TicketId = ticket.Id;
                        implTask.ReqTaskId = reqTask.Id;
                        AllVisibleImplTasks.Add(implTask);
                    }
                }
            }
        }

        private void ResetImplTaskList()
        {
            AllTicketImplTasks = [];
            foreach (var reqTask in ActTicket.Tasks)
            {
                foreach (var implTask in reqTask.ImplementationTasks)
                {
                    implTask.TicketId = ActTicket.Id;
                    implTask.ReqTaskId = reqTask.Id;
                    AllTicketImplTasks.Add(implTask);
                }
            }
        }
    }
}

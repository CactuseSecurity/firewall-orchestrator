using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;

namespace FWO.Ui.Services
{
    public enum ObjAction
    {
        display,
        edit,
        add,
        approve,
        plan,
        implement,
        review,
        displayAssign,
        displayApprovals,
        displayApprove,
        displayPromote,
        displayDelete,
        displaySaveTicket
    }

    public class RequestHandler
    {
        public List<RequestTicket> TicketList { get; set; } = new List<RequestTicket>();
        public RequestTicket ActTicket { get; set; } = new RequestTicket();
        public RequestTask ActReqTask { get; set; } = new RequestTask();
        public ImplementationTask ActImplTask { get; set; } = new ImplementationTask();
        public RequestApproval ActApproval { get; set; } = new RequestApproval();

        public WorkflowPhases Phase = WorkflowPhases.request;
        public List<Device> Devices = new List<Device>();
        public List<ImplementationTask> AllImplTasks = new List<ImplementationTask>();
        public StateMatrix ActStateMatrix = new StateMatrix();
        public StateMatrix MasterStateMatrix = new StateMatrix();
        public ActionHandler ActionHandler;
        public bool ReadOnlyMode = false;

        public bool DisplayTicketMode = false;
        public bool EditTicketMode = false;
        public bool AddTicketMode = false;

        public bool DisplayReqTaskMode = false;
        public bool EditReqTaskMode = false;
        public bool AddReqTaskMode = false;
        public bool PlanReqTaskMode = false;
        public bool ApproveReqTaskMode = false;

        public bool DisplayImplTaskMode = false;
        public bool EditImplTaskMode = false;
        public bool AddImplTaskMode = false;
        public bool ImplementImplTaskMode = false;

        public bool DisplayAssignMode = false;
        public bool DisplayApprovalMode = false;
        public bool DisplayApproveMode = false;
        public bool DisplayPromoteMode = false;
        public bool DisplaySaveTicketMode = false;
        public bool DisplayDeleteMode = false;

        private Action<Exception?, string, string, bool>? DisplayMessageInUi { get; set; }
        private UserConfig userConfig;
        private ApiConnection apiConnection;
        private StateMatrixDict stateMatrixDict = new StateMatrixDict();
        private RequestDbAccess dbAcc;
    

        private ObjAction contOption = ObjAction.display;


        public RequestHandler(Action<Exception?, string, string, bool> displayMessageInUi, UserConfig userConfig, 
            ApiConnection apiConnection, WorkflowPhases phase)
        {
            this.DisplayMessageInUi = displayMessageInUi;
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
            this.Phase = phase;
        }

        public async Task Init(int viewOpt = 0)
        {
            ActionHandler = new ActionHandler(apiConnection, this);
            await ActionHandler.Init();
            dbAcc = new RequestDbAccess(DisplayMessageInUi, userConfig, apiConnection, ActionHandler){};
            Devices = await apiConnection.SendQueryAsync<List<Device>>(FWO.Api.Client.Queries.DeviceQueries.getDeviceDetails);
            await stateMatrixDict.Init(Phase, apiConnection);
            MasterStateMatrix = stateMatrixDict.Matrices[TaskType.master.ToString()];
            TicketList = await dbAcc.FetchTickets(MasterStateMatrix, viewOpt);
        }

        public StateMatrix StateMatrix(string taskType)
        {
            return stateMatrixDict.Matrices[taskType];
        }

        public async Task AutoPromote(StatefulObject statefulObject, ActionScopes scope, int? toStateId)
        {
            bool promotePossible = false;
            if(toStateId != null)
            {
                statefulObject.StateId = (int)toStateId;
                promotePossible = true;
            }
            else
            {
                List<int> possibleStates = ActStateMatrix.getAllowedTransitions(statefulObject.StateId);
                if(possibleStates.Count == 1)
                {
                    statefulObject.StateId = possibleStates[0];
                    promotePossible = true;
                }
            }
            
            if(promotePossible)
            {
                switch(scope)
                {
                    case ActionScopes.Ticket:
                        SetTicketEnv((RequestTicket)statefulObject);
                        await PromoteTicket(statefulObject);
                        break;
                    case ActionScopes.RequestTask:
                        SetReqTaskEnv((RequestTask)statefulObject);
                        await PromoteReqTask(statefulObject);
                        break;
                    case ActionScopes.ImplementationTask:
                        SetImplTaskEnv((ImplementationTask)statefulObject);
                        await PromoteImplTask(statefulObject);
                        break;
                    case ActionScopes.Approval:
                        await SetApprovalEnv();
                        await ApproveTask(statefulObject);
                        break;
                    default:
                        break;
                }
            }
        }

        public void SetContinueEnv(ObjAction action)
        {
            contOption = action;
        }

        public void ContinuePhase(RequestTask reqTask)
        {
            SelectReqTask(reqTask, contOption);
        }

        private void ContinueImplPhase(ImplementationTask implTask)
        {
            SelectImplTask(implTask, contOption);
        }


        // Tickets

        public void SelectTicket(RequestTicket ticket, ObjAction action)
        {
            SetTicketEnv(ticket);
            SetTicketOpt(action);
        }

        public void SetTicketEnv(RequestTicket ticket)
        {
            ActTicket = ticket;
            AllImplTasks = new List<ImplementationTask>();
            foreach(var reqTask in ActTicket.Tasks)
            {
                foreach(var implTask in reqTask.ImplementationTasks)
                {
                    implTask.TicketId = ActTicket.Id;
                    implTask.ReqTaskId = reqTask.Id;
                    AllImplTasks.Add(implTask);
                }
            }
            ActStateMatrix = MasterStateMatrix;
        }

        public void SetTicketOpt(ObjAction action)
        {
            ResetTicketActions();
            DisplayTicketMode = (action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add);
            EditTicketMode = (action == ObjAction.edit || action == ObjAction.add);
            AddTicketMode = action == ObjAction.add;
        }

        public void SetTicketPopUpOpt(ObjAction action)
        {
            DisplayPromoteMode = action == ObjAction.displayPromote;
            DisplaySaveTicketMode = action == ObjAction.displaySaveTicket;
        }

        public void ResetTicketActions()
        {
            DisplayTicketMode = false;
            EditTicketMode = false;
            AddTicketMode = false;
            DisplayPromoteMode = false;
            DisplaySaveTicketMode = false;
        }

        public async Task SaveTicket(StatefulObject ticket)
        {
            try
            {
                ActTicket.StateId = ticket.StateId;
                if (ActTicket.Sanitize())
                {
                    DisplayMessageInUi!(null, userConfig.GetText("save_request"), userConfig.GetText("U0001"), true);
                }
                if (CheckTicketValues())
                {
                    foreach(RequestTask reqTask in ActTicket.Tasks)
                    {
                        reqTask.StateId = ActTicket.StateId;
                    }

                    if (AddTicketMode)
                    {                  
                        // insert new ticket
                        ActTicket.CreationDate = DateTime.Now;
                        ActTicket.Requester = userConfig.User;
                        TicketList = await dbAcc.AddTicketToDb(ActTicket, TicketList);
                    }
                    else
                    {
                        // Update existing ticket
                        TicketList = await dbAcc.UpdateTicketInDb(ActTicket, TicketList);
                    }
                    ResetTicketActions();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, userConfig.GetText("save_request"), "", true);
            }
        }

        public async Task PromoteTicket(StatefulObject ticket)
        {
            try
            {
                ActTicket.StateId = ticket.StateId;
                await UpdateTicketState();
                ResetTicketActions();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, userConfig.GetText("promote_ticket"), "", true);
            }
        }


        // Request Tasks
        
        public void SelectReqTask (RequestTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskOpt(action);
        }

        public void SelectReqTaskPopUp (RequestTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskPopUpOpt(action);
        }

        public void SetReqTaskEnv (RequestTask reqTask)
        {
            ActReqTask = reqTask;
            RequestTicket? tick = TicketList.FirstOrDefault(x => x.Id == ActReqTask.TicketId);
            if(tick != null)
            {
                ActTicket = tick;
            }
            ActStateMatrix = stateMatrixDict.Matrices[reqTask.TaskType];
        }

        public void SetReqTaskOpt(ObjAction action)
        {
            ResetReqTaskActions();
            DisplayReqTaskMode = (action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add || action == ObjAction.approve || action == ObjAction.plan);
            EditReqTaskMode = (action == ObjAction.edit || action == ObjAction.add);
            AddReqTaskMode = action == ObjAction.add;
            PlanReqTaskMode = action == ObjAction.plan;
            ApproveReqTaskMode = action == ObjAction.approve;
        }

        public void SetReqTaskPopUpOpt(ObjAction action)
        {
            DisplayAssignMode = action == ObjAction.displayAssign;
            DisplayApprovalMode = action == ObjAction.displayApprovals;
            DisplayApproveMode = action == ObjAction.displayApprove;
            DisplayPromoteMode = action == ObjAction.displayPromote;
            DisplayDeleteMode = action == ObjAction.displayDelete;
        }

        public void ResetReqTaskActions()
        {
            DisplayReqTaskMode = false;
            EditReqTaskMode = false;
            AddReqTaskMode = false;
            PlanReqTaskMode = false;
            ApproveReqTaskMode = false;

            DisplayAssignMode = false;
            DisplayApprovalMode = false;
            DisplayApproveMode = false;
            DisplayPromoteMode = false;
            DisplayDeleteMode = false;
        }

        public async Task StartWorkOnReqTask(RequestTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            reqTask.CurrentHandler = userConfig.User;
            List<int> actPossibleStates = ActStateMatrix.getAllowedTransitions(reqTask.StateId);
            if(actPossibleStates.Count == 1 && actPossibleStates[0] >= ActStateMatrix.LowestStartedState && actPossibleStates[0] < ActStateMatrix.LowestEndState)
            {
                reqTask.StateId = actPossibleStates[0];
            }
            await dbAcc.UpdateReqTaskStateInDb(reqTask);
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
            await UpdateTicketStateFromTasks();
            SetReqTaskOpt(action);
        }

        public async Task AssignGroup()
        {
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler;
            if(CheckAssignValues())
            {
                await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
            }
            DisplayAssignMode = false;
        }

        public async Task AssignBack()
        {
            ActReqTask.AssignedGroup = ActReqTask.RecentHandler?.Dn;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler;
            await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
            DisplayAssignMode = false;
        }

        public async Task AddReqTask()
        {
            if (ActTicket.Id > 0) // ticket already created -> write directly to db
            {
                ActReqTask.TicketId = ActTicket.Id;
                ActReqTask.Id = await dbAcc.AddReqTaskToDb(ActReqTask);
            }
            ActTicket.Tasks.Add(ActReqTask);
        }

        public async Task ChangeReqTask()
        {
            if(ActReqTask.Id > 0)
            {
                await dbAcc.UpdateReqTaskInDb(ActReqTask);
            }
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
        }

        public async Task ConfDeleteReqTask()
        {
            if(ActReqTask.Id > 0)
            {
                await dbAcc.DeleteReqTaskFromDb(ActReqTask);
            }

            ActTicket.Tasks.Remove(ActReqTask);
            // todo: adapt TaskNumbers of following tasks?
            DisplayDeleteMode = false;
        }

        public async Task PromoteReqTask(StatefulObject reqTask)
        {
            try
            {
                ActReqTask.StateId = reqTask.StateId;
                if (ActReqTask.Start == null && ActReqTask.StateId >= ActStateMatrix.LowestStartedState)
                {
                    ActReqTask.Start = DateTime.Now;
                    ActReqTask.CurrentHandler = userConfig.User;
                }
                if (Phase == WorkflowPhases.planning && ActReqTask.Stop == null && ActReqTask.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActReqTask.Stop = DateTime.Now;
                }
                await dbAcc.UpdateReqTaskStateInDb(ActReqTask);

                if(Phase == WorkflowPhases.planning)
                {
                    foreach(ImplementationTask implTask in ActReqTask.ImplementationTasks)
                    {
                        implTask.StateId = ActReqTask.StateId;
                        await dbAcc.UpdateImplTaskStateInDb(implTask);
                    }
                }
                
                ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
                await UpdateTicketStateFromTasks();
                DisplayPromoteMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, userConfig.GetText("promote_task"), "", true);
            }
        } 

        public async Task SetApprovalEnv()
        {
            if(ActReqTask.Approvals.Count == 0)
            {
                await AddApproval();
            }
            ActApproval = ActReqTask.Approvals.Last();  // todo: select own approvals
            ActApproval.SetOptComment(ActApproval.Comment);
        }

        public async Task AddApproval(string extParams = "")
        {
            ApprovalParams approvalParams = new ApprovalParams();
            if(extParams != "")
            {
                approvalParams = System.Text.Json.JsonSerializer.Deserialize<ApprovalParams>(extParams) ?? throw new Exception("Extparams could not be parsed.");
            }

            RequestApproval approval = new RequestApproval()
            {
                TaskId = ActReqTask.Id,
                StateId = (extParams != "" ? approvalParams.StateId : ActStateMatrix.LowestEndState),
                ApproverGroup = (extParams != "" ? approvalParams.ApproverGroup : ""), // todo: get from owner ???,
                TenantId = ActTicket.TenantId, // ??
                Deadline = (extParams != "" ? (approvalParams.Deadline > 0 ? DateTime.Now.AddDays(approvalParams.Deadline) : null) 
                            : (userConfig.ReqApprovalDeadline > 0 ? DateTime.Now.AddDays(userConfig.ReqApprovalDeadline) : null)),
                InitialApproval = ActReqTask.Approvals.Count == 0
            };
            ActReqTask.Approvals.Add(approval);
            if(!approval.InitialApproval)
            {
                // todo: checks if new approval allowed (only one open per group?, ...)
                await dbAcc.AddApprovalToDb(approval);
                DisplayMessageInUi!(null, userConfig.GetText("add_approval"), userConfig.GetText("U8002"), false);
            }
        }

        public async Task ApproveTask(StatefulObject approval)
        {
            try
            {
                ActApproval.StateId = approval.StateId;
                ActApproval.Comment = approval.OptComment();
                if(ActApproval.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActApproval.ApprovalDate = DateTime.Now;
                    ActApproval.ApproverDn = userConfig.User.Dn;
                }
                if (ActApproval.Sanitize())
                {
                    DisplayMessageInUi!(null, userConfig.GetText("save_approval"), userConfig.GetText("U0001"), true);
                }
                await dbAcc.UpdateApprovalInDb(ActApproval);
                ActReqTask.Approvals[ActReqTask.Approvals.FindIndex(x => x.Id == ActApproval.Id)] = ActApproval;
                await UpdateTaskStateFromApprovals();
                ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
                await UpdateTicketStateFromTasks();
                ApproveReqTaskMode = false;
                DisplayApproveMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, userConfig.GetText("save_approval"), "", true);
            }
        }


        // Implementation Tasks

        public void SelectImplTask(ImplementationTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            SetImplTaskOpt(action);
        }

        public void SelectReqImplPopUp (ImplementationTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            SetImplTaskPopUpOpt(action);
        }

        public void SetImplTaskEnv(ImplementationTask implTask)
        {
            ActImplTask = implTask;
            RequestTicket? tick = TicketList.FirstOrDefault(x => x.Id == ActImplTask.TicketId);
            if(tick != null)
            {
                ActTicket = tick;
                RequestTask? reqTask = ActTicket.Tasks.FirstOrDefault(x => x.Id == ActImplTask.ReqTaskId);
                if(reqTask != null)
                {
                    ActReqTask = reqTask;
                }
            }
            ActStateMatrix = stateMatrixDict.Matrices[implTask.TaskType];
        }

        public void SetImplTaskOpt(ObjAction action)
        {
            ResetImplTaskActions();
            DisplayImplTaskMode = (action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add || action == ObjAction.implement);
            EditImplTaskMode = (action == ObjAction.edit || action == ObjAction.add);
            AddImplTaskMode = action == ObjAction.add;
            ImplementImplTaskMode = action == ObjAction.implement;
        }
        
        public void SetImplTaskPopUpOpt(ObjAction action)
        {
            DisplayPromoteMode = action == ObjAction.displayPromote;
            DisplayDeleteMode = action == ObjAction.displayDelete;
        }

        public void ResetImplTaskActions()
        {
            DisplayImplTaskMode = false;
            EditImplTaskMode = false;
            AddImplTaskMode = false;
            ImplementImplTaskMode = false;

            DisplayPromoteMode = false;
            DisplayDeleteMode = false;
        }

        public async Task StartWorkOnImplTask(ImplementationTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            implTask.CurrentHandler = userConfig.User;
            List<int> actPossibleStates = ActStateMatrix.getAllowedTransitions(implTask.StateId);
            if(actPossibleStates.Count == 1 && actPossibleStates[0] >= ActStateMatrix.LowestStartedState && actPossibleStates[0] < ActStateMatrix.LowestEndState)
            {
                implTask.StateId = actPossibleStates[0];
            }
            await dbAcc.UpdateImplTaskStateInDb(implTask);
            ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id)] = ActImplTask;
            if(!ActStateMatrix.PhaseActive[WorkflowPhases.planning] && ActReqTask.Start == null)
            {
                ActReqTask.Start = ActImplTask.Start;
            }
            await UpdateTicketStateFromImplTasks();
            SetImplTaskOpt(action);
        }

        public async Task AddImplTask()
        {
            ActImplTask.Id = await dbAcc.AddImplTaskToDb(ActImplTask);
            ActReqTask.ImplementationTasks.Add(ActImplTask);
        }

        public async Task ChangeImplTask()
        {
            await dbAcc.UpdateImplTaskInDb(ActImplTask);
            ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.ImplTaskNumber == ActImplTask.ImplTaskNumber)] = ActImplTask;
        }

        public async Task PromoteImplTask(StatefulObject implTask)
        {
            try
            {
                SetImplTaskEnv(ActImplTask);
                ActImplTask.StateId = implTask.StateId;
                ActImplTask.CurrentHandler = userConfig.User;
                if (Phase == WorkflowPhases.implementation && ActImplTask.Stop == null && ActImplTask.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActImplTask.Stop = DateTime.Now;
                }
                await dbAcc.UpdateImplTaskStateInDb(ActImplTask);
                ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id)] = ActImplTask;
                if(!ActStateMatrix.PhaseActive[WorkflowPhases.planning] && ActReqTask.Stop == null)
                {
                    ActReqTask.Stop = ActImplTask.Stop;
                }
                await UpdateReqTaskStateFromImplTasks(ActReqTask);
                ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
                await UpdateTicketStateFromTasks();
                DisplayPromoteMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, userConfig.GetText("save_task"), "", true);
            }
        }

        public async Task ConfDeleteImplTask()
        {
            await dbAcc.DeleteImplTaskFromDb(ActImplTask);
            ActReqTask.ImplementationTasks.Remove(ActImplTask);
            DisplayDeleteMode = false;
        }

        private async Task AutoCreateImplTasks()
        {
            foreach(var reqTask in ActTicket.Tasks)
            {
                ImplementationTask newImplTask;
                switch (userConfig.ReqAutoCreateImplTasks)
                {
                    case AutoCreateImplTaskOptions.never:
                        break;
                    case AutoCreateImplTaskOptions.onlyForOneDevice:
                        if(Devices.Count == 1)
                        {
                            newImplTask = new ImplementationTask(reqTask)
                                { ImplTaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = Devices[0].Id, StateId = reqTask.StateId };
                            newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                            reqTask.ImplementationTasks.Add(newImplTask);
                        }
                        break;
                    case AutoCreateImplTaskOptions.forEachDevice:
                        foreach(var device in Devices)
                        {
                            newImplTask = new ImplementationTask(reqTask)
                                { ImplTaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = device.Id, StateId = reqTask.StateId };
                            newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                            reqTask.ImplementationTasks.Add(newImplTask);
                        }
                        break;
                    case AutoCreateImplTaskOptions.enterInReqTask:
                        newImplTask = new ImplementationTask(reqTask)
                            { ImplTaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = reqTask.DeviceId, StateId = reqTask.StateId };
                        newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                        reqTask.ImplementationTasks.Add(newImplTask);
                        break;
                    default:
                        break;
                }
            }
        }


        // State changes

        public async Task UpdateTaskStateFromApprovals()
        {
            if (ActReqTask.Approvals.Count > 0)
            {
                List<int> approvalStates = new List<int>();
                foreach (var approval in ActReqTask.Approvals)
                {
                    approvalStates.Add(approval.StateId);
                }
                ActReqTask.StateId = ActStateMatrix.getDerivedStateFromSubStates(approvalStates);
            }
            await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
        }

        public async Task UpdateReqTaskStateFromImplTasks(RequestTask reqTask)
        {
            if (reqTask.ImplementationTasks.Count > 0)
            {
                List<int> implTaskStates = new List<int>();
                foreach (var implTask in reqTask.ImplementationTasks)
                {
                    implTaskStates.Add(implTask.StateId);
                }
                reqTask.StateId = ActStateMatrix.getDerivedStateFromSubStates(implTaskStates);
            }
            await dbAcc.UpdateReqTaskStateInDb(reqTask);
        }

        public async Task UpdateTicketStateFromImplTasks()
        {
            List<int> taskStates = new List<int>();
            foreach (RequestTask reqTask in ActTicket.Tasks)
            {
                await UpdateReqTaskStateFromImplTasks(reqTask);
            }
            await UpdateTicketStateFromTasks();
        }

        public async Task UpdateTicketStateFromTasks()
        {
            if (ActTicket.Tasks.Count > 0)
            {
                List<int> taskStates = new List<int>();
                foreach (RequestTask tsk in ActTicket.Tasks)
                {
                    taskStates.Add(tsk.StateId);
                }
                ActTicket.StateId = MasterStateMatrix.getDerivedStateFromSubStates(taskStates);
            }
            await UpdateTicketState();
        }

        public async Task UpdateTicketState()
        {
            if (MasterStateMatrix.IsLastActivePhase && ActTicket.StateId >= MasterStateMatrix.LowestEndState)
            {
                ActTicket.CompletionDate = DateTime.Now;
            }
            bool alreadyExistingImplTask = false;
            foreach (var reqTask in ActTicket.Tasks)
            {
                if (reqTask.ImplementationTasks.Count > 0)
                {
                    alreadyExistingImplTask = true;
                }
            }
            if(Phase <= WorkflowPhases.approval && ActTicket.Tasks.Count > 0 && !alreadyExistingImplTask &&
                !MasterStateMatrix.PhaseActive[WorkflowPhases.planning] && ActTicket.StateId >= MasterStateMatrix.LowestEndState)
            {
                await AutoCreateImplTasks();
            }

            await dbAcc.UpdateTicketStateInDb(ActTicket);
            TicketList[TicketList.FindIndex(x => x.Id == ActTicket.Id)] = ActTicket;
        }


        // checks
        private bool CheckTicketValues()
        {
            if (ActTicket.Title == null || ActTicket.Title == "")
            {
                DisplayMessageInUi!(null, userConfig.GetText("save_request"), userConfig.GetText("E5102"), true);
                return false;
            }
            return true;
        }

        private bool CheckAssignValues()
        {
            if (ActReqTask.AssignedGroup == null || ActReqTask.AssignedGroup == "")
            {
                DisplayMessageInUi!(null, userConfig.GetText("assign_group"), userConfig.GetText("E8010"), true);
                return false;
            }
            return true;
        }
    }
}

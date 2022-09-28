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
        displaySaveTicket,
        displayComment
    }

    public class RequestHandler
    {
        public List<RequestTicket> TicketList { get; set; } = new List<RequestTicket>();
        public RequestTicket ActTicket { get; set; } = new RequestTicket();
        public RequestReqTask ActReqTask { get; set; } = new RequestReqTask();
        public RequestImplTask ActImplTask { get; set; } = new RequestImplTask();
        public RequestApproval ActApproval { get; set; } = new RequestApproval();

        public WorkflowPhases Phase = WorkflowPhases.request;
        public List<Device> Devices = new List<Device>();
        public List<RequestPriority> PrioList = new List<RequestPriority>();
        public List<RequestImplTask> AllImplTasks = new List<RequestImplTask>();
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
        public bool DisplayAssignApprovalMode = false;
        public bool DisplayPromoteMode = false;
        public bool DisplaySaveTicketMode = false;
        public bool DisplayDeleteMode = false;
        public bool DisplayCommentMode = false;
        

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
            PrioList = System.Text.Json.JsonSerializer.Deserialize<List<RequestPriority>>(userConfig.ReqPriorities) ?? throw new Exception("Config data could not be parsed.");
        }

        public StateMatrix StateMatrix(string taskType)
        {
            return stateMatrixDict.Matrices[taskType];
        }

        public async Task AutoPromote(RequestStatefulObject statefulObject, RequestObjectScopes scope, int? toStateId)
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
                    case RequestObjectScopes.Ticket:
                        SetTicketEnv((RequestTicket)statefulObject);
                        await PromoteTicket(statefulObject);
                        break;
                    case RequestObjectScopes.RequestTask:
                        SetReqTaskEnv((RequestReqTask)statefulObject);
                        ActReqTask.StateId = statefulObject.StateId;
                        await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
                        break;
                    case RequestObjectScopes.ImplementationTask:
                        SetImplTaskEnv((RequestImplTask)statefulObject);
                        ActImplTask.StateId = statefulObject.StateId;
                        await dbAcc.UpdateImplTaskStateInDb(ActImplTask);
                        break;
                    case RequestObjectScopes.Approval:
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


        // Tickets

        public void SelectTicket(RequestTicket ticket, ObjAction action)
        {
            SetTicketEnv(ticket);
            SetTicketOpt(action);
        }

        public void SetTicketEnv(RequestTicket ticket)
        {
            ActTicket = ticket;
            AllImplTasks = new List<RequestImplTask>();
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

        public async Task SaveTicket(RequestStatefulObject ticket)
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
                    foreach(RequestReqTask reqTask in ActTicket.Tasks)
                    {
                        reqTask.StateId = ActTicket.StateId;
                    }

                    if(ActTicket.Deadline == null)
                    {
                        int? tickDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.TicketDeadline;
                        ActTicket.Deadline = (tickDeadline != null && tickDeadline > 0 ? DateTime.Now.AddDays((int)tickDeadline) : null);
                    }

                    if (AddTicketMode)
                    {                  
                        // insert new ticket
                        ActTicket.CreationDate = DateTime.Now;
                        ActTicket.Requester = userConfig.User;
                        ActTicket = await dbAcc.AddTicketToDb(ActTicket);
                        TicketList.Add(ActTicket);
                    }
                    else
                    {
                        // Update existing ticket
                        ActTicket = await dbAcc.UpdateTicketInDb(ActTicket);
                        TicketList[TicketList.FindIndex(x => x.Id == ActTicket.Id)] = ActTicket;
                    }

                    // update of request tasks and creation of impl tasks may be necessary
                    foreach(RequestReqTask reqtask in ActTicket.Tasks)
                    {
                        List<int> ticketStateList = new List<int>();
                        ticketStateList.Add(ActTicket.StateId);
                        reqtask.StateId = stateMatrixDict.Matrices[reqtask.TaskType].getDerivedStateFromSubStates(ticketStateList);
                        await dbAcc.UpdateReqTaskStateInDb(reqtask);
                        if( reqtask.ImplementationTasks.Count == 0 && !stateMatrixDict.Matrices[reqtask.TaskType].PhaseActive[WorkflowPhases.planning] 
                            && reqtask.StateId >= stateMatrixDict.Matrices[reqtask.TaskType].MinImplTasksNeeded)
                        {
                            await AutoCreateImplTasks(reqtask);
                        }
                    }

                    //check for further promotion (req tasks may be promoted)
                    await UpdateTicketStateFromTasks();

                    ResetTicketActions();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi!(exception, userConfig.GetText("save_request"), "", true);
            }
        }

        public async Task PromoteTicket(RequestStatefulObject ticket)
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
        
        public void SelectReqTask (RequestReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskMode(action);
        }

        public void SelectReqTaskPopUp (RequestReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskPopUpOpt(action);
        }

        public void SetReqTaskEnv (RequestReqTask reqTask)
        {
            ActReqTask = reqTask;
            RequestTicket? tick = TicketList.FirstOrDefault(x => x.Id == ActReqTask.TicketId);
            if(tick != null)
            {
                ActTicket = tick;
            }
            ActStateMatrix = stateMatrixDict.Matrices[reqTask.TaskType];
        }

        public void SetReqTaskMode(ObjAction action)
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
            DisplayCommentMode = action == ObjAction.displayComment;
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
            DisplayCommentMode = false;
        }

        public async Task StartWorkOnReqTask(RequestReqTask reqTask, ObjAction action)
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
            SetReqTaskMode(action);
        }

        public async Task ContinuePhase(RequestReqTask reqTask)
        {
            SelectReqTask(reqTask, contOption);
            if(reqTask.CurrentHandler != userConfig.User)
            {
                reqTask.CurrentHandler = userConfig.User;
                await dbAcc.UpdateReqTaskStateInDb(reqTask);
                ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
            }
        }

        public async Task AssignReqTaskGroup(RequestStatefulObject statefulObject)
        {
            ActReqTask.AssignedGroup = statefulObject.AssignedGroup;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler;
            if(CheckAssignValues(ActReqTask))
            {
                await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
            }
            DisplayAssignMode = false;
        }

        public async Task AssignReqTaskBack()
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

        public async Task ConfAddCommentToReqTask(string commentText)
        {
            RequestComment comment = new RequestComment()
            {
                Scope = RequestObjectScopes.RequestTask.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            long commentId = await dbAcc.AddCommentToDb(comment);
            if(commentId != 0)
            {
                await dbAcc.AssignCommentToReqTaskInDb(ActReqTask.Id, commentId);
            }
            ActReqTask.Comments.Add(new RequestCommentDataHelper(comment){});
            DisplayCommentMode = false;
        }

        public async Task PromoteReqTask(RequestStatefulObject reqTask)
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
                    foreach(RequestImplTask implTask in ActReqTask.ImplementationTasks)
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


        // approvals

        public async Task SetApprovalEnv(RequestApproval? approval = null)
        {
            if(approval != null)
            {
                ActApproval = approval;
            }
            else
            {
                if(ActReqTask.Approvals.Count == 0)
                {
                    await AddApproval();
                }
                ActApproval = ActReqTask.Approvals.FirstOrDefault(x => x.StateId < ActStateMatrix.LowestEndState) ?? (ActApproval = ActReqTask.Approvals.Last());  // todo: select own approvals
            }
        }

        public void SetApprovalPopUpOpt(ObjAction action)
        {
            DisplayAssignApprovalMode = action == ObjAction.displayAssign;
            DisplayCommentMode = action == ObjAction.displayComment;
        }

        public async Task SelectApprovalPopUp (RequestApproval approval, ObjAction action)
        {
            await SetApprovalEnv(approval);
            SetApprovalPopUpOpt(action);
        }

        public async Task AddApproval(string extParams = "")
        {
            ApprovalParams approvalParams = new ApprovalParams();
            if(extParams != "")
            {
                approvalParams = System.Text.Json.JsonSerializer.Deserialize<ApprovalParams>(extParams) ?? throw new Exception("Extparams could not be parsed.");
            }

            DateTime? deadline = null;
            if(extParams != "")
            {
                deadline = (approvalParams.Deadline > 0 ? DateTime.Now.AddDays(approvalParams.Deadline) : null);
            }
            else
            {
                int? appDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.ApprovalDeadline;
                deadline = (appDeadline != null && appDeadline > 0 ? DateTime.Now.AddDays((int)appDeadline) : null);
            }

            RequestApproval approval = new RequestApproval()
            {
                TaskId = ActReqTask.Id,
                StateId = (extParams != "" ? approvalParams.StateId : ActStateMatrix.LowestEndState),
                ApproverGroup = (extParams != "" ? approvalParams.ApproverGroup : ""), // todo: get from owner ???,
                TenantId = ActTicket.TenantId, // ??
                Deadline = deadline,
                InitialApproval = ActReqTask.Approvals.Count == 0
            };
            if(!approval.InitialApproval)
            {
                // todo: checks if new approval allowed (only one open per group?, ...)
                approval.Id = await dbAcc.AddApprovalToDb(approval);
                DisplayMessageInUi!(null, userConfig.GetText("add_approval"), userConfig.GetText("U8002"), false);
            }
            ActReqTask.Approvals.Add(approval);
        }

        public async Task ApproveTask(RequestStatefulObject approval)
        {
            try
            {
                ActApproval.StateId = approval.StateId;
                if(ActApproval.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActApproval.ApprovalDate = DateTime.Now;
                    ActApproval.ApproverDn = userConfig.User.Dn;
                }
                if(approval.OptComment() != null && approval.OptComment() != "")
                {
                    await ConfAddCommentToApproval(approval.OptComment());
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

        public async Task AssignApprovalGroup(RequestStatefulObject statefulObject)
        {
            ActApproval.AssignedGroup = statefulObject.AssignedGroup;
            // ActApproval.RecentHandler = ActApproval.CurrentHandler;
            if(CheckAssignValues(ActApproval))
            {
                await dbAcc.UpdateApprovalInDb(ActApproval);
            }
            DisplayAssignApprovalMode = false;
        }

        // public async Task AssignApprovalBack()
        // {
        //     ActApproval.AssignedGroup = ActApproval.RecentHandler?.Dn;
        //     ActApproval.RecentHandler = ActApproval.CurrentHandler;
        //     await dbAcc.UpdateApprovalInDb(ActApproval);
        //     DisplayAssignApprovalMode = false;
        // }

        public async Task ConfAddCommentToApproval(string commentText)
        {
            RequestComment comment = new RequestComment()
            {
                Scope = RequestObjectScopes.Approval.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            long commentId = await dbAcc.AddCommentToDb(comment);
            if(commentId != 0)
            {
                await dbAcc.AssignCommentToApprovalInDb(ActApproval.Id, commentId);
            }
            ActApproval.Comments.Add(new RequestCommentDataHelper(comment){});
            DisplayCommentMode = false;
        }


        // Implementation Tasks

        public void SelectImplTask(RequestImplTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            SetImplTaskOpt(action);
        }

        public void SelectImplTaskPopUp (RequestImplTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            SetImplTaskPopUpOpt(action);
        }

        public void SetImplTaskEnv(RequestImplTask implTask)
        {
            ActImplTask = implTask;
            RequestTicket? tick = TicketList.FirstOrDefault(x => x.Id == ActImplTask.TicketId);
            if(tick != null)
            {
                ActTicket = tick;
                RequestReqTask? reqTask = ActTicket.Tasks.FirstOrDefault(x => x.Id == ActImplTask.ReqTaskId);
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
            DisplayAssignMode = action == ObjAction.displayAssign;
            DisplayCommentMode = action == ObjAction.displayComment;
            DisplayApprovalMode = action == ObjAction.displayApprovals;
        }

        public void ResetImplTaskActions()
        {
            DisplayImplTaskMode = false;
            EditImplTaskMode = false;
            AddImplTaskMode = false;
            ImplementImplTaskMode = false;

            DisplayPromoteMode = false;
            DisplayDeleteMode = false;
            DisplayAssignMode = false;
            DisplayCommentMode = false;
            DisplayApprovalMode = false;
        }

        public async Task StartWorkOnImplTask(RequestImplTask implTask, ObjAction action)
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

        public async Task ContinueImplPhase(RequestImplTask implTask)
        {
            SelectImplTask(implTask, contOption);
            if(implTask.CurrentHandler != userConfig.User)
            {
                implTask.CurrentHandler = userConfig.User;
                await dbAcc.UpdateImplTaskStateInDb(implTask);
                ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id)] = ActImplTask;
            }
        }

        public async Task AssignImplTaskGroup(RequestStatefulObject statefulObject)
        {
            ActImplTask.AssignedGroup = statefulObject.AssignedGroup;
            ActImplTask.RecentHandler = ActImplTask.CurrentHandler;
            if(CheckAssignValues(ActImplTask))
            {
                await dbAcc.UpdateImplTaskStateInDb(ActImplTask);
            }
            DisplayAssignMode = false;
        }

        public async Task AssignImplTaskBack()
        {
            ActImplTask.AssignedGroup = ActImplTask.RecentHandler?.Dn;
            ActImplTask.RecentHandler = ActImplTask.CurrentHandler;
            await dbAcc.UpdateImplTaskStateInDb(ActImplTask);
            DisplayAssignMode = false;
        }

        public async Task AddImplTask()
        {
            ActImplTask.Id = await dbAcc.AddImplTaskToDb(ActImplTask);
            ActReqTask.ImplementationTasks.Add(ActImplTask);
        }

        public async Task ChangeImplTask()
        {
            await dbAcc.UpdateImplTaskInDb(ActImplTask);
            ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.TaskNumber == ActImplTask.TaskNumber)] = ActImplTask;
        }

        public async Task ConfAddCommentToImplTask(string commentText)
        {
            RequestComment comment = new RequestComment()
            {
                Scope = RequestObjectScopes.ImplementationTask.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            long commentId = await dbAcc.AddCommentToDb(comment);
            if(commentId != 0)
            {
                await dbAcc.AssignCommentToImplTaskInDb(ActImplTask.Id, commentId);
            }
            ActImplTask.Comments.Add(new RequestCommentDataHelper(comment){});
            DisplayCommentMode = false;
        }

        public async Task PromoteImplTask(RequestStatefulObject implTask)
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

        private async Task AutoCreateMissingImplTasks()
        {
            if(Phase == WorkflowPhases.approval && !MasterStateMatrix.PhaseActive[WorkflowPhases.planning] 
                && ActTicket.StateId >= MasterStateMatrix.LowestEndState)
            {
                foreach(var reqTask in ActTicket.Tasks)
                {
                    if(reqTask.ImplementationTasks.Count == 0)
                    {
                        await AutoCreateImplTasks(reqTask);
                    }
                }
            }
        }

        private async Task AutoCreateImplTasks(RequestReqTask reqTask)
        {
            RequestImplTask newImplTask;
            switch (userConfig.ReqAutoCreateImplTasks)
            {
                case AutoCreateImplTaskOptions.never:
                    break;
                case AutoCreateImplTaskOptions.onlyForOneDevice:
                    if(Devices.Count == 1)
                    {
                        newImplTask = new RequestImplTask(reqTask)
                            { TaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = Devices[0].Id, StateId = reqTask.StateId };
                        newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                        reqTask.ImplementationTasks.Add(newImplTask);
                    }
                    break;
                case AutoCreateImplTaskOptions.forEachDevice:
                    foreach(var device in Devices)
                    {
                        newImplTask = new RequestImplTask(reqTask)
                            { TaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = device.Id, StateId = reqTask.StateId };
                        newImplTask.Title += ": "+ Devices[Devices.FindIndex(x => x.Id == device.Id)].Name;
                        newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                        reqTask.ImplementationTasks.Add(newImplTask);
                    }
                    break;
                case AutoCreateImplTaskOptions.enterInReqTask:
                    newImplTask = new RequestImplTask(reqTask)
                        { TaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = reqTask.DeviceId, StateId = reqTask.StateId };
                    newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                    reqTask.ImplementationTasks.Add(newImplTask);
                    break;
                default:
                    break;
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

            // in the case impl tasks are already existing
            foreach(var implTask in ActReqTask.ImplementationTasks)
            {
                implTask.StateId = ActReqTask.StateId;
                await dbAcc.UpdateImplTaskInDb(implTask);
            }
        }

        public async Task UpdateReqTaskStateFromImplTasks(RequestReqTask reqTask)
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
            foreach (RequestReqTask reqTask in ActTicket.Tasks)
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
                foreach (RequestReqTask tsk in ActTicket.Tasks)
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
            await AutoCreateMissingImplTasks();
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

        private bool CheckAssignValues(RequestStatefulObject statefulObject)
        {
            if (statefulObject.AssignedGroup == null || statefulObject.AssignedGroup == "")
            {
                DisplayMessageInUi!(null, userConfig.GetText("assign_group"), userConfig.GetText("E8010"), true);
                return false;
            }
            return true;
        }
    }
}

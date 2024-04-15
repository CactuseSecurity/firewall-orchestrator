using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Middleware.Client;

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
        displayComment,
        displayCleanup,
        displayPathAnalysis
    }

    public class RequestHandler
    {
        public List<RequestTicket> TicketList { get; set; } = new ();
        public RequestTicket ActTicket { get; set; } = new ();
        public RequestReqTask ActReqTask { get; set; } = new ();
        public RequestImplTask ActImplTask { get; set; } = new ();
        public RequestApproval ActApproval { get; set; } = new ();

        public WorkflowPhases Phase = WorkflowPhases.request;
        public List<Device> Devices = new ();
        public List<FwoOwner> AllOwners { get; set; } = new ();
        public List<RequestPriority> PrioList = new ();
        public List<RequestImplTask> AllTicketImplTasks = new ();
        public List<RequestImplTask> AllVisibleImplTasks = new ();
        public StateMatrix ActStateMatrix = new ();
        public StateMatrix MasterStateMatrix = new ();
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
        public bool ReviewImplTaskMode = false;

        public bool DisplayAssignReqTaskMode = false;
        public bool DisplayAssignImplTaskMode = false;
        public bool DisplayApprovalReqMode = false;
        public bool DisplayApprovalImplMode = false;
        public bool DisplayApproveMode = false;
        public bool DisplayAssignApprovalMode = false;
        public bool DisplayPromoteTicketMode = false;
        public bool DisplayPromoteReqTaskMode = false;
        public bool DisplayPromoteImplTaskMode = false;
        public bool DisplaySaveTicketMode = false;
        public bool DisplayDeleteReqTaskMode = false;
        public bool DisplayDeleteImplTaskMode = false;
        public bool DisplayCleanupMode = false;
        public bool DisplayReqTaskCommentMode = false;
        public bool DisplayImplTaskCommentMode = false;
        public bool DisplayApprovalCommentMode = false;
        public bool DisplayPathAnalysisMode = false;
        

        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;
        public UserConfig userConfig;
        private readonly ApiConnection apiConnection;
        public readonly MiddlewareClient MiddlewareClient;
        private readonly StateMatrixDict stateMatrixDict = new ();
        private RequestDbAccess dbAcc;

        private ObjAction contOption = ObjAction.display;
        private bool InitOngoing = false;


        public RequestHandler()
        {}

        public RequestHandler(Action<Exception?, string, string, bool> displayMessageInUi, UserConfig userConfig, 
            ApiConnection apiConnection, MiddlewareClient middlewareClient, WorkflowPhases phase)
        {
            DisplayMessageInUi = displayMessageInUi;
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
            Phase = phase;
            MiddlewareClient = middlewareClient;
        }


        public async Task Init(List<int> ownerIds, bool allStates = false, bool ignoreOwners = false)
        {
            try
            {
                if(!InitOngoing)
                {
                    InitOngoing = true;
                    ActionHandler = new (apiConnection, this);
                    await ActionHandler.Init();
                    dbAcc = new RequestDbAccess(DisplayMessageInUi, userConfig, apiConnection, ActionHandler){};
                    Devices = await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getDeviceDetails);
                    AllOwners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
                    await stateMatrixDict.Init(Phase, apiConnection);
                    MasterStateMatrix = stateMatrixDict.Matrices[TaskType.master.ToString()];
                    TicketList = await dbAcc.FetchTickets(MasterStateMatrix, ownerIds, allStates, ignoreOwners);
                    PrioList = System.Text.Json.JsonSerializer.Deserialize<List<RequestPriority>>(userConfig.ReqPriorities) ?? throw new Exception("Config data could not be parsed.");
                    InitOngoing = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("init_environment"), "", true);
            }
        }

        public void FilterForRequester()
        {
            List<RequestTicket> filteredTicketList = new ();
            foreach(var ticket in TicketList)
            {
                if(userConfig.User.DbId == ticket.Requester?.DbId)
                {
                    filteredTicketList.Add(ticket);
                }
            }
            TicketList = filteredTicketList;
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
                        ActReqTask.CurrentHandler = statefulObject.CurrentHandler;
                        await UpdateActReqTaskState();
                        break;
                    case RequestObjectScopes.ImplementationTask:
                        SetImplTaskEnv((RequestImplTask)statefulObject);
                        ActImplTask.StateId = statefulObject.StateId;
                        ActImplTask.CurrentHandler = statefulObject.CurrentHandler;
                        await UpdateActImplTaskState();
                        break;
                    case RequestObjectScopes.Approval:
                        if(SetReqTaskEnv(((RequestApproval)statefulObject).TaskId))
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

        public void SetContinueEnv(ObjAction action)
        {
            contOption = action;
        }


        // Tickets

        public async Task<RequestTicket?> ResolveTicket(long ticketId)
        {
            List<RequestTicket> AllTicketList = await dbAcc.FetchTickets(MasterStateMatrix, AllOwners.ConvertAll(x => x.Id), true, true);
            return AllTicketList.FirstOrDefault(x => x.Id == ticketId);
        }

        public async Task<string> HandleExtTicketId(WorkflowPhases phase, long ticketId)
        {
            RequestTicket? ticket = await ResolveTicket(ticketId);
            if(ticket != null)
            {
                if(ticket.StateId < MasterStateMatrix.LowestEndState)
                {
                    SelectTicket(ticket, ObjAction.edit);
                }
                else if(MasterStateMatrix.IsLastActivePhase)
                {
                    SelectTicket(ticket, ObjAction.display);
                }
                else
                {
                    (WorkflowPhases newPhase, bool foundNewPhase) = await FindNewPhase(phase, ticket.StateId);
                    if(foundNewPhase)
                    {
                        return newPhase.ToString();
                    }
                }
            }
            return "";
        }

        private async Task<(WorkflowPhases, bool)> FindNewPhase(WorkflowPhases phase, int stateId)
        {
            bool foundNewPhase = false;
            GlobalStateMatrix glbStateMatrix = new ();
            await glbStateMatrix.Init(apiConnection, TaskType.master);
            bool cont = true;
            while(cont)
            {
                bool newPhase = MasterStateMatrix.getNextActivePhase(ref phase);
                if(newPhase)
                {
                    foundNewPhase = true;
                }
                cont = stateId >= glbStateMatrix.GlobalMatrix[phase].LowestEndState && newPhase;
            }
            return (phase, foundNewPhase);
        }

        public void SelectTicket(RequestTicket ticket, ObjAction action)
        {
            SetTicketEnv(ticket);
            SetTicketOpt(action);
        }

        public void SetTicketEnv(RequestTicket ticket)
        {
            ActTicket = ticket;
            ResetImplTaskList();
            SetRelevantOwner();
            ActStateMatrix = MasterStateMatrix;
        }

        public void ResetImplTaskList()
        {
            AllTicketImplTasks = new List<RequestImplTask>();
            foreach(var reqTask in ActTicket.Tasks)
            {
                foreach(var implTask in reqTask.ImplementationTasks)
                {
                    implTask.TicketId = ActTicket.Id;
                    implTask.ReqTaskId = reqTask.Id;
                    AllTicketImplTasks.Add(implTask);
                }
            }
        }

        public void SetRelevantOwner()
        {
            foreach(var reqTask in ActTicket.Tasks)
            {
                if(reqTask.TaskType == TaskType.new_interface.ToString() && reqTask.Owners.Count > 0)
                {
                    ActTicket.RelevantOwner = reqTask.Owners.First().Owner;
                    break;
                }
            }
        }

        public void SetTicketOpt(ObjAction action)
        {
            ResetTicketActions();
            DisplayTicketMode = action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add;
            EditTicketMode = action == ObjAction.edit || action == ObjAction.add;
            AddTicketMode = action == ObjAction.add;
        }

        public void SetTicketPopUpOpt(ObjAction action)
        {
            DisplayPromoteTicketMode = action == ObjAction.displayPromote;
            DisplaySaveTicketMode = action == ObjAction.displaySaveTicket;
        }

        public void ResetTicketActions()
        {
            DisplayTicketMode = false;
            EditTicketMode = false;
            AddTicketMode = false;
            DisplayPromoteTicketMode = false;
            DisplaySaveTicketMode = false;
        }

        public async Task<long> SaveTicket(RequestStatefulObject ticket)
        {
            try
            {
                ActTicket.StateId = ticket.StateId;
                if (ActTicket.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_request"), userConfig.GetText("U0001"), true);
                }
                foreach(RequestReqTask reqTask in ActTicket.Tasks)
                {
                    if(reqTask.StateId < ActTicket.StateId)
                    {
                        reqTask.StateId = ActTicket.StateId;
                    }
                }

                if(ActTicket.Deadline == null)
                {
                    int? tickDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.TicketDeadline;
                    ActTicket.Deadline = tickDeadline != null && tickDeadline > 0 ? DateTime.Now.AddDays((int)tickDeadline) : null;
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
                    if(reqtask.StateId <= ActTicket.StateId)
                    {
                        List<int> ticketStateList = new () { ActTicket.StateId };
                        reqtask.StateId = stateMatrixDict.Matrices[reqtask.TaskType].getDerivedStateFromSubStates(ticketStateList);
                        await dbAcc.UpdateReqTaskStateInDb(reqtask);
                    }
                    if( reqtask.ImplementationTasks.Count == 0 && !stateMatrixDict.Matrices[reqtask.TaskType].PhaseActive[WorkflowPhases.planning] 
                        && reqtask.StateId >= stateMatrixDict.Matrices[reqtask.TaskType].MinImplTasksNeeded)
                    {
                        await AutoCreateImplTasks(reqtask);
                    }
                }

                //check for further promotion (req tasks may be promoted)
                await UpdateActTicketStateFromReqTasks();

                ResetTicketActions();
                return ActTicket.Id;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_request"), "", true);
            }
            return 0;
        }

        public async Task PromoteTicket(RequestStatefulObject ticket)
        {
            try
            {
                ActTicket.StateId = ticket.StateId;
                await UpdateActTicketState();
                ResetTicketActions();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_ticket"), "", true);
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

        public bool SetReqTaskEnv (long reqTaskId)
        {
            RequestReqTask? reqTask;
            foreach(var ticket in TicketList)
            {
                reqTask = ticket.Tasks.FirstOrDefault(x => x.Id == reqTaskId);
                if (reqTask != null)
                {
                    SetReqTaskEnv(reqTask);
                    return true;
                }
            }
            return false;
        }

        public void SetReqTaskEnv (RequestReqTask reqTask)
        {
            ActReqTask = new RequestReqTask(reqTask);
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
            DisplayReqTaskMode = action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add || action == ObjAction.approve || action == ObjAction.plan;
            EditReqTaskMode = action == ObjAction.edit || action == ObjAction.add;
            AddReqTaskMode = action == ObjAction.add;
            PlanReqTaskMode = action == ObjAction.plan;
            ApproveReqTaskMode = action == ObjAction.approve;
        }

        public void SetReqTaskPopUpOpt(ObjAction action)
        {
            DisplayAssignReqTaskMode = action == ObjAction.displayAssign;
            DisplayApprovalReqMode = action == ObjAction.displayApprovals;
            DisplayApproveMode = action == ObjAction.displayApprove;
            DisplayPromoteReqTaskMode = action == ObjAction.displayPromote;
            DisplayDeleteReqTaskMode = action == ObjAction.displayDelete;
            DisplayReqTaskCommentMode = action == ObjAction.displayComment;
            DisplayPathAnalysisMode = action == ObjAction.displayPathAnalysis;
        }

        public void ResetReqTaskActions()
        {
            DisplayReqTaskMode = false;
            EditReqTaskMode = false;
            AddReqTaskMode = false;
            PlanReqTaskMode = false;
            ApproveReqTaskMode = false;

            DisplayAssignReqTaskMode = false;
            DisplayApprovalReqMode = false;
            DisplayApproveMode = false;
            DisplayPromoteReqTaskMode = false;
            DisplayDeleteReqTaskMode = false;
            DisplayReqTaskCommentMode = false;
            DisplayPathAnalysisMode = false;
        }

        public async Task StartWorkOnReqTask(RequestReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            ActReqTask.CurrentHandler = userConfig.User;
            List<int> actPossibleStates = ActStateMatrix.getAllowedTransitions(ActReqTask.StateId);
            if(actPossibleStates.Count == 1 && actPossibleStates[0] >= ActStateMatrix.LowestStartedState && actPossibleStates[0] < ActStateMatrix.LowestEndState)
            {
                ActReqTask.StateId = actPossibleStates[0];
                if(Phase == WorkflowPhases.approval)
                {
                    await SetApprovalEnv();
                    List<int> nextApprovalState = ActStateMatrix.getAllowedTransitions(ActApproval.StateId);
                    if (nextApprovalState.Count == 1 && nextApprovalState[0] >= ActStateMatrix.LowestStartedState && nextApprovalState[0] < ActStateMatrix.LowestEndState)
                    {
                        ActApproval.StateId = nextApprovalState[0];
                        await UpdateActApproval();
                    }
                }
            }
            await UpdateActReqTaskState();
            await UpdateActTicketStateFromReqTasks();
            SetReqTaskMode(action);
        }

        public async Task ContinuePhase(RequestReqTask reqTask)
        {
            SelectReqTask(reqTask, contOption);
            if(ActReqTask.CurrentHandler != userConfig.User)
            {
                ActReqTask.CurrentHandler = userConfig.User;
                await UpdateActReqTaskState();
            }
        }

        public async Task AssignReqTaskGroup(RequestStatefulObject statefulObject)
        {
            ActReqTask.AssignedGroup = statefulObject.AssignedGroup;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler ?? userConfig.User;
            if(CheckAssignValues(ActReqTask))
            {
                await UpdateActReqTaskState();
            }
            DisplayAssignReqTaskMode = false;
        }

        public async Task AssignReqTaskBack()
        {
            ActReqTask.AssignedGroup = ActReqTask.RecentHandler?.Dn;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler ?? userConfig.User;
            await UpdateActReqTaskState();
            DisplayAssignReqTaskMode = false;
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
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.TaskNumber == ActReqTask.TaskNumber)] = ActReqTask;
        }

        public async Task ChangeOwner()
        {
            if(ActReqTask.Id > 0)
            {
                await dbAcc.UpdateOwnersInDb(ActReqTask);
            }
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.TaskNumber == ActReqTask.TaskNumber)] = ActReqTask;
        }

        public async Task ConfDeleteReqTask()
        {
            if(ActReqTask.Id > 0)
            {
                await dbAcc.DeleteReqTaskFromDb(ActReqTask);
            }

            ActTicket.Tasks.RemoveAll(x => x.Id == ActReqTask.Id);
            // todo: adapt TaskNumbers of following tasks?
            DisplayDeleteReqTaskMode = false;
        }

        public async Task ConfAddCommentToReqTask(string commentText)
        {
            RequestComment comment = new ()
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
            DisplayReqTaskCommentMode = false;
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
                await UpdateActReqTaskState();

                if(Phase == WorkflowPhases.planning)
                {
                    foreach(RequestImplTask implTask in ActReqTask.ImplementationTasks)
                    {
                        implTask.StateId = ActReqTask.StateId;
                        await dbAcc.UpdateImplTaskStateInDb(implTask);
                    }
                }
                
                await UpdateActTicketStateFromReqTasks();
                DisplayPromoteReqTaskMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("promote_task"), "", true);
            }
        } 

        public async Task HandlePathAnalysisAction(string extParams = "")
        {
            try
            {
                PathAnalysisActionParams pathAnalysisParams = new ();
                if(extParams != "")
                {
                    pathAnalysisParams = System.Text.Json.JsonSerializer.Deserialize<PathAnalysisActionParams>(extParams) ?? throw new Exception("Extparams could not be parsed.");
                }

                switch(pathAnalysisParams.Option)
                {
                    case PathAnalysisOptions.WriteToDeviceList:
                        ActReqTask.SetDeviceList(await new PathAnalysis(apiConnection).getAllDevices(ActReqTask.Elements));
                        break;
                    case PathAnalysisOptions.DisplayFoundDevices:
                        SetReqTaskPopUpOpt(ObjAction.displayPathAnalysis);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("path_analysis"), "", true);
            }
        }

        // approvals

        public async Task SetApprovalEnv(RequestApproval? approval = null, bool createIfMissing = true)
        {
            if(approval != null)
            {
                ActApproval = new RequestApproval(approval);
            }
            else
            {
                if(ActReqTask.Approvals.Count == 0 && createIfMissing)
                {
                    await AddApproval();
                }
                ActApproval = ActReqTask.Approvals.FirstOrDefault(x => x.StateId < ActStateMatrix.LowestEndState) ?? (ActApproval = ActReqTask.Approvals.Last() ?? new());  // todo: select own approvals
            }
        }

        public void SetApprovalPopUpOpt(ObjAction action)
        {
            DisplayAssignApprovalMode = action == ObjAction.displayAssign;
            DisplayApprovalCommentMode = action == ObjAction.displayComment;
        }

        public async Task SelectApprovalPopUp (RequestApproval approval, ObjAction action)
        {
            await SetApprovalEnv(approval);
            SetApprovalPopUpOpt(action);
        }

        public void ResetApprovalActions()
        {
            DisplayAssignApprovalMode = false;
            DisplayApprovalCommentMode = false;
        }

        public async Task AddApproval(string extParams = "")
        {
            ApprovalParams approvalParams = new ();
            if(extParams != "")
            {
                approvalParams = System.Text.Json.JsonSerializer.Deserialize<ApprovalParams>(extParams) ?? throw new Exception("Extparams could not be parsed.");
            }

            DateTime? deadline = null;
            if(extParams != "")
            {
                deadline = approvalParams.Deadline > 0 ? DateTime.Now.AddDays(approvalParams.Deadline) : null;
            }
            else
            {
                int? appDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.ApprovalDeadline;
                deadline = appDeadline != null && appDeadline > 0 ? DateTime.Now.AddDays((int)appDeadline) : null;
            }

            RequestApproval approval = new ()
            {
                TaskId = ActReqTask.Id,
                StateId = extParams != "" ? approvalParams.StateId : ActStateMatrix.LowestEndState,
                ApproverGroup = extParams != "" ? approvalParams.ApproverGroup : "", // todo: get from owner ???,
                TenantId = ActTicket.TenantId, // ??
                Deadline = deadline,
                InitialApproval = ActReqTask.Approvals.Count == 0
            };
            if(!approval.InitialApproval)
            {
                // todo: checks if new approval allowed (only one open per group?, ...)
                approval.Id = await dbAcc.AddApprovalToDb(approval);
                DisplayMessageInUi(null, userConfig.GetText("add_approval"), userConfig.GetText("U8002"), false);
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
                    await ConfAddCommentToApproval(approval.OptComment()!);
                }
                
                if (ActApproval.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_approval"), userConfig.GetText("U0001"), true);
                }
                await UpdateActApproval();
                await UpdateActReqTaskStateFromApprovals();
                ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
                await UpdateActTicketStateFromReqTasks();
                ApproveReqTaskMode = false;
                DisplayApproveMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_approval"), "", true);
            }
        }

        public async Task AssignApprovalGroup(RequestStatefulObject statefulObject)
        {
            ActApproval.AssignedGroup = statefulObject.AssignedGroup;
            // ActApproval.RecentHandler = ActApproval.CurrentHandler;
            if(CheckAssignValues(ActApproval))
            {
                await UpdateActApproval();
            }
            DisplayAssignApprovalMode = false;
        }

        // public async Task AssignApprovalBack()
        // {
        //     ActApproval.AssignedGroup = ActApproval.RecentHandler?.Dn;
        //     ActApproval.RecentHandler = ActApproval.CurrentHandler;
        //     await UpdateActApproval();
        //     DisplayAssignApprovalMode = false;
        // }

        public async Task ConfAddCommentToApproval(string commentText)
        {
            RequestComment comment = new ()
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
            DisplayApprovalCommentMode = false;
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
            ActImplTask = new RequestImplTask(implTask);
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

        public async Task StartWorkOnImplTask(RequestImplTask implTask, ObjAction action)
        {
            SetImplTaskEnv(implTask);
            ActImplTask.CurrentHandler = userConfig.User;
            List<int> actPossibleStates = ActStateMatrix.getAllowedTransitions(ActImplTask.StateId);
            if(actPossibleStates.Count == 1 && actPossibleStates[0] >= ActStateMatrix.LowestStartedState && actPossibleStates[0] < ActStateMatrix.LowestEndState)
            {
                ActImplTask.StateId = actPossibleStates[0];
            }
            await UpdateActImplTaskState();
            if(!ActStateMatrix.PhaseActive[WorkflowPhases.planning] && ActReqTask.Start == null)
            {
                ActReqTask.Start = ActImplTask.Start;
            }
            await UpdateActTicketStateFromImplTasks();
            SetImplTaskOpt(action);
        }

        public async Task ContinueImplPhase(RequestImplTask implTask)
        {
            SelectImplTask(implTask, contOption);
            if(ActImplTask.CurrentHandler != userConfig.User)
            {
                ActImplTask.CurrentHandler = userConfig.User;
                await UpdateActImplTaskState();
            }
        }

        public bool SelectOwnerImplTasks(FwoOwner selectedOwnerOpt)
        {
            try
            {
                AllVisibleImplTasks = new ();
                if(selectedOwnerOpt.Id != -3)
                {
                    foreach(var ticket in TicketList)
                    {
                        foreach(var reqTask in ticket.Tasks)
                        {
                            foreach(var implTask in reqTask.ImplementationTasks)
                            {
                                if (selectedOwnerOpt.Id == -1 || (selectedOwnerOpt.Id == -2 && implTask.CurrentHandler?.DbId == userConfig.User.DbId)
                                    || (selectedOwnerOpt.Id > 0 && reqTask.Owners.FirstOrDefault(o => o.Owner.Id == selectedOwnerOpt.Id) != null))
                                {
                                    implTask.TicketId = ticket.Id;
                                    implTask.ReqTaskId = reqTask.Id;
                                    AllVisibleImplTasks.Add(implTask);
                                }
                            }
                        }
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
                AllVisibleImplTasks = new ();
                if(selectedDeviceOpt.Id != -1)
                {
                    foreach(var ticket in TicketList)
                    {
                        foreach(var reqTask in ticket.Tasks)
                        {
                            foreach(var implTask in reqTask.ImplementationTasks)
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
                }
                return selectedDeviceOpt.Id == -1;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("select_device"), "", true);
            }
            return true;
        }

        public async Task AssignImplTaskGroup(RequestStatefulObject statefulObject)
        {
            ActImplTask.AssignedGroup = statefulObject.AssignedGroup;
            ActImplTask.RecentHandler = ActImplTask.CurrentHandler ?? userConfig.User;
            if(CheckAssignValues(ActImplTask))
            {
                await UpdateActImplTaskState();
            }
            DisplayAssignImplTaskMode = false;
        }

        public async Task AssignImplTaskBack()
        {
            ActImplTask.AssignedGroup = ActImplTask.RecentHandler?.Dn;
            ActImplTask.RecentHandler = ActImplTask.CurrentHandler ?? userConfig.User;
            await UpdateActImplTaskState();
            DisplayAssignImplTaskMode = false;
        }

        public async Task AddImplTask()
        {
            ActImplTask.Id = await dbAcc.AddImplTaskToDb(ActImplTask);
            ActReqTask.ImplementationTasks.Add(ActImplTask);
        }

        public async Task ChangeImplTask()
        {
            await dbAcc.UpdateImplTaskInDb(ActImplTask, ActReqTask);
            ActReqTask.ImplementationTasks[ActReqTask.ImplementationTasks.FindIndex(x => x.TaskNumber == ActImplTask.TaskNumber)] = ActImplTask;
        }

        public async Task ConfAddCommentToImplTask(string commentText)
        {
            RequestComment comment = new ()
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
            DisplayImplTaskCommentMode = false;
        }

        public async Task PromoteImplTask(RequestStatefulObject implTask)
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

        public void SyncReqTaskStopTime()
        {
            bool openImplTask = false;
            foreach(var impltask in ActReqTask.ImplementationTasks)
            {
                if(impltask.Stop == null)
                {
                    openImplTask = true;
                }
            }
            if(!openImplTask && ActReqTask.Stop == null)
            {
                ActReqTask.Stop = ActImplTask.Stop;
            }
        }

        public async Task ConfDeleteImplTask()
        {
            await dbAcc.DeleteImplTaskFromDb(ActImplTask);
            ActReqTask.ImplementationTasks.RemoveAt(ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id));
            DisplayDeleteImplTaskMode = false;
        }

        public async Task ConfCleanupImplTasks()
        {
            foreach(var impltask in ActReqTask.ImplementationTasks)
            {
                await dbAcc.DeleteImplTaskFromDb(impltask);
            }
            ActReqTask.ImplementationTasks.Clear();
            DisplayCleanupMode = false;
        }

        private async Task AutoCreateOrUpdateImplTasks()
        {
            if(Phase <= WorkflowPhases.approval && !MasterStateMatrix.PhaseActive[WorkflowPhases.planning] 
                && ActTicket.StateId >= MasterStateMatrix.LowestEndState)
            {
                foreach(var reqTask in ActTicket.Tasks)
                {
                    // Todo: further analysis how many impl tasks currently have to be there and create or update where needed
                    if(reqTask.ImplementationTasks.Count == 0 
                        && reqTask.StateId >= stateMatrixDict.Matrices[reqTask.TaskType].MinImplTasksNeeded)
                    {
                        await AutoCreateImplTasks(reqTask);
                    }
                    else
                    {
                        foreach(var impltask in reqTask.ImplementationTasks)
                        {
                            if (impltask.StateId < reqTask.StateId)
                            {
                                impltask.StateId = reqTask.StateId;
                                await dbAcc.UpdateImplTaskStateInDb(impltask);
                            }
                        }
                    }
                }
            }
        }

        private async Task AutoCreateImplTasks(RequestReqTask reqTask)
        {
            RequestImplTask newImplTask;
            if(reqTask.TaskType == TaskType.access.ToString())
            {
                switch (userConfig.ReqAutoCreateImplTasks)
                {
                    case AutoCreateImplTaskOptions.never:
                        break;
                    case AutoCreateImplTaskOptions.onlyForOneDevice:
                        if(Devices.Count > 0)
                        {
                            await createAccessImplTask(reqTask, Devices[0].Id, false);
                        }
                        break;
                    case AutoCreateImplTaskOptions.forEachDevice:
                        foreach(var device in Devices)
                        {
                            await createAccessImplTask(reqTask, device.Id);
                        }
                        break;
                    case AutoCreateImplTaskOptions.enterInReqTask:
                        foreach(var deviceId in reqTask.getDeviceList())
                        {
                            await createAccessImplTask(reqTask, deviceId);
                        }
                        break;
                    case AutoCreateImplTaskOptions.afterPathAnalysis:
                        await CreateAccessImplTasksFromPathAnalysis(reqTask);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                newImplTask = new RequestImplTask(reqTask)
                    { TaskNumber = reqTask.HighestImplTaskNumber() + 1, StateId = reqTask.StateId };
                newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
                reqTask.ImplementationTasks.Add(newImplTask);
            }
        }

        public async Task CreateAccessImplTasksFromPathAnalysis(RequestReqTask reqTask)
        {
            foreach(var device in await new PathAnalysis(apiConnection).getAllDevices(reqTask.Elements))
            {
                if(reqTask.ImplementationTasks.FirstOrDefault(x => x.DeviceId == device.Id) == null)
                {
                    await createAccessImplTask(reqTask, device.Id);
                }
            }
        }

        private async Task createAccessImplTask(RequestReqTask reqTask, int deviceId, bool adaptTitle=true)
        {
            RequestImplTask newImplTask;
            newImplTask = new RequestImplTask(reqTask)
                { TaskNumber = reqTask.HighestImplTaskNumber() + 1, DeviceId = deviceId, StateId = reqTask.StateId };
            if(adaptTitle)
            {
                newImplTask.Title += ": "+ Devices[Devices.FindIndex(x => x.Id == deviceId)].Name;
            }
            newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask);
            reqTask.ImplementationTasks.Add(newImplTask);
        }


        // State changes

        public async Task UpdateActImplTaskState()
        {
            await dbAcc.UpdateImplTaskStateInDb(ActImplTask);
            int index = ActReqTask.ImplementationTasks.FindIndex(x => x.Id == ActImplTask.Id);
            if(index >= 0)
            {
                ActReqTask.ImplementationTasks[index] = ActImplTask;
            }
            else
            {
                // due to actions the impl task may not be assigned
                ActReqTask.ImplementationTasks.Add(ActImplTask);
            }
        }

        public async Task UpdateActApproval()
        {
            await dbAcc.UpdateApprovalInDb(ActApproval);
            ActReqTask.Approvals[ActReqTask.Approvals.FindIndex(x => x.Id == ActApproval.Id)] = ActApproval;
        }

        public async Task UpdateActReqTaskStateFromApprovals()
        {
            if (ActReqTask.Approvals.Count > 0)
            {
                List<int> approvalStates = new ();
                foreach (var approval in ActReqTask.Approvals)
                {
                    approvalStates.Add(approval.StateId);
                }
                ActReqTask.StateId = ActStateMatrix.getDerivedStateFromSubStates(approvalStates);
            }
            await UpdateActReqTaskState();

            // in the case impl tasks are already existing
            foreach(var implTask in ActReqTask.ImplementationTasks)
            {
                implTask.StateId = ActReqTask.StateId;
                await dbAcc.UpdateImplTaskInDb(implTask, ActReqTask);
            }
        }

        public async Task UpdateReqTaskStateFromImplTasks(RequestReqTask reqTask)
        {
            if (reqTask.ImplementationTasks.Count > 0)
            {
                List<int> implTaskStates = new ();
                foreach (var implTask in reqTask.ImplementationTasks)
                {
                    implTaskStates.Add(implTask.StateId);
                }
                reqTask.StateId = ActStateMatrix.getDerivedStateFromSubStates(implTaskStates);
            }
            await dbAcc.UpdateReqTaskStateInDb(reqTask);
        }

        public async Task UpdateActReqTaskState()
        {
            await dbAcc.UpdateReqTaskStateInDb(ActReqTask);
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.Id == ActReqTask.Id)] = ActReqTask;
        }

        public async Task UpdateActTicketStateFromImplTasks()
        {
            foreach (RequestReqTask reqTask in ActTicket.Tasks)
            {
                await UpdateReqTaskStateFromImplTasks(reqTask);
            }
            await UpdateActTicketStateFromReqTasks();
        }

        public async Task UpdateActTicketStateFromReqTasks()
        {
            if (ActTicket.Tasks.Count > 0)
            {
                List<int> taskStates = new ();
                foreach (RequestReqTask tsk in ActTicket.Tasks)
                {
                    taskStates.Add(tsk.StateId);
                }
                ActTicket.StateId = MasterStateMatrix.getDerivedStateFromSubStates(taskStates);
            }
            await UpdateActTicketState();
        }

        public async Task UpdateActTicketState()
        {
            if (MasterStateMatrix.IsLastActivePhase && ActTicket.StateId >= MasterStateMatrix.LowestEndState)
            {
                ActTicket.CompletionDate = DateTime.Now;
            }
            await AutoCreateOrUpdateImplTasks();
            await dbAcc.UpdateTicketStateInDb(ActTicket);
            TicketList[TicketList.FindIndex(x => x.Id == ActTicket.Id)] = ActTicket;
        }


        // checks

        public async Task<bool> CheckRuleUid(int? deviceId, string? ruleUid)
        {
            return await dbAcc.FindRuleUid(deviceId, ruleUid);
        }

        private bool CheckAssignValues(RequestStatefulObject statefulObject)
        {
            // if (statefulObject.AssignedGroup == null || statefulObject.AssignedGroup == "")
            // {
            //     DisplayMessageInUi(null, userConfig.GetText("assign_group"), userConfig.GetText("E8010"), true);
            //     return false;
            // }
            return true;
        }
    }
}

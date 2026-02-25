using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using System.Text.Json;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        public bool DisplayReqTaskMode = false;
        public bool EditReqTaskMode = false;
        public bool AddReqTaskMode = false;
        public bool PlanReqTaskMode = false;
        public bool DisplayAssignReqTaskMode = false;
        public bool DisplayApprovalReqMode = false;
        public bool DisplayDeleteReqTaskMode = false;
        public bool DisplayReqTaskCommentMode = false;
        public bool DisplayPathAnalysisMode = false;

        // Request Tasks

        public void SelectReqTask(WfReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskMode(action);
        }

        public void SelectReqTaskPopUp(WfReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskPopUpOpt(action);
        }

        public bool SetReqTaskEnv(long reqTaskId)
        {
            WfReqTask? reqTask;
            foreach (var ticket in TicketList)
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

        public void SetReqTaskEnv(WfReqTask reqTask)
        {
            ActReqTask = new(reqTask);
            WfTicket? tick = TicketList.FirstOrDefault(x => x.Id == ActReqTask.TicketId);
            if (tick != null)
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

        public async Task StartWorkOnReqTask(WfReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            ActReqTask.CurrentHandler = userConfig.User;
            List<int> actPossibleStates = ActStateMatrix.getAllowedTransitions(ActReqTask.StateId);
            if (actPossibleStates.Count == 1 && actPossibleStates[0] >= ActStateMatrix.LowestStartedState && actPossibleStates[0] < ActStateMatrix.LowestEndState)
            {
                ActReqTask.StateId = actPossibleStates[0];
                if (Phase == WorkflowPhases.approval)
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

        public async Task ContinuePhase(WfReqTask reqTask)
        {
            SelectReqTask(reqTask, contOption);
            if (ActReqTask.CurrentHandler != userConfig.User)
            {
                ActReqTask.CurrentHandler = userConfig.User;
                await UpdateActReqTaskState();
            }
        }

        public async Task AssignReqTaskGroup(WfStatefulObject statefulObject)
        {
            ActReqTask.AssignedGroup = statefulObject.AssignedGroup;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler ?? userConfig.User;
            if (ActionHandler != null)
            {
                await UpdateActReqTaskState();
                await ActionHandler.DoOnAssignmentActions(statefulObject, ActReqTask.AssignedGroup);
            }
            DisplayAssignReqTaskMode = false;
        }

        public async Task AssignReqTaskBack()
        {
            ActReqTask.AssignedGroup = ActReqTask.RecentHandler?.Dn;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler ?? userConfig.User;
            await UpdateActReqTaskState();
            if (ActionHandler != null)
            {
                await ActionHandler.DoOnAssignmentActions(ActReqTask, ActReqTask.AssignedGroup);
            }
            DisplayAssignReqTaskMode = false;
        }

        public async Task AddReqTask()
        {
            if (ActTicket.Id > 0) // ticket already created -> write directly to db
            {
                ActReqTask.TicketId = ActTicket.Id;
                if (dbAcc != null)
                {
                    ActReqTask.Id = await dbAcc.AddReqTaskToDb(ActReqTask);
                }
            }
            ActTicket.Tasks.Add(ActReqTask);
        }

        public async Task ChangeReqTask()
        {
            if (ActReqTask.Id > 0 && dbAcc != null)
            {
                await dbAcc.UpdateReqTaskInDb(ActReqTask);
            }
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.TaskNumber == ActReqTask.TaskNumber)] = ActReqTask;
        }

        public async Task ChangeOwner()
        {
            if (ActReqTask.Id > 0 && dbAcc != null)
            {
                await dbAcc.UpdateOwnersInDb(ActReqTask);
            }
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.TaskNumber == ActReqTask.TaskNumber)] = ActReqTask;
        }

        public async Task ConfDeleteReqTask()
        {
            if (ActReqTask.Id > 0 && dbAcc != null)
            {
                await dbAcc.DeleteReqTaskFromDb(ActReqTask);
            }

            ActTicket.Tasks.RemoveAll(x => x.Id == ActReqTask.Id);
            // todo: adapt TaskNumbers of following tasks?
            DisplayDeleteReqTaskMode = false;
        }

        public async Task ConfAddCommentToReqTask(string commentText)
        {
            WfComment comment = new()
            {
                Scope = WfObjectScopes.RequestTask.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            if (dbAcc != null)
            {
                long commentId = await dbAcc.AddCommentToDb(comment);
                if (commentId != 0)
                {
                    await dbAcc.AssignCommentToReqTaskInDb(ActReqTask.Id, commentId);
                }
            }
            ActReqTask.Comments.Add(new WfCommentDataHelper(comment) { });
            DisplayReqTaskCommentMode = false;
        }

        public string GetRequestingOwner()
        {
            int? ownerId = ActReqTask.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner);
            if (ownerId != null)
            {
                return AllOwners.FirstOrDefault(x => x.Id == ownerId)?.Display() ?? "";
            }
            return "";
        }

        public async Task SetAddInfoInReqTask(WfReqTask reqTask, string key, string newValue)
        {
            try
            {
                reqTask.SetAddInfo(key, newValue);
                if (dbAcc != null)
                {
                    await dbAcc.UpdateReqTaskAdditionalInfo(reqTask);
                }
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
                PathAnalysisActionParams pathAnalysisParams = new();
                if (extParams != "")
                {
                    pathAnalysisParams = System.Text.Json.JsonSerializer.Deserialize<PathAnalysisActionParams>(extParams) ?? throw new JsonException("Extparams could not be parsed.");
                }

                switch (pathAnalysisParams.Option)
                {
                    case PathAnalysisOptions.WriteToDeviceList:
                        if (apiConnection != null)
                        {
                            ActReqTask.SetDeviceList(await PathAnalysis.GetAllDevices(ActReqTask.Elements, apiConnection));
                        }
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
    }
}

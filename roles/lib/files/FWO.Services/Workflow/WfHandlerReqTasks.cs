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

        public async Task<bool> ReloadActReqTask()
        {
            try
            {
                if (ActReqTask.Id <= 0 || ActReqTask.TicketId <= 0 || dbAcc == null)
                {
                    return false;
                }

                WfTicket? refreshedTicket = await dbAcc.FetchTicket(ActReqTask.TicketId, GetOwnerIdsForFiltering(), GetTicketFilter());
                if (refreshedTicket != null)
                {
                    int ticketIndex = TicketList.FindIndex(ticket => ticket.Id == refreshedTicket.Id);
                    if (ticketIndex >= 0)
                    {
                        TicketList[ticketIndex] = refreshedTicket;
                    }
                }
                WfReqTask? refreshedTask = refreshedTicket?.Tasks.FirstOrDefault(task => task.Id == ActReqTask.Id);
                if (refreshedTask == null)
                {
                    return false;
                }

                SetTicketEnv(refreshedTicket!);
                SetReqTaskEnv(refreshedTask);
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_requests"), "", true);
                return false;
            }
        }

        public void SelectReqTask(WfReqTask reqTask, ObjAction action)
        {
            if (action == ObjAction.add && reqTask.TicketId <= 0 && ActTicket != null)
            {
                reqTask.TicketId = ActTicket.Id;
            }
            SetReqTaskEnv(reqTask);
            SetReqTaskMode(action);
        }

        public async Task SelectReqTask(WfReqTask reqTask, ObjAction action, bool reload)
        {
            SetReqTaskEnv(await LoadReqTaskDetails(reqTask, reload));
            SetReqTaskMode(action);
        }

        public void SelectReqTaskPopUp(WfReqTask reqTask, ObjAction action)
        {
            SetReqTaskEnv(reqTask);
            SetReqTaskPopUpOpt(action);
        }

        public async Task SelectReqTaskPopUp(WfReqTask reqTask, ObjAction action, bool reload)
        {
            SetReqTaskEnv(await LoadReqTaskDetails(reqTask, reload));
            SetReqTaskPopUpOpt(action);
        }

        private async Task<WfReqTask> LoadReqTaskDetails(WfReqTask reqTask, bool reload)
        {
            if (!reload || reqTask.Id <= 0 || reqTask.TicketId <= 0 || dbAcc == null)
            {
                return reqTask;
            }

            try
            {
                WfTicket? ticket = await dbAcc.FetchTicket(reqTask.TicketId, GetOwnerIdsForFiltering(), GetTicketFilter());
                if (ticket != null)
                {
                    int ticketIndex = TicketList.FindIndex(existingTicket => existingTicket.Id == ticket.Id);
                    if (ticketIndex >= 0)
                    {
                        TicketList[ticketIndex] = ticket;
                    }
                    SetTicketEnv(ticket);
                }
                WfReqTask? fullReqTask = ticket?.Tasks.FirstOrDefault(task => task.Id == reqTask.Id);
                if (ticket == null || fullReqTask == null)
                {
                    return reqTask;
                }
                return fullReqTask;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_requests"), "", true);
                return reqTask;
            }
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
            ActTicket = ResolveTicketForReqTask(reqTask) ?? throw new InvalidOperationException($"Could not resolve ticket {ActReqTask.TicketId} for request task {reqTask.Id}.");
            ActStateMatrix = stateMatrixDict.Matrices[reqTask.TaskType];
        }

        /// <summary>
        /// Resolves the ticket that belongs to the supplied request task from the current context or ticket list.
        /// </summary>
        private WfTicket? ResolveTicketForReqTask(WfReqTask reqTask)
        {
            if (reqTask.TicketId > 0)
            {
                if (ActTicket != null && ActTicket.Id == reqTask.TicketId)
                {
                    return ActTicket;
                }

                return TicketList.FirstOrDefault(ticket => ticket.Id == reqTask.TicketId);
            }

            if (ActTicket != null)
            {
                if (AddTicketMode && ActTicket.Id <= 0)
                {
                    return ActTicket;
                }

                if (ActTicket.Tasks.Contains(reqTask))
                {
                    return ActTicket;
                }
            }

            return TicketList.FirstOrDefault(ticket => ticket.Tasks.Any(task => task.Id == reqTask.Id));
        }

        public bool TrySetReqTaskEnv(WfReqTask reqTask)
        {
            if (!stateMatrixDict.Matrices.ContainsKey(reqTask.TaskType))
            {
                return false;
            }

            if (ResolveTicketForReqTask(reqTask) == null)
            {
                return false;
            }

            SetReqTaskEnv(reqTask);
            return true;
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
            if (ReadOnlyMode)
            {
                // read-only users follow the phase without starting work, so no state is written
                await SelectReqTask(reqTask, action, true);
                return;
            }
            SetReqTaskEnv(await LoadReqTaskDetails(reqTask, true));
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
            await ViewReqTaskInPhase(reqTask);
            if (!ReadOnlyMode && ActReqTask.CurrentHandler != userConfig.User)
            {
                ActReqTask.CurrentHandler = userConfig.User;
                await UpdateActReqTaskState();
            }
        }

        /// <summary>
        /// Opens a request task in the view of the current phase without starting work on it or taking it over,
        /// so that read-only users can see the phase dialog. No state is written.
        /// </summary>
        /// <param name="reqTask">The request task to open.</param>
        public async Task ViewReqTaskInPhase(WfReqTask reqTask)
        {
            await SelectReqTask(reqTask, contOption, true);
        }

        public async Task AssignReqTaskGroup(WfStatefulObject statefulObject)
        {
            ActReqTask.AssignedGroup = statefulObject.AssignedGroup;
            ActReqTask.RecentHandler = ActReqTask.CurrentHandler ?? userConfig.User;
            if (ActionHandler != null)
            {
                await UpdateActReqTaskState();
                await ActionHandler.DoOnAssignmentActions(statefulObject, WfObjectScopes.RequestTask, ActReqTask.AssignedGroup);
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
                await ActionHandler.DoOnAssignmentActions(ActReqTask, WfObjectScopes.RequestTask, ActReqTask.AssignedGroup);
            }
            DisplayAssignReqTaskMode = false;
        }

        public async Task AddReqTask()
        {
            if (ActTicket.Locked || !CheckActTicketChangeable())
            {
                return;
            }

            if (ActReqTask.Approvals.Count == 0)
            {
                await AddApproval();
            }

            if (ActTicket.Id > 0) // ticket already created -> write directly to db
            {
                ActReqTask.TicketId = ActTicket.Id;
                if (dbAcc != null)
                {
                    long newTaskId = await dbAcc.AddReqTaskToDb(ActReqTask);
                    if (newTaskId <= 0)
                    {
                        return;
                    }

                    // The insert creates the task's elements and approvals in separate API calls.
                    // Reload the ticket so the task table uses the complete persisted task graph.
                    if (!await ReloadActTicket() || !ActTicket.Tasks.Any(task => task.Id == ActReqTask.Id))
                    {
                        ActTicket.Tasks.Add(ActReqTask);
                    }
                    return;
                }
            }
            ActTicket.Tasks.Add(ActReqTask);
        }

        /// <summary>
        /// Persists the active request task and replaces it in the active ticket.
        /// </summary>
        /// <param name="allowLockedApprovalUpdate">Allows the explicitly permitted approval-phase field update on a locked task.</param>
        public async Task ChangeReqTask(bool allowLockedApprovalUpdate = false)
        {
            if ((ActReqTask.Locked && !allowLockedApprovalUpdate) || !CheckActTicketChangeable())
            {
                return;
            }

            if (ActReqTask.Id > 0 && dbAcc != null)
            {
                await dbAcc.UpdateReqTaskInDb(ActReqTask, allowLockedApprovalUpdate);
            }
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.TaskNumber == ActReqTask.TaskNumber)] = ActReqTask;
        }

        public async Task ChangeOwner()
        {
            if (ActReqTask.Locked || !CheckActTicketChangeable())
            {
                return;
            }

            if (ActReqTask.Id > 0 && dbAcc != null)
            {
                await dbAcc.UpdateOwnersInDb(ActReqTask);
            }
            ActTicket.Tasks[ActTicket.Tasks.FindIndex(x => x.TaskNumber == ActReqTask.TaskNumber)] = ActReqTask;
        }

        public async Task ConfDeleteReqTask()
        {
            if (ActTicket.Locked || !CheckActTicketChangeable())
            {
                DisplayDeleteReqTaskMode = false;
                return;
            }

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

        /// <summary>
        /// Writes a bookkeeping key into the additional info of a request task.
        /// </summary>
        /// <param name="reqTask">Request task to write to.</param>
        /// <param name="key">Additional info key.</param>
        /// <param name="newValue">Value to store under the key.</param>
        /// <param name="previousTicket">Already loaded stored ticket, passed on by callers that write to
        /// several tasks of the same ticket, see SetAddInfoInReqTasks.</param>
        public async Task SetAddInfoInReqTask(WfReqTask reqTask, string key, string newValue, WfTicket? previousTicket = null)
        {
            try
            {
                reqTask.SetAddInfo(key, newValue);
                if (dbAcc != null)
                {
                    await dbAcc.UpdateReqTaskAdditionalInfo(reqTask, previousTicket);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("update_task"), "", true);
            }
        }

        /// <summary>
        /// Writes a bookkeeping key into the additional info of several request tasks.
        /// </summary>
        /// <param name="reqTasks">Request tasks to write to.</param>
        /// <param name="key">Additional info key.</param>
        /// <param name="newValue">Value to store under the key.</param>
        /// <remarks>
        /// Reads the stored ticket once per ticket instead of once per task, which matters for request
        /// tasks spread over many devices.
        /// </remarks>
        public async Task SetAddInfoInReqTasks(IEnumerable<WfReqTask> reqTasks, string key, string newValue)
        {
            foreach (IGrouping<long, WfReqTask> ticketTasks in reqTasks.GroupBy(reqTask => reqTask.TicketId))
            {
                WfTicket? storedTicket = dbAcc != null ? await dbAcc.LoadPreviousTicket(ticketTasks.Key) : null;
                foreach (WfReqTask reqTask in ticketTasks)
                {
                    await SetAddInfoInReqTask(reqTask, key, newValue, storedTicket);
                }
            }
        }

        public async Task RemoveAddInfoInReqTask(WfReqTask reqTask, string key)
        {
            try
            {
                reqTask.RemoveAddInfo(key);
                if (dbAcc != null)
                {
                    await dbAcc.UpdateReqTaskAdditionalInfo(reqTask);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("update_task"), "", true);
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
                            ActReqTask.SetDeviceList(await RoutingBasedPathAnalyzer.GetAllDevices(ActReqTask.Elements, apiConnection));
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

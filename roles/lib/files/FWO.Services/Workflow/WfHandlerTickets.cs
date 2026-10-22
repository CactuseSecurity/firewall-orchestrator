using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        public bool DisplayTicketMode = false;
        public bool EditTicketMode = false;
        public bool AddTicketMode = false;
        public bool DisplayPromoteTicketMode = false;
        public bool DisplaySaveTicketMode = false;
        public bool DisplayTicketCommentMode = false;

        // Tickets

        public async Task<WfTicket?> ResolveTicket(long ticketId)
        {
            WfTicket? ticket = null;
            if (dbAcc != null)
            {
                ticket = await dbAcc.FetchTicket(ticketId, GetOwnerIdsForFiltering(), GetVisibilityTicketFilter());
                if (ticket != null)
                {
                    SetTicketEnv(ticket);
                    return ticket;
                }
            }
            return null;
        }

        public async Task<string> HandleInjectedTicketId(WorkflowPhases phase, long ticketId)
        {
            WfTicket? ticket = await ResolveTicket(ticketId);
            if (ticket != null)
            {
                if (ticket.StateId < MasterStateMatrix.LowestEndState)
                {
                    await SelectTicket(ticket, ObjAction.edit, true);
                }
                else if (MasterStateMatrix.IsLastActivePhase)
                {
                    await SelectTicket(ticket, ObjAction.display, true);
                }
                else
                {
                    (WorkflowPhases newPhase, bool foundNewPhase) = await FindNewPhase(phase, ticket.StateId);
                    if (foundNewPhase)
                    {
                        return newPhase.ToString();
                    }
                }
            }
            return "";
        }

        public async Task<List<WfTicket>> GetOpenTickets(string taskType, int cutOffPeriod = 0, SchedulerInterval interval = SchedulerInterval.Days)
        {
            if (dbAcc != null)
            {
                DateTime createdFrom = interval switch
                {
                    SchedulerInterval.Days => DateTime.Now.Date.AddDays(-cutOffPeriod),
                    SchedulerInterval.Weeks => DateTime.Now.Date.AddDays(-cutOffPeriod * GlobalConst.kDaysPerWeek),
                    SchedulerInterval.Months => DateTime.Now.Date.AddMonths(-cutOffPeriod),
                    _ => throw new NotSupportedException("Time interval is not supported."),
                };
                return await dbAcc.GetTicketsByParameters(taskType, StateMatrix(taskType).LowestInputState, StateMatrix(taskType).LowestEndState, createdFrom, null, null);
            }
            return [];
        }

        public async Task SelectTicket(WfTicket ticket, ObjAction action, bool reload = false)
        {
            if (ReloadTasks && reload && dbAcc != null)
            {
                WfTicket? refreshedTicket = await ReloadTicket(ticket);
                if (refreshedTicket == null)
                {
                    return;
                }
                ticket = refreshedTicket;
            }
            SetTicketEnv(ticket);
            SetTicketOpt(action);
        }

        private async Task<WfTicket?> ReloadTicket(WfTicket ticket)
        {
            if (dbAcc == null)
            {
                return null;
            }

            WfTicket? refreshedTicket = await dbAcc.FetchTicket(ticket.Id, GetOwnerIdsForFiltering(), GetVisibilityTicketFilter());
            if (refreshedTicket == null)
            {
                return null;
            }

            int ticketIndex = TicketList.FindIndex(existingTicket => existingTicket.Id == refreshedTicket.Id);
            if (ticketIndex >= 0)
            {
                TicketList[ticketIndex] = refreshedTicket;
            }
            return refreshedTicket;
        }

        public void SetTicketEnv(WfTicket ticket)
        {
            ActTicket = ticket;
            ResetImplTaskList();
            ActStateMatrix = MasterStateMatrix;
        }

        /// <summary>
        /// Reloads the active ticket after an action changed its tasks or metadata.
        /// </summary>
        /// <returns><see langword="true"/> when the ticket was refreshed successfully.</returns>
        public async Task<bool> ReloadActTicket()
        {
            try
            {
                if (ActTicket.Id <= 0 || dbAcc == null)
                {
                    return false;
                }

                WfTicket? refreshedTicket = await ReloadTicket(ActTicket);
                if (refreshedTicket == null)
                {
                    return false;
                }
                SetTicketEnv(refreshedTicket);
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_requests"), "", true);
                return false;
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

        /// <summary>
        /// True if the last <see cref="SaveTicket"/> saved a new ticket that could not be read back: the returned id
        /// is valid, but <see cref="ActTicket"/> is the in-memory copy without the ids of its request tasks.
        /// </summary>
        public bool LastSaveReloadFailed { get; private set; }

        public async Task<long> SaveTicket(WfStatefulObject ticket)
        {
            LastSaveReloadFailed = false;
            try
            {
                return dbAcc == null ? 0 : await SaveTicketToDb(ticket);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_request"), "", true);
            }
            return 0;
        }

        private async Task<long> SaveTicketToDb(WfStatefulObject ticket)
        {
            int previousTicketStateId = ActTicket.StateId;
            int targetTicketStateId = ticket.StateId;
            ActTicket.StateId = targetTicketStateId;
            PrepareTicketData();
            bool requestTasksSynchronized = ShouldSynchronizeRequestTasks(previousTicketStateId);

            if (requestTasksSynchronized)
            {
                await SynchronizeRequestTasks(targetTicketStateId, ticket.OptComment());
            }

            await PersistTicket();

            if (LastSaveReloadFailed)
            {
                // The ticket was saved, but its request tasks are unknown here and must not be processed again.
                ResetTicketActions();
                return ActTicket.Id;
            }

            if (!requestTasksSynchronized)
            {
                await UpdateRequestTasksAndTicket(ticket.OptComment());
            }

            ResetTicketActions();
            return ActTicket.Id;
        }

        private bool ShouldSynchronizeRequestTasks(int previousTicketStateId)
        {
            return !AddTicketMode && Phase != WorkflowPhases.request && previousTicketStateId != ActTicket.StateId;
        }

        private async Task SynchronizeRequestTasks(int targetTicketStateId, string? approvalComment)
        {
            await UpdateRequestTasksFromTicket(targetTicketStateId: targetTicketStateId, approvalComment: approvalComment);
            ActTicket.StateId = targetTicketStateId;
        }

        private async Task PersistTicket()
        {
            if (AddTicketMode)
            {
                await AddNewTicket();
                return;
            }

            ActTicket = await dbAcc!.UpdateTicketInDb(ActTicket);
            TicketList[TicketList.FindIndex(x => x.Id == ActTicket.Id)] = ActTicket;
        }

        private async Task AddNewTicket()
        {
            ActTicket.CreationDate = DateTime.Now;
            UiUser requester = ActTicket.Requester ?? userConfig.User;
            if (requester.DbId <= 0 && userConfig.User.DbId > 0)
            {
                requester = userConfig.User;
            }

            ActTicket.Requester = requester;
            WfTicketCreationResult creation = await dbAcc!.AddTicketToDb(ActTicket);
            ActTicket = creation.Ticket;
            if (creation.ReloadFailed)
            {
                LastSaveReloadFailed = true;
                return;
            }
            TicketList.Add(ActTicket);
        }

        private async Task UpdateRequestTasksAndTicket(string? approvalComment)
        {
            bool requestTaskActionsChangedState = await UpdateRequestTasksFromTicket(approvalComment: approvalComment);
            if (requestTaskActionsChangedState && Phase != WorkflowPhases.request)
            {
                await UpdateActTicketStateFromReqTasks();
            }
        }

        private bool CanViewTicket(WfTicket ticket, HashSet<int> exclusiveVisibilityGroupIds)
        {
            return CanViewStatefulObject(ticket, MasterStateMatrix, "ticket", ticket.Id.ToString(), exclusiveVisibilityGroupIds);
        }

        private bool CanViewReqTask(WfReqTask reqTask, HashSet<int> exclusiveVisibilityGroupIds)
        {
            StateMatrix stateMatrix = StateMatrix(reqTask.TaskType);
            return CanViewStatefulObject(reqTask, stateMatrix, "request-task", $"{reqTask.TaskType}/{reqTask.Id}", exclusiveVisibilityGroupIds);
        }

        private bool CanViewImplTask(WfImplTask implTask, HashSet<int> exclusiveVisibilityGroupIds)
        {
            StateMatrix stateMatrix = StateMatrix(implTask.TaskType);
            return CanViewStatefulObject(implTask, stateMatrix, "impl-task", $"{implTask.TaskType}/{implTask.Id}", exclusiveVisibilityGroupIds);
        }

        private bool CanViewApproval(WfApproval approval, StateMatrix reqTaskMatrix, HashSet<int> exclusiveVisibilityGroupIds)
        {
            if (!userConfig.ReqVisibilityBased)
            {
                return true;
            }

            bool visible = WorkflowVisibilityHelper.CanAccessStatefulObject(approval, reqTaskMatrix, userConfig.User.WorkflowVisibilityGroupIds,
                exclusiveVisibilityGroupIds) || IsExplicitlyAssigned(approval);
            LogVisibilityDecision("approval", approval.StateId, approval.Id.ToString(), reqTaskMatrix, visible, exclusiveVisibilityGroupIds);
            return visible;
        }

        private bool CanViewStatefulObject(WfStatefulObject statefulObject, StateMatrix stateMatrix, string objectType, string objectId,
            HashSet<int> exclusiveVisibilityGroupIds)
        {
            if (!userConfig.ReqVisibilityBased)
            {
                return true;
            }

            bool visible = WorkflowVisibilityHelper.CanAccessStatefulObject(statefulObject, stateMatrix, userConfig.User.WorkflowVisibilityGroupIds,
                exclusiveVisibilityGroupIds) || IsExplicitlyAssigned(statefulObject);
            LogVisibilityDecision(objectType, statefulObject.StateId, objectId, stateMatrix, visible, exclusiveVisibilityGroupIds);
            return visible;
        }

        private bool IsExplicitlyAssigned(WfStatefulObject statefulObject)
        {
            return IsAssignedToCurrentUser(statefulObject.CurrentHandler) || IsAssignedToCurrentUserGroup(statefulObject.AssignedGroup)
                || (statefulObject is WfApproval approval && IsExplicitApprovalAssignment(approval));
        }

        private bool IsExplicitApprovalAssignment(WfApproval approval)
        {
            return IsAssignedToCurrentUserDn(approval.ApproverDn) || IsAssignedToCurrentUserGroup(approval.ApproverGroup);
        }

        private bool IsAssignedToCurrentUser(UiUser? handler)
        {
            return handler != null && (handler.DbId == userConfig.User.DbId || DistName.DnEquals(handler.Dn, userConfig.User.Dn));
        }

        private bool IsAssignedToCurrentUserDn(string? dn)
        {
            return DistName.DnEquals(dn, userConfig.User.Dn);
        }

        private bool IsAssignedToCurrentUserGroup(string? groupDn)
        {
            return !string.IsNullOrWhiteSpace(groupDn)
                && (DistName.DnEquals(groupDn, userConfig.User.Dn)
                    || userConfig.User.Groups.Any(group => DistName.DnEquals(group, groupDn)));
        }

        private bool ApplyVisibilityRestrictions(WfTicket ticket)
        {
            if (!userConfig.ReqVisibilityBased)
            {
                return true;
            }

            HashSet<int> exclusiveVisibilityGroupIds = GetWorkflowExclusiveVisibilityGroupIds();
            bool ticketVisible = CanViewTicket(ticket, exclusiveVisibilityGroupIds);
            bool hadRequestTasks = ticket.Tasks.Count > 0;
            List<WfReqTask> visibleReqTasks = [];

            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                StateMatrix reqTaskMatrix = StateMatrix(reqTask.TaskType);
                reqTask.ImplementationTasks = [.. reqTask.ImplementationTasks.Where(implTask => CanViewImplTask(implTask, exclusiveVisibilityGroupIds))];
                reqTask.Approvals = [.. reqTask.Approvals.Where(approval => CanViewApproval(approval, reqTaskMatrix, exclusiveVisibilityGroupIds))];

                bool taskVisible = CanViewReqTask(reqTask, exclusiveVisibilityGroupIds) || reqTask.ImplementationTasks.Count > 0 || reqTask.Approvals.Count > 0;
                if (!taskVisible)
                {
                    continue;
                }

                visibleReqTasks.Add(reqTask);
            }

            ticket.Tasks = visibleReqTasks;
            if (hadRequestTasks && ticket.Tasks.Count == 0)
            {
                Log.WriteDebug("Workflow visibility",
                    $"Denied ticket {ticket.Id} in state {ticket.StateId} because no visible request tasks remain. " +
                    $"user groups: [{string.Join(", ", userConfig.User.WorkflowVisibilityGroupIds)}], exclusive groups: [{string.Join(", ", exclusiveVisibilityGroupIds)}]");
                return false;
            }

            return ticketVisible || ticket.Tasks.Count > 0;
        }

        private List<int>? GetOwnerIdsForFiltering()
        {
            return userConfig.ReqOwnerBased ? AllOwners.ConvertAll(owner => owner.Id) : null;
        }

        private Func<WfTicket, bool>? GetVisibilityTicketFilter()
        {
            return userConfig.ReqVisibilityBased && !SystemContext ? ApplyVisibilityRestrictions : null;
        }

        private void LogVisibilityDecision(string objectType, int stateId, string objectId, StateMatrix stateMatrix, bool visible,
            HashSet<int> exclusiveVisibilityGroupIds)
        {
            if (visible)
            {
                return;
            }

            List<int> requiredGroupIds = stateMatrix.GetVisibilityGroupIds(stateId);
            Log.WriteDebug("Workflow visibility",
                $"Denied {objectType} {objectId} in state {stateId}. Required groups: [{string.Join(", ", requiredGroupIds)}], " +
                $"user groups: [{string.Join(", ", userConfig.User.WorkflowVisibilityGroupIds)}], exclusive groups: [{string.Join(", ", exclusiveVisibilityGroupIds)}]");
        }

        public async Task ConfAddCommentToTicket(string commentText)
        {
            WfComment comment = new()
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            if (dbAcc != null)
            {
                long commentId = await dbAcc.AddCommentToDb(comment);
                if (commentId != 0)
                {
                    await dbAcc.AssignCommentToTicketInDb(ActTicket.Id, commentId);
                }
            }
            ActTicket.Comments.Add(new WfCommentDataHelper(comment) { });
        }

        private async Task<(WorkflowPhases, bool)> FindNewPhase(WorkflowPhases phase, int stateId)
        {
            bool foundNewPhase = false;
            if (apiConnection != null)
            {
                GlobalStateMatrix glbStateMatrix = GlobalStateMatrix.Create();
                await glbStateMatrix.Init(apiConnection, WfTaskType.master);
                bool cont = true;
                while (cont)
                {
                    bool newPhase = MasterStateMatrix.getNextActivePhase(ref phase);
                    if (newPhase)
                    {
                        foundNewPhase = true;
                    }
                    cont = stateId >= glbStateMatrix.GlobalMatrix[phase].LowestEndState && newPhase;
                }
            }
            return (phase, foundNewPhase);
        }

        private void PrepareTicketData()
        {
            if (ActTicket.Sanitize())
            {
                DisplayMessageInUi(null, userConfig.GetText("save_request"), userConfig.GetText("U0001"), true);
            }
            // For an existing ticket, request-task promotion is handled by
            // UpdateRequestTasksFromTicket(), which must compare and persist
            // the actual task state. Do not overwrite that baseline here.
            if (AddTicketMode)
            {
                foreach (WfReqTask reqTask in ActTicket.Tasks)
                {
                    if (reqTask.StateId < ActTicket.StateId)
                    {
                        reqTask.StateId = ActTicket.StateId;
                    }
                }
            }

            if (ActTicket.Deadline == null)
            {
                int? tickDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.TicketDeadline;
                ActTicket.Deadline = tickDeadline != null && tickDeadline > 0 ? DateTime.Now.AddDays((int)tickDeadline) : null;
            }
        }
    }
}

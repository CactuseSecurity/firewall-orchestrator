using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Promotes the request tasks handed to an external ticket system past the planning in the action "Prepare
    /// implementation": they get no implementation tasks, as the external ticket system plans them itself, so nobody
    /// would plan them in FWO and they would hold the ticket in the planning.
    /// </summary>
    public partial class WfHandler
    {
        private const string kCreateImplTasksLogTitle = "Create Implementation Tasks";

        /// <summary>Replaces the promotion of a request task, for unit tests only.</summary>
        internal Func<WfReqTask, int, Task>? ReqTaskPromotionOverride { get; set; }

        /// <summary>
        /// Promotes the request tasks in scope that are handed to an external ticket system to the target state, see
        /// <see cref="SelectRequestTasksOfExternalTicketSystem"/>. Each promotion is commented on the request task and
        /// derives the ticket state anew.
        /// </summary>
        /// <param name="ticket">Ticket whose request tasks are promoted.</param>
        /// <param name="scopeTask">Request task in the request task scope: only this task, a bundled task together with
        /// its bundle once every member has reached the state of this task. Null in the ticket scope.</param>
        /// <param name="targetStateId">State to promote to, see <see cref="CreateImplTasksActionParams"/>.</param>
        /// <returns>The number of promoted request tasks.</returns>
        public async Task<int> PromoteRequestTasksOfExternalTicketSystem(WfTicket ticket, WfReqTask? scopeTask, int targetStateId)
        {
            List<WfReqTask> reqTasks = await SelectRequestTasksOfExternalTicketSystem(ticket, scopeTask, targetStateId);
            foreach (WfReqTask reqTask in reqTasks)
            {
                await PromoteRequestTaskOfExternalTicketSystem(reqTask, targetStateId);
            }
            if (reqTasks.Count > 0)
            {
                Log.WriteInfo(kCreateImplTasksLogTitle, $"Promoted {reqTasks.Count} request tasks of ticket {ticket.Id} handed to an " +
                    $"external ticket system to state {targetStateId}.");
            }
            return reqTasks.Count;
        }

        /// <summary>
        /// The request tasks in scope to promote to the target state: tasks whose management hands them to a selected
        /// external ticket system, that have passed the approval and are below the target state and not yet finished in
        /// the implementation, e.g. not rejected.
        /// </summary>
        /// <param name="ticket">Ticket whose request tasks are checked.</param>
        /// <param name="scopeTask">Request task in the request task scope; null in the ticket scope.</param>
        /// <param name="targetStateId">State to promote to.</param>
        internal async Task<List<WfReqTask>> SelectRequestTasksOfExternalTicketSystem(WfTicket ticket, WfReqTask? scopeTask, int targetStateId)
        {
            SetTicketEnv(ticket);
            List<WfReqTask> candidates = [.. ActTicket.Tasks];
            if (scopeTask != null)
            {
                candidates = TasksOfRequestTaskScope(candidates, scopeTask);
            }
            List<WfReqTask> selected = [];
            foreach (WfReqTask reqTask in candidates.OrderBy(task => task.TaskNumber))
            {
                if (IsReadyForExternalTicketSystemPromotion(reqTask, targetStateId) && await IsHandedToSelectedExternalTicketSystem(reqTask))
                {
                    selected.Add(reqTask);
                }
            }
            return selected;
        }

        /// <summary>
        /// Whether the state of a request task allows the promotion: approved, below the target state and not finished
        /// in the implementation. A target below the end of the planning would not let the ticket leave the planning,
        /// so the task is not promoted then and a warning is logged.
        /// </summary>
        private bool IsReadyForExternalTicketSystemPromotion(WfReqTask reqTask, int targetStateId)
        {
            if (!stateMatrixDict.Matrices.TryGetValue(reqTask.TaskType, out StateMatrix? matrix)
                || reqTask.StateId < matrix.ApprovalLowestEndState
                || reqTask.StateId >= targetStateId
                || reqTask.StateId >= matrix.ImplementationLowestEndState)
            {
                return false;
            }
            if (targetStateId < matrix.PreImplementationEndState)
            {
                Log.WriteWarning(kCreateImplTasksLogTitle, $"Request task {reqTask.Id} is not promoted: the target state {targetStateId} is " +
                    $"below the end state {matrix.PreImplementationEndState} of the phases before the implementation.");
                return false;
            }
            return true;
        }

        /// <summary>Promotes a request task to the target state and comments why.</summary>
        private async Task PromoteRequestTaskOfExternalTicketSystem(WfReqTask reqTask, int targetStateId)
        {
            if (ReqTaskPromotionOverride != null)
            {
                await ReqTaskPromotionOverride(reqTask, targetStateId);
                return;
            }
            SetReqTaskEnv(reqTask);
            await ConfAddCommentToReqTask(userConfig.GetText("U8005"));
            reqTask.StateId = targetStateId;
            await PromoteReqTask(reqTask);
        }
    }
}

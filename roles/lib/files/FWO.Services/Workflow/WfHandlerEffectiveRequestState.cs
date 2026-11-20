using FWO.Basics;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

public partial class WfHandler
{
    /// <summary>
    /// Loads the complete ticket once before implementation-task creation and combines persisted request content
    /// with workflow state that has already changed in this process.
    /// </summary>
    private async Task<WfTicket> LoadImplCreationPreparationTicket(WfTicket runtimeTicket)
    {
        if (dbAcc == null)
        {
            return runtimeTicket;
        }

        WfTicket? persistedTicket = await dbAcc.FetchTicket(
            runtimeTicket.Id, GetOwnerIdsForFiltering(), GetTicketFilter());
        if (persistedTicket == null || persistedTicket.Id != runtimeTicket.Id)
        {
            throw new EffectiveRequestStateMaterializationException(
                EffectiveRequestStateFailureCategory.ReadbackFailure);
        }

        Dictionary<long, WfReqTask> persistedTasksById = persistedTicket.Tasks
            .Where(task => task.Id > 0)
            .GroupBy(task => task.Id)
            .ToDictionary(group => group.Key, group => group.Single());
        HashSet<long> usedPersistedTaskIds = [];
        List<WfReqTask> preparationTasks = [];
        foreach (WfReqTask runtimeTask in runtimeTicket.Tasks)
        {
            if (!CanReloadForImplCreation(runtimeTask))
            {
                preparationTasks.Add(runtimeTask);
                continue;
            }
            if (!persistedTasksById.TryGetValue(runtimeTask.Id, out WfReqTask? persistedTask))
            {
                throw new EffectiveRequestStateMaterializationException(
                    EffectiveRequestStateFailureCategory.ReadbackFailure);
            }
            CopyImplCreationRuntimeState(persistedTask, runtimeTask);
            preparationTasks.Add(persistedTask);
            usedPersistedTaskIds.Add(persistedTask.Id);
        }
        preparationTasks.AddRange(persistedTicket.Tasks.Where(task => task.Id <= 0
            || !usedPersistedTaskIds.Contains(task.Id)));
        return new WfTicket(persistedTicket)
        {
            StateId = runtimeTicket.StateId,
            Tasks = preparationTasks
        };
    }

    /// <summary>Returns whether a task has a persisted representation that the action preparation load must use.</summary>
    private static bool CanReloadForImplCreation(WfReqTask task)
    {
        return task.Id > 0 && task.TicketId > 0
            && task.TaskType != WfTaskType.new_interface.ToString();
    }

    /// <summary>
    /// Materializes selected request tasks and returns their verified readback instances in caller order.
    /// </summary>
    /// <param name="ticket">The complete persisted ticket containing the selected tasks and their references.</param>
    /// <param name="taskIds">Request-task IDs about to become operational.</param>
    /// <returns>The selected tasks from the verified ticket.</returns>
    public async Task<List<WfReqTask>> PrepareRequestTasksForExecution(WfTicket ticket,
        IReadOnlyCollection<long> taskIds)
    {
        WfTicket effectiveTicket = dbAcc == null
            ? ticket
            : await dbAcc.MaterializeEffectiveRequestState(ticket, taskIds);
        Dictionary<long, WfReqTask> tasksById = effectiveTicket.Tasks
            .Where(task => task.Id > 0)
            .GroupBy(task => task.Id)
            .ToDictionary(group => group.Key, group => group.Single());
        return taskIds.Select(taskId => tasksById.TryGetValue(taskId, out WfReqTask? task)
                ? task
                : throw new InvalidOperationException("Prepared request task is missing from the ticket readback."))
            .ToList();
    }

    /// <summary>
    /// Creates a manual implementation-task draft from the effective request state installed by the action boundary.
    /// </summary>
    /// <param name="requestTask">The request task or transient bundle used by the manual editor.</param>
    /// <returns>A prepared draft, or null after a displayed preparation failure.</returns>
    public async Task<WfImplTask?> CreateImplementationTaskDraft(WfReqTask requestTask)
    {
        try
        {
            WfReqTask effectiveTask = requestTask;
            if (requestTask.Id > 0 && !ReadOnlyMode
                && CanActOnReqTaskInCurrentPhase(ActReqTask))
            {
                List<long> taskIds = ManualDraftTaskIds(requestTask);
                if (taskIds.All(taskId => ActTicket.Tasks.Any(task => task.Id == taskId)))
                {
                    List<WfReqTask> effectiveTasks = [.. taskIds.Select(taskId =>
                        ActTicket.Tasks.Single(task => task.Id == taskId))];
                    effectiveTask = effectiveTasks.Count > 1
                        ? CreateMergedBundleRequestTask(effectiveTasks)
                        : effectiveTasks.Single();
                }
            }
            return new WfImplTask(effectiveTask, false);
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("auto_create_impltasks"), "", true);
            return null;
        }
    }

    /// <summary>
    /// Selects the active-ticket constituents represented by a manual request-task or bundle draft.
    /// </summary>
    private List<long> ManualDraftTaskIds(WfReqTask requestTask)
    {
        string bundleId = requestTask.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            return [requestTask.Id];
        }
        return [.. ActTicket.Tasks.Where(task => task.StateId == requestTask.StateId
                && task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId) == bundleId)
            .Select(task => task.Id)];
    }

    /// <summary>
    /// Restores in-process state transitions onto a task refreshed from persisted effective content.
    /// </summary>
    private static void CopyImplCreationRuntimeState(WfReqTask target, WfReqTask source)
    {
        target.StateId = source.StateId;
        target.Start = source.Start;
        target.Stop = source.Stop;
        target.CurrentHandler = source.CurrentHandler;
        target.RecentHandler = source.RecentHandler;
        target.AssignedGroup = source.AssignedGroup;
    }
}

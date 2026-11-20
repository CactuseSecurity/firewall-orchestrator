using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

public partial class WfHandler
{
    private WfTicket? latestImplCreationDetailTicket;

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
    /// Creates a manual implementation-task draft from verified persisted request state.
    /// </summary>
    /// <param name="requestTask">The request task or transient bundle used by the manual editor.</param>
    /// <returns>A prepared draft, or null after a displayed preparation failure.</returns>
    public async Task<WfImplTask?> CreateImplementationTaskDraft(WfReqTask requestTask)
    {
        try
        {
            WfReqTask effectiveTask = requestTask;
            if (dbAcc != null && requestTask.Id > 0 && !ReadOnlyMode
                && CanActOnReqTaskInCurrentPhase(ActReqTask))
            {
                List<long> taskIds = ManualDraftTaskIds(requestTask);
                latestImplCreationDetailTicket = null;
                List<WfReqTask> detailedTasks = [];
                foreach (long taskId in taskIds)
                {
                    WfReqTask selected = ActTicket.Tasks.Single(task => task.Id == taskId);
                    detailedTasks.Add(await LoadReqTaskDetailsForImplCreation(selected));
                }
                WfTicket preparationTicket = BuildImplCreationPreparationTicket(detailedTasks);
                List<WfReqTask> preparedTasks = await PrepareRequestTasksForExecution(preparationTicket, taskIds);
                effectiveTask = preparedTasks.Count > 1
                    ? CreateMergedBundleRequestTask(preparedTasks)
                    : preparedTasks.Single();
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
    /// Selects the persisted constituents represented by a manual request-task or bundle draft.
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
    /// Combines a full persisted read with transient task state used by the current creation run.
    /// </summary>
    private WfTicket BuildImplCreationPreparationTicket(List<WfReqTask> detailedTasks)
    {
        WfTicket source = latestImplCreationDetailTicket ?? ActTicket;
        WfTicket preparationTicket = new(source) { Tasks = [.. source.Tasks] };
        foreach (WfReqTask task in detailedTasks)
        {
            int index = preparationTicket.Tasks.FindIndex(candidate => candidate.Id == task.Id);
            if (index >= 0)
            {
                preparationTicket.Tasks[index] = task;
            }
            else
            {
                preparationTicket.Tasks.Add(task);
            }
        }
        return preparationTicket;
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

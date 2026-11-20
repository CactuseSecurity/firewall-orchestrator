using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves persisted ticket-local object references without inspecting unrelated siblings.</summary>
internal sealed class TicketObjectReferenceIndex
{
    private readonly WfReqTask accessTask;
    private readonly IReadOnlyDictionary<long, List<WfReqTask>> candidatesById;

    private TicketObjectReferenceIndex(
        WfReqTask accessTask,
        IReadOnlyDictionary<long, List<WfReqTask>> candidatesById)
    {
        this.accessTask = accessTask;
        this.candidatesById = candidatesById;
    }

    /// <summary>Creates a lazy lookup after validating only the access-task container contract.</summary>
    internal static TicketObjectReferenceIndex Create(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks)
    {
        if (accessTask == null || accessTask.Id <= 0 || accessTask.TicketId <= 0 ||
            accessTask.TaskNumber <= 0 || accessTask.Elements == null || ticketTasks == null)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }

        List<WfReqTask> tasks = [.. ticketTasks.Where(task => task != null)];
        if (tasks.Count(task => task.Id == accessTask.Id && task.TicketId == accessTask.TicketId) != 1)
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }

        Dictionary<long, List<WfReqTask>> candidates = tasks
            .Where(task => task.Id > 0)
            .GroupBy(task => task.Id)
            .ToDictionary(group => group.Key, group => group.ToList());
        return new TicketObjectReferenceIndex(accessTask, candidates);
    }

    /// <summary>Resolves and validates the one task named by an element's persisted reference.</summary>
    internal WfReqTask Resolve(
        WfReqElement element,
        CreationRuleNetworkResolutionFailureCategory unresolvedCategory)
    {
        if (element == null || element.ObjectTaskId is not > 0 ||
            !candidatesById.TryGetValue(element.ObjectTaskId.Value, out List<WfReqTask>? matches) ||
            matches.Count != 1)
        {
            throw Failure(unresolvedCategory);
        }

        WfReqTask candidate = matches[0];
        if (candidate.TicketId != accessTask.TicketId || candidate.TaskNumber <= 0 ||
            candidate.Elements == null || !IsSupportedNetworkTask(candidate))
        {
            throw Failure(unresolvedCategory);
        }
        if (!IsBefore(candidate))
        {
            throw Failure(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid);
        }
        return candidate;
    }

    /// <summary>Checks whether one candidate is strictly ordered before the access task.</summary>
    internal bool IsBefore(WfReqTask candidate) =>
        candidate.TaskNumber < accessTask.TaskNumber ||
        candidate.TaskNumber == accessTask.TaskNumber && candidate.Id < accessTask.Id;

    /// <summary>Allows only network object and network-group request tasks.</summary>
    private static bool IsSupportedNetworkTask(WfReqTask task)
    {
        bool isGroupTask = task.TaskType == WfTaskType.group_create.ToString() ||
            task.TaskType == WfTaskType.group_modify.ToString() ||
            task.TaskType == WfTaskType.group_delete.ToString();
        if (!isGroupTask && !WfObjectTaskHelper.IsObjectTask(task.TaskType))
        {
            return false;
        }
        return !WfObjectTaskHelper.OrdersServiceObject(task) &&
            !task.Elements.Any(element => element.Field == ElemFieldType.time.ToString());
    }

    /// <summary>Creates a typed reference-resolution failure.</summary>
    private static NetworkResolutionException Failure(
        CreationRuleNetworkResolutionFailureCategory category) => new(category);
}

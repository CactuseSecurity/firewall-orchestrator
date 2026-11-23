using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

/// <summary>
/// Plans and applies current values for every exact ticket-local object reference in a ticket.
/// </summary>
public static class RequestSnapshotRefresher
{
    /// <summary>
    /// Builds a non-mutating ticket-wide refresh plan.
    /// </summary>
    /// <param name="ticket">Complete ticket whose request elements are inspected.</param>
    /// <returns>Ordered replacements for every stale exact reference.</returns>
    public static RequestSnapshotRefreshPlan CreatePlan(WfTicket ticket)
    {
        ValidateElementIdentities(ticket);
        Dictionary<long, WfReqTask> tasksById = ticket.Tasks
            .Where(task => task.Id > 0)
            .ToDictionary(task => task.Id);
        List<WfReqElement> replacements = [];
        List<long> changedTaskIds = [];
        HashSet<long> changedTasks = [];

        foreach (WfReqTask task in ticket.Tasks)
        {
            foreach (WfReqElement target in task.Elements.Where(element => element.ObjectTaskId is > 0))
            {
                WfReqElement? replacement = CreateReplacement(target, tasksById);
                if (replacement == null || SnapshotValuesEqual(target, replacement))
                {
                    continue;
                }
                if (target.Id <= 0)
                {
                    throw new InvalidDataException("A stale request snapshot is not persisted.");
                }
                replacements.Add(replacement);
                if (changedTasks.Add(task.Id))
                {
                    changedTaskIds.Add(task.Id);
                }
            }
        }
        return new RequestSnapshotRefreshPlan(replacements, changedTaskIds);
    }

    /// <summary>
    /// Refuses malformed in-memory graphs which could address one persisted element more than once.
    /// </summary>
    private static void ValidateElementIdentities(WfTicket ticket)
    {
        HashSet<long> persistedElementIds = [];
        foreach (WfReqTask task in ticket.Tasks)
        {
            foreach (WfReqElement element in task.Elements)
            {
                if (element.TaskId != task.Id)
                {
                    throw new InvalidDataException("A request element does not belong to its containing task.");
                }
                if (element.Id > 0 && !persistedElementIds.Add(element.Id))
                {
                    throw new InvalidDataException("A persisted request element occurs more than once in the ticket.");
                }
            }
        }
    }

    /// <summary>
    /// Applies a proven refresh plan to the existing in-memory element instances.
    /// </summary>
    /// <param name="ticket">Ticket that produced the plan.</param>
    /// <param name="plan">Plan already persisted successfully.</param>
    public static void ApplyPlan(WfTicket ticket, RequestSnapshotRefreshPlan plan)
    {
        Dictionary<(long TaskId, long ElementId), WfReqElement> elements = ticket.Tasks
            .SelectMany(task => task.Elements)
            .Where(element => element.Id > 0)
            .ToDictionary(element => (element.TaskId, element.Id));
        foreach (WfReqElement replacement in plan.Replacements)
        {
            if (!elements.TryGetValue((replacement.TaskId, replacement.Id), out WfReqElement? target))
            {
                throw new InvalidDataException("A planned request snapshot no longer exists.");
            }
            WfObjectTaskHelper.CopyRequestedStateSnapshot(target, replacement);
        }
    }

    private static WfReqElement? CreateReplacement(WfReqElement target,
        IReadOnlyDictionary<long, WfReqTask> tasksById)
    {
        long objectTaskId = target.ObjectTaskId.GetValueOrDefault();
        if (!tasksById.TryGetValue(objectTaskId, out WfReqTask? referencedTask))
        {
            throw new InvalidDataException("A ticket-local request snapshot references a missing task.");
        }
        if (IsGroupTask(referencedTask.TaskType))
        {
            return null;
        }
        if (!WfObjectTaskHelper.IsObjectTask(referencedTask.TaskType)
            || !WfObjectTaskHelper.HasValidElementStructure(referencedTask))
        {
            throw new InvalidDataException("A ticket-local request snapshot references an invalid object task.");
        }
        WfReqElement source = WfObjectTaskHelper.GetRequestedElement(referencedTask)
            ?? throw new InvalidDataException("A referenced object task has no requested state.");
        if (!FieldsMatch(target.Field, source.Field))
        {
            throw new InvalidDataException("A ticket-local request snapshot references an incompatible object field.");
        }
        WfReqElement replacement = new(target);
        WfObjectTaskHelper.CopyRequestedStateSnapshot(replacement, source);
        return replacement;
    }

    private static bool IsGroupTask(string taskType)
    {
        return taskType == WfTaskType.group_create.ToString()
            || taskType == WfTaskType.group_modify.ToString()
            || taskType == WfTaskType.group_delete.ToString();
    }

    private static bool FieldsMatch(string targetField, string sourceField)
    {
        return targetField == sourceField
            || targetField is nameof(ElemFieldType.source) or nameof(ElemFieldType.destination)
                && sourceField is nameof(ElemFieldType.source) or nameof(ElemFieldType.destination);
    }

    private static bool SnapshotValuesEqual(WfReqElement left, WfReqElement right)
    {
        return Equals(left.GetIpRange(), right.GetIpRange())
            && left.Port == right.Port
            && left.PortEnd == right.PortEnd
            && left.ProtoId == right.ProtoId
            && string.Equals(left.GroupName, right.GroupName, StringComparison.Ordinal)
            && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && SamePointInTime(left.StartTime, right.StartTime)
            && SamePointInTime(left.EndTime, right.EndTime);
    }

    private static bool SamePointInTime(DateTime? left, DateTime? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }
        return left.Value.ToUniversalTime() == right.Value.ToUniversalTime();
    }
}

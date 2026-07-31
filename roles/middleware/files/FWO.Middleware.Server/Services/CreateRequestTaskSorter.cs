using FWO.Data.Workflow;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Orders request tasks for persistence using the same priority model as firewall change requests.
/// </summary>
public static class CreateRequestTaskSorter
{
    private const int DefaultPriority = 7;

    /// <summary>
    /// Returns the tasks in save order and renumbers them if sorting is enabled.
    /// </summary>
    public static List<WfReqTask> OrderForSave(IEnumerable<WfReqTask> tasks, bool sortTasks, CreateRequestTaskSortConfig? sortConfig = null)
    {
        if (!sortTasks)
        {
            return [.. tasks];
        }

        sortConfig ??= new CreateRequestTaskSortConfig();
        List<TaskEntry> entries = [.. tasks.SelectMany((task, index) => ExpandTask(task, index, sortConfig))];
        List<WfReqTask> orderedTasks = [.. entries
            .OrderBy(entry => entry.Priority)
            .ThenBy(entry => entry.OriginalIndex)
            .ThenBy(entry => entry.SegmentIndex)
            .Select(entry => entry.Task)];

        RenumberTasks(orderedTasks);
        return orderedTasks;
    }

    private static IEnumerable<TaskEntry> ExpandTask(WfReqTask task, int originalIndex, CreateRequestTaskSortConfig sortConfig)
    {
        if (task.TaskType != WfTaskType.group_modify.ToString())
        {
            yield return new TaskEntry(task, originalIndex, 0, GetPriority(task, sortConfig));
            yield break;
        }

        List<WfReqElement> addElements = [.. task.Elements.Where(IsAddMemberElement)];
        List<WfReqElement> removeElements = [.. task.Elements.Where(IsRemoveMemberElement)];
        if (sortConfig.AllowTaskSplit && addElements.Count > 0 && removeElements.Count > 0)
        {
            yield return new TaskEntry(CloneTask(task, addElements), originalIndex, 0, sortConfig.GroupModifyAddPriority);
            yield return new TaskEntry(CloneTask(task, removeElements), originalIndex, 1, sortConfig.GroupModifyRemovePriority);
            yield break;
        }

        yield return new TaskEntry(task, originalIndex, 0, GetPriority(task, sortConfig));
    }

    private static int GetPriority(WfReqTask task, CreateRequestTaskSortConfig sortConfig)
    {
        bool hasAddElements = ContainsAddElements(task);
        bool hasRemoveElements = ContainsRemoveElements(task);
        return task.TaskType switch
        {
            var taskType when taskType == WfTaskType.group_create.ToString() => sortConfig.GroupCreatePriority,
            var taskType when taskType == WfTaskType.group_modify.ToString() && hasAddElements && hasRemoveElements => Math.Min(sortConfig.GroupModifyAddPriority, sortConfig.GroupModifyRemovePriority),
            var taskType when taskType == WfTaskType.group_modify.ToString() && hasAddElements => sortConfig.GroupModifyAddPriority,
            var taskType when taskType == WfTaskType.access.ToString() => sortConfig.AccessPriority,
            // These priorities are currently unused by createRequest; they become relevant
            // once the API can emit rule_modify and rule_delete tasks as well.
            var taskType when taskType == WfTaskType.rule_modify.ToString() => sortConfig.RuleModifyPriority,
            var taskType when taskType == WfTaskType.rule_delete.ToString() => sortConfig.RuleDeletePriority,
            var taskType when taskType == WfTaskType.group_modify.ToString() && hasRemoveElements => sortConfig.GroupModifyRemovePriority,
            var taskType when taskType == WfTaskType.group_delete.ToString() => sortConfig.GroupDeletePriority,
            _ => DefaultPriority
        };
    }

    private static bool ContainsAddElements(WfReqTask task)
    {
        return task.Elements.Any(IsAddMemberElement);
    }

    private static bool ContainsRemoveElements(WfReqTask task)
    {
        return task.Elements.Any(IsRemoveMemberElement);
    }

    private static bool IsAddMemberElement(WfReqElement element)
    {
        return string.Equals(element.RequestAction, RequestAction.create.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(element.RequestAction, RequestAction.addAfterCreation.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemoveMemberElement(WfReqElement element)
    {
        return string.Equals(element.RequestAction, RequestAction.delete.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static WfReqTask CloneTask(WfReqTask task, List<WfReqElement> elements)
    {
        return new WfReqTask(task)
        {
            Elements = [.. elements],
            Approvals = [.. task.Approvals],
            Owners = [.. task.Owners],
            Comments = [.. task.Comments],
            RemovedElements = [.. task.RemovedElements],
            NewOwners = [.. task.NewOwners],
            RemovedOwners = [.. task.RemovedOwners]
        };
    }

    private static void RenumberTasks(List<WfReqTask> tasks)
    {
        int taskNumber = 1;
        foreach (WfReqTask task in tasks)
        {
            task.TaskNumber = taskNumber++;
        }
    }

    private sealed record TaskEntry(WfReqTask Task, int OriginalIndex, int SegmentIndex, int Priority);
}

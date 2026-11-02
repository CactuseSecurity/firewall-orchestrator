using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

/// <summary>Validates and orders ticket tasks used to resolve effective network state.</summary>
internal static class TicketNetworkStateProjector
{
    /// <summary>Maximum supported nesting level for ticket-defined network groups.</summary>
    internal const int kMaximumGroupDepth = 100;

    /// <summary>Validates ticket scope and returns deterministic task order.</summary>
    internal static IReadOnlyList<WfReqTask> Project(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks)
    {
        if (!IsValidAccessTask(accessTask) || ticketTasks == null)
        {
            throw new NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
        List<WfReqTask> tasks = [.. ticketTasks];
        if (!HasValidTicketScope(tasks, accessTask))
        {
            throw new NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
        return tasks.OrderBy(task => task.TaskNumber).ThenBy(task => task.Id).ToList().AsReadOnly();
    }

    /// <summary>Checks the required identity and collection fields of the access task.</summary>
    private static bool IsValidAccessTask(WfReqTask? accessTask) =>
        accessTask != null && accessTask.Id > 0 && accessTask.TicketId > 0 &&
        accessTask.ManagementId is > 0 && accessTask.Elements != null;

    /// <summary>Checks task identity, shared scope, access-task presence, and deterministic order keys.</summary>
    private static bool HasValidTicketScope(List<WfReqTask> tasks, WfReqTask accessTask) =>
        tasks.All(task => IsValidTicketTask(task, accessTask)) &&
        tasks.Count(task => task.Id == accessTask.Id) == 1 &&
        tasks.GroupBy(task => (task.TaskNumber, task.Id)).All(group => group.Count() == 1);

    /// <summary>Checks one ticket task against the requested ticket and management.</summary>
    private static bool IsValidTicketTask(WfReqTask? task, WfReqTask accessTask) =>
        task != null && task.Id > 0 && task.TaskNumber > 0 &&
        task.TicketId == accessTask.TicketId && task.ManagementId == accessTask.ManagementId &&
        task.Elements != null;

    /// <summary>Checks whether a candidate task is strictly ordered before the access task.</summary>
    internal static bool IsBefore(WfReqTask candidate, WfReqTask accessTask) =>
        candidate.TaskNumber < accessTask.TaskNumber ||
        candidate.TaskNumber == accessTask.TaskNumber && candidate.Id < accessTask.Id;
}

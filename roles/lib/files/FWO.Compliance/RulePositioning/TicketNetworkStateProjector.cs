using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

internal static class TicketNetworkStateProjector
{
    internal const int kMaximumGroupDepth = 100;

    /// <summary>Validates ticket scope and returns deterministic task order.</summary>
    internal static IReadOnlyList<WfReqTask> Project(
        WfReqTask accessTask,
        IReadOnlyCollection<WfReqTask> ticketTasks)
    {
        if (accessTask == null || ticketTasks == null || accessTask.Id <= 0 || accessTask.TicketId <= 0 ||
            accessTask.ManagementId is not > 0 || accessTask.Elements == null)
        {
            throw new NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
        List<WfReqTask> tasks = [.. ticketTasks];
        if (tasks.Any(task => task == null || task.Id <= 0 || task.TaskNumber <= 0 ||
            task.TicketId != accessTask.TicketId ||
            task.ManagementId != accessTask.ManagementId || task.Elements == null) ||
            tasks.Count(task => task.Id == accessTask.Id) != 1 ||
            tasks.GroupBy(task => (task.TaskNumber, task.Id)).Any(group => group.Count() != 1))
        {
            throw new NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory.InvalidInput);
        }
        return tasks.OrderBy(task => task.TaskNumber).ThenBy(task => task.Id).ToList().AsReadOnly();
    }

    /// <summary>Checks whether a candidate task is strictly ordered before the access task.</summary>
    internal static bool IsBefore(WfReqTask candidate, WfReqTask accessTask) =>
        candidate.TaskNumber < accessTask.TaskNumber ||
        candidate.TaskNumber == accessTask.TaskNumber && candidate.Id < accessTask.Id;
}

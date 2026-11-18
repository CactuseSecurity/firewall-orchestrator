using FWO.Data.Workflow;

namespace FWO.Compliance.RulePositioning;

/// <summary>Validates and deterministically orders ticket tasks used by positioning input resolvers.</summary>
internal static class TicketRequestStateProjector
{
    /// <summary>Maximum supported nesting level for ticket-defined groups.</summary>
    internal const int kMaximumGroupDepth = 100;

    /// <summary>Validates ticket scope and returns tasks in deterministic protocol order.</summary>
    internal static bool TryProject(
        WfReqTask? accessTask,
        IReadOnlyCollection<WfReqTask>? ticketTasks,
        out IReadOnlyList<WfReqTask> orderedTasks)
    {
        orderedTasks = Array.Empty<WfReqTask>();
        if (!IsValidAccessTask(accessTask) || ticketTasks == null)
        {
            return false;
        }
        List<WfReqTask> tasks = [.. ticketTasks];
        if (!HasValidTicketScope(tasks, accessTask!))
        {
            return false;
        }
        orderedTasks = tasks.OrderBy(task => task.TaskNumber).ThenBy(task => task.Id).ToList().AsReadOnly();
        return true;
    }

    /// <summary>Checks whether a candidate task is strictly ordered before the access task.</summary>
    internal static bool IsBefore(WfReqTask candidate, WfReqTask accessTask) =>
        candidate.TaskNumber < accessTask.TaskNumber ||
        candidate.TaskNumber == accessTask.TaskNumber && candidate.Id < accessTask.Id;

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
}

using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Ui.Services;

/// <summary>Resolves the request tasks that form one editable approval bundle.</summary>
internal static class ApprovalTaskBundleHelper
{
    /// <summary>
    /// Groups the tasks into the bundles displayed as one, each ordered by task id: if bundled tasks are displayed
    /// as one, tasks with the same bundle id and state form one group; object and group tasks and unbundled tasks
    /// always form a group of their own.
    /// </summary>
    /// <param name="tasks">The request tasks to group.</param>
    /// <param name="userConfig">The user configuration holding the bundling settings.</param>
    internal static IEnumerable<List<WfReqTask>> GroupTasks(IEnumerable<WfReqTask> tasks, UserConfig userConfig)
    {
        if (!userConfig.ReqDisplayBundledTasksAsOne || !userConfig.ReqConsiderBundling)
        {
            return tasks.Select(task => new List<WfReqTask> { task });
        }

        return tasks.GroupBy(task =>
        {
            if (WfObjectTaskHelper.IsManagementObjectTask(task.TaskType))
            {
                return $"task:{task.Id}";
            }

            string bundleId = task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
            return string.IsNullOrWhiteSpace(bundleId)
                ? $"task:{task.Id}"
                : $"bundle:{bundleId}:state:{task.StateId}";
        }).Select(group => group.OrderBy(task => task.Id).ToList());
    }

    /// <summary>
    /// Returns the request task a new implementation task of a group is created from: a single task itself, a bundle
    /// displayed as one its merged request task, which keeps the id of the first task, like automatically created ones.
    /// </summary>
    /// <param name="taskGroup">The request tasks of the group, at least one.</param>
    internal static WfReqTask CreationRequestTask(IReadOnlyList<WfReqTask> taskGroup) => taskGroup.Count > 1
        ? WfHandler.CreateMergedBundleRequestTask([.. taskGroup])
        : taskGroup[0];

    /// <summary>Returns all tasks that must receive the same bundle-level edit.</summary>
    internal static IEnumerable<WfReqTask> GetTasksToUpdate(
        IEnumerable<WfReqTask> ticketTasks, WfReqTask task, UserConfig userConfig)
    {
        string bundleId = task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
        if (!userConfig.ReqDisplayBundledTasksAsOne
            || !userConfig.ReqConsiderBundling
            || string.IsNullOrWhiteSpace(bundleId))
        {
            return [task];
        }

        return ticketTasks.Where(candidate =>
            candidate.TaskType == task.TaskType
            && candidate.StateId == task.StateId
            && candidate.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId) == bundleId).ToList();
    }
}

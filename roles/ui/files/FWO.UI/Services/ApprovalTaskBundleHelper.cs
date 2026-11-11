using FWO.Config.Api;
using FWO.Data.Workflow;

namespace FWO.Ui.Services;

/// <summary>Resolves the request tasks that form one editable approval bundle.</summary>
internal static class ApprovalTaskBundleHelper
{
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

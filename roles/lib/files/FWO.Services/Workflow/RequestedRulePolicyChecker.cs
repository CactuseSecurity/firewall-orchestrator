using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public interface IRequestedRulePolicyChecker
    {
        Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks);
    }
}

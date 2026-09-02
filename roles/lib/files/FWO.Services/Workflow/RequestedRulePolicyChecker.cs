using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Config.Api;

namespace FWO.Services.Workflow
{
    public interface IRequestedRulePolicyChecker
    {
        Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks);
    }

    public interface IRequestedRulePolicyCheckerFactory
    {
        IRequestedRulePolicyChecker Create(UserConfig userConfig, ApiConnection apiConnection);
    }
}

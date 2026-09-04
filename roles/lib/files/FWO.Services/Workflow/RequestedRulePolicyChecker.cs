using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data.Middleware;
using FWO.Middleware.Client;

namespace FWO.Services.Workflow
{
    public interface IRequestedRulePolicyChecker
    {
        /// <summary>
        /// Evaluates the requested workflow rules against the selected compliance policies.
        /// </summary>
        Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks);
    }

    public interface IRequestedRulePolicyCheckerFactory
    {
        /// <summary>
        /// Creates a policy checker for the supplied user configuration and API connection.
        /// </summary>
        IRequestedRulePolicyChecker Create(UserConfig userConfig, ApiConnection apiConnection, MiddlewareClient? middlewareClient = null);
    }
}

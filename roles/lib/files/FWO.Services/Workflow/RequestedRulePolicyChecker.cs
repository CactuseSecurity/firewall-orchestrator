using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data.Middleware;
using FWO.Middleware.Client;

namespace FWO.Services.Workflow
{
    /// <summary>Resolves request-visible Flow groups for workflow policy checks.</summary>
    public interface IFlowGroupResolver
    {
        /// <summary>Resolves the explicitly requested Flow groups and members.</summary>
        Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(FlowGroupResolutionParameters parameters);
    }

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

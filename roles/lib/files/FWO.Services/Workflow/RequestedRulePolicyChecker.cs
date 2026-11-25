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
        Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(
            FlowGroupResolutionParameters parameters,
            CancellationToken cancellationToken = default);
    }

    public interface IRequestedRulePolicyChecker
    {
        /// <summary>
        /// Evaluates the requested workflow rules against the selected compliance policies.
        /// </summary>
        Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks);

        /// <summary>
        /// Evaluates the requested workflow rules against the selected compliance policies and returns the result
        /// with the violation reasons per checked request task. Tasks that are not checked (e.g. deletions) are not contained.
        /// </summary>
        Task<IReadOnlyDictionary<long, RequestTaskComplianceResult>> CheckRequestTasks(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
        {
            return Task.FromResult<IReadOnlyDictionary<long, RequestTaskComplianceResult>>(new Dictionary<long, RequestTaskComplianceResult>());
        }
    }

    /// <summary>Compliance result of one request task.</summary>
    /// <param name="TaskId">Id of the checked request task.</param>
    /// <param name="IsCompliant">Whether the task complies with all selected policies.</param>
    /// <param name="Reasons">Reasons of the violations; empty if compliant.</param>
    public sealed record RequestTaskComplianceResult(long TaskId, bool IsCompliant, IReadOnlyList<string> Reasons);

    public interface IRequestedRulePolicyCheckerFactory
    {
        /// <summary>
        /// Creates a policy checker for the supplied user configuration and API connection.
        /// </summary>
        IRequestedRulePolicyChecker Create(UserConfig userConfig, ApiConnection apiConnection, MiddlewareClient? middlewareClient = null);
    }
}

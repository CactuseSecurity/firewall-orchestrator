using FWO.Api.Client;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Resolves the target managements of object tasks for the workflow REST endpoints.
/// </summary>
/// <remarks>
/// Runs with the shared internal API connection as middleware-server, so every sub-management and the
/// provisioning settings are found regardless of the managements visible to the caller.
/// </remarks>
public sealed class ImplTaskTargetService
{
    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    /// <param name="apiConnection">Shared internal API connection, which runs as middleware-server.</param>
    public ImplTaskTargetService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Returns the target managements of an object task.
    /// </summary>
    /// <param name="managementId">Management the request task belongs to.</param>
    /// <param name="isServiceObject">True for services, false for network objects.</param>
    /// <param name="filter">Optional response filter; null applies no restriction.</param>
    /// <param name="visibleManagementIds">Managements visible to the caller, or null when the caller sees every
    /// management. Targets outside this set are returned without name, before the filter is applied.</param>
    /// <returns>The matching target managements.</returns>
    public async Task<GetImplTaskTargetsResponse> GetImplTaskTargetsAsync(int managementId, bool isServiceObject, ImplTaskTargetFilter? filter,
        IReadOnlySet<int>? visibleManagementIds = null)
    {
        List<Management> targets = await new ImplTaskManagementResolver(apiConnection).ResolveObjectTargets(managementId, isServiceObject);
        return new GetImplTaskTargetsResponse
        {
            Targets = [.. targets
                .Select(target => new ImplTaskTargetResponse
                {
                    Id = target.Id,
                    Name = visibleManagementIds == null || visibleManagementIds.Contains(target.Id) ? target.Name : string.Empty
                })
                .Where(target => MatchesFilter(target, filter))]
        };
    }

    private static bool MatchesFilter(ImplTaskTargetResponse target, ImplTaskTargetFilter? filter)
    {
        return filter == null
            || ((filter.Id == null || filter.Id == target.Id)
                && (filter.Name == null || string.Equals(filter.Name, target.Name, StringComparison.OrdinalIgnoreCase)));
    }
}

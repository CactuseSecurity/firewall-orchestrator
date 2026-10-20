using FWO.Api.Client;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Resolves the target managements of object tasks for the workflow REST endpoints.
/// </summary>
/// <remarks>
/// Runs with the shared internal API connection as middleware-server, so every sub-management and the
/// device specific settings are found regardless of the managements visible to the caller.
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
    /// Returns one page of the target managements of an object task.
    /// </summary>
    /// <remarks>
    /// The targets are computed from the management hierarchy and the device specific settings, so the page cannot
    /// be cut in the GraphQL query; it is cut here after the filter is applied. Targets are ordered by name and
    /// then id after the names hidden from the caller are removed, so the order does not reveal hidden names:
    /// visible targets come first, targets without name follow by id.
    /// </remarks>
    /// <param name="managementId">Management the request task belongs to.</param>
    /// <param name="kind">Kind of the created objects, which selects the device specific setting.</param>
    /// <param name="options">Filter and paging options of the request.</param>
    /// <param name="visibleManagementIds">Managements visible to the caller, or null when the caller sees every
    /// management. Targets outside this set are returned without name, before the filter is applied.</param>
    /// <returns>The matching target managements of the requested page.</returns>
    public async Task<ListPage<ImplTaskTargetResponse>> GetImplTaskTargetsAsync(int managementId, ObjectTaskKind kind,
        GetImplTaskTargetsOptions options, IReadOnlySet<int>? visibleManagementIds = null)
    {
        List<Management> targets = await new ImplTaskManagementResolver(apiConnection).ResolveObjectTargets(managementId, kind);
        int pageSize = options.Limit ?? GetImplTaskTargetsOptions.kMaxLimit;
        List<ImplTaskTargetResponse> pageWithLookahead = [.. targets
            .Select(target => new ImplTaskTargetResponse
            {
                Id = target.Id,
                Name = visibleManagementIds == null || visibleManagementIds.Contains(target.Id) ? target.Name : string.Empty
            })
            .Where(target => MatchesFilter(target, options.Filter))
            .OrderBy(target => string.IsNullOrEmpty(target.Name))
            .ThenBy(target => target.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.Id)
            .Skip(options.Offset ?? 0)
            .Take(pageSize + 1)];
        return ListPage<ImplTaskTargetResponse>.FromLookahead(pageWithLookahead, pageSize);
    }

    /// <summary>
    /// Returns one page of the effective auto plan settings of the global level and the requested managements and
    /// gateways.
    /// </summary>
    /// <remarks>
    /// The settings are inherited along the device specific settings hierarchy, so the page cannot be cut in the
    /// GraphQL query; it is cut here after the filter is applied. The resolver already orders the settings: global
    /// first, then managements and gateways by id. Only ids and two flags are returned, no names.
    /// </remarks>
    /// <param name="deviceIds">Gateways to resolve.</param>
    /// <param name="managementIds">Managements to resolve.</param>
    /// <param name="options">Filter and paging options of the request.</param>
    /// <returns>The matching settings of the requested page.</returns>
    public async Task<ListPage<AutoPlanSettingResponse>> GetAutoPlanSettingsAsync(List<int> deviceIds, List<int> managementIds,
        GetAutoPlanSettingsOptions options)
    {
        List<AutoPlanScopeSettings> settings = await new ImplTaskAutoPlanResolver(apiConnection).Resolve(deviceIds, managementIds);
        int pageSize = options.Limit ?? GetAutoPlanSettingsOptions.kMaxLimit;
        List<AutoPlanSettingResponse> pageWithLookahead = [.. settings
            .Select(setting => new AutoPlanSettingResponse
            {
                ScopeType = setting.ScopeType,
                Id = setting.Id,
                AutoPlanImplementation = setting.AutoPlanImplementation,
                AutoPlanImplementationForRules = setting.AutoPlanImplementationForRules
            })
            .Where(setting => MatchesFilter(setting, options.Filter))
            .Skip(options.Offset ?? 0)
            .Take(pageSize + 1)];
        return ListPage<AutoPlanSettingResponse>.FromLookahead(pageWithLookahead, pageSize);
    }

    private static bool MatchesFilter(AutoPlanSettingResponse setting, AutoPlanSettingsFilter? filter)
    {
        return filter == null
            || ((filter.ScopeType == null || filter.ScopeType == setting.ScopeType)
                && (filter.Id == null || filter.Id == setting.Id)
                && (filter.AutoPlanImplementation == null || filter.AutoPlanImplementation == setting.AutoPlanImplementation)
                && (filter.AutoPlanImplementationForRules == null || filter.AutoPlanImplementationForRules == setting.AutoPlanImplementationForRules));
    }

    private static bool MatchesFilter(ImplTaskTargetResponse target, ImplTaskTargetFilter? filter)
    {
        return filter == null
            || ((filter.Id == null || filter.Id == target.Id)
                && (filter.Name == null || string.Equals(filter.Name, target.Name, StringComparison.OrdinalIgnoreCase)));
    }
}

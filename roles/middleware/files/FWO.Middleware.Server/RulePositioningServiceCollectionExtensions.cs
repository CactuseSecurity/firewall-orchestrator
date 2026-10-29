using FWO.Compliance.RulePositioning;
using FWO.Compliance.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Middleware.Server;

/// <summary>
/// Registers the services used to calculate and persist implementation-task rule positions.
/// </summary>
public static class RulePositioningServiceCollectionExtensions
{
    /// <summary>
    /// Adds the generic and customer-specific rule-positioning strategies and their dependencies.
    /// </summary>
    public static IServiceCollection AddRulePositioning(this IServiceCollection services)
    {
        services.AddSingleton<IRulebaseGraphSnapshotReader, RulebaseGraphSnapshotReader>();
        services.AddSingleton<IDesignatedZoneMatrixReader, DesignatedZoneMatrixReader>();
        services.AddSingleton<ICheckPointGraphPositioningRecipeCatalog, CheckPointGraphPositioningRecipeCatalog>();
        services.AddSingleton<ICreationRuleNetworkInputResolver, CreationRuleNetworkInputResolver>();
        services.AddSingleton<ICheckPointLayerGuardSnapshotReader, CheckPointLayerGuardSnapshotReader>();
        services.AddSingleton<ICheckPointPositioningRecipeCatalog, CheckPointPositioningRecipeCatalog>();
        services.AddSingleton<CheckPointGraphRulePositioningStrategy>();
        services.AddSingleton<CheckPointRulePositioningStrategy>();
        services.AddSingleton<IRulePositioningStrategy>(serviceProvider =>
            serviceProvider.GetRequiredService<CheckPointGraphRulePositioningStrategy>());
        services.AddSingleton<IRulePositioningStrategy>(serviceProvider =>
            serviceProvider.GetRequiredService<CheckPointRulePositioningStrategy>());
        services.AddSingleton<RulePositioningManager>();
        services.AddSingleton<IRulePositioningWorkflowCaller, RulePositioningWorkflowCaller>();

        return services;
    }
}

namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>Immutable Check Point layer-guard zone-pair calculation inputs.</summary>
public sealed record CheckPointLayerGuardZonePairPositioningInput
{
    public CreationRuleNetworkInput NetworkInput { get; }
    public CheckPointLayerGuardSnapshot GuardSnapshot { get; }

    /// <summary>Creates layer-guard zone-pair input without depending on higher-level calculation assemblies.</summary>
    public CheckPointLayerGuardZonePairPositioningInput(
        CreationRuleNetworkInput networkInput,
        CheckPointLayerGuardSnapshot guardSnapshot)
    {
        NetworkInput = networkInput ?? throw new ArgumentNullException(nameof(networkInput));
        GuardSnapshot = guardSnapshot ?? throw new ArgumentNullException(nameof(guardSnapshot));
    }
}

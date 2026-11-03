namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>Immutable Check Point layer-guard zone-pair calculation inputs.</summary>
public sealed record CheckPointLayerGuardZonePairPositioningInput
{
    /// <summary>Gets the normalized creation-rule address ranges.</summary>
    public CreationRuleNetworkInput NetworkInput { get; }
    /// <summary>Gets the fixed-import inline-layer guard snapshot.</summary>
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

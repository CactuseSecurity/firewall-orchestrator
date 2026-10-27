namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>Immutable KfW-specific Check Point calculation inputs.</summary>
public sealed record KfwCheckPointPositioningInput
{
    public CreationRuleNetworkInput NetworkInput { get; }
    public CheckPointLayerGuardSnapshot GuardSnapshot { get; }

    /// <summary>Creates customer input without depending on higher-level calculation assemblies.</summary>
    public KfwCheckPointPositioningInput(
        CreationRuleNetworkInput networkInput,
        CheckPointLayerGuardSnapshot guardSnapshot)
    {
        NetworkInput = networkInput ?? throw new ArgumentNullException(nameof(networkInput));
        GuardSnapshot = guardSnapshot ?? throw new ArgumentNullException(nameof(guardSnapshot));
    }
}

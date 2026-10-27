namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>
/// Couples Phase 1 calculation inputs with the immutable Check Point snapshot used by blocks.
/// </summary>
public sealed record CheckPointPositioningContext(
    RulePositioningContext CalculationContext,
    RulebaseGraphSnapshot Snapshot,
    KfwCheckPointPositioningInput? KfwInput = null);

using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Reads immutable Check Point inline-layer guard semantics.</summary>
public interface ICheckPointLayerGuardSnapshotReader
{
    /// <summary>Loads inline-layer guards at the graph's exact successful import.</summary>
    Task<PositioningResult<CheckPointLayerGuardSnapshot, CheckPointLayerGuardReadFailureCategory>> ReadAsync(
        RulebaseGraphSnapshot graph,
        long importId,
        CancellationToken cancellationToken);
}

/// <summary>Provides fixed caller-safe diagnostics for layer-guard reader failures.</summary>
public static class CheckPointLayerGuardReadFailureDiagnostics
{
    /// <summary>Maps a layer-guard failure category to fixed diagnostic text.</summary>
    public static string GetDiagnostic(this CheckPointLayerGuardReadFailureCategory category) => category switch
    {
        CheckPointLayerGuardReadFailureCategory.InvalidInput => "The layer-guard request is invalid.",
        CheckPointLayerGuardReadFailureCategory.ScopeMismatch => "The layer-guard scope does not match the rulebase graph.",
        CheckPointLayerGuardReadFailureCategory.InvalidGuardData => "The imported layer-guard data is invalid.",
        _ => "The imported layer guards could not be loaded."
    };
}

/// <summary>Stable failure categories for Check Point guard reads.</summary>
public enum CheckPointLayerGuardReadFailureCategory
{
    InvalidInput,
    ScopeMismatch,
    InvalidGuardData,
    QueryFailed
}

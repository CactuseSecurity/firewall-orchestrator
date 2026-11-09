using FWO.Data.RulePositioning;

namespace FWO.Services.RulePositioning;

/// <summary>Reads immutable normalized rulebase graphs at one completed import.</summary>
public interface IRulebaseGraphSnapshotReader
{
    /// <summary>
    /// Loads the latest successfully imported structural graph for one management and gateway.
    /// </summary>
    Task<PositioningResult<RulebaseGraphReadValue, RulebaseGraphReadFailureCategory>> ReadAsync(
        int managementId,
        int gatewayId,
        CancellationToken cancellationToken);
}

/// <summary>Successful graph-reader value at one fixed import.</summary>
public sealed record RulebaseGraphReadValue
{
    /// <summary>Gets the immutable normalized graph snapshot.</summary>
    public RulebaseGraphSnapshot Snapshot { get; }
    /// <summary>Gets the import identifier used to build the snapshot.</summary>
    public long ImportId { get; }

    /// <summary>Creates a graph value containing one immutable snapshot.</summary>
    public RulebaseGraphReadValue(RulebaseGraphSnapshot snapshot, long importId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(importId);
        Snapshot = snapshot;
        ImportId = importId;
    }
}

/// <summary>Provides fixed caller-safe diagnostics for graph-reader failures.</summary>
public static class RulebaseGraphReadFailureDiagnostics
{
    /// <summary>Maps a graph-reader failure category to fixed diagnostic text.</summary>
    public static string GetDiagnostic(this RulebaseGraphReadFailureCategory category) => category switch
    {
        RulebaseGraphReadFailureCategory.InvalidInput => "The rulebase graph scope is invalid.",
        RulebaseGraphReadFailureCategory.NoSuccessfulImport => "No successfully completed policy import is available.",
        RulebaseGraphReadFailureCategory.ScopeMismatch => "The requested management and gateway scope is unavailable or inconsistent.",
        RulebaseGraphReadFailureCategory.InvalidGraphData => "The imported rulebase graph is incomplete or invalid.",
        _ => "The imported rulebase graph could not be loaded."
    };
}

/// <summary>Stable failure categories for normalized graph reads.</summary>
public enum RulebaseGraphReadFailureCategory
{
    InvalidInput,
    NoSuccessfulImport,
    ScopeMismatch,
    InvalidGraphData,
    QueryFailed
}

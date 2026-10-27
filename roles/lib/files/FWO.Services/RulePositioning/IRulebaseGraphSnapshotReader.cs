using FWO.Data.RulePositioning;

namespace FWO.Services.RulePositioning;

public interface IRulebaseGraphSnapshotReader
{
    /// <summary>
    /// Loads the latest successfully imported structural graph for one management and gateway.
    /// </summary>
    Task<RulebaseGraphReadResult> ReadAsync(
        int managementId,
        int gatewayId,
        CancellationToken cancellationToken);
}

public abstract class RulebaseGraphReadResult
{
}

public sealed class RulebaseGraphReadSuccess : RulebaseGraphReadResult
{
    public RulebaseGraphSnapshot Snapshot { get; }
    public long ImportId { get; }

    /// <summary>
    /// Creates a successful result containing one immutable snapshot.
    /// </summary>
    public RulebaseGraphReadSuccess(RulebaseGraphSnapshot snapshot, long importId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (importId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(importId));
        }
        Snapshot = snapshot;
        ImportId = importId;
    }
}

public sealed class RulebaseGraphReadFailure : RulebaseGraphReadResult
{
    public RulebaseGraphReadFailureCategory Category { get; }
    public string Diagnostic => GetDiagnostic(Category);

    /// <summary>
    /// Creates a failure with its fixed category-derived diagnostic.
    /// </summary>
    public RulebaseGraphReadFailure(RulebaseGraphReadFailureCategory category)
    {
        Category = category;
    }

    /// <summary>
    /// Maps a failure category to caller-safe diagnostic text.
    /// </summary>
    private static string GetDiagnostic(RulebaseGraphReadFailureCategory category) => category switch
    {
        RulebaseGraphReadFailureCategory.InvalidInput => "The rulebase graph scope is invalid.",
        RulebaseGraphReadFailureCategory.NoSuccessfulImport => "No successfully completed policy import is available.",
        RulebaseGraphReadFailureCategory.ScopeMismatch => "The requested management and gateway scope is unavailable or inconsistent.",
        RulebaseGraphReadFailureCategory.InvalidGraphData => "The imported rulebase graph is incomplete or invalid.",
        _ => "The imported rulebase graph could not be loaded."
    };
}

public enum RulebaseGraphReadFailureCategory
{
    InvalidInput,
    NoSuccessfulImport,
    ScopeMismatch,
    InvalidGraphData,
    QueryFailed
}

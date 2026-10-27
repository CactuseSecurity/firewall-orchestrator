using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;

namespace FWO.Services.RulePositioning.CheckPoint;

public interface ICheckPointLayerGuardSnapshotReader
{
    /// <summary>Loads inline-layer guards at the graph's exact successful import.</summary>
    Task<CheckPointLayerGuardReadResult> ReadAsync(
        RulebaseGraphSnapshot graph,
        long importId,
        CancellationToken cancellationToken);
}

public abstract class CheckPointLayerGuardReadResult;

public sealed class CheckPointLayerGuardReadSuccess(CheckPointLayerGuardSnapshot snapshot)
    : CheckPointLayerGuardReadResult
{
    public CheckPointLayerGuardSnapshot Snapshot { get; } = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
}

public sealed class CheckPointLayerGuardReadFailure(CheckPointLayerGuardReadFailureCategory category)
    : CheckPointLayerGuardReadResult
{
    public CheckPointLayerGuardReadFailureCategory Category { get; } = category;
    public string Diagnostic { get; } = category switch
    {
        CheckPointLayerGuardReadFailureCategory.InvalidInput => "The layer-guard request is invalid.",
        CheckPointLayerGuardReadFailureCategory.ScopeMismatch => "The layer-guard scope does not match the rulebase graph.",
        CheckPointLayerGuardReadFailureCategory.InvalidGuardData => "The imported layer-guard data is invalid.",
        _ => "The imported layer guards could not be loaded."
    };
}

public enum CheckPointLayerGuardReadFailureCategory
{
    InvalidInput,
    ScopeMismatch,
    InvalidGuardData,
    QueryFailed
}

using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Services.RulePositioning;

/// <summary>Calculates and persists one implementation task's semantic rule position.</summary>
public interface IRulePositioningWorkflowCaller
{
    /// <summary>Checks task shape and identity before loading any external context.</summary>
    bool IsApplicable(WfReqTask? requestTask, WfImplTask? implementationTask);

    /// <summary>Runs one independent best-effort calculation and persistence attempt.</summary>
    Task<RulePositioningWorkflowOutcome> CalculateAndPersistAsync(
        WfReqTask requestTask,
        WfImplTask implementationTask,
        CancellationToken cancellationToken);
}

/// <summary>Stable outcomes of a rule-positioning workflow call.</summary>
public enum RulePositioningWorkflowStatus
{
    NotApplicable,
    Stored,
    ClearedAfterFailure,
    PersistenceFailed
}

/// <summary>Safe typed result of one rule-positioning workflow call.</summary>
public sealed record RulePositioningWorkflowOutcome(
    RulePositioningWorkflowStatus Status,
    RulePositioningFailureCategory? FailureCategory = null);

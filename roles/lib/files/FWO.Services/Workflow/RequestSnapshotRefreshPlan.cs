using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

/// <summary>
/// Immutable set of request-element value replacements for one ticket-wide refresh.
/// </summary>
public sealed class RequestSnapshotRefreshPlan
{
    /// <summary>Complete replacement elements in ticket order.</summary>
    public IReadOnlyList<WfReqElement> Replacements { get; }

    /// <summary>Request-task IDs containing replacements, in ticket order.</summary>
    public IReadOnlyList<long> ChangedTaskIds { get; }

    /// <summary>Whether the ticket contains stale snapshots.</summary>
    public bool HasChanges => Replacements.Count > 0;

    /// <summary>Creates an immutable refresh plan.</summary>
    internal RequestSnapshotRefreshPlan(List<WfReqElement> replacements, List<long> changedTaskIds)
    {
        Replacements = replacements.AsReadOnly();
        ChangedTaskIds = changedTaskIds.AsReadOnly();
    }
}

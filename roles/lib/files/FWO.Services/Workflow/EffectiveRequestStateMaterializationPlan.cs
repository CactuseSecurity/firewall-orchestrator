using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

/// <summary>
/// Immutable collection contract for complete request-element replacements.
/// </summary>
public sealed class EffectiveRequestStateMaterializationPlan
{
    /// <summary>
    /// Gets the complete replacement elements whose persisted values changed.
    /// </summary>
    public IReadOnlyList<WfReqElement> ChangedElements { get; }

    /// <summary>
    /// Gets the request-task IDs containing changed elements.
    /// </summary>
    public IReadOnlyList<long> ChangedTaskIds { get; }

    /// <summary>
    /// Gets whether persistence is required.
    /// </summary>
    public bool HasChanges => ChangedElements.Count > 0;

    /// <summary>
    /// Creates a materialization plan from validated replacement elements and task IDs.
    /// </summary>
    /// <param name="changedElements">Complete replacement elements.</param>
    /// <param name="changedTaskIds">Request tasks containing replacements.</param>
    internal EffectiveRequestStateMaterializationPlan(List<WfReqElement> changedElements, List<long> changedTaskIds)
    {
        ChangedElements = changedElements.AsReadOnly();
        ChangedTaskIds = changedTaskIds.AsReadOnly();
    }
}

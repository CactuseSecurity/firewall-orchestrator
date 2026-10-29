using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using System.Collections.ObjectModel;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Contains the caller-resolved inputs required to calculate one rule position.
/// </summary>
public sealed record RulePositioningContext
{
    public WfImplTask ImplementationTask { get; init; }
    public WfReqTask ParentRequestTask { get; init; }
    public Device TargetGateway { get; init; }
    public Management TargetManagement { get; init; }
    public GatewayDeviceSpecificSettings EffectiveSettings { get; init; }

    /// <summary>Gets the optional immutable ticket-task slice used by ticket-aware strategies.</summary>
    public IReadOnlyList<WfReqTask>? TicketRequestTasks { get; init; }

    /// <summary>Creates one caller-resolved positioning context.</summary>
    public RulePositioningContext(
        WfImplTask implementationTask,
        WfReqTask parentRequestTask,
        Device targetGateway,
        Management targetManagement,
        GatewayDeviceSpecificSettings effectiveSettings,
        IReadOnlyCollection<WfReqTask>? ticketRequestTasks = null)
    {
        ImplementationTask = implementationTask;
        ParentRequestTask = parentRequestTask;
        TargetGateway = targetGateway;
        TargetManagement = targetManagement;
        EffectiveSettings = effectiveSettings;
        TicketRequestTasks = ticketRequestTasks == null
            ? null
            : new ReadOnlyCollection<WfReqTask>([.. ticketRequestTasks]);
    }
}
